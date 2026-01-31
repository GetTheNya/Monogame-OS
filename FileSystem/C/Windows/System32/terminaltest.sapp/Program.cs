using System;
using Microsoft.Xna.Framework;
using TheGame.Core.OS;

namespace TerminalTest;

public class Program : TerminalApplication {
    public static Program Main(string[] args) {
        var app = new Program();
        if (args != null && args.Length > 0) {
            app.RunTests(args);
        } else {
            app.RunInteractive();
        }
        return app;
    }

    private void RunTests(string[] args) {
        WriteLine("Running Console Tests...");
        foreach (var arg in args) {
            if (arg == "--color") TestColors();
            if (arg == "--signal") TestSignal();
        }
        WriteLine("Tests complete. Returing exit code 0.");
        Process.ExitCode = 0;
        Exit();
    }

    private void RunInteractive() {
        WriteLine("--- Console Test Interactive Mode ---", Color.Cyan);
        WriteLine("Type 'exit' to quit, 'color' for color test, 'err' for error test.");
        
        while (true) {
            Write("> ");
            string input = ReadLine();
            if (input == null || input == "exit") break;
            
            if (input == "color") TestColors();
            else if (input == "err") StandardError.WriteLine("This is an error message!", Color.Red);
            else WriteLine($"You said: {input}");
        }
        
        Exit();
    }

    private void TestColors() {
        WriteLine("Color Test:", Color.Yellow);
        WriteLine("\u001b[31mRed Text\u001b[0m");
        WriteLine("\u001b[32mGreen Text\u001b[0m");
        WriteLine("\u001b[34mBlue Text\u001b[0m");
        WriteLine("\u001b[33mYellow Text\u001b[0m");
    }

    private void TestSignal() {
        WriteLine("Waiting for Ctrl+C... (type something to skip)");
        ReadLine();
    }

    protected override void OnCancel() {
        WriteLine("\nReceived SIGINT (Ctrl+C). Terminating...", Color.Red);
        Process.ExitCode = 130;
        base.OnCancel();
    }
}
