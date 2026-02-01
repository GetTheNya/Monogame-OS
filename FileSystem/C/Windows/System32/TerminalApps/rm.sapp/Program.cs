using System;
using System.IO;
using Microsoft.Xna.Framework;
using TheGame.Core.OS;

namespace Rm;

public class Program : TerminalApplication {
    public static Program Main(string[] args) {
        return new Program();
    }

    protected override void Run(string[] args) {
        if (args != null && args.Length > 0) {
            foreach (var item in args) {
                string path = VirtualFileSystem.Instance.ResolvePath(Process.WorkingDirectory, item);
                if (VirtualFileSystem.Instance.Exists(path)) {
                    VirtualFileSystem.Instance.Delete(path);
                    WriteLine($"rm: removed '{item}'");
                } else {
                    WriteLine($"rm: cannot remove '{item}': No such file or directory", Color.Red);
                    Process.ExitCode = 1;
                }
            }
        } else {
            WriteLine("rm: missing operand", Color.Red);
            Process.ExitCode = 1;
        }
    }

}
