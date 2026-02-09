// Main Program Entry Point
using Microsoft.Xna.Framework;
using TheGame.Core.UI;
using TheGame.Core.OS;

namespace CrashApp;

/// <summary>
/// Entry point for the Crash App.
/// </summary>
public class Program {
    public static Window CreateWindow(string[] args) {
        return new MainWindow();
    }
}
