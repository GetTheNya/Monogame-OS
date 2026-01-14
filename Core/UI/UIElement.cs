using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using TheGame.Core.Input;
using TheGame.Graphics;
using Microsoft.Xna.Framework.Graphics;

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
    public bool IsVisible { get; set; } = true;
    public bool ConsumesInput { get; set; } = true; // If true, mouse/keyboard input is blocked for elements below
    public bool IsActive { get; set; } = true;
    public float Opacity { get; set; } = 1.0f;
    public float AbsoluteOpacity => (Parent?.AbsoluteOpacity ?? 1.0f) * Opacity;
    public Action OnRightClickAction { get; set; }
    public Action OnDoubleClickAction { get; set; }
    public object Tag { get; set; }

    protected UIElement() {
    }

    protected UIElement(Vector2 position, Vector2 size) {
        Position = position;
        Size = size;
    }

    // Allows parents to offset children (e.g. Window title bar)
    public virtual Vector2 GetChildOffset(UIElement child) => Vector2.Zero;

    // Relative to parent, or absolute if no parent? 
    // Let's assume Position is relative to Parent.
    public Vector2 AbsolutePosition => (Parent?.AbsolutePosition ?? Vector2.Zero) + (Parent?.GetChildOffset(this) ?? Vector2.Zero) + Position;
    public Rectangle Bounds => new Rectangle(AbsolutePosition.ToPoint(), Size.ToPoint());

    public bool IsMouseOver { get; private set; }
    public bool IsFocused { get; set; }

    public virtual void Update(GameTime gameTime) {
        if (!IsActive || !IsVisible) return;

        // Create a temporary copy to avoid crash if children are added/removed during update
        var childrenCopy = Children.ToArray();
        for (int i = childrenCopy.Length - 1; i >= 0; i--) {
            childrenCopy[i].Update(gameTime);
        }

        UpdateInput();
    }

    protected bool _isPressed;

    protected virtual void UpdateInput() {
        if (!IsVisible) return;

        bool wasMouseOver = IsMouseOver;
        // Check hover once at the start. If someone else consumed it, we aren't hovered.
        IsMouseOver = InputManager.IsMouseHovering(Bounds);

        // Store input states while we are still "the top element" (before we consume it ourselves)
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
                OnRightClickAction?.Invoke();
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
                    OnClick();
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

        DrawSelf(spriteBatch, shapeBatch);

        foreach (var child in Children) {
            child.Draw(spriteBatch, shapeBatch);
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

    public void BringToFront(UIElement child) {
        if (Children.Remove(child)) {
            Children.Add(child);
            child.Parent = this; // Should already be this, but safe to set
        }
    }
}
