using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Core.UI;
using TheGame.Graphics;

namespace TheGame.Core.OS;

/// <summary>
/// Represents a unified application that manages both background logic and user interface.
/// This is the modern entry point for all apps in the OS.
/// </summary>
public abstract class Application {
    /// <summary>
    /// The underlying OS process managing this application.
    /// </summary>
    public Process Process { get; set; }
    
    /// <summary> Standard I/O streams for this application's process. </summary>
    public StandardIO IO => Process?.IO;

    /// <summary> Standard input stream. </summary>
    public System.IO.TextReader StandardInput => IO?.In ?? System.IO.TextReader.Null;

    /// <summary> Standard output stream. </summary>
    public System.IO.TextWriter StandardOutput => IO?.Out ?? System.IO.TextWriter.Null;

    /// <summary> Standard error stream. </summary>
    public System.IO.TextWriter StandardError => IO?.Error ?? System.IO.TextWriter.Null;

    /// <summary>
    /// Class that provides APIs for application to communicate with OS
    /// </summary>
    public SystemAPI SystemAPI => Process.SystemAPI;

    /// <summary>
    /// The primary window of the application. May be null for background services.
    /// </summary>
    public Window MainWindow { get; protected set; }
    
    /// <summary>
    /// Gets a list of all windows currently owned by this application.
    /// </summary>
    public List<Window> Windows => Process?.Windows;
    
    /// <summary>
    /// If true (default), closing MainWindow terminates the process.
    /// If false, the app goes to background mode instead.
    /// </summary>
    public bool ExitOnMainWindowClose { get; set; } = true;
    
    /// <summary>
    /// Gets or sets the process priority for background throttling.
    /// </summary>
    public ProcessPriority Priority {
        get => Process?.Priority ?? ProcessPriority.Normal;
        set { if (Process != null) Process.Priority = value; }
    }

    /// <summary>
    /// If true, the application logic is asynchronous and runs on the main thread (cooperatively).
    /// Default is false. TerminalApplication overrides this to true.
    /// </summary>
    public virtual bool IsAsync => false;

    /// <summary>
    /// Internal entry point that handles the transition from process start to application logic.
    /// This ensures that both sync and async paths follow the same initialization steps.
    /// </summary>
    internal async System.Threading.Tasks.Task StartAsync(string[] args) {
        OnPrepare(args);
        
        if (IsAsync) {
            await OnLoadAsync(args);
        } else {
            OnLoad(args);
        }
    }

    /// <summary>
    /// Called before OnLoad or OnLoadAsync. Use this for plumbing that must run regardless of the load path.
    /// </summary>
    protected virtual void OnPrepare(string[] args) { }
    
    // --- Lifecycle Hooks (implement these, don't call base) ---
    
    /// <summary>
    /// Called once when the application starts. Use this to create windows and initialize resources.
    /// </summary>
    protected virtual void OnLoad(string[] args) { }

    /// <summary>
    /// Async version of OnLoad. Used if IsAsync is true.
    /// </summary>
    protected virtual System.Threading.Tasks.Task OnLoadAsync(string[] args) => System.Threading.Tasks.Task.CompletedTask;
    
    /// <summary>
    /// Called every frame for the application to update its state.
    /// This runs even if all windows are closed, as long as the process is alive.
    /// Respects ProcessPriority throttling.
    /// </summary>
    protected virtual void OnUpdate(GameTime gameTime) { }

    /// <summary>
    /// Async version of OnUpdate. Used if IsAsync is true.
    /// </summary>
    protected virtual System.Threading.Tasks.Task OnUpdateAsync(GameTime gameTime) => System.Threading.Tasks.Task.CompletedTask;
    
    /// <summary>
    /// Called by the OS to allow the application to draw global/overlay visuals.
    /// </summary>
    protected virtual void OnDraw(SpriteBatch spriteBatch, ShapeBatch shapeBatch) { }
    
    /// <summary>
    /// Called when the application is about to be terminated. Use this for cleanup.
    /// </summary>
    protected virtual void OnClose() { }
    
    // --- Legacy Methods (for backward compatibility) ---
    
