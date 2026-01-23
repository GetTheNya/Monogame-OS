using Microsoft.Xna.Framework;
using FontStashSharp;
using TheGame.Core.Input;
using TheGame.Graphics;
using TheGame.Core.Animation;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using TheGame.Core.UI.Controls;
using TheGame.Core.OS;

namespace TheGame.Core.UI;

public class Window : UIElement {
    public static Window ActiveWindow { get; set; }
    private const int TitleBarHeight = 30;
    private const int ResizeHandleSize = 15;

    // Logic states
    private bool _isDragging;
    private Vector2 _dragOffset;
    private bool _isResizing;
    private Vector2 _resizeStartSize;
    private Vector2 _resizeStartMouse;
    private Vector2 _resizeStartPos;
    private Vector2 _resizeDir;

    // Drag/Click Discriminationw
    private bool _potentialDrag;
    private Vector2 _potentialDragStart;

    private bool _isMaximized;
    private Rectangle _restoreRect;

    public bool IsMaximized => _isMaximized;
    public Rectangle RestoreBounds => _restoreRect;
    public bool LayoutDirty { get; set; }

    // Animation / Minimize state
    private Vector2 _preMinimizeSize;
    private Vector2 _preMinimizePos;
    private bool _wasMaximizedBeforeMinimize;
    private bool _isAnimating;

    // Buttons
    private Button _closeButton;
    private Button _maxButton;
    public Button _minButton;

    // Snapping
    private Rectangle? _snapPreview;
    private Vector2 _renderSnapPos;
    private Vector2 _renderSnapSize;
    private float _snapPreviewOpacity;
    private bool _obscuredByAnotherWindow;
    
    // Title bar blink animation
    private bool _isBlinking;
    private double _blinkTimer;
    private const double BlinkDuration = 0.6; // Total blink duration in seconds
    
    /// <summary>
    /// Called when the window is closed (before removal from parent).
    /// </summary>
    public event Action OnClosed;

    public string Title { get; set; } = "Window";
    public Texture2D Icon { get; set; }
    public string AppId { get; set; }
    public bool CanResize { get; set; } = true;
    public bool ShowTitleBar { get; set; } = true;
    public Color BackgroundColor { get; set; } = new Color(30, 30, 30, 240); // Dark semi-transparent
    public Color BorderColor { get; set; } = Color.White;

    // Process ownership
    private Process _ownerProcess;
    public Process OwnerProcess {
        get => _ownerProcess;
        internal set {
            _ownerProcess = value;
            OnOwnerProcessSet();
        }
    }

    /// <summary>
    /// Called when the OwnerProcess is assigned to the window.
    /// Override this to perform setup that requires the process owner (like hotkey registration).
    /// </summary>
    protected virtual void OnOwnerProcessSet() { }
    
    // Modal support
    public Window ParentWindow { get; set; }
    public List<Window> ChildWindows { get; } = new();
    public bool IsModal { get; set; }
    public bool IsBlocked => ChildWindows.Any(c => c.IsModal && c.IsVisible);
    
    // Taskbar control
    public bool ShowInTaskbar { get; set; } = true;

    // Animation Properties

    public override Vector2 ClientSize => Size - new Vector2(0, TitleBarHeight);

    public void AnimateOpen(Rectangle fromBounds) {
        Vector2 targetPos = Position;
        Vector2 targetSize = Size;

        Position = fromBounds.Location.ToVector2();
        Size = fromBounds.Size.ToVector2();
        Opacity = 0f;

        Tweener.To(this, p => Position = p, Position, targetPos, 0.4f, Easing.EaseOutQuad);
        Tweener.To(this, s => Size = s, Size, targetSize, 0.4f, Easing.EaseOutQuad);
        Tweener.To(this, o => Opacity = o, 0f, 1f, 0.2f, Easing.Linear);
    }
    
    /// <summary>
    /// Triggers a title bar blink animation (used when clicking a blocked window).
    /// </summary>
    public void BlinkTitleBar() {
        _isBlinking = true;
        _blinkTimer = 0;
    }

    private ShapeBatch _contentBatch;
    private RasterizerState _scissorState;
    
    // RenderTarget2D for off-screen window content rendering
    private RenderTarget2D _windowRenderTarget;
    public RenderTarget2D WindowRenderTarget => _windowRenderTarget;

    /// <summary>
    /// A static snapshot of the window texture, used for previews when the window is minimized or animating.
    /// </summary>
    public Texture2D Snapshot { get; private set; }

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

