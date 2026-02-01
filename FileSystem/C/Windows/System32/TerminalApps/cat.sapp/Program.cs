using System;
using System.IO;
using Microsoft.Xna.Framework;
using TheGame.Core.OS;

namespace Cat;

public class Program : TerminalApplication {
    public static Program Main(string[] args) {
        return new Program();
    }

    protected override void Run(string[] args) {
        if (args != null && args.Length > 0) {
            foreach (var file in args) {
                string resolved = VirtualFileSystem.Instance.ResolvePath(Process.WorkingDirectory, file);
                if (VirtualFileSystem.Instance.Exists(resolved)) {
                    Write(VirtualFileSystem.Instance.ReadAllText(resolved));
                } else {
                    WriteLine($"cat: {file}: No such file or directory", Color.Red);
                    Process.ExitCode = 1;
                }
            }
        } else {
            // Read from stdin
            string line;
            while ((line = ReadLine()) != null) {
                WriteLine(line);
            }
        }
    }
}
