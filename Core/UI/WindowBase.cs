using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using TheGame.Core.Input;
using TheGame.Core.OS;
using TheGame.Graphics;

namespace TheGame.Core.UI;

public abstract class WindowBase : UIElement {
    public static WindowBase ActiveWindow { get; set; }

    /// <summary>
    /// Class that provides APIs for application to communicate with OS
    /// </summary>
    public SystemAPI SystemAPI => OwnerProcess?.SystemAPI;

    /// <summary>
    /// Called when the window is closed (before removal from parent).
    /// </summary>
    public event Action OnClosed;

    /// <summary>
    /// Called when a close is requested. Call the provided callback with 'true' to proceed with closing.
    /// </summary>
    public event Action<Action<bool>> OnCloseRequested;

    // Process ownership
    private Process _ownerProcess;
    private bool _onLoadCalled;

    public Process OwnerProcess {
        get => _ownerProcess;
        internal set {
            if (_ownerProcess == value) return;
            _ownerProcess = value;
            // Call OnLoad when OwnerProcess is first set
            if (_ownerProcess != null && !_onLoadCalled) {
                _onLoadCalled = true;
                try {
                    OnLoad();
                } catch (Exception ex) {
                    if (_ownerProcess != null && CrashHandler.IsAppException(ex, _ownerProcess)) {
                        CrashHandler.HandleAppException(_ownerProcess, ex);
                    } else {
                        throw;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Called after OwnerProcess is assigned. Build your UI here.
    /// </summary>
    protected virtual void OnLoad() { }

    /// <summary>
    /// Called every frame after the base Update logic completes.
    /// No need to call base - the framework handles it automatically.
    /// </summary>
    protected virtual void OnUpdate(GameTime gameTime) { }

    /// <summary>
    /// Called during Draw, inside the scissor-clipped content area.
    /// Use this for custom rendering. No need to call base.
    /// </summary>
    protected virtual void OnDraw(SpriteBatch spriteBatch, ShapeBatch batch) { }

    // Modal support
    public WindowBase ParentWindow { get; set; }
    public List<WindowBase> ChildWindows { get; } = new();
    public bool IsModal { get; set; }
    public bool IsBlocked => ChildWindows.Any(c => c.IsModal && c.IsVisible);

    // RenderTarget2D for off-screen window content rendering
    protected RenderTarget2D _windowRenderTarget;
    public RenderTarget2D WindowRenderTarget => _windowRenderTarget;

    /// <summary>
    /// A static snapshot of the window texture, used for previews when the window is minimized or animating.
    /// </summary>
    public Texture2D Snapshot { get; protected set; }

    protected ShapeBatch _contentBatch;
    protected RasterizerState _scissorState;

    // Taskbar control
    public bool ShowInTaskbar { get; set; } = true;
    public string AppId { get; set; }
    public virtual string Title { get; set; } = "Window";

    private Texture2D _icon;
    public Texture2D Icon { 
        get => _icon ?? OwnerProcess?.Icon; 
        set => _icon = value; 
    }

    public WindowBase() : this(Vector2.Zero, new Vector2(400, 300)) { }

    public WindowBase(Vector2 position, Vector2 size) {
        Position = position;
        Size = size;
        _contentBatch = new ShapeBatch(G.GraphicsDevice, G.ContentManager);
        _scissorState = new RasterizerState { ScissorTestEnable = true };
    }

    /// <summary>
    /// Captures the current content of the WindowRenderTarget into a Snapshot texture.
    /// </summary>
    public void CaptureSnapshot() {
        if (_windowRenderTarget == null) return;
        
        // Dispose old snapshot if it exists
        Snapshot?.Dispose();
        
        // Create a new texture and copy data
        Snapshot = new Texture2D(G.GraphicsDevice, _windowRenderTarget.Width, _windowRenderTarget.Height);
        Color[] data = new Color[_windowRenderTarget.Width * _windowRenderTarget.Height];
        _windowRenderTarget.GetData(data);
        Snapshot.SetData(data);
    }

    public override void Update(GameTime gameTime) {
        bool obscuredByAnotherWindow = InputManager.IsMouseConsumed;
        
        // Block ALL updates (including child controls) if a modal dialog is active
        if (IsBlocked) {
            bool isHoveringThisWindow = InputManager.IsMouseHovering(Bounds) && !obscuredByAnotherWindow;
            bool isJustPressed = isHoveringThisWindow && InputManager.IsMouseButtonJustPressed(MouseButton.Left);
            
            if (isJustPressed) {
                var modalChild = ChildWindows.FirstOrDefault(c => c.IsModal && c.IsVisible);
                if (modalChild != null) {
                    ActiveWindow = modalChild;
                    modalChild.Parent?.BringToFront(modalChild);
                    if (modalChild is Window win) win.BlinkTitleBar();
                }
                InputManager.IsMouseConsumed = true;
            }
            return;
        }

        try {
            base.Update(gameTime);
        } catch (Exception ex) {
            if (OwnerProcess != null && CrashHandler.IsAppException(ex, OwnerProcess)) {
                CrashHandler.HandleAppException(OwnerProcess, ex);
            } else {
                throw;
            }
        }
        
        try {
            OnUpdate(gameTime);
        } catch (Exception ex) {
            if (OwnerProcess != null && CrashHandler.IsAppException(ex, OwnerProcess)) {
                CrashHandler.HandleAppException(OwnerProcess, ex);
            } else {
                throw;
            }
        }
    }

    public override void Draw(SpriteBatch spriteBatch, ShapeBatch batch) {
        if (!IsVisible && Opacity <= 0) return;

        try {
            DrawWindowToRT(spriteBatch, batch);
        } catch (Exception ex) {
            if (OwnerProcess != null && CrashHandler.IsAppException(ex, OwnerProcess)) {
                CrashHandler.HandleAppException(OwnerProcess, ex);
            } else {
                throw;
            }
        }

        // Final barrier flush
        batch.End();
        spriteBatch.End();
        batch.Begin();
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
    }

    protected abstract void DrawWindowToRT(SpriteBatch spriteBatch, ShapeBatch globalBatch);

    protected void EnsureRenderTarget(GraphicsDevice gd, int width, int height) {
        width = Math.Max(1, width);
        height = Math.Max(1, height);

        if (_windowRenderTarget == null || 
            _windowRenderTarget.Width != width || 
            _windowRenderTarget.Height != height) {
            
            _windowRenderTarget?.Dispose();
            _windowRenderTarget = new RenderTarget2D(
                gd, 
                width, 
                height,
                false,
                SurfaceFormat.Color,
                DepthFormat.None,
                0,
                RenderTargetUsage.DiscardContents
            );
        }
    }

    public virtual void HandleFocus() {
        if (ActiveWindow != this) {
            ActiveWindow = this;
            Parent?.BringToFront(this);
        }
    }

    public virtual void Close() {
        if (OnCloseRequested != null) {
            OnCloseRequested.Invoke((canClose) => {
                if (canClose) ExecuteClose();
            });
            return;
        }
        ExecuteClose();
    }

    public virtual void Terminate() {
        ExecuteClose();
    }

    protected virtual void ExecuteClose() {
        foreach (var child in ChildWindows.ToList()) {
            child.ExecuteClose();
        }
        ChildWindows.Clear();
        
        if (ParentWindow != null) {
            ParentWindow.ChildWindows.Remove(this);
            ParentWindow = null;
        }
        
        if (ActiveWindow == this) {
            FindAndFocusNextWindow();
        }

        OnClosed?.Invoke();

        // Basic fade out or immediate removal
        Parent?.RemoveChild(this);
        OwnerProcess?.OnWindowClosed(this);
        DisposeGraphicsResources();
    }

    protected void FindAndFocusNextWindow() {
        if (Parent == null) return;
        for (int i = Parent.Children.Count - 1; i >= 0; i--) {
            var child = Parent.Children[i] as WindowBase;
            if (child != null && child != this && child.IsVisible && child.Opacity > 0.5f) {
                ActiveWindow = child;
                return;
            }
        }
        if (ActiveWindow == this) ActiveWindow = null;
    }

    protected virtual void DisposeGraphicsResources() {
        _windowRenderTarget?.Dispose();
        _windowRenderTarget = null;
        Snapshot?.Dispose();
        Snapshot = null;
    }
}
