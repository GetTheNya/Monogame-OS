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

public class Window : WindowBase {
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

    public override string Title { get; set; } = "Window";

    public bool CanResize { get; set; } = true;
    public bool ShowTitleBar { get; set; } = true;
    public Color BackgroundColor { get; set; } = new Color(30, 30, 30, 240); // Dark semi-transparent
    public Color BorderColor { get; set; } = Color.White;

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

    public Window() : this(Vector2.Zero, new Vector2(400, 300)) {
        // Center on screen by default
        var viewport = G.GraphicsDevice.Viewport;
        Position = new Vector2(
            (viewport.Width - Size.X) / 2,
            (viewport.Height - Size.Y - 40) / 2  // Account for taskbar
        );
    }

    public Window(Vector2 position, Vector2 size) : base(position, size) {
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
        
        BringToFront(_closeButton);
        BringToFront(_maxButton);
        if (_minButton != null) BringToFront(_minButton);
    }

    public override void Update(GameTime gameTime) {
        _obscuredByAnotherWindow = InputManager.IsMouseConsumed;
        
        if (IsBlocked) {
            base.Update(gameTime); // handles modal blink trigger
            return;
        }
        
        if (_isBlinking) {
            _blinkTimer += gameTime.ElapsedGameTime.TotalSeconds;
            if (_blinkTimer >= BlinkDuration) {
                _isBlinking = false;
                _blinkTimer = 0;
            }
        }

        if (_isDragging || _isResizing) {
            InputManager.IsMouseConsumed = true;
            InputManager.IsScrollConsumed = true;
        }
        
        if (IsVisible && !_obscuredByAnotherWindow && !_isDragging && !_isResizing) {
            const int ResizeEdge = 6;
            bool isHoveringRaw = InputManager.IsMouseHovering(Bounds, ignoreConsumed: true);
            bool isJustPressed = isHoveringRaw && InputManager.IsMouseButtonJustPressed(MouseButton.Left);
            
            if (isJustPressed && !_isMaximized && CanResize) {
                var mousePos = InputManager.MousePosition;
                Rectangle bounds = new Rectangle((int)AbsolutePosition.X, (int)AbsolutePosition.Y, (int)Size.X, (int)Size.Y);
                
                if (bounds.Contains(mousePos)) {
                    bool left = mousePos.X >= bounds.Left && mousePos.X <= bounds.Left + ResizeEdge;
                    bool right = mousePos.X >= bounds.Right - ResizeEdge && mousePos.X <= bounds.Right;
                    bool top = mousePos.Y >= bounds.Top && mousePos.Y <= bounds.Top + ResizeEdge;
                    bool bottom = mousePos.Y >= bounds.Bottom - ResizeEdge && mousePos.Y <= bounds.Bottom;

                    if (left || right || top || bottom) {
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

        if (IsVisible && !_isAnimating && !_obscuredByAnotherWindow) {
            bool isHoveringRaw = InputManager.IsMouseHovering(Bounds, ignoreConsumed: true);
            bool isJustPressed = isHoveringRaw && InputManager.IsMouseButtonJustPressed(MouseButton.Left);
            if (isJustPressed) {
                HandleFocus();
            }
        }
        
        UpdateButtons();
        
        base.Update(gameTime);
    }

    public override UIElement GetElementAt(Vector2 pos) {
        if (!IsVisible || !Bounds.Contains(pos)) return null;

        var foundChrome = _closeButton.GetElementAt(pos) ?? 
                          (_maxButton.IsVisible ? _maxButton.GetElementAt(pos) : null) ?? 
                          _minButton.GetElementAt(pos);
        if (foundChrome != null) return foundChrome;

        Rectangle titleBarRect = new Rectangle((int)AbsolutePosition.X, (int)AbsolutePosition.Y, (int)Size.X, TitleBarHeight);
        if (titleBarRect.Contains(pos)) return this;

        Rectangle contentRect = new Rectangle((int)AbsolutePosition.X, (int)AbsolutePosition.Y + TitleBarHeight, (int)Size.X, (int)Size.Y - TitleBarHeight);
        if (contentRect.Contains(pos)) {
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
        if (child == _closeButton || child == _maxButton || child == _minButton) return Vector2.Zero;
        return new Vector2(0, TitleBarHeight);
    }

    private void UpdateButtons() {
        float rightSide = Size.X;
        float top = 5;

        _closeButton.Position = new Vector2(rightSide - 25, top);
        _maxButton.Position = new Vector2(rightSide - 50, top);
        _minButton.Position = new Vector2(rightSide - 75, top);
        _maxButton.IsVisible = CanResize;
    }

    public void Minimize() {
        Vector2 fallback = new Vector2(Position.X + Size.X / 2f, G.GraphicsDevice.Viewport.Height);
        Minimize(Taskbar.Instance?.GetButtonCenter(OwnerProcess) ?? fallback);
    }

    public void Minimize(Vector2 targetPos) {
        if (_isAnimating) return;
        CaptureSnapshot();
        _isAnimating = true;
        Tweener.CancelAll(this);

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

    public void Restore() {
        Vector2 fallback = new Vector2(Position.X + Size.X / 2f, G.GraphicsDevice.Viewport.Height);
        Restore(Taskbar.Instance?.GetButtonCenter(OwnerProcess) ?? fallback);
    }

    public void Restore(Vector2 sourcePos) {
        if (_isAnimating) return;
        Snapshot?.Dispose();
        Snapshot = null;
        _isAnimating = true;

        Tweener.CancelAll(this);
        IsVisible = true;
        Position = sourcePos;
        Size = new Vector2(50, 20);
        Opacity = 0f;

        float duration = 0.3f;
        var easing = Easing.EaseOutQuad;

        Vector2 targetPos, targetSize;
        if (_wasMaximizedBeforeMinimize) {
            var viewport = G.GraphicsDevice.Viewport;
            var workArea = new Rectangle(0, 0, viewport.Width, viewport.Height - 40);
            targetPos = workArea.Location.ToVector2();
            targetSize = workArea.Size.ToVector2();
            _isMaximized = true;
        } else {
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

    protected override void ExecuteClose() {
        Tweener.CancelAll(this);
        Tweener.To(this, v => Opacity = v, Opacity, 0f, 0.15f, Easing.Linear).OnComplete = () => {
            base.ExecuteClose();
        };
    }

    public void ToggleMaximize(Rectangle workArea) {
        Tweener.CancelAll(this);
        if (_isMaximized) {
            _isMaximized = false;
            Tweener.To(this, v => Position = v, Position, _restoreRect.Location.ToVector2(), 0.3f, Easing.EaseOutQuad);
            Tweener.To(this, v => Size = v, Size, _restoreRect.Size.ToVector2(), 0.3f, Easing.EaseOutQuad);
        } else {
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

        if (!IsVisible || IsBlocked) return;
        if (_obscuredByAnotherWindow && !_isDragging && !_isResizing) return;

        bool inputConsumedByChild = InputManager.IsMouseConsumed;
        if (_isDragging || _isResizing) {
            inputConsumedByChild = false;
            _potentialDrag = false;
        }

        bool isHoveringRaw = InputManager.IsMouseHovering(Bounds, ignoreConsumed: true);
        bool isDoubleClick = isHoveringRaw && InputManager.IsDoubleClick(MouseButton.Left, ignoreConsumed: true);
        bool isJustPressed = isHoveringRaw && InputManager.IsMouseButtonJustPressed(MouseButton.Left);

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
                    Rectangle titleBarRect = new Rectangle((int)AbsolutePosition.X, (int)AbsolutePosition.Y, (int)Size.X, TitleBarHeight);
                    if (titleBarRect.Contains(mouseP)) CustomCursor.Instance.SetCursor(CursorType.Pointer);
                }
            } else {
                Rectangle titleBarRect = new Rectangle((int)AbsolutePosition.X, (int)AbsolutePosition.Y, (int)Size.X, TitleBarHeight);
                if (titleBarRect.Contains(mouseP)) CustomCursor.Instance.SetCursor(CursorType.Pointer);
            }
        }

        if (inputConsumedByChild) {
            base.UpdateInput();
            return;
        }

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
            if (titleBarRect.Contains(mousePos)) {
                _potentialDrag = true;
                _potentialDragStart = mousePos.ToVector2();
                _dragOffset = mousePos.ToVector2() - Position;
            }
        }

        if (_potentialDrag) {
            if (InputManager.IsMouseButtonDown(MouseButton.Left)) {
                if (Vector2.Distance(InputManager.MousePosition.ToVector2(), _potentialDragStart) > 5f) {
                    _isDragging = true;
                    _potentialDrag = false;
                    InputManager.IsMouseConsumed = true;

                    if (_isMaximized) {
                        float relX = (_potentialDragStart.X - Position.X) / Size.X;
                        _isMaximized = false;
                        Size = _restoreRect.Size.ToVector2();
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

        if (_isDragging) {
            CustomCursor.Instance.SetCursor(CursorType.Move);
            if (InputManager.IsMouseButtonDown(MouseButton.Left)) {
                var mousePos = InputManager.MousePosition;
                Position = mousePos.ToVector2() - _dragOffset;
                InputManager.IsMouseConsumed = true;

                Rectangle? newTarget = null;
                if (CanResize) {
                    var viewport = G.GraphicsDevice.Viewport;
                    int screenW = viewport.Width;
                    int screenH = viewport.Height - 40;
                    int edgeMargin = 10;

                    if (mousePos.Y < edgeMargin) newTarget = new Rectangle(0, 0, screenW, screenH);
                    else if (mousePos.X < edgeMargin) newTarget = new Rectangle(0, 0, screenW / 2, screenH);
                    else if (mousePos.X > screenW - edgeMargin) newTarget = new Rectangle(screenW / 2, 0, screenW / 2, screenH);
                }

                if (newTarget != _snapPreview) {
                    _snapPreview = newTarget;
                    if (_snapPreview.HasValue) {
                        if (_snapPreviewOpacity < 0.1f) {
                            _renderSnapPos = Position;
                            _renderSnapSize = Size;
                        }
                        var target = _snapPreview.Value;
                        Tweener.CancelAll(this, "SnapPreview");
                        Tweener.To(this, v => _renderSnapPos = v, _renderSnapPos, target.Location.ToVector2(), 0.25f, Easing.EaseOutQuad).Tag = "SnapPreview";
                        Tweener.To(this, v => _renderSnapSize = v, _renderSnapSize, target.Size.ToVector2(), 0.25f, Easing.EaseOutQuad).Tag = "SnapPreview";
                        Tweener.To(this, v => _snapPreviewOpacity = v, _snapPreviewOpacity, 1f, 0.2f, Easing.EaseOutQuad).Tag = "SnapPreview";
                    } else {
                        Tweener.CancelAll(this, "SnapPreview");
                        Tweener.To(this, v => _renderSnapPos = v, _renderSnapPos, Position, 0.2f, Easing.EaseOutQuad).Tag = "SnapPreview";
                        Tweener.To(this, v => _renderSnapSize = v, _renderSnapSize, Size, 0.2f, Easing.EaseOutQuad).Tag = "SnapPreview";
                        Tweener.To(this, v => _snapPreviewOpacity = v, _snapPreviewOpacity, 0f, 0.2f, Easing.EaseOutQuad).Tag = "SnapPreview";
                    }
                }
            } else {
                if (_snapPreview.HasValue) {
                    _isMaximized = true;
                    Rectangle target = _snapPreview.Value;
                    Tweener.To(this, v => Position = v, Position, target.Location.ToVector2(), 0.2f, Easing.EaseOutQuad);
                    Tweener.To(this, v => Size = v, Size, target.Size.ToVector2(), 0.2f, Easing.EaseOutQuad);
                    Tweener.CancelAll(this, "SnapPreview");
                    Tweener.To(this, v => _renderSnapPos = v, _renderSnapPos, target.Location.ToVector2(), 0.2f, Easing.EaseOutQuad).Tag = "SnapPreview";
                    Tweener.To(this, v => _renderSnapSize = v, _renderSnapSize, target.Size.ToVector2(), 0.2f, Easing.EaseOutQuad).Tag = "SnapPreview";
                    Tweener.To(this, v => _snapPreviewOpacity = v, 1f, 0f, 0.25f, Easing.EaseOutQuad).Tag = "SnapPreview";
                } else {
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

        if (_isResizing) {
            if ((_resizeDir.X == -1 && _resizeDir.Y == -1) || (_resizeDir.X == 1 && _resizeDir.Y == 1)) CustomCursor.Instance.SetCursor(CursorType.DiagonalNW);
            else if ((_resizeDir.X == 1 && _resizeDir.Y == -1) || (_resizeDir.X == -1 && _resizeDir.Y == 1)) CustomCursor.Instance.SetCursor(CursorType.DiagonalNE);
            else if (_resizeDir.X != 0) CustomCursor.Instance.SetCursor(CursorType.Horizontal);
            else if (_resizeDir.Y != 0) CustomCursor.Instance.SetCursor(CursorType.Vertical);

            if (InputManager.IsMouseButtonDown(MouseButton.Left)) {
                var currentMouse = InputManager.MousePosition.ToVector2();
                var delta = currentMouse - _resizeStartMouse;
                Vector2 newSize = _resizeStartSize;
                Vector2 newPos = _resizeStartPos;

                if (_resizeDir.X == 1) newSize.X = Math.Max(100, _resizeStartSize.X + delta.X);
                if (_resizeDir.X == -1) newSize.X = Math.Max(100, _resizeStartSize.X - delta.X);
                if (_resizeDir.Y == 1) newSize.Y = Math.Max(100, _resizeStartSize.Y + delta.Y);
                if (_resizeDir.Y == -1) newSize.Y = Math.Max(100, _resizeStartSize.Y - delta.Y);
                if (_resizeDir.X == -1) newPos.X = _resizeStartPos.X + (_resizeStartSize.X - newSize.X);
                if (_resizeDir.Y == -1) newPos.Y = _resizeStartPos.Y + (_resizeStartSize.Y - newSize.Y);

                Size = newSize;
                Position = newPos;
                InputManager.IsMouseConsumed = true;
                OnResize?.Invoke();
            } else {
                _isResizing = false;
                _preMinimizePos = Position;
                _preMinimizeSize = Size;
                _restoreRect = new Rectangle(Position.ToPoint(), Size.ToPoint());
            }
        }

        base.UpdateInput();
    }

    public override void Draw(SpriteBatch spriteBatch, ShapeBatch batch) {
        if (!IsVisible && Opacity <= 0) return;

        if (_snapPreviewOpacity > 0.01f) {
            var color = new Color(0, 120, 215) * (_snapPreviewOpacity * 0.4f);
            var borderColor = new Color(0, 120, 215) * (_snapPreviewOpacity * 0.8f);
            batch.FillRectangle(_renderSnapPos, _renderSnapSize, color, rounded: 5f);
            batch.BorderRectangle(_renderSnapPos, _renderSnapSize, borderColor, thickness: 2f, rounded: 5f);
        }

        base.Draw(spriteBatch, batch);
    }
    
    protected override void DrawWindowToRT(SpriteBatch spriteBatch, ShapeBatch globalBatch) {
        globalBatch.End();
        spriteBatch.End();

        var gd = G.GraphicsDevice;
        var screenAbsPos = RawAbsolutePosition;
        var windowW = (int)Size.X;
        var windowH = (int)Size.Y;
        
        const int padding = 2;
        int rtW = windowW + padding * 2;
        int rtH = windowH + padding * 2;
        var paddingOffset = new Vector2(padding, padding);

        if (windowW <= 0 || windowH <= 0) {
            globalBatch.Begin();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            return;
        }

        EnsureRenderTarget(gd, rtW, rtH);
        var previousTargets = gd.GetRenderTargets();
        RenderTarget2D previousTarget = previousTargets.Length > 0 ? previousTargets[0].RenderTarget as RenderTarget2D : null;
        var previousViewport = gd.Viewport;

        gd.SetRenderTarget(_windowRenderTarget);
        gd.Viewport = new Viewport(0, 0, _windowRenderTarget.Width, _windowRenderTarget.Height);
        gd.Clear(Color.Transparent);

        UIElement.RenderOffset = screenAbsPos - paddingOffset;
        _contentBatch.BlurUVOffset = screenAbsPos - paddingOffset;
        _contentBatch.ScreenSizeOverride = new Vector2(previousViewport.Width, previousViewport.Height);

        try {
            _contentBatch.Begin(null, null);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            DrawBackBlur(spriteBatch, _contentBatch);
            DrawTitleBarToRT(_contentBatch);
            _contentBatch.End();
            spriteBatch.End();

            var contentScissorRect = new Rectangle(padding, padding + TitleBarHeight, windowW, windowH - TitleBarHeight);
            var previousScissorRect = gd.ScissorRectangle;
            var previousRasterizerState = gd.RasterizerState;
            gd.ScissorRectangle = contentScissorRect;
            
            _contentBatch.Begin(null, null);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, null, _scissorState);
            foreach (var child in Children) {
                if (child == _closeButton || child == _maxButton || child == _minButton) continue;
                child.Draw(spriteBatch, _contentBatch);
            }
            gd.RasterizerState = _scissorState;
            _contentBatch.End();
            spriteBatch.End();

            _contentBatch.Begin(null, null);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, null, _scissorState);
            OnDraw(spriteBatch, _contentBatch);
            gd.RasterizerState = _scissorState;
            _contentBatch.End();
            spriteBatch.End();
            
            gd.ScissorRectangle = previousScissorRect;
            gd.RasterizerState = previousRasterizerState;
            
            _contentBatch.Begin(null, null);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            DrawBorderAndChrome(spriteBatch, _contentBatch);
            _contentBatch.End();
            spriteBatch.End();

        } finally {
            UIElement.RenderOffset = Vector2.Zero;
            _contentBatch.BlurUVOffset = Vector2.Zero;
            _contentBatch.ScreenSizeOverride = null;
        }

        gd.SetRenderTarget(previousTarget);
        gd.Viewport = previousViewport;

        spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
        var destRect = new Rectangle((int)screenAbsPos.X - padding, (int)screenAbsPos.Y - padding, rtW, rtH);
        spriteBatch.Draw(_windowRenderTarget, destRect, new Rectangle(0, 0, rtW, rtH), Color.White * AbsoluteOpacity);
        spriteBatch.End();

        globalBatch.Begin();
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
    }
    
    private void DrawTitleBarToRT(ShapeBatch batch) {
        var absPos = AbsolutePosition;
        Color baseTitleBarColor = ((ActiveWindow == this) ? new Color(60, 60, 60) : new Color(40, 40, 40));
        if (_isBlinking) {
            float blinkPhase = (float)(_blinkTimer / BlinkDuration);
            float intensity = (float)Math.Abs(Math.Sin(blinkPhase * Math.PI * 6));
            baseTitleBarColor = Color.Lerp(baseTitleBarColor, new Color(120, 120, 140), intensity * 0.6f);
        }
        Color titleBarColor = baseTitleBarColor * 0.8f;
        batch.DrawBlurredRectangle(absPos, new Vector2(Size.X, TitleBarHeight), titleBarColor * AbsoluteOpacity, Color.Transparent, 0f, 5f, AbsoluteOpacity);
    }
    
    private void DrawBorderAndChrome(SpriteBatch spriteBatch, ShapeBatch batch) {
        var absPos = AbsolutePosition;
        var border = BorderColor * AbsoluteOpacity;
        if (!_isMaximized) batch.BorderRectangle(absPos, Size, border, thickness: 1f, rounded: 5f);
        batch.DrawLine(new Vector2(absPos.X + 5, absPos.Y + TitleBarHeight), new Vector2(absPos.X + Size.X - 5, absPos.Y + TitleBarHeight), 1f, border * 0.5f, border * 0.5f, 1f);

        float titleXOffset = 10;
        float controlButtonsWidth = 85;
        if (Icon != null && Size.X > controlButtonsWidth + 40) {
            float iconSize = 18f;
            float iconY = absPos.Y + (TitleBarHeight - iconSize) / 2f;
            float scale = iconSize / Icon.Width;
            batch.DrawTexture(Icon, new Vector2(absPos.X + 10, iconY), Color.White * AbsoluteOpacity, scale);
            titleXOffset += iconSize + 8;
        }
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
        foreach (var child in Children) {
            if (child == _closeButton || child == _maxButton || child == _minButton) child.Draw(spriteBatch, batch);
        }
    }

    protected void DrawBackBlur(SpriteBatch spriteBatch, ShapeBatch batch) {
        var absPos = AbsolutePosition;
        var glassTint = BackgroundColor * 0.4f * AbsoluteOpacity;
        batch.DrawBlurredRectangle(absPos + new Vector2(0, TitleBarHeight), new Vector2(Size.X, Size.Y - TitleBarHeight), glassTint, Color.Transparent, 0f, _isMaximized ? 0f : 5f, AbsoluteOpacity);
    }
}
