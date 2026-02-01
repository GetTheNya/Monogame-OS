using System;
using System.IO;
using Microsoft.Xna.Framework;
using TheGame.Core.OS;

namespace Dir;

public class Program : TerminalApplication {
    public static Program Main(string[] args) {
        return new Program();
    }

    protected override void Run(string[] args) {
        string[] targets = args != null && args.Length > 0 ? args : new[] { Process.WorkingDirectory };

        foreach (var targetPath in targets) {
            WriteLine($"Directory of {targetPath}\n", Color.Gray);
        
            string resolvedPath = VirtualFileSystem.Instance.ResolvePath(Process.WorkingDirectory, targetPath);

            if (!VirtualFileSystem.Instance.Exists(resolvedPath)) {
                WriteLine($"dir: {targetPath}: No such file or directory", Color.Red);
                Process.ExitCode = 1;
                continue;
            }

            if (!VirtualFileSystem.Instance.IsDirectory(resolvedPath)) {
                WriteLine(Path.GetFileName(resolvedPath), Color.White);
                continue;
            }

            var directories = VirtualFileSystem.Instance.GetDirectories(resolvedPath);
            var files = VirtualFileSystem.Instance.GetFiles(resolvedPath);

            foreach (var dir in directories) {
                string name = Path.GetFileName(dir);
                if (string.IsNullOrEmpty(name)) name = dir;
                WriteLine(name, Color.LightBlue);
            }

            foreach (var file in files) {
                WriteLine(Path.GetFileName(file), Color.White);
            }

            WriteLine($"\n{directories.Length} Dir(s), {files.Length} File(s)", Color.Gray);
        }
    }
}
