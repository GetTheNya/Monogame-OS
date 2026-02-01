using System;
using System.IO;
using Microsoft.Xna.Framework;
using TheGame.Core.OS;

namespace Mv;

public class Program : TerminalApplication {
    public static Program Main(string[] args) {
        return new Program();
    }

    protected override void Run(string[] args) {
        if (args != null && args.Length == 2) {
            string source = VirtualFileSystem.Instance.ResolvePath(Process.WorkingDirectory, args[0]);
            string dest = VirtualFileSystem.Instance.ResolvePath(Process.WorkingDirectory, args[1]);

            if (!VirtualFileSystem.Instance.Exists(source)) {
                WriteLine($"mv: cannot stat '{args[0]}': No such file or directory", Color.Red);
                Process.ExitCode = 1;
                return;
            }

            // If destination is a directory, move source INTO it
            if (VirtualFileSystem.Instance.IsDirectory(dest)) {
                string fileName = Path.GetFileName(source.TrimEnd('\\'));
                dest = Path.Combine(dest, fileName);
            }

            VirtualFileSystem.Instance.Move(source, dest);
            WriteLine($"mv: moved '{args[0]}' to '{args[1]}'");
        } else {
            WriteLine("mv: usage: mv <source> <destination>", Color.Red);
            Process.ExitCode = 1;
        }
    }

}
