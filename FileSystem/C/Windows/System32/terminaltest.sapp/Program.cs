using System;
using Microsoft.Xna.Framework;
using TheGame.Core.OS;

namespace TerminalTest;

public class Program : TerminalApplication {
    private bool _isInteractive = false;

    public static Program Main(string[] args) {
        return new Program();
    }

    protected override async System.Threading.Tasks.Task OnLoadAsync(string[] args) {
        if (args != null && args.Length > 0) {
            await RunTestsAsync(args);
        } else {
            await RunInteractiveAsync();
        }
    }

    private async System.Threading.Tasks.Task RunTestsAsync(string[] args) {
        WriteLine("Running Console Tests...");
        foreach (var arg in args) {
            if (arg == "--color") TestColors();
            if (arg == "--signal") await TestSignalAsync();
        }
        WriteLine("Tests complete. Returing exit code 0.");
        Process.ExitCode = 0;
        Exit();
    }

    private async System.Threading.Tasks.Task RunInteractiveAsync() {
        WriteLine("--- Console Test Interactive Mode ---", Color.Cyan);
        WriteLine("Type 'exit' to quit, 'color' for color test, 'err' for error test.");
        
        while (true) {
            Write("> ");
            string rawInput = await ReadLineAsync();
            if (rawInput == null) break; // Terminated

            string input = rawInput.Trim().ToLower();
            
            if (input == "exit") {
                Exit();
                break;
            }
            
            if (input == "color") TestColors();
            else if (input == "err") StandardError.WriteLine("This is an error message!", Color.Red);
            else if (!string.IsNullOrWhiteSpace(input)) WriteLine($"You said: {input}");
        }
    }

    private void TestColors() {
        WriteLine("Color Test:", Color.Yellow);
        WriteLine("\u001b[31mRed Text\u001b[0m");
        WriteLine("\u001b[32mGreen Text\u001b[0m");
        WriteLine("\u001b[34mBlue Text\u001b[0m");
        WriteLine("\u001b[33mYellow Text\u001b[0m");
    }

    private async System.Threading.Tasks.Task TestSignalAsync() {
        WriteLine("Waiting for Ctrl+C... (type something to skip)");
        await ReadLineAsync();
    }

    protected override void OnCancel() {
        WriteLine("\nReceived SIGINT (Ctrl+C). Terminating...", Color.Red);
        Process.ExitCode = 130;
        base.OnCancel();
    }
}
