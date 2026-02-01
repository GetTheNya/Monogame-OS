using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using TheGame.Core.Input;
using TheGame.Graphics;
using Microsoft.Xna.Framework.Graphics;

using TheGame.Core.OS;
using System.Linq;

namespace TheGame.Core.UI;

public abstract class UIElement : IContextMenuProvider {
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
    public bool IsEnabled { get; set; } = true;
    public float Opacity { get; set; } = 1.0f;
    public float AbsoluteOpacity => (Parent?.AbsoluteOpacity ?? 1.0f) * Opacity;
    public Action OnRightClickAction { get; set; }
    public Action OnDoubleClickAction { get; set; }
    
    /// <summary>
    /// Optional name for element identification (e.g., "SubmitBtn").
    /// </summary>
    public string Name { get; set; }
    
    /// <summary>
    /// Generic tag for storing custom metadata.
    /// </summary>
    public object Tag { get; set; }
    
    /// <summary>
    /// Finds the first child of type T with optional name match (deep search).
    /// If name is null, returns first element of type T.
    /// </summary>
    public T GetChild<T>(string name = null) where T : UIElement {
        foreach (var child in Children) {
            if (child is T typed && (name == null || child.Name == name)) {
                return typed;
            }
            var found = child.GetChild<T>(name);
            if (found != null) return found;
        }
        return null;
    }
    
    /// <summary>
    /// Finds all children of type T with optional name match (deep search).
    /// </summary>
    public List<T> GetChildren<T>(string name = null) where T : UIElement {
        var results = new List<T>();
        GetChildrenRecursive(name, results);
        return results;
    }
    
    private void GetChildrenRecursive<T>(string name, List<T> results) where T : UIElement {
        foreach (var child in Children) {
            if (child is T typed && (name == null || child.Name == name)) {
                results.Add(typed);
            }
            child.GetChildrenRecursive(name, results);
        }
    }
    
    /// <summary>
    /// Finds element by path (e.g., "Sidebar/Settings/VolumeSlider").
    /// </summary>
    public T GetChildByPath<T>(string path) where T : UIElement {
        if (string.IsNullOrEmpty(path)) return this as T;
        var parts = path.Split('/');
        UIElement current = this;
        
        foreach (var part in parts) {
            var found = current.Children.FirstOrDefault(c => c.Name == part);
            if (found == null) return null;
            current = found;
        }
        
        return current as T;
    }

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

    /// <summary>
    /// Finds the window that contains this UI element by traversing up the parent tree.
    /// </summary>
    public Window GetOwnerWindow() {
        if (this is Window window) return window;
        return Parent?.GetOwnerWindow();
    }

    // Allows parents to offset children (e.g. Window title bar)
    public virtual Vector2 GetChildOffset(UIElement child) => Vector2.Zero;

    /// <summary>
    /// Static render offset used during RenderTarget rendering.
    /// When set, AbsolutePosition is adjusted to create local coordinates.
    /// </summary>
    public static Vector2 RenderOffset { get; set; } = Vector2.Zero;

    // Relative to parent, or absolute if no parent? 
    // Let's assume Position is relative to Parent.
    // RawAbsolutePosition computes the true screen position without any offset
    protected Vector2 RawAbsolutePosition => (Parent?.RawAbsolutePosition ?? Vector2.Zero) + (Parent?.GetChildOffset(this) ?? Vector2.Zero) + Position;
    
    // AbsolutePosition subtracts RenderOffset ONCE at the end (not recursively)
    public Vector2 AbsolutePosition => RawAbsolutePosition - RenderOffset;
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

        bool alreadyConsumed = InputManager.IsMouseConsumed;
        IsMouseOver = UIManager.IsHovered(this);
        bool justPressed = IsMouseOver && !alreadyConsumed && InputManager.IsMouseButtonJustPressed(MouseButton.Left);
        bool justRightPressed = IsMouseOver && !alreadyConsumed && InputManager.IsMouseButtonJustPressed(MouseButton.Right);
        bool justReleased = InputManager.IsMouseButtonJustReleased(MouseButton.Left);

        if (IsMouseOver) {
            // If we are hovered and we consume input, block anything underneath from seeing a hover or click.
            if (ConsumesInput)
                InputManager.IsMouseConsumed = true;

            if (IsEnabled) {
                OnHover();
                if (justPressed) {
                    _isPressed = true;
                }
            }

            if (justRightPressed) {
                if (HandleContextMenuInput()) {
                    InputManager.IsMouseConsumed = true;
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

                if (wasOver && IsEnabled && !alreadyConsumed) {
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
            
            var children = Children.ToList();
            foreach (var child in children) {
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

    public virtual void PopulateContextMenu(ContextMenuContext context, List<MenuItem> items) {
        // Override to add items
    }

    protected bool HandleContextMenuInput() {
        try {
            var context = new ContextMenuContext(this, InputManager.MousePosition.ToVector2());
            Shell.ContextMenu.Show(context);
            OnRightClickAction?.Invoke();
            return true;
        } catch (Exception ex) {
            var process = GetOwnerProcess();
            if (process != null && CrashHandler.IsAppException(ex, process)) {
                CrashHandler.HandleAppException(process, ex);
            } else {
                throw; // Re-throw if it's an OS-level exception
            }
            return false;
        }
    }
}