    public Window(Vector2 position, Vector2 size) {
        Position = position;
        Size = size;

        _contentBatch = new ShapeBatch(G.GraphicsDevice, G.ContentManager);
        _scissorState = new RasterizerState { ScissorTestEnable = true };

        // Initialize Buttons
        _closeButton = new Button(Vector2.Zero, new Vector2(20, 20), "X") {
            BackgroundColor = Color.Transparent,
            BorderColor = Color.Transparent,
            HoverColor = new Color(200, 50, 50),
            OnClickAction = () => { Close(); }
        };
        AddChild(_closeButton);

        _maxButton = new Button(Vector2.Zero, new Vector2(20, 20), "O") {
            BackgroundColor = Color.Transparent,
            BorderColor = Color.Transparent,
            OnClickAction = () => {
                // Hardcoded work area for now (Screen - 40px taskbar)
                var viewport = G.GraphicsDevice.Viewport;
                ToggleMaximize(new Rectangle(0, 0, viewport.Width, viewport.Height - 40));
            }
        };
        AddChild(_maxButton);

        _minButton = new Button(Vector2.Zero, new Vector2(20, 20), "_") {
            BackgroundColor = Color.Transparent,
            BorderColor = Color.Transparent,
            OnClickAction = () => { Minimize(); }
        };
        AddChild(_minButton);

        _preMinimizePos = position;
        _preMinimizeSize = size;
        _restoreRect = new Rectangle(position.ToPoint(), size.ToPoint());

        UpdateButtons();
        
        // Ensure chrome buttons are at the end of the children list for priority
        BringToFront(_closeButton);
        BringToFront(_maxButton);
        if (_minButton != null) BringToFront(_minButton);
    }

    public override void Update(GameTime gameTime) {
        _obscuredByAnotherWindow = InputManager.IsMouseConsumed;
        
        // Block ALL updates (including child controls) if a modal dialog is active
        if (IsBlocked) {
            // Check if user clicked on THIS blocked window (not on the modal itself)
            // Use normal hover check (respects consumed) so it doesn't trigger when modal is on top
            bool isHoveringThisWindow = InputManager.IsMouseHovering(Bounds) && !_obscuredByAnotherWindow;
            bool isJustPressed = isHoveringThisWindow && InputManager.IsMouseButtonJustPressed(MouseButton.Left);
            
            if (isJustPressed) {
                // Find the modal child and bring it to focus with a blink
                var modalChild = ChildWindows.FirstOrDefault(c => c.IsModal && c.IsVisible);
                if (modalChild != null) {
                    ActiveWindow = modalChild;
                    modalChild.Parent?.BringToFront(modalChild);
                    // Trigger title bar blink animation
                    modalChild.BlinkTitleBar();
                }
                InputManager.IsMouseConsumed = true;
            }
            return; // Don't update children or window input
        }
        
        // Update title bar blink animation
        if (_isBlinking) {
            _blinkTimer += gameTime.ElapsedGameTime.TotalSeconds;
            if (_blinkTimer >= BlinkDuration) {
                _isBlinking = false;
                _blinkTimer = 0;
            }
        }

        // If we are actively dragging or resizing, we MUST consume the mouse
        // BEFORE children update so they don't see it as a hover/click.
        if (_isDragging || _isResizing) {
            InputManager.IsMouseConsumed = true;
            InputManager.IsScrollConsumed = true; // Block wheel too
        }
        
        // CRITICAL: Check for resize/drag BEFORE children update
        // This prevents clicking on resize edges from triggering controls underneath
        if (IsVisible && !_obscuredByAnotherWindow && !_isDragging && !_isResizing) {
            const int ResizeEdge = 6;
            bool isHoveringRaw = InputManager.IsMouseHovering(Bounds, ignoreConsumed: true);
            bool isJustPressed = isHoveringRaw && InputManager.IsMouseButtonJustPressed(MouseButton.Left);
            
            // Check for resize edge clicks FIRST (before children can consume input)
            if (isJustPressed && !_isMaximized && CanResize) {
                var mousePos = InputManager.MousePosition;
                Rectangle bounds = new Rectangle((int)AbsolutePosition.X, (int)AbsolutePosition.Y, (int)Size.X, (int)Size.Y);
                
                if (bounds.Contains(mousePos)) {
                    bool left = mousePos.X >= bounds.Left && mousePos.X <= bounds.Left + ResizeEdge;
                    bool right = mousePos.X >= bounds.Right - ResizeEdge && mousePos.X <= bounds.Right;
                    bool top = mousePos.Y >= bounds.Top && mousePos.Y <= bounds.Top + ResizeEdge;
                    bool bottom = mousePos.Y >= bounds.Bottom - ResizeEdge && mousePos.Y <= bounds.Bottom;

                    if (left || right || top || bottom) {
                        // Consume input IMMEDIATELY before children can see it
                        InputManager.IsMouseConsumed = true;
                        _isResizing = true;
                        _resizeDir = Vector2.Zero;
                        if (left) _resizeDir.X = -1;
                        if (right) _resizeDir.X = 1;
                        if (top) _resizeDir.Y = -1;
                        if (bottom) _resizeDir.Y = 1;

                        _resizeStartMouse = mousePos.ToVector2();
                        _resizeStartSize = Size;
                        _resizeStartPos = Position;

                        HandleFocus();
                    }
                }
            }
        }

        // --- NEW: Focus handling moved earlier ---
        if (IsVisible && !_isAnimating && !_obscuredByAnotherWindow) {
            bool isHoveringRaw = InputManager.IsMouseHovering(Bounds, ignoreConsumed: true);
            bool isJustPressed = isHoveringRaw && InputManager.IsMouseButtonJustPressed(MouseButton.Left);
            if (isJustPressed) {
                HandleFocus();
            }
        }
        // -----------------------------------------
        
        UpdateButtons();
        
        try {
            base.Update(gameTime);
        } catch (Exception ex) {
            if (OwnerProcess != null && CrashHandler.IsAppException(ex, OwnerProcess)) {
                CrashHandler.HandleAppException(OwnerProcess, ex);
            } else {
                throw;
            }
        }
    }

