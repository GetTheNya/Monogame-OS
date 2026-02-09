using System;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using TheGame;
using TheGame.Core.OS;

namespace Neofetch;

public class Program : TerminalApplication {
    public static Program Main(string[] args) {
        return new Program();
    }

    private readonly string[] asciiLogo = new string[] {
        @"   _    _            _    ",
        @"  | |  | |          | |   ",
        @"  | |__| | ___ _ __ | |_  ",
        @"  |  __  |/ _ \ '_ \| __| ",
        @"  | |  | |  __/ | | | |_  ",
        @"  |_|  |_|\___|_| |_|\__| ",
        @"        OS v0.4.2         ",
        @"      (hentai edition)    ",
    };

    protected override void Run(string[] args) {
        string user = "user";
        string hostname = "hentos";
        string separator = new string('-', user.Length + hostname.Length + 1);

        int logoWidth = asciiLogo.Max(s => s.Length) + 2;
        int totalInfoLines = 11;
        int maxLines = Math.Max(asciiLogo.Length, totalInfoLines);

        for (int i = 0; i < maxLines; i++) {
            // Draw Logo
            if (i < asciiLogo.Length) {
                Color logoColor = i >= asciiLogo.Length - 2 ? Color.Magenta : Color.Cyan;
                Write(AnsiCodes.Wrap(asciiLogo[i].PadRight(logoWidth), logoColor));
            } else {
                Write(new string(' ', logoWidth));
            }

            // Draw Info
            switch (i) {
                case 0: Write(AnsiCodes.Wrap($"{user}@{hostname}", Color.Cyan)); break;
                case 1: Write(separator); break;
                case 2: Write(AnsiCodes.Wrap("OS: ", Color.Cyan)); Write("HentOS x64"); break;
                case 3: Write(AnsiCodes.Wrap("Host: ", Color.Cyan)); Write("Virtual Machine (MonoGame)"); break;
                case 4: Write(AnsiCodes.Wrap("Kernel: ", Color.Cyan)); Write("0.4.2-hentai"); break;
                case 5: Write(AnsiCodes.Wrap("Packages: ", Color.Cyan)); Write(AppLoader.Instance.TotalAppsToLoad.ToString()); break;
                case 6: Write(AnsiCodes.Wrap("Shell: ", Color.Cyan)); Write("H-Term"); break;
                case 7: Write(AnsiCodes.Wrap("Resolution: ", Color.Cyan)); Write($"{G.GraphicsDevice.Viewport.Width}x{G.GraphicsDevice.Viewport.Height}"); break;
                case 8: Write(AnsiCodes.Wrap("DE: ", Color.Cyan)); Write("NeonEnvironment"); break;
                case 9: Write(AnsiCodes.Wrap("WM: ", Color.Cyan)); Write("HentWindowManger"); break;
                case 10: Write(AnsiCodes.Wrap("Terminal: ", Color.Cyan)); Write("H-Terminal"); break;
            }
            WriteLine("");
        }

        WriteLine("");
        Write(new string(' ', logoWidth));
        
        // Color squares
        Color[] colors = new Color[] {
            Color.Black, Color.Red, Color.Green, Color.Yellow, 
            Color.Blue, Color.Magenta, Color.Cyan, Color.White
        };

        // Note: TerminalApplication/AnsiCodes don't easily support background colors via Wrap
        // using simple spaces for now, or if Terminal supports background ANSI... 
        // Real neofetch uses background colors for squares.
        foreach (var color in colors) {
            Write(AnsiCodes.Wrap("██", color)); 
        }
        WriteLine("");
        Write(new string(' ', logoWidth));
        foreach (var color in colors) {
            // Bright variants (approximated)
            Color bright = new Color(Math.Min(255, color.R + 100), Math.Min(255, color.G + 100), Math.Min(255, color.B + 100));
            Write(AnsiCodes.Wrap("██", bright)); 
        }
        WriteLine("");
    }
}
