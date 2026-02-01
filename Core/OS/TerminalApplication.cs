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
    public bool RequireTerminal { get; protected set; } = false;

    public override bool IsAsync => true;

    /// <summary>
    /// Reads a line from standard input asynchronously.
    /// </summary>
    public override async System.Threading.Tasks.Task<string> ReadLineAsync() {
        if (StandardInput is TerminalReader reader) {
            return await reader.ReadLineAsync();
        }
        return await base.ReadLineAsync();
    }

    protected override void OnPrepare(string[] args) {
        base.OnPrepare(args);
        
        if (Process != null) {
            // Register default signal handlers for CLI apps
            Process.OnSignalCancel += OnCancel;
        }
    }

    protected override void OnLoad(string[] args) {
        base.OnLoad(args);

        // If this app is launched without a terminal, and it's not explicitly disabled,
        // we might want to request one from the OS in the future.
        // For now, the AppLoader will handle the auto-spawning of terminals for TerminalOnly apps.
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