    [Obsolete("Use OnLoad instead")]
    public virtual void Initialize(string[] args) => OnLoad(args);
    
    [Obsolete("Use OnUpdate instead")]
    public virtual void Update(GameTime gameTime) => OnUpdate(gameTime);
    
    [Obsolete("Use OnDraw instead")]
    public virtual void Draw(SpriteBatch spriteBatch, ShapeBatch shapeBatch) => OnDraw(spriteBatch, shapeBatch);
    
    [Obsolete("Use OnClose instead")]
    public virtual void Terminate() => OnClose();
    
    // --- Window Creation Helpers ---

    /// <summary>
    /// Creates and returns a new window associated with this application.
    /// </summary>
    protected T CreateWindow<T>() where T : Window, new() {
        if (Process == null) throw new InvalidOperationException("Application has not been initialized with a Process.");
        return Shell.Process.CreateWindow<T>(Process);
    }

    /// <summary>
    /// Opens a window and registers it with the UI layer.
    /// </summary>
    public void OpenWindow(Window window, Rectangle? startBounds = null) {
        Shell.UI.OpenWindow(window, startBounds, Process);
    }
    
    /// <summary>
    /// Opens the MainWindow with optional animation from startBounds.
    /// <para>
    /// <b>WARNING:</b> Do NOT call this from OnLoad() - ProcessManager automatically 
    /// opens MainWindow with animation from the launcher (desktop icon, explorer, etc.).
    /// </para>
    /// <para>
    /// Use this only for:
    /// <list type="bullet">
    /// <item>Re-showing MainWindow after going to background mode</item>
    /// <item>Opening MainWindow lazily (not during OnLoad)</item>
    /// </list>
    /// </para>
    /// </summary>
    public void OpenMainWindow(Rectangle? startBounds = null) {
        if (MainWindow != null) OpenWindow(MainWindow, startBounds);
    }

    /// <summary>
    /// Shows a modal dialog that blocks input to the application's main window.
    /// </summary>
    public void OpenModal(Window dialog, Rectangle? startBounds = null) {
        if (Process == null) {
            Shell.UI.OpenWindow(dialog, startBounds);
            return;
        }
        Shell.Process.ShowModal(Process, dialog, MainWindow, startBounds);
    }
    
    /// <summary>
    /// Creates and shows a modal dialog of the specified type.
    /// </summary>
    public void ShowModal<T>(Action<T> configure = null) where T : Window, new() {
        var dialog = CreateWindow<T>();
        configure?.Invoke(dialog);
        OpenModal(dialog);
    }
    
    // --- Process Control ---

    /// <summary>
    /// Terminates the application and closes all its windows.
    /// </summary>
    public void Exit() {
        if (Process != null) Shell.Process.Exit(Process);
    }

    /// <summary>
    /// Closes all windows and enters background mode.
    /// The process continues receiving OnUpdate calls.
    /// </summary>
    public void GoToBackground() {
        if (Process != null) Shell.Process.GoToBackground(Process);
    }

    /// <summary>
    /// Sets the progress value on the application's taskbar button.
    /// value: -1.0 to 1.0. -1.0 hides the progress bar.
    /// </summary>
    public void SetProgress(float value, Color? color = null) {
        if (Process != null) Shell.Taskbar.SetProgress(Process, value, color);
    }

    // --- Standard I/O Helpers ---

    /// <summary> Writes text to standard output. </summary>
    public void Write(string text) => StandardOutput.Write(text);

    /// <summary> Writes a line of text to standard output. </summary>
    public void WriteLine(string text = "") => StandardOutput.WriteLine(text);

    /// <summary> Writes a line of text to standard output with a specific color (using ANSI codes). </summary>
    public void WriteLine(string text, Color color) {
        StandardOutput.WriteLine(AnsiCodes.Wrap(text, color));
    }



    /// <summary> Reads a line from standard input. </summary>
    public string ReadLine() => StandardInput.ReadLine();

    /// <summary> Reads a line from standard input asynchronously. </summary>
    public virtual System.Threading.Tasks.Task<string> ReadLineAsync() => StandardInput.ReadLineAsync();
}
