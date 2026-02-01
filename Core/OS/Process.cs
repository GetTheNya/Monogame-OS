using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Core.UI;
using TheGame.Core.Input;
using TheGame.Graphics;

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

    /// <summary> If true, the process runs its logic on a background thread (useful for console apps). </summary>
    public bool IsThreaded { get; set; } = false;
    private System.Threading.Thread _processThread;
    
    /// <summary>All windows owned by this process.</summary>
    public List<Window> Windows { get; } = new();
    
    /// <summary>The main/primary window of this process (can be null for background).</summary>
    public Window MainWindow { get; set; }

    /// <summary>The icon associated with this process/application.</summary>
    public Texture2D Icon { get; set; }

    /// <summary>Current progress value (-1.0 to 1.0). -1.0 means no progress.</summary>
    public float Progress { get; set; } = -1.0f;

    /// <summary>Color of the progress bar.</summary>
    public Color ProgressColor { get; set; } = new Color(0, 200, 0); // Default Green
    
    /// <summary> Standard I/O streams for this process. </summary>
    public StandardIO IO { get; } = new StandardIO();

    /// <summary> Environment variables for this process. </summary>
    public Dictionary<string, string> Environment { get; } = new();

    /// <summary> The return code of the process when it terminates (0 = Success). </summary>
    public int ExitCode { get; set; } = 0;

    /// <summary> Event triggered when a cancel signal (Ctrl+C) is received. </summary>
    public event Action OnSignalCancel;

    /// <summary> Triggers the cancel signal internally (from Shell or Terminal). </summary>
    internal void TriggerSignalCancel() => OnSignalCancel?.Invoke();

    /// <summary> Returns true if this process has a redirected output stream (console). </summary>
    public bool HasConsole => IO.Out != System.IO.TextWriter.Null;

    // Priority-based update timing (only applies when in Background state)
    internal double UpdateAccumulator;
    internal double UpdateInterval => Priority switch {
        ProcessPriority.High => 0,           // Every frame (no throttle)
        ProcessPriority.Normal => 1.0 / 30.0, // ~30 times per second
        ProcessPriority.Low => 1.0 / 10.0,   // ~10 times per second
        _ => 0
    };
    
    /// <summary>
    /// The application instance associated with this process, if any.
    /// </summary>
    public Application Application { get; internal set; }
    
    public Process() {
    }
    
    // --- Lifecycle Hooks (Legacy Support) ---
    
    /// <summary> Legacy. Use Initialize in Application or override Initialize in Process. </summary>
    public virtual void OnStart(string[] args) { }
    
    /// <summary> Legacy. Use Update in Application or override Update in Process. </summary>
    public virtual void OnUpdate(GameTime gameTime) { }
    
    /// <summary> Legacy. Use Terminate in Application or override Cleanup in Process. </summary>
    public virtual void OnTerminate() { }

    // --- Modern Lifecycle Methods ---

    protected internal virtual void Initialize(string[] args) {
        if (IsThreaded) {
            _processThread = new System.Threading.Thread(() => {
                try {
                    OnStart(args);
                    if (Application != null) {
                        var onLoadMethod = Application.GetType().GetMethod("OnLoad", 
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        onLoadMethod?.Invoke(Application, new object[] { args });
                    }
                    
                    // Most console apps will block on ReadLine inside OnLoad or a helper.
                    // If they use OnUpdate, we still want to support that in a loop.
                    while (State != ProcessState.Terminated) {
                        Update(new GameTime());
                        System.Threading.Thread.Sleep(1); 
                    }
                } catch (Exception ex) {
                    DebugLogger.Log($"Threaded process {AppId} crashed: {ex.Message}");
                    if (CrashHandler.IsAppException(ex, this)) {
                        CrashHandler.HandleAppException(this, ex);
                    } else {
                        Terminate();
                    }
                }
            });
            _processThread.IsBackground = true;
            _processThread.Start();
            return;
        }

        OnStart(args);
        if (Application != null) {
            var onLoadMethod = Application.GetType().GetMethod("OnLoad", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            onLoadMethod?.Invoke(Application, new object[] { args });
        }
    }

    protected internal virtual void Update(GameTime gameTime) {
        // Skip main-thread update if this is a threaded process (it's already running its own loop)
        if (IsThreaded && System.Threading.Thread.CurrentThread != _processThread) return;

        OnUpdate(gameTime);
        if (Application != null) {
            var onUpdateMethod = Application.GetType().GetMethod("OnUpdate", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            onUpdateMethod?.Invoke(Application, new object[] { gameTime });
        }
    }

    protected internal virtual void Draw(SpriteBatch spriteBatch, ShapeBatch shapeBatch) {
        // Call OnDraw directly (not obsolete wrapper)
        if (Application != null) {
            var onDrawMethod = Application.GetType().GetMethod("OnDraw", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            onDrawMethod?.Invoke(Application, new object[] { spriteBatch, shapeBatch });
        }
    }

    protected internal virtual void Cleanup() {
        OnTerminate();
        // Call OnClose directly (not obsolete wrapper)
        if (Application != null) {
            var onCloseMethod = Application.GetType().GetMethod("OnClose", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            onCloseMethod?.Invoke(Application, null);
        }
    }
    
    // --- Public API ---
    
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
    public void ShowModal(Window dialog, Window parent = null, Rectangle? startBounds = null) {
        parent ??= MainWindow;
        if (parent == null) {
            Shell.UI.OpenWindow(dialog, startBounds);
            return;
        }
        
        dialog.OwnerProcess = this;
        dialog.ParentWindow = parent;
        dialog.IsModal = true;
        Windows.Add(dialog);
        parent.ChildWindows.Add(dialog);
        Shell.UI.OpenWindow(dialog, startBounds);
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
        
        State = ProcessState.Terminated;

        // Wake up any blocking ReadLine
        if (IO.In is TerminalReader tr) tr.EnqueueInput(null); 

        Cleanup();
        
        // Dispose I/O streams
        IO.Dispose();

        // Clear signal handlers
        OnSignalCancel = null;

        // Remove tray icons owned by this process
        Shell.SystemTray.RemoveIconsForProcess(this);

        // Remove media owned by this process
        AudioManager.Instance.CleanupProcess(this);
        
        // Remove local hotkeys for this process
        Shell.Hotkeys.UnregisterLocal(this);
        
        foreach (var window in Windows.ToList()) {
            window.Terminate();
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
        
        bool wasMainWindow = MainWindow == window;
        if (wasMainWindow) {
            MainWindow = Windows.FirstOrDefault(w => w.IsVisible);
            DebugLogger.Log($"Process.OnWindowClosed: {AppId} - MainWindow was closed, new MainWindow: {MainWindow?.Title ?? "null"}");
        }
        
        DebugLogger.Log($"Process.OnWindowClosed: {AppId} - Windows.Count={Windows.Count}, State={State}");
        
        // Check ExitOnMainWindowClose for Application-based apps
        if (wasMainWindow && Application != null && !Application.ExitOnMainWindowClose) {
            // Don't terminate, just go to background
            if (Windows.Count == 0) {
                DebugLogger.Log($"Process.OnWindowClosed: {AppId} - Going to background (ExitOnMainWindowClose=false)");
                State = ProcessState.Background;
            }
            return;
        }
        
        // Auto-terminate if no windows left
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
