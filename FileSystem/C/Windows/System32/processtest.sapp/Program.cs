// Main Program Entry Point
using Microsoft.Xna.Framework;
using TheGame.Core.UI;
using TheGame.Core.OS;

namespace ProcessTestApp;

/// <summary>
/// Entry point for the Process Test app.
/// </summary>
public class Program {
    public static Window CreateWindow(string[] args) {
        return new MainWindow();
    }
}
