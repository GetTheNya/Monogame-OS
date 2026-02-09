// Background Service - Entry Point
using TheGame.Core.OS;

namespace BackgroundServiceApp;

/// <summary>
/// Entry point for the Background Service demo.
/// This returns a Process instead of a Window to demonstrate true background processes.
/// </summary>
public class Program {
    // Note: This method returns a Process, not a Window!
    // The AppLoader will detect this and handle it differently
    public static Process CreateProcess(string[] args) {
        return new NotificationServiceProcess();
    }
}
