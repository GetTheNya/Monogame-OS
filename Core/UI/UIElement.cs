using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using TheGame.Core.Input;
using TheGame.Graphics;
using Microsoft.Xna.Framework.Graphics;

using TheGame.Core.OS;
using System.Linq;

namespace TheGame.Core.UI;

public abstract class UIElement {
    public UIElement Parent { get; set; }
    public List<UIElement> Children { get; private set; } = new();

    private Vector2 _position;
    public Vector2 Position {
        get => _position;
        set {
            if (_position == value) return;
            _position = value;
            OnMove?.Invoke();
        }
    }

    private Vector2 _size;
    public Vector2 Size {
        get => _size;
        set {
            if (_size == value) return;
            _size = value;
            OnResize?.Invoke();
        }
    }

    public System.Action OnResize { get; set; }
    public System.Action OnMove { get; set; }
    public virtual Vector2 ClientSize => Size;
    public bool IsVisible { get; set; } = true;
    public bool ConsumesInput { get; set; } = true; // If true, mouse/keyboard input is blocked for elements below
    public bool CanFocus { get; set; } = true; // If true, clicking this element will focus it
    public bool IsActive { get; set; } = true;
    public float Opacity { get; set; } = 1.0f;
    public float AbsoluteOpacity => (Parent?.AbsoluteOpacity ?? 1.0f) * Opacity;
    public Action OnRightClickAction { get; set; }
    public Action OnDoubleClickAction { get; set; }
    public object Tag { get; set; }

    // Tooltip properties
    public string Tooltip { get; set; }
    public float TooltipDelay { get; set; } = 0.5f;

    protected UIElement() {
    }

    protected UIElement(Vector2 position, Vector2 size) {
        Position = position;
        Size = size;
    }

    /// <summary>
    /// Finds the process that owns this UI element by traversing up the parent tree.
    /// </summary>
    public Process GetOwnerProcess() {
        if (this is Window window) return window.OwnerProcess;
        return Parent?.GetOwnerProcess();
    }

    // Allows parents to offset children (e.g. Window title bar)
    public virtual Vector2 GetChildOffset(UIElement child) => Vector2.Zero;

    // Relative to parent, or absolute if no parent? 
    // Let's assume Position is relative to Parent.
    public Vector2 AbsolutePosition => (Parent?.AbsolutePosition ?? Vector2.Zero) + (Parent?.GetChildOffset(this) ?? Vector2.Zero) + Position;
    public Rectangle Bounds => new Rectangle(AbsolutePosition.ToPoint(), Size.ToPoint());

    public bool IsMouseOver { get; protected set; }
    
    private bool _isFocused;
    public bool IsFocused {
        get => _isFocused;
        set {
            if (_isFocused == value) return;
            _isFocused = value;
            if (_isFocused) OnFocused();
            else OnUnfocused();
        }
    }

    protected virtual void OnFocused() { }
    protected virtual void OnUnfocused() { }

    // Clipboard & Selection API
    public virtual void Copy() { }
    public virtual void Cut() { }
    public virtual void Paste() { }
    public virtual void DeleteSelection() { }
    public virtual bool HasSelection() => false;
    
    /// <summary>
    /// Returns the element at the specified position, recursing into children.
    /// Default implementation uses simple bounds check.
    /// </summary>
    public virtual UIElement GetElementAt(Vector2 pos) {
        if (!IsVisible || !Bounds.Contains(pos)) return null;

        // Check children first (top-most in Z-order are at the end of the list)
        for (int i = Children.Count - 1; i >= 0; i--) {
            var found = Children[i].GetElementAt(pos);
            if (found != null) return found;
        }

        // Only return this element if it actually consumes input.
        return ConsumesInput ? this : null;
    }

    /// <summary>
    /// Returns the absolute screen position of the text caret, if applicable.
    /// </summary>
    public virtual Vector2? GetCaretPosition() => null;

