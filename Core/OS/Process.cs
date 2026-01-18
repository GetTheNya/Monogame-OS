using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using TheGame.Core.UI;

namespace TheGame.Core.OS;

/// <summary>
/// Represents the update priority for a background process.
/// Higher priority = more frequent OnUpdate calls when in background.
/// </summary>
public enum ProcessPriority {
    High,   // Every frame (no throttle)
    Normal, // ~30 times per second
    Low     // ~10 times per second
}

/// <summary>
/// Represents the current state of a process.
/// </summary>
public enum ProcessState {
    Starting,    // Process is being initialized
    Running,     // Process has visible windows
    Background,  // Process is running without visible windows
    Terminated   // Process has ended
}

/// <summary>
/// Represents a running application process that can own zero or more windows.
/// </summary>
public class Process {
    /// <summary>Unique identifier for this process instance.</summary>
    public string ProcessId { get; } = Guid.NewGuid().ToString();
    
    /// <summary>The app identifier (e.g., "EXPLORER", "NOTEPAD").</summary>
    public string AppId { get; internal set; }
    
    /// <summary>Current state of the process.</summary>
    public ProcessState State { get; set; } = ProcessState.Starting;
    
    /// <summary>Update priority when running in background.</summary>
    public ProcessPriority Priority { get; set; } = ProcessPriority.Normal;
    
    /// <summary>All windows owned by this process.</summary>
    public List<Window> Windows { get; } = new();
    
    /// <summary>The main/primary window of this process (can be null for background).</summary>
    public Window MainWindow { get; set; }
    
    // Priority-based update timing (only applies when in Background state)
    internal double UpdateAccumulator;
    internal double UpdateInterval => Priority switch {
        ProcessPriority.High => 0,           // Every frame (no throttle)
        ProcessPriority.Normal => 1.0 / 30.0, // ~30 times per second
        ProcessPriority.Low => 1.0 / 10.0,   // ~10 times per second
        _ => 0
    };
    
    public Process() {
    }
    
    /// <summary>
    /// Called when the process starts. Override to create windows or initialize.
    /// </summary>
    public virtual void OnStart(string[] args) { }
    
    /// <summary>
    /// Called each frame (or throttled if in background). Override for background work.
    /// </summary>
    public virtual void OnUpdate(GameTime gameTime) { }
    
    /// <summary>
    /// Called when the process is terminating. Override for cleanup.
    /// </summary>
    public virtual void OnTerminate() { }
    
    /// <summary>
    /// Creates and returns a new window owned by this process.
    /// </summary>
    public T CreateWindow<T>() where T : Window, new() {
        var window = new T();
        window.OwnerProcess = this;
        Windows.Add(window);
        if (MainWindow == null) MainWindow = window;
        return window;
    }
    
    /// <summary>
    /// Creates a window of the given type and returns it.
    /// </summary>
    public Window CreateWindow(Type windowType) {
        if (!typeof(Window).IsAssignableFrom(windowType)) {
            throw new ArgumentException($"Type {windowType.Name} is not a Window");
        }
        var window = (Window)Activator.CreateInstance(windowType);
        window.OwnerProcess = this;
        Windows.Add(window);
        if (MainWindow == null) MainWindow = window;
        return window;
    }
    
    /// <summary>
    /// Shows a modal dialog that blocks input to its parent window.
    /// </summary>
    public void ShowModal(Window dialog, Window parent = null) {
        parent ??= MainWindow;
        if (parent == null) {
            Shell.UI.OpenWindow(dialog);
            return;
        }
        
        dialog.OwnerProcess = this;
        dialog.ParentWindow = parent;
        dialog.IsModal = true;
        Windows.Add(dialog);
        parent.ChildWindows.Add(dialog);
        Shell.UI.OpenWindow(dialog);
    }
    
    /// <summary>
    /// Closes all windows and enters background mode.
    /// The process continues receiving OnUpdate calls.
    /// </summary>
    public void GoToBackground() {
        foreach (var window in Windows.ToList()) {
            window.Close();
        }
        State = ProcessState.Background;
    }
    
    /// <summary>
    /// Terminates this process, closing all windows.
    /// </summary>
    public void Terminate() {
        if (State == ProcessState.Terminated) return;
        
        OnTerminate();
        
        // Remove tray icons owned by this process
        Shell.SystemTray.RemoveIconsForProcess(this);
        
        foreach (var window in Windows.ToList()) {
            window.Close();
        }
        Windows.Clear();
        
        State = ProcessState.Terminated;
        ProcessManager.Instance.UnregisterProcess(this);
    }
    
    /// <summary>
    /// Called when a window owned by this process is closed.
    /// </summary>
    internal void OnWindowClosed(Window window) {
        DebugLogger.Log($"Process.OnWindowClosed: {AppId} - Removing window '{window.Title}'");
        Windows.Remove(window);
        if (MainWindow == window) {
            MainWindow = Windows.FirstOrDefault(w => w.IsVisible);
            DebugLogger.Log($"Process.OnWindowClosed: {AppId} - MainWindow was closed, new MainWindow: {MainWindow?.Title ?? "null"}");
        }
        
        DebugLogger.Log($"Process.OnWindowClosed: {AppId} - Windows.Count={Windows.Count}, State={State}");
        
        // Auto-terminate if no windows left
        // Note: We don't check State because UpdateState() may have set it to Background
        // during window fade-out animations, but we still want to terminate
        if (Windows.Count == 0) {
            DebugLogger.Log($"Process.OnWindowClosed: {AppId} - Calling Terminate() (no windows left)");
            Terminate();
        } else {
            DebugLogger.Log($"Process.OnWindowClosed: {AppId} - NOT terminating (still have {Windows.Count} window(s))");
        }
    }
    
    /// <summary>
    /// Updates the process state based on window visibility.
    /// </summary>
    internal void UpdateState() {
        if (State == ProcessState.Terminated) return;
        
        // Don't update state if we have no windows - let OnWindowClosed handle termination
        if (Windows.Count == 0) return;
        
        bool hasVisibleWindows = Windows.Any(w => w.IsVisible && w.Opacity > 0.1f);
        State = hasVisibleWindows ? ProcessState.Running : ProcessState.Background;
    }
}
