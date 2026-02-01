using System;

namespace TheGame.Core.OS;

/// <summary>
/// A specialized base class for applications that run primarily in a terminal.
/// Automatically handles terminal window management and command-line lifecycle.
/// </summary>
public abstract class TerminalApplication : Application {
    /// <summary>
    /// If true, the application will terminate immediately if it cannot find a terminal to attach to.
    /// Default is false, which allows it to run in background mode.
    /// </summary>
    public bool RequireTerminal { get; protected set; } = false;

    public override bool IsThreaded => true;

    protected override void OnLoad(string[] args) {
        base.OnLoad(args);

        // If this app is launched without a terminal, and it's not explicitly disabled,
        // we might want to request one from the OS in the future.
        // For now, the AppLoader will handle the auto-spawning of terminals for TerminalOnly apps.
        
        if (Process != null) {
            // Register default signal handlers for CLI apps
            Process.OnSignalCancel += OnCancel;
        }
    }

    /// <summary>
    /// Called when a cancel signal (Ctrl+C) is received while this app is the active terminal process.
    /// </summary>
    protected virtual void OnCancel() {
        // Default behavior is to terminate the application
        Exit();
    }

    protected override void OnClose() {
        if (Process != null) {
            Process.OnSignalCancel -= OnCancel;
        }
        base.OnClose();
    }
}
