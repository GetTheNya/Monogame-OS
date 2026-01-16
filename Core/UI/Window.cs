using Microsoft.Xna.Framework;
using FontStashSharp;
using TheGame.Core.Input;
using TheGame.Graphics;
using TheGame.Core.Animation;
using Microsoft.Xna.Framework.Graphics;
using System;
using TheGame.Core.UI.Controls;

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

    // Drag/Click Discrimination
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

    public string Title { get; set; } = "Window";
    public Texture2D Icon { get; set; }
    public string AppId { get; set; }
    public bool CanResize { get; set; } = true;
    public bool ShowTitleBar { get; set; } = true;
    public Color BackgroundColor { get; set; } = new Color(30, 30, 30, 240); // Dark semi-transparent
    public Color BorderColor { get; set; } = Color.White;

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

    private ShapeBatch _contentBatch;
    private RasterizerState _scissorState;

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
    }

    public override void Update(GameTime gameTime) {
        _obscuredByAnotherWindow = InputManager.IsMouseConsumed;
        
        // CRITICAL: Check for resize/drag BEFORE children update
        // This prevents clicking on resize edges from triggering controls underneath
        if (IsVisible && !_obscuredByAnotherWindow && !_isDragging && !_isResizing) {
            const int ResizeEdge = 6;
            bool isHoveringRaw = InputManager.IsMouseHovering(Bounds, ignoreConsumed: true);
            bool isJustPressed = isHoveringRaw && InputManager.IsMouseButtonJustPressed(MouseButton.Left, ignoreConsumed: true);
            
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

                        ActiveWindow = this;
                        Parent?.BringToFront(this);
                    }
                }
            }
        }
        
        UpdateButtons();
        base.Update(gameTime);
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
        Minimize(Taskbar.Instance?.GetButtonCenter(this) ?? fallback);
    }

    public void Minimize(Vector2 targetPos) {
        if (_isAnimating) return;
        _isAnimating = true;

        Tweener.CancelAll(this);

        // Save current state to restore to
        _preMinimizePos = Position;
        _preMinimizeSize = Size;

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
        Restore(Taskbar.Instance?.GetButtonCenter(this) ?? fallback);
    }

    public void Restore(Vector2 sourcePos) {
        if (_isAnimating) return;
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

        // Ensure we have some safe defaults if never minimized
        if (_preMinimizeSize == Vector2.Zero) _preMinimizeSize = new Vector2(400, 300);
        if (_preMinimizePos == Vector2.Zero) _preMinimizePos = new Vector2(100, 100);

        Tweener.To(this, v => Position = v, Position, _preMinimizePos, duration, easing);
        Tweener.To(this, v => Size = v, Size, _preMinimizeSize, duration, easing);
        Tweener.To(this, v => Opacity = v, 0f, 1f, duration, easing).OnComplete = () => { _isAnimating = false; };

        Parent?.BringToFront(this);
        ActiveWindow = this;
    }

    public void Close() {
        if (ActiveWindow == this) {
            FindAndFocusNextWindow();
        }

        Tweener.CancelAll(this);
        // Fade out then remove
        Tweener.To(this, v => Opacity = v, Opacity, 0f, 0.15f, Easing.Linear).OnComplete = () => { Parent?.RemoveChild(this); };
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

        bool isHoveringStrict = InputManager.IsMouseHovering(Bounds); // Normal child-priority hover
        bool isHoveringRaw = InputManager.IsMouseHovering(Bounds, ignoreConsumed: true); // Raw window hover
        
        bool isDoubleClick = isHoveringRaw && InputManager.IsDoubleClick(MouseButton.Left, ignoreConsumed: true);
        bool isJustPressed = isHoveringRaw && InputManager.IsMouseButtonJustPressed(MouseButton.Left, ignoreConsumed: true);

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

        if (isHoveringStrict && ConsumesInput)
            InputManager.IsMouseConsumed = true;

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
            ActiveWindow = this;
            Parent?.BringToFront(this);

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

        // Draw Snap Preview if dragging near edges or animating
        if (_snapPreviewOpacity > 0.01f) {
            var color = new Color(0, 120, 215) * (_snapPreviewOpacity * 0.4f);
            var borderColor = new Color(0, 120, 215) * (_snapPreviewOpacity * 0.8f);
            batch.FillRectangle(_renderSnapPos, _renderSnapSize, color, rounded: 5f);
            batch.BorderRectangle(_renderSnapPos, _renderSnapSize, borderColor, thickness: 2f, rounded: 5f);
        }

        // 1. Draw Background
        DrawBackBlur(spriteBatch, batch);

        // 2. Draw Content and Children (Isolated & Clipped)
        DrawWindowContent(spriteBatch, batch);

        // 3. Draw Window Chrome (Title bar, border)
        DrawSelf(spriteBatch, batch);


        // 4. Draw OVERLAY Children (Chrome Buttons)
        foreach (var child in Children) {
            if (child == _closeButton || child == _maxButton || child == _minButton) {
                child.Draw(spriteBatch, batch);
            }
        }

        // 4. Final barrier flush
        batch.End();
        spriteBatch.End();
        batch.Begin();
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
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

    protected override void DrawSelf(SpriteBatch spriteBatch, ShapeBatch batch) {
        var absPos = AbsolutePosition;
        var border = BorderColor * AbsoluteOpacity;

        // Title Bar
        Color titleBarColor = ((ActiveWindow == this) ? new Color(60, 60, 60) : new Color(40, 40, 40)) * 0.8f;
        batch.DrawBlurredRectangle(
            absPos,
            new Vector2(Size.X, TitleBarHeight),
            titleBarColor * AbsoluteOpacity,
            Color.Transparent, // Колір рамки
            0f, // Товщина рамки
            5f, // Заокруглення (знизу)
            AbsoluteOpacity
        );
        //batch.FillRectangle(absPos, new Vector2(Size.X, TitleBarHeight), titleBarColor * AbsoluteOpacity, rounded: 5f);

        // Border
        if (!_isMaximized) {
            batch.BorderRectangle(absPos, Size, border, thickness: 1f, rounded: 5f);
        }

        // Subtle title bar separator (inset from edges)
        batch.DrawLine(new Vector2(absPos.X + 5, absPos.Y + TitleBarHeight), new Vector2(absPos.X + Size.X - 5, absPos.Y + TitleBarHeight), 1f, border * 0.5f, border * 0.5f, 1f);

        float titleXOffset = 10;
        float controlButtonsWidth = 85; // Roughly space for X, O, _

        // Draw Icon in Title Bar (only if there is reasonable space)
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

                if (availableWidth > 10) { // Only try to draw if we have at least 10px
                    string textToDraw = Title;
                    var textSize = font.MeasureString(textToDraw);

                    if (textSize.X > availableWidth) {
                        while (textSize.X > availableWidth && textToDraw.Length > 0) {
                            textToDraw = textToDraw.Substring(0, textToDraw.Length - 1);
                            textSize = font.MeasureString(textToDraw + "...");
                        }

                        textToDraw += "...";
                    }

                    font.DrawText(batch, textToDraw, absPos + new Vector2(titleXOffset, 5), Color.White * AbsoluteOpacity);
                }
            }
        }
    }

    private void DrawWindowContent(SpriteBatch spriteBatch, ShapeBatch globalBatch) {
        // End global batches to isolate states and scissor
        globalBatch.End();
        spriteBatch.End();

        var absPos = AbsolutePosition;
        var clientX = (int)absPos.X;
        var clientY = (int)absPos.Y + TitleBarHeight;
        var clientW = (int)Size.X;
        var clientH = (int)Size.Y - TitleBarHeight;

        var screenRect = G.GraphicsDevice.Viewport.Bounds;
        var scissorRect = new Rectangle(clientX, clientY, clientW, clientH);
        scissorRect = Rectangle.Intersect(scissorRect, screenRect);

        if (scissorRect.Width > 0 && scissorRect.Height > 0) {
            var oldScissor = G.GraphicsDevice.ScissorRectangle;
            G.GraphicsDevice.ScissorRectangle = scissorRect;

            // --- Pass 1: Background and Standard Children ---
            // Set up batches for clipping
            _contentBatch.Begin(null, null);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, null, _scissorState);
            G.GraphicsDevice.RasterizerState = _scissorState;

            // Background Fill
            //_contentBatch.FillRectangle(new Vector2(clientX, clientY), new Vector2(clientW, clientH), BackgroundColor * AbsoluteOpacity);

            // Draw Children that aren't Chrome Buttons
            // Because of our GetChildOffset override, these will already have shifted AbsolutePositions
            foreach (var child in Children) {
                if (child == _closeButton || child == _maxButton || child == _minButton) continue;
                child.Draw(spriteBatch, _contentBatch);
            }

            _contentBatch.End();
            spriteBatch.End();

            // --- Pass 2: Local Coordinate Content (Overrides) ---
            var transform = Matrix.CreateTranslation(clientX, clientY, 0);
            _contentBatch.Begin(transform, null);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, null, _scissorState, null, transform);
            G.GraphicsDevice.RasterizerState = _scissorState;

            DrawContent(spriteBatch, _contentBatch);

            _contentBatch.End();
            spriteBatch.End();

            // Restore Scissor
            G.GraphicsDevice.ScissorRectangle = oldScissor;
        }

        // Restart global batches
        globalBatch.Begin();
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
    }

    public virtual void DrawContent(SpriteBatch spriteBatch, ShapeBatch batch) {
    }
}
