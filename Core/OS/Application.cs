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
    public Process Process { get; internal set; }
    
    /// <summary>
    /// The primary window of the application. May be null for background services.
    /// </summary>
    public Window MainWindow { get; protected set; }
    
    /// <summary>
    /// Gets a list of all windows currently owned by this application.
    /// </summary>
    public List<Window> Windows => Process?.Windows;
    
    /// <summary>
    /// Called once when the application starts. Use this to create windows and initialize resources.
    /// </summary>
    public virtual void Initialize(string[] args) { }
    
    /// <summary>
    /// Called every frame for the application to update its state.
    /// This runs even if all windows are closed, as long as the process is alive.
    /// </summary>
    public virtual void Update(GameTime gameTime) { }
    
    /// <summary>
    /// Called by the OS to allow the application to draw global/overlay visuals.
    /// Usually, windows handle their own drawing, but this hook allows for screen-wide effects.
    /// </summary>
    public virtual void Draw(SpriteBatch spriteBatch, ShapeBatch shapeBatch) { }
    
    /// <summary>
    /// Called when the application is about to be terminated. Use this for cleanup.
    /// </summary>
    public virtual void Terminate() { }

    /// <summary>
    /// Creates and returns a new window associated with this application.
    /// </summary>
    protected T CreateWindow<T>() where T : Window, new() {
        if (Process == null) throw new InvalidOperationException("Application has not been initialized with a Process.");
        return Process.CreateWindow<T>();
    }

    /// <summary>
    /// Opens a window and registers it with the UI layer.
    /// </summary>
    public void OpenWindow(Window window, Rectangle? startBounds = null) {
        Shell.UI.OpenWindow(window, startBounds);
    }

    /// <summary>
    /// Shows a modal dialog that blocks input to the application's main window.
    /// </summary>
    public void OpenModal(Window dialog, Rectangle? startBounds = null) {
        if (Process == null) {
            Shell.UI.OpenWindow(dialog, startBounds);
            return;
        }
        Process.ShowModal(dialog, MainWindow, startBounds);
    }

    /// <summary>
    /// Terminates the application and closes all its windows.
    /// </summary>
    public void Exit() {
        Process?.Terminate();
    }
}
