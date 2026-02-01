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

    /// <summary>
    /// Reads a line from standard input, blocking the current thread until input is available.
    /// Use this only within the synchronous 'Run' method to avoid blocking the UI thread.
    /// </summary>
    public new string ReadLine() {
        if (StandardInput is TerminalReader reader) {
            return reader.ReadLine();
        }
        return base.ReadLine();
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

    protected override async System.Threading.Tasks.Task OnLoadAsync(string[] args) {
        // If the user has overridden the synchronous Run method, we execute it on a background thread.
        // This allows them to use blocking ReadLine() without freezing the OS UI.
        
        // Check if Run is overridden (not the base empty implementation)
        var runMethod = GetType().GetMethod("Run", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        if (runMethod != null && runMethod.DeclaringType != typeof(TerminalApplication)) {
            await System.Threading.Tasks.Task.Run(() => Run(args));
            
            // Auto-exit when Run finishes, similar to traditional CLI apps
            // Only if we haven't already exited
            if (Process.State != ProcessState.Terminated) {
                Exit();
            }
        } else {
            await base.OnLoadAsync(args);
        }
    }

    /// <summary>
    /// Synchronous entry point for terminal applications. 
    /// Runs on a background thread, allowing the use of blocking I/O (like ReadLine()).
    /// </summary>
    protected virtual void Run(string[] args) { }

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
