using System;
using System.IO;
using Microsoft.Xna.Framework;
using TheGame.Core.OS;

namespace Touch;

public class Program : TerminalApplication {
    public static Program Main(string[] args) {
        return new Program();
    }

    protected override void Run(string[] args) {
        if (args != null && args.Length > 0) {
            foreach (var file in args) {
                string path = Path.Combine(Process.WorkingDirectory, file); 
                VirtualFileSystem.Instance.CreateFile(path);
                WriteLine($"touch: {file} created");
            }
        } else {
            WriteLine($"file: file name/s are not provided", Color.Red);
            Process.ExitCode = 1;
        }
    }
}
