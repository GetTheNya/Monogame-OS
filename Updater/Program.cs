using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace Updater;

internal class Program {
    [DllImport("kernel32.dll")]
    static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    const int SW_HIDE = 0;
    const int SW_SHOW = 5;

    static void Main(string[] args) {
        // Hides immediately after launch.
        IntPtr hWnd = GetConsoleWindow();
        if (hWnd != IntPtr.Zero) ShowWindow(hWnd, SW_HIDE);

        if (args.Length < 4) {
            // Show console if there's an error
            if (hWnd != IntPtr.Zero) ShowWindow(hWnd, SW_SHOW);
            Console.WriteLine("Usage: Updater.exe <sourceZip> <targetDir> <exeName> <processId>");
            Console.WriteLine("\nReceived args: " + string.Join(" ", args));
            Console.WriteLine("\nPress any key to exit.");
            Console.ReadKey();
            return;
        }

        string sourceZip = args[0];
        string targetDir = args[1];
        string exeName = args[2];
        
        if (!int.TryParse(args[3], out int processId)) {
             if (hWnd != IntPtr.Zero) ShowWindow(hWnd, SW_SHOW);
             Console.WriteLine("Invalid Process ID: " + args[3]);
             Console.ReadKey();
             return;
        }

        try {
            // Waits for the game process to close indefinitely and silently.
            try {
                var process = Process.GetProcessById(processId);
                process.WaitForExit(); 
            } catch {
                // Process already closed
            }

            // Shows the console when it starts working.
            if (hWnd != IntPtr.Zero) ShowWindow(hWnd, SW_SHOW);
            ConfigureConsole();
            PrintBanner();

            Log("Update could remove your files if there is same file names inside update package.");
            Console.Write("Do you want to continue? (y/n): ");
            var key = Console.ReadKey(true);
            Console.WriteLine(key.KeyChar);
            if (key.KeyChar != 'y' && key.KeyChar != 'Y') {
                Log("Update cancelled by user.");
                Thread.Sleep(2000);
                return;
            }

            Log($"Starting update from: {sourceZip}");
            Log($"Target directory: {targetDir}");

            CleanupTargetDir(targetDir);
            ExtractZip(sourceZip, targetDir);

            Log("Update completed successfully!");
            
            // Restart TheGame.exe
            string exePath = Path.Combine(targetDir, exeName);
            if (File.Exists(exePath)) {
                Log($"Restarting {exeName}...");
                Process.Start(new ProcessStartInfo {
                    FileName = exePath,
                    WorkingDirectory = targetDir,
                    UseShellExecute = true
                });
            }

            Log("Updater will close in 3 seconds.");
            Thread.Sleep(3000);

        } catch (Exception ex) {
            if (hWnd != IntPtr.Zero) ShowWindow(hWnd, SW_SHOW);
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[ERROR] {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("\nPress any key to exit.");
            Console.ReadKey();
        }
    }

    static void ConfigureConsole() {
        try {
            Console.BackgroundColor = ConsoleColor.DarkBlue;
            Console.ForegroundColor = ConsoleColor.White;
            Console.Clear();
            Console.Title = "HentOS System Updater";
        } catch { }
    }

    static void PrintBanner() {
        Console.WriteLine(@"
  _   _             _   _____  _____ 
 | | | |           | | |  _  |/  ___|
 | |_| | ___ _ __ _| |_| | | |\ `--. 
 |  _  |/ _ \ '_ \_   _| | | | `--. \
 | | | |  __/ | | || |_| \_/ //\__/ /
 \_| |_/\___|_| |_| \___\___/ \____/ 
          SYSTEM UPDATER
");
    }

    static void Log(string message) {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
    }

    static void CleanupTargetDir(string targetDir) {
        Log("Cleaning up target directory...");
        var currentExe = Process.GetCurrentProcess().MainModule?.FileName;
        var dirInfo = new DirectoryInfo(targetDir);

        foreach (var file in dirInfo.GetFiles()) {
            if (currentExe != null && file.FullName.Equals(currentExe, StringComparison.OrdinalIgnoreCase)) continue;
            if (file.Name.StartsWith("Updater", StringComparison.OrdinalIgnoreCase)) continue;
            
            try {
                file.Delete();
            } catch (Exception ex) {
                Log($"[WARNING] Could not delete file {file.Name}: {ex.Message}");
            }
        }

        foreach (var dir in dirInfo.GetDirectories()) {
            // Ignore FileSystem to preserve user data (C:\Users, etc.)
            // The extraction will still overwrite system files inside it if they exist in the ZIP.
            if (dir.Name.Equals("FileSystem", StringComparison.OrdinalIgnoreCase)) continue;
            
            try {
                dir.Delete(true);
            } catch (Exception ex) {
                Log($"[WARNING] Could not delete directory {dir.Name}: {ex.Message}");
            }
        }
    }

    static void ExtractZip(string zipPath, string targetDir) {
        if (!File.Exists(zipPath)) throw new FileNotFoundException("Update ZIP not found", zipPath);

        using (ZipArchive archive = ZipFile.OpenRead(zipPath)) {
            int total = archive.Entries.Count;
            int current = 0;

            foreach (ZipArchiveEntry entry in archive.Entries) {
                current++;
                
                // Skip directory entries (marked by trailing slash)
                if (entry.FullName.EndsWith("/") || entry.FullName.EndsWith("\\")) continue;

                string fullPath = Path.GetFullPath(Path.Combine(targetDir, entry.FullName));
                
                // Security check: ensure extraction is within targetDir
                if (!fullPath.StartsWith(Path.GetFullPath(targetDir), StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                string? dir = Path.GetDirectoryName(fullPath);
                if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                try {
                    // Extract with overwrite enabled (true)
                    entry.ExtractToFile(fullPath, true);
                    float progress = (float)current / total * 100;
                    // Pad name with spaces to clear previous text
                    string displayName = entry.Name.Length > 40 ? "..." + entry.Name.Substring(entry.Name.Length - 37) : entry.Name.PadRight(40);
                    Console.Write($"\rExtracting: [{current}/{total}] {progress:F0}% - {displayName}");
                } catch (Exception ex) {
                    Console.WriteLine($"\n[WARNING] Failed to extract {entry.FullName}: {ex.Message}");
                }
            }
        }
        Console.WriteLine("\nExtraction finished.");
    }
}