    public override UIElement GetElementAt(Vector2 pos) {
        if (!IsVisible || !Bounds.Contains(pos)) return null;

        // 1. Check Chrome Buttons (Topmost priority)
        var foundChrome = _closeButton.GetElementAt(pos) ?? 
                          (_maxButton.IsVisible ? _maxButton.GetElementAt(pos) : null) ?? 
                          _minButton.GetElementAt(pos);
        if (foundChrome != null) return foundChrome;

        // 2. Check Title Bar area (Blocks children under the title bar)
        Rectangle titleBarRect = new Rectangle((int)AbsolutePosition.X, (int)AbsolutePosition.Y, (int)Size.X, TitleBarHeight);
        if (titleBarRect.Contains(pos)) return this;

        // 3. Check Content Area
        Rectangle contentRect = new Rectangle((int)AbsolutePosition.X, (int)AbsolutePosition.Y + TitleBarHeight, (int)Size.X, (int)Size.Y - TitleBarHeight);
        if (contentRect.Contains(pos)) {
            // Only check children that are NOT chrome buttons (they were already checked)
            for (int i = Children.Count - 1; i >= 0; i--) {
                var child = Children[i];
                if (child == _closeButton || child == _maxButton || child == _minButton) continue;
                var found = child.GetElementAt(pos);
                if (found != null) return found;
            }
        }

        return ConsumesInput ? this : null;
    }

    public override Vector2 GetChildOffset(UIElement child) {
        // Only content children get offset. Title bar buttons stay at the top.
        if (child == _closeButton || child == _maxButton || child == _minButton) return Vector2.Zero;
        return new Vector2(0, TitleBarHeight);
    }

    private void UpdateButtons() {
        float rightSide = Size.X;
        float top = 5;

        _closeButton.Position = new Vector2(rightSide - 25, top);
        _maxButton.Position = new Vector2(rightSide - 50, top);
        _minButton.Position = new Vector2(rightSide - 75, top);

        // Hide maximize button if resizing is disabled
        _maxButton.IsVisible = CanResize;
    }

    public void Minimize() {
        Vector2 fallback = new Vector2(Position.X + Size.X / 2f, G.GraphicsDevice.Viewport.Height);
        Minimize(Taskbar.Instance?.GetButtonCenter(OwnerProcess) ?? fallback);
    }

    public void Minimize(Vector2 targetPos) {
        if (_isAnimating) return;
        
        // Capture a snapshot before starting the minimize animation
        // so we have a good quality image for the taskbar preview.
        CaptureSnapshot();

        _isAnimating = true;

        Tweener.CancelAll(this);

        // Save current state to restore to
        _preMinimizePos = Position;
        _preMinimizeSize = Size;
        _wasMaximizedBeforeMinimize = _isMaximized;

        float duration = 0.3f;
        var easing = Easing.EaseInQuad;

        Tweener.To(this, v => Position = v, Position, targetPos, duration, easing);
        Tweener.To(this, v => Size = v, Size, new Vector2(50, 20), duration, easing);
        Tweener.To(this, v => Opacity = v, Opacity, 0f, duration, easing).OnComplete = () => {
            IsVisible = false;
            _isAnimating = false;
        };

        if (ActiveWindow == this) {
            FindAndFocusNextWindow();
        }
    }

    private void FindAndFocusNextWindow() {
        if (Parent == null) return;

        // Search through children from top to bottom (end of list is top)
        for (int i = Parent.Children.Count - 1; i >= 0; i--) {
            var child = Parent.Children[i] as Window;
            // Must be a visible window that isn't the one currently losing focus
            if (child != null && child != this && child.IsVisible && child.Opacity > 0.5f) {
                ActiveWindow = child;
                return;
            }
        }

        // Fallback: no windows left to focus
        if (ActiveWindow == this) ActiveWindow = null;
    }

    public void Restore() {
        Vector2 fallback = new Vector2(Position.X + Size.X / 2f, G.GraphicsDevice.Viewport.Height);
        Restore(Taskbar.Instance?.GetButtonCenter(OwnerProcess) ?? fallback);
    }

    public void Restore(Vector2 sourcePos) {
        if (_isAnimating) return;
        
        // Clear snapshot when restoring so we use live RT for previews
        Snapshot?.Dispose();
        Snapshot = null;

        _isAnimating = true;

        DebugLogger.Log($"Window.Restore() called: {Title}");
        Tweener.CancelAll(this);

        IsVisible = true;
        // Start from taskbar
        Position = sourcePos;
        Size = new Vector2(50, 20);
        Opacity = 0f;

        float duration = 0.3f;
        var easing = Easing.EaseOutQuad;

        // If was maximized, restore to current work area (not saved position)
        Vector2 targetPos, targetSize;
        if (_wasMaximizedBeforeMinimize) {
            var viewport = G.GraphicsDevice.Viewport;
            var workArea = new Rectangle(0, 0, viewport.Width, viewport.Height - 40);
            targetPos = workArea.Location.ToVector2();
            targetSize = workArea.Size.ToVector2();
            _isMaximized = true;
        } else {
            // Ensure we have some safe defaults if never minimized
            if (_preMinimizeSize == Vector2.Zero) _preMinimizeSize = new Vector2(400, 300);
            if (_preMinimizePos == Vector2.Zero) _preMinimizePos = new Vector2(100, 100);
            targetPos = _preMinimizePos;
            targetSize = _preMinimizeSize;
        }

        Tweener.To(this, v => Position = v, Position, targetPos, duration, easing);
        Tweener.To(this, v => Size = v, Size, targetSize, duration, easing);
        Tweener.To(this, v => Opacity = v, 0f, 1f, duration, easing).OnComplete = () => { _isAnimating = false; };

        Parent?.BringToFront(this);
        ActiveWindow = this;
    }

