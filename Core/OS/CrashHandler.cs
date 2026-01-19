using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using TheGame.Core.UI;

namespace TheGame.Core.OS;

/// <summary>
/// Handles app crashes, logging, and error dialogs.
/// Prevents app exceptions from crashing the entire OS.
/// </summary>
public static class CrashHandler {
    
    /// <summary>
    /// Checks if an exception originated from a JIT-compiled app (not the OS).
    /// </summary>
    public static bool IsAppException(Exception ex, Process process) {
        if (ex == null || process == null) return false;
        
        var stackTrace = new StackTrace(ex, true);
        
        // Check if any frame in the stack trace belongs to the app's assembly
        for (int i = 0; i < stackTrace.FrameCount; i++) {
            var frame = stackTrace.GetFrame(i);
            var method = frame?.GetMethod();
            var assembly = method?.DeclaringType?.Assembly;
            
            if (assembly != null) {
                var assemblyName = assembly.GetName().Name;
                
                // Check if this is the app's assembly (matches AppId or appId with suffix from reload)
                if (assemblyName == process.AppId || assemblyName.StartsWith(process.AppId + "_")) {
                    return true;
                }
            }
        }
        
        return false;
    }

    /// <summary>
    /// Attempts to find an app process responsible for the exception and handle it.
    /// Returns true if an app was found and handled.
    /// </summary>
    public static bool TryHandleAnyAppException(Exception ex) {
        if (ex == null) return false;

        var stackTrace = new StackTrace(ex, true);
        for (int i = 0; i < stackTrace.FrameCount; i++) {
            var frame = stackTrace.GetFrame(i);
            var method = frame?.GetMethod();
            var assembly = method?.DeclaringType?.Assembly;
            
            if (assembly != null) {
                var assemblyName = assembly.GetName().Name;
                if (string.IsNullOrEmpty(assemblyName)) continue;

                // Find process whose AppId matches or is a prefix of the assembly name
                var process = ProcessManager.Instance.GetAllProcesses().FirstOrDefault(p => 
                    assemblyName == p.AppId || assemblyName.StartsWith(p.AppId + "_"));

                if (process != null) {
                    HandleAppException(process, ex);
                    return true;
                }
            }
        }

        return false;
    }
    
    /// <summary>
    /// Main entry point for handling app crashes.
    /// Shows error dialog, logs crash details, and terminates the app.
    /// </summary>
    public static void HandleAppException(Process process, Exception ex) {
        if (process == null || ex == null) return;
        
        string appId = process.AppId ?? "UNKNOWN";
        
        DebugLogger.Log($"[CrashHandler] App '{appId}' crashed: {ex.GetType().Name}: {ex.Message}");
        
        // Log the crash
        string logPath = LogCrash(appId, ex);
        
        // Show error dialog
        ShowCrashDialog(appId, ex, logPath);
        
        // Terminate the process
        try {
            process.Terminate();
        } catch (Exception termEx) {
            DebugLogger.Log($"[CrashHandler] Error terminating crashed process: {termEx.Message}");
        }
    }
    
    /// <summary>
    /// Logs crash details to C:\Windows\Logs\{AppId}\crash_{timestamp}.log
    /// </summary>
    private static string LogCrash(string appId, Exception ex) {
        try {
            // Create log directory
            string logDir = $"C:\\Windows\\Logs\\{appId}";
            string hostLogDir = VirtualFileSystem.Instance.ToHostPath(logDir);
            Directory.CreateDirectory(hostLogDir);
            
            // Generate log filename with timestamp
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string logFileName = $"crash_{timestamp}.log";
            string logPath = Path.Combine(logDir, logFileName);
            string hostLogPath = Path.Combine(hostLogDir, logFileName);
            
            // Build crash report
            var sb = new StringBuilder();
            sb.AppendLine("========================================");
            sb.AppendLine("APP CRASH REPORT");
            sb.AppendLine("========================================");
            sb.AppendLine($"App: {appId}");
            sb.AppendLine($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            sb.AppendLine($"Exception Type: {ex.GetType().FullName}");
            sb.AppendLine($"Message: {ex.Message}");
            sb.AppendLine();
            sb.AppendLine("Stack Trace:");
            sb.AppendLine(ex.StackTrace ?? "(No stack trace available)");
            
            // Include inner exceptions
            if (ex.InnerException != null) {
                sb.AppendLine();
                sb.AppendLine("Inner Exception:");
                sb.AppendLine($"  Type: {ex.InnerException.GetType().FullName}");
                sb.AppendLine($"  Message: {ex.InnerException.Message}");
                sb.AppendLine($"  Stack Trace: {ex.InnerException.StackTrace}");
            }
            
            sb.AppendLine();
            sb.AppendLine("========================================");
            
            // Write to file
            File.WriteAllText(hostLogPath, sb.ToString());
            
            DebugLogger.Log($"[CrashHandler] Crash log written to: {logPath}");
            
            return logPath;
        } catch (Exception logEx) {
            DebugLogger.Log($"[CrashHandler] Failed to write crash log: {logEx.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Shows a crash dialog to the user.
    /// </summary>
    private static void ShowCrashDialog(string appId, Exception ex, string logPath) {
        try {
            // Build error message
            string title = $"{appId} - Application Error";
            string message = $"App '{appId}' has crashed.\n\n" +
                           $"Error: {ex.GetType().Name}\n" +
                           $"{ex.Message}\n\n";
            
            if (!string.IsNullOrEmpty(logPath)) {
                message += $"Crash details saved to:\n{logPath}";
            }
            
            // Show notification
            Shell.Notifications.Show(title, message, onClick: () => {
                Shell.Execute(logPath);
            });
            
            DebugLogger.Log($"[CrashHandler] Crash dialog shown for {appId}");
        } catch (Exception dialogEx) {
            DebugLogger.Log($"[CrashHandler] Failed to show crash dialog: {dialogEx.Message}");
        }
    }
}
