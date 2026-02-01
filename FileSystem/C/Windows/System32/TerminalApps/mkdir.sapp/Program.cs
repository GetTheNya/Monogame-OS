using System;
using System.IO;
using Microsoft.Xna.Framework;
using TheGame.Core.OS;

namespace Mkdir;

public class Program : TerminalApplication {
    public static Program Main(string[] args) {
        return new Program();
    }

    protected override void Run(string[] args) {
        if (args != null && args.Length > 0) {
            foreach (var folder in args) {
                string path = Path.Combine(Process.WorkingDirectory, folder); 
                VirtualFileSystem.Instance.CreateDirectory(path);
                WriteLine($"mkdir: {folder} created");
            }
        } else {
            WriteLine($"mkdir: directory name/s are not provided", Color.Red);
            Process.ExitCode = 1;
        }
    }
}
