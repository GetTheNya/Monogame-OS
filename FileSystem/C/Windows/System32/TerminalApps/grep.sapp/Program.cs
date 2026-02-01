using System;
using Microsoft.Xna.Framework;
using TheGame.Core.OS;

namespace Grep;

public class Program : TerminalApplication {
    public static Program Main(string[] args) {
        return new Program();
    }

    protected override void Run(string[] args) {
        if (args == null || args.Length == 0) {
            return;
        }

        string pattern = args[0];
        string line;
        while ((line = ReadLine()) != null) {
            if (line.Contains(pattern, StringComparison.OrdinalIgnoreCase)) {
                WriteLine(line);
            }
        }
    }
}
