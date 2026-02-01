using System;
using TheGame.Core.OS;

namespace Echo;

public class Program : TerminalApplication {
    public static Program Main(string[] args) {
        return new Program();
    }

    protected override void Run(string[] args) {
        if (args != null && args.Length > 0) {
            WriteLine(string.Join(" ", args));
        }
    }
}