    public virtual void Update(GameTime gameTime) {
        if (!IsActive || !IsVisible) return;

        try {
            // Iterate in reverse using index to avoid allocation and handle removal
            for (int i = Children.Count - 1; i >= 0; i--) {
                if (i < Children.Count) { // Check bounds in case child was removed
                    Children[i].Update(gameTime);
                }
            }

            UpdateInput();
        } catch (Exception ex) {
            if (!CrashHandler.TryHandleAnyAppException(ex)) {
                throw;
            }
        }
    }

    protected bool _isPressed;

    protected virtual void UpdateInput() {
        if (!IsVisible) return;

        bool wasMouseOver = IsMouseOver;
        // Unified hover check: uses the global result from recursive GetElementAt
        IsMouseOver = UIManager.IsHovered(this);

        // Store input states while we are still "the top element" (before we consume it ourselves)
        // ignoreConsumed: false ensures we only react if no one else (in front) caught the click
        bool justPressed = IsMouseOver && InputManager.IsMouseButtonJustPressed(MouseButton.Left);
        bool justRightPressed = IsMouseOver && InputManager.IsMouseButtonJustPressed(MouseButton.Right);
        bool justReleased = InputManager.IsMouseButtonJustReleased(MouseButton.Left);

        if (IsMouseOver) {
            // If we are hovered and we consume input, block anything underneath from seeing a hover or click.
            if (ConsumesInput)
                InputManager.IsMouseConsumed = true;

            OnHover();
            if (justPressed) {
                _isPressed = true;
            }

            if (justRightPressed) {
                try {
                    OnRightClickAction?.Invoke();
                } catch (Exception ex) {
                    var process = GetOwnerProcess();
                    if (process != null && CrashHandler.IsAppException(ex, process)) {
                        CrashHandler.HandleAppException(process, ex);
                    } else {
                        throw; // Re-throw if it's an OS-level exception
                    }
                }
            }
        }

        if (_isPressed) {
            // Even if released outside, we consume the release if we were the one being pressed?
            // Actually, usually we only consume if we click. 
            // But we must reset _isPressed.
            if (justReleased) {
                bool wasOver = IsMouseOver; // Use our calculated hover state
                _isPressed = false;

                if (wasOver) {
                    try {
                        OnClick();
                    } catch (Exception ex) {
                        var process = GetOwnerProcess();
                        if (process != null && CrashHandler.IsAppException(ex, process)) {
                            CrashHandler.HandleAppException(process, ex);
                        } else {
                            throw;
                        }
                    }
                    if (ConsumesInput)
                        InputManager.IsMouseConsumed = true; // Consume release
                }
            } else if (!InputManager.IsMouseButtonDown(MouseButton.Left)) {
                // Mouse up but somehow missed JustReleased (or lost focus logic)
                _isPressed = false;
            }
        }
    }

    public virtual void Draw(SpriteBatch spriteBatch, ShapeBatch shapeBatch) {
        if (!IsVisible) return;

        try {
            DrawSelf(spriteBatch, shapeBatch);

            foreach (var child in Children) {
                child.Draw(spriteBatch, shapeBatch);
            }
        } catch (Exception ex) {
            if (!CrashHandler.TryHandleAnyAppException(ex)) {
                throw;
            }
        }
    }

    protected virtual void DrawSelf(SpriteBatch spriteBatch, ShapeBatch shapeBatch) {
    }

    protected virtual void OnClick() {
    }

    protected virtual void OnHover() {
    }

    public virtual void AddChild(UIElement child) {
        child.Parent = this;
        Children.Add(child);
    }

    public void RemoveChild(UIElement child) {
        if (Children.Remove(child)) {
            child.Parent = null;
        }
    }

    public void ClearChildren() {
        foreach (var child in Children) child.Parent = null;
        Children.Clear();
    }

    public void BringToFront(UIElement child) {
        if (Children.Remove(child)) {
            Children.Add(child);
            child.Parent = this; // Should already be this, but safe to set
        }
    }

    /// <summary>
    /// Recursively closes any open popups or overlays in this element or its children.
    /// </summary>
    public virtual void ClosePopups() {
        // Use a for loop to avoid modification issues during iteration
        for (int i = 0; i < Children.Count; i++) {
            Children[i].ClosePopups();
        }
    }
}