    public void Close() {
        // Close child modal windows first
        foreach (var child in ChildWindows.ToList()) {
            child.Close();
        }
        ChildWindows.Clear();
        
        // Remove from parent's child list if this is a modal
        if (ParentWindow != null) {
            ParentWindow.ChildWindows.Remove(this);
            ParentWindow = null;
        }
        
        if (ActiveWindow == this) {
            FindAndFocusNextWindow();
        }

        // Invoke close event (for tray icon cleanup, etc.)
        OnClosed?.Invoke();

        Tweener.CancelAll(this);
        // Fade out then remove
        Tweener.To(this, v => Opacity = v, Opacity, 0f, 0.15f, Easing.Linear).OnComplete = () => {
            // Dispose graphics resources to prevent memory leaks
            DisposeGraphicsResources();
            
            Parent?.RemoveChild(this);
            OwnerProcess?.OnWindowClosed(this);
            if (ActiveWindow == this) ActiveWindow = null;
        };
    }
    
    /// <summary>
    /// Disposes of graphics resources used by this window.
    /// </summary>
    private void DisposeGraphicsResources() {
        _windowRenderTarget?.Dispose();
        _windowRenderTarget = null;
        Snapshot?.Dispose();
        Snapshot = null;
    }

    public void HandleFocus() {
        if (ActiveWindow != this) {
            ActiveWindow = this;
            Parent?.BringToFront(this);
        }
    }

    public void ToggleMaximize(Rectangle workArea) {
        Tweener.CancelAll(this);

        // In Windows, if you are snapped or maximized, the button restores you to floating.
        if (_isMaximized) {
            _isMaximized = false;

            Tweener.To(this, v => Position = v, Position, _restoreRect.Location.ToVector2(), 0.3f, Easing.EaseOutQuad);
            Tweener.To(this, v => Size = v, Size, _restoreRect.Size.ToVector2(), 0.3f, Easing.EaseOutQuad);
        } else {
            // We were floating -> Save current state and go Full Maximize
            _restoreRect = new Rectangle(Position.ToPoint(), Size.ToPoint());
            _isMaximized = true;

            Tweener.To(this, v => Position = v, Position, workArea.Location.ToVector2(), 0.3f, Easing.EaseOutQuad);
            Tweener.To(this, v => Size = v, Size, workArea.Size.ToVector2(), 0.3f, Easing.EaseOutQuad);
        }

        Parent?.BringToFront(this);
    }

    public void SetMaximized(bool maximized, Rectangle workArea) {
        _isMaximized = maximized;
        if (maximized) {
            _restoreRect = new Rectangle(Position.ToPoint(), Size.ToPoint());
            Position = workArea.Location.ToVector2();
            Size = workArea.Size.ToVector2();
        } else {
            Position = _restoreRect.Location.ToVector2();
            Size = _restoreRect.Size.ToVector2();
        }
    }

    protected override void UpdateInput() {
        if (LayoutDirty && !InputManager.IsMouseButtonDown(MouseButton.Left)) {
            TheGame.Core.OS.Shell.UI.SaveWindowLayout(this);
            LayoutDirty = false;
        }

        if (!IsVisible) return;
        
        // Block input if a child modal dialog is active
        if (IsBlocked) {
            // Still allow the window to be seen, but don't process any input
            return;
        }
        
        // If another window or overlay already took the mouse, we don't react at all
        // (unless we are already in the middle of a drag/resize operation)
        if (_obscuredByAnotherWindow && !_isDragging && !_isResizing) return;

        // 1. Capture state before we potentially modify IsMouseConsumed
        bool inputConsumedByChild = InputManager.IsMouseConsumed;

        // If we are actively dragging or resizing, we override children to maintain window control
        if (_isDragging || _isResizing) {
            inputConsumedByChild = false;
            _potentialDrag = false;
        }

        bool isHoveringStrict = IsMouseOver; // Use unified hover state
        bool isHoveringRaw = InputManager.IsMouseHovering(Bounds, ignoreConsumed: true); // Raw window hover
        
        bool isDoubleClick = isHoveringRaw && InputManager.IsDoubleClick(MouseButton.Left, ignoreConsumed: true);
        bool isJustPressed = isHoveringRaw && InputManager.IsMouseButtonJustPressed(MouseButton.Left);

        // 2. Hover cursor logic (Always check if hovering over window)
        const int ResizeEdge = 6;
        if (isHoveringRaw) {
            var mouseP = InputManager.MousePosition;
            Rectangle bounds = new Rectangle((int)AbsolutePosition.X, (int)AbsolutePosition.Y, (int)Size.X, (int)Size.Y);

            if (!_isMaximized && CanResize) {
                bool left = mouseP.X >= bounds.Left && mouseP.X <= bounds.Left + ResizeEdge;
                bool right = mouseP.X >= bounds.Right - ResizeEdge && mouseP.X <= bounds.Right;
                bool top = mouseP.Y >= bounds.Top && mouseP.Y <= bounds.Top + ResizeEdge;
                bool bottom = mouseP.Y >= bounds.Bottom - ResizeEdge && mouseP.Y <= bounds.Bottom;

                if ((left && top) || (right && bottom)) CustomCursor.Instance.SetCursor(CursorType.DiagonalNW);
                else if ((right && top) || (left && bottom)) CustomCursor.Instance.SetCursor(CursorType.DiagonalNE);
                else if (left || right) CustomCursor.Instance.SetCursor(CursorType.Horizontal);
                else if (top || bottom) CustomCursor.Instance.SetCursor(CursorType.Vertical);
                else {
                    // Check title bar
                    Rectangle titleBarRect = new Rectangle((int)AbsolutePosition.X, (int)AbsolutePosition.Y, (int)Size.X, TitleBarHeight);
                    if (titleBarRect.Contains(mouseP)) {
                        CustomCursor.Instance.SetCursor(CursorType.Pointer);
                    }
                }
            } else {
                // Maximized: just pointer on title bar
                Rectangle titleBarRect = new Rectangle((int)AbsolutePosition.X, (int)AbsolutePosition.Y, (int)Size.X, TitleBarHeight);
                if (titleBarRect.Contains(mouseP)) {
                    CustomCursor.Instance.SetCursor(CursorType.Pointer);
                }
            }
        }

        // No longer consuming early here - base.UpdateInput() handles it via ConsumesInput property

        if (inputConsumedByChild) {
            base.UpdateInput(); // Keep base logic alive for state management
            return;
        }

        // 4. Double Click Maximize (only if CanResize)
        if (isDoubleClick && CanResize) {
            var titleRect = new Rectangle((int)AbsolutePosition.X, (int)AbsolutePosition.Y, (int)Size.X, TitleBarHeight);
            if (titleRect.Contains(InputManager.MousePosition)) {
                var viewport = G.GraphicsDevice.Viewport;
                ToggleMaximize(new Rectangle(0, 0, viewport.Width, viewport.Height - 40));
                InputManager.IsMouseConsumed = true;
                _potentialDrag = false;
                return;
            }
        }

        if (isJustPressed) {
            var mousePos = InputManager.MousePosition;
            var titleBarRect = new Rectangle((int)AbsolutePosition.X, (int)AbsolutePosition.Y, (int)Size.X, TitleBarHeight);

            // Check Title Bar for Drag potential
            if (titleBarRect.Contains(mousePos)) {
                _potentialDrag = true;
                _potentialDragStart = mousePos.ToVector2();
                _dragOffset = mousePos.ToVector2() - Position;
                DebugLogger.Log($"Window Potential Drag {Title}");
            }
        }

        // 5. Handle Potential Drag -> Real Drag Logic
        if (_potentialDrag) {
            if (InputManager.IsMouseButtonDown(MouseButton.Left)) {
                if (Vector2.Distance(InputManager.MousePosition.ToVector2(), _potentialDragStart) > 5f) {
                    // Start Dragging
                    _isDragging = true;
                    DebugLogger.Log($"Window Start Drag {Title}");
                    _potentialDrag = false;
                    InputManager.IsMouseConsumed = true;

                    // Drag-to-Restore Logic
                    if (_isMaximized) {
                        float relX = (_potentialDragStart.X - Position.X) / Size.X;

                        _isMaximized = false;
                        Size = _restoreRect.Size.ToVector2();

                        // Align relative to mouse
                        float newX = InputManager.MousePosition.X - (Size.X * relX);
                        float newY = InputManager.MousePosition.Y - (TitleBarHeight / 2f);
                        Position = new Vector2(newX, newY);
                        _dragOffset = InputManager.MousePosition.ToVector2() - Position;
                    }
                }
            } else {
                _potentialDrag = false;
            }
        }

        // Processing Active Drag
        if (_isDragging) {
            CustomCursor.Instance.SetCursor(CursorType.Move);
            if (InputManager.IsMouseButtonDown(MouseButton.Left)) {
                var mousePos = InputManager.MousePosition;
                Position = mousePos.ToVector2() - _dragOffset;
                InputManager.IsMouseConsumed = true;

                // Aero Snap Edge Detection (only if CanResize)
                Rectangle? newTarget = null;
                if (CanResize) {
                    var viewport = G.GraphicsDevice.Viewport;
                    int screenW = viewport.Width;
                    int screenH = viewport.Height - 40; // Subtract taskbar
                    int edgeMargin = 10;

                    if (mousePos.Y < edgeMargin) {
                        newTarget = new Rectangle(0, 0, screenW, screenH);
                    } else if (mousePos.X < edgeMargin) {
                        newTarget = new Rectangle(0, 0, screenW / 2, screenH);
                    } else if (mousePos.X > screenW - edgeMargin) {
                        newTarget = new Rectangle(screenW / 2, 0, screenW / 2, screenH);
                    }
                }

                if (newTarget != _snapPreview) {
                    _snapPreview = newTarget;
                    if (_snapPreview.HasValue) {
                        // START: Start from the window's current bounds
                        if (_snapPreviewOpacity < 0.1f) {
                            _renderSnapPos = Position;
                            _renderSnapSize = Size;
                        }

                        // ANIMATE TO TARGET: Grow from window to snap zone
                        var target = _snapPreview.Value;
                        Tweener.CancelAll(this, "SnapPreview");
                        Tweener.To(this, v => _renderSnapPos = v, _renderSnapPos, target.Location.ToVector2(), 0.25f, Easing.EaseOutQuad).Tag = "SnapPreview";
                        Tweener.To(this, v => _renderSnapSize = v, _renderSnapSize, target.Size.ToVector2(), 0.25f, Easing.EaseOutQuad).Tag = "SnapPreview";
                        Tweener.To(this, v => _snapPreviewOpacity = v, _snapPreviewOpacity, 1f, 0.2f, Easing.EaseOutQuad).Tag = "SnapPreview";
                    } else {
                        // RETRACT: shrink back to window if we leave the zone
                        Tweener.CancelAll(this, "SnapPreview");
                        Tweener.To(this, v => _renderSnapPos = v, _renderSnapPos, Position, 0.2f, Easing.EaseOutQuad).Tag = "SnapPreview";
                        Tweener.To(this, v => _renderSnapSize = v, _renderSnapSize, Size, 0.2f, Easing.EaseOutQuad).Tag = "SnapPreview";
                        Tweener.To(this, v => _snapPreviewOpacity = v, _snapPreviewOpacity, 0f, 0.2f, Easing.EaseOutQuad).Tag = "SnapPreview";
                    }
                }

            } else {
                // DROP
                if (_snapPreview.HasValue) {
                    _isMaximized = true;

                    Rectangle target = _snapPreview.Value;

                    // Both window and ghost animate to the final destination
                    Tweener.To(this, v => Position = v, Position, target.Location.ToVector2(), 0.2f, Easing.EaseOutQuad);
                    Tweener.To(this, v => Size = v, Size, target.Size.ToVector2(), 0.2f, Easing.EaseOutQuad);

                    // Fade ghost out as it merges
                    Tweener.CancelAll(this, "SnapPreview");
                    Tweener.To(this, v => _renderSnapPos = v, _renderSnapPos, target.Location.ToVector2(), 0.2f, Easing.EaseOutQuad).Tag = "SnapPreview";
                    Tweener.To(this, v => _renderSnapSize = v, _renderSnapSize, target.Size.ToVector2(), 0.2f, Easing.EaseOutQuad).Tag = "SnapPreview";
                    Tweener.To(this, v => _snapPreviewOpacity = v, 1f, 0f, 0.25f, Easing.EaseOutQuad).Tag = "SnapPreview";
                } else {
                    // Dropped in floating space
                    _preMinimizePos = Position;
                    _preMinimizeSize = Size;
                    _restoreRect = new Rectangle(Position.ToPoint(), Size.ToPoint());
                }

                _isDragging = false;
                _snapPreview = null;
                if (_snapPreviewOpacity > 0 && !_isAnimating) {
                    Tweener.To(this, v => _snapPreviewOpacity = v, _snapPreviewOpacity, 0f, 0.2f, Easing.EaseOutQuad).Tag = "SnapPreview";
                }
            }
        }

        // Processing Active Resize
        if (_isResizing) {
            // Set Resize Cursor based on active resize direction
            if ((_resizeDir.X == -1 && _resizeDir.Y == -1) || (_resizeDir.X == 1 && _resizeDir.Y == 1)) CustomCursor.Instance.SetCursor(CursorType.DiagonalNW);
            else if ((_resizeDir.X == 1 && _resizeDir.Y == -1) || (_resizeDir.X == -1 && _resizeDir.Y == 1)) CustomCursor.Instance.SetCursor(CursorType.DiagonalNE);
            else if (_resizeDir.X != 0) CustomCursor.Instance.SetCursor(CursorType.Horizontal);
            else if (_resizeDir.Y != 0) CustomCursor.Instance.SetCursor(CursorType.Vertical);

            if (InputManager.IsMouseButtonDown(MouseButton.Left)) {
                var currentMouse = InputManager.MousePosition.ToVector2();
                var delta = currentMouse - _resizeStartMouse;

                Vector2 newSize = _resizeStartSize;
                Vector2 newPos = _resizeStartPos;

                // 1. Calculate and Clamp Size
                if (_resizeDir.X == 1) newSize.X = Math.Max(100, _resizeStartSize.X + delta.X); // Right
                if (_resizeDir.X == -1) newSize.X = Math.Max(100, _resizeStartSize.X - delta.X); // Left

                if (_resizeDir.Y == 1) newSize.Y = Math.Max(100, _resizeStartSize.Y + delta.Y); // Bottom
                if (_resizeDir.Y == -1) newSize.Y = Math.Max(100, _resizeStartSize.Y - delta.Y); // Top

                // 2. Adjust Position for Left/Top resize based on ACTUAL size change
                if (_resizeDir.X == -1) {
                    float actualDeltaX = _resizeStartSize.X - newSize.X;
                    newPos.X = _resizeStartPos.X + actualDeltaX;
                }

                if (_resizeDir.Y == -1) {
                    float actualDeltaY = _resizeStartSize.Y - newSize.Y;
                    newPos.Y = _resizeStartPos.Y + actualDeltaY;
                }

                Size = newSize;
                Position = newPos;
                InputManager.IsMouseConsumed = true;

                OnResize?.Invoke();
            } else {
                _isResizing = false;
                // Update stable state trackers
                _preMinimizePos = Position;
                _preMinimizeSize = Size;
                _restoreRect = new Rectangle(Position.ToPoint(), Size.ToPoint());
            }
        }

        base.UpdateInput();
    }

    public override void Draw(SpriteBatch spriteBatch, ShapeBatch batch) {
        if (!IsVisible && Opacity <= 0) return;

        try {
            // Draw Snap Preview if dragging near edges or animating (screen space, before RT)
            if (_snapPreviewOpacity > 0.01f) {
                var color = new Color(0, 120, 215) * (_snapPreviewOpacity * 0.4f);
                var borderColor = new Color(0, 120, 215) * (_snapPreviewOpacity * 0.8f);
                batch.FillRectangle(_renderSnapPos, _renderSnapSize, color, rounded: 5f);
                batch.BorderRectangle(_renderSnapPos, _renderSnapSize, borderColor, thickness: 2f, rounded: 5f);
            }

            // Render entire window to RenderTarget
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
    
    /// <summary>
    /// Renders the entire window (blur, title bar, content, border, chrome buttons) to a RenderTarget2D.
    /// </summary>
    private void DrawWindowToRT(SpriteBatch spriteBatch, ShapeBatch globalBatch) {
        // End global batches to isolate states
        globalBatch.End();
        spriteBatch.End();

        var gd = G.GraphicsDevice;
        var screenAbsPos = RawAbsolutePosition; // Screen position without RenderOffset
        var windowW = (int)Size.X;
        var windowH = (int)Size.Y;
        
        // Add padding for anti-aliasing (prevents border clipping)
        const int padding = 2;
        int rtW = windowW + padding * 2;
        int rtH = windowH + padding * 2;
        var paddingOffset = new Vector2(padding, padding);

        // Ensure we have valid dimensions
        if (windowW <= 0 || windowH <= 0) {
            globalBatch.Begin();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            return;
        }

        // Ensure RenderTarget is properly sized for FULL window with AA padding
        EnsureRenderTarget(gd, rtW, rtH);

        // Save current state
        var previousTargets = gd.GetRenderTargets();
        RenderTarget2D previousTarget = previousTargets.Length > 0 ? 
            previousTargets[0].RenderTarget as RenderTarget2D : null;
        var previousViewport = gd.Viewport;

        // === RENDER TO WINDOW'S LOCAL RENDER TARGET ===
        gd.SetRenderTarget(_windowRenderTarget);
        gd.Viewport = new Viewport(0, 0, _windowRenderTarget.Width, _windowRenderTarget.Height);
        gd.Clear(Color.Transparent);

        // Set RenderOffset so window origin (0,0) maps to local (padding, padding) in RT
        // RenderOffset = RawAbsolutePosition - paddingOffset
        UIElement.RenderOffset = screenAbsPos - paddingOffset;
        
        // Set BlurUVOffset so local RT (0,0) samples from screenAbsPos - paddingOffset
        _contentBatch.BlurUVOffset = screenAbsPos - paddingOffset;
        _contentBatch.ScreenSizeOverride = new Vector2(previousViewport.Width, previousViewport.Height);

        try {
            // === Pass 1: Draw blur background and title bar ===
            _contentBatch.Begin(null, null);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

            // Draw blur for body (uses AbsolutePosition which now has RenderOffset applied)
            DrawBackBlur(spriteBatch, _contentBatch);
            
            // Draw title bar with blur
            DrawTitleBarToRT(_contentBatch);

            _contentBatch.End();
            spriteBatch.End();

            // === Pass 2: Draw children (content area) with scissor clipping ===
            // Content must be clipped to actual window bounds, not the padded RT
            var contentOffsetFromWindow = new Vector2(0, TitleBarHeight);
            
            // Set up scissor rect for content area (in RT coordinates)
            // Content starts at (padding, padding + TitleBarHeight) and is (windowW, windowH - TitleBarHeight)
            var contentScissorRect = new Rectangle(
                padding, 
                padding + TitleBarHeight, 
                windowW, 
                windowH - TitleBarHeight);
            
            var previousScissorRect = gd.ScissorRectangle;
            var previousRasterizerState = gd.RasterizerState;
            gd.ScissorRectangle = contentScissorRect;
            
            _contentBatch.Begin(null, null);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, null, _scissorState);

            foreach (var child in Children) {
                if (child == _closeButton || child == _maxButton || child == _minButton) continue;
                child.Draw(spriteBatch, _contentBatch);
            }

            // Set scissor RasterizerState BEFORE End() - this is when GPU drawing happens
            gd.RasterizerState = _scissorState;
            _contentBatch.End();
            spriteBatch.End();

            // === Pass 3: Local coordinate content (DrawContent override) ===
            // Also uses scissor clipping
            _contentBatch.Begin(null, null);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, null, _scissorState);

            DrawContent(spriteBatch, _contentBatch);

            // Set scissor RasterizerState BEFORE End()
            gd.RasterizerState = _scissorState;
            _contentBatch.End();
            spriteBatch.End();
            
            // Restore scissor state
            gd.ScissorRectangle = previousScissorRect;
            gd.RasterizerState = previousRasterizerState;
            
            // === Pass 4: Draw border and chrome buttons ===
            _contentBatch.Begin(null, null);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

            // Draw border (but not title bar blur - already drawn)
            DrawBorderAndChrome(spriteBatch, _contentBatch);

            _contentBatch.End();
            spriteBatch.End();

        } finally {
            // Always clear the offsets to avoid affecting other rendering
            UIElement.RenderOffset = Vector2.Zero;
            _contentBatch.BlurUVOffset = Vector2.Zero;
            _contentBatch.ScreenSizeOverride = null;
        }

        // === RESTORE AND COMPOSITE ===
        gd.SetRenderTarget(previousTarget);
        gd.Viewport = previousViewport;

        // Draw the composited window at absolute position IMMEDIATELY
        spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
        
        var destRect = new Rectangle(
            (int)screenAbsPos.X - padding, 
            (int)screenAbsPos.Y - padding, 
            rtW, 
            rtH);

        spriteBatch.Draw(
            _windowRenderTarget, 
            destRect,
            new Rectangle(0, 0, rtW, rtH),
            Color.White * AbsoluteOpacity
        );
        
        spriteBatch.End(); // Flush RT content NOW

        // Restart global batches
        globalBatch.Begin();
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
    }
    
    /// <summary>
    /// Draws the title bar with blur effect to the RenderTarget.
    /// </summary>
    private void DrawTitleBarToRT(ShapeBatch batch) {
        var absPos = AbsolutePosition; // Uses RenderOffset, so this is local coords

        Color baseTitleBarColor = ((ActiveWindow == this) ? new Color(60, 60, 60) : new Color(40, 40, 40));
        
        if (_isBlinking) {
            float blinkPhase = (float)(_blinkTimer / BlinkDuration);
            float intensity = (float)Math.Abs(Math.Sin(blinkPhase * Math.PI * 6));
            baseTitleBarColor = Color.Lerp(baseTitleBarColor, new Color(120, 120, 140), intensity * 0.6f);
        }
        
        Color titleBarColor = baseTitleBarColor * 0.8f;
        batch.DrawBlurredRectangle(
            absPos,
            new Vector2(Size.X, TitleBarHeight),
            titleBarColor * AbsoluteOpacity,
            Color.Transparent,
            0f,
            5f,
            AbsoluteOpacity
        );
    }
    
    /// <summary>
    /// Draws border, separator line, icon, title text, and chrome buttons to the RT.
    /// </summary>
    private void DrawBorderAndChrome(SpriteBatch spriteBatch, ShapeBatch batch) {
        var absPos = AbsolutePosition;
        var border = BorderColor * AbsoluteOpacity;

        // Border
        if (!_isMaximized) {
            batch.BorderRectangle(absPos, Size, border, thickness: 1f, rounded: 5f);
        }

        // Subtle title bar separator
        batch.DrawLine(
            new Vector2(absPos.X + 5, absPos.Y + TitleBarHeight), 
            new Vector2(absPos.X + Size.X - 5, absPos.Y + TitleBarHeight), 
            1f, border * 0.5f, border * 0.5f, 1f);

        float titleXOffset = 10;
        float controlButtonsWidth = 85;

        // Draw Icon in Title Bar
        if (Icon != null && Size.X > controlButtonsWidth + 40) {
            float iconSize = 18f;
            float iconY = absPos.Y + (TitleBarHeight - iconSize) / 2f;
            float scale = iconSize / Icon.Width;
            batch.DrawTexture(Icon, new Vector2(absPos.X + 10, iconY), Color.White * AbsoluteOpacity, scale);
            titleXOffset += iconSize + 8;
        }

        // Title Text
        if (GameContent.FontSystem != null) {
            SpriteFontBase font = GameContent.FontSystem.GetFont(20);
            if (font != null) {
                float availableWidth = Size.X - controlButtonsWidth - titleXOffset;
                if (availableWidth > 10) {
                    string textToDraw = TextHelper.TruncateWithEllipsis(font, Title, availableWidth);
                    font.DrawText(batch, textToDraw, absPos + new Vector2(titleXOffset, 5), Color.White * AbsoluteOpacity);
                }
            }
        }
        
        // Draw chrome buttons (close, max, min)
        foreach (var child in Children) {
            if (child == _closeButton || child == _maxButton || child == _minButton) {
                child.Draw(spriteBatch, batch);
            }
        }
    }

    protected void DrawBackBlur(SpriteBatch spriteBatch, ShapeBatch batch) {
        var absPos = AbsolutePosition;

        var glassTint = BackgroundColor * 0.4f * AbsoluteOpacity;

        // Малюємо тіло з блюром
        batch.DrawBlurredRectangle(
            absPos + new Vector2(0, TitleBarHeight),
            new Vector2(Size.X, Size.Y - TitleBarHeight),
            glassTint,
            Color.Transparent, // Колір рамки
            0f, // Товщина рамки
            _isMaximized ? 0f : 5f, // Заокруглення (знизу)
            AbsoluteOpacity
        );

    }


    /// <summary>
    /// Ensures the window's RenderTarget2D is properly sized for the content area.
    /// </summary>
    private void EnsureRenderTarget(GraphicsDevice gd, int width, int height) {
        // Clamp to minimum size to avoid issues
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

    public virtual void DrawContent(SpriteBatch spriteBatch, ShapeBatch batch) {
    }
}
