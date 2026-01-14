using Microsoft.Xna.Framework;
using TheGame.Graphics;
using System;
using TheGame.Core.Input;
using TheGame.Core.Animation;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Core.UI.Controls;

namespace TheGame.Core.UI;

public class StartMenu : Panel {
    private const float MenuItemHeight = 30f;
    private const float MenuPadding = 5f;
    
    private Vector2 _targetPosition;
    private Vector2 _hiddenPosition;

    public StartMenu(Vector2 position, Vector2 size) : base(position, size) {
        IsVisible = false; // Hidden by default
        BackgroundColor = new Color(30, 30, 30);
        BorderColor = new Color(80, 80, 80);
        
        // Store target positions
        _targetPosition = position;
        _hiddenPosition = new Vector2(position.X, position.Y + size.Y); // Slide down by full height
        
        Opacity = 0f;

        // Add some mock items (using null for icons initially)
        AddMenuItem(0, "Notepad", null, () => { 
            Console.WriteLine("Open Notepad");
            Toggle();
        });

        AddMenuItem(1, "Calculator", null, () => { 
            Console.WriteLine("Open Calculator");
            Toggle();
        });
        
        AddMenuItem(2, "Shut Down", null, () => { 
             System.Environment.Exit(0);
        });
    }
    
    // Animation Property needed for opacity? UIElement handles it via Draw.

    public void Toggle() {
        Tweener.CancelAll(this);
        if (IsVisible && Position.Y < _hiddenPosition.Y - 10) { // If open (or opening)
             // Close
             // Tweener.To(this, v => Opacity = v, 1f, 0f, 0.2f, Easing.Linear).OnComplete = () => { IsVisible = false; };
             Tweener.To(this, v => Position = v, Position, _hiddenPosition, 0.2f, Easing.EaseOutQuad).OnComplete = () => { IsVisible = false; };
        } else {
             // Open
             IsVisible = true;
             Position = _hiddenPosition;
             Opacity = 1f; // No Fade
             // Tweener.To(this, v => Opacity = v, 0f, 1f, 0.2f, Easing.Linear);
             Tweener.To(this, v => Position = v, _hiddenPosition, _targetPosition, 0.25f, Easing.EaseOutQuad);
             // Don't bring to front - taskbar should stay on top
        }
    }

    public void ClearMenuItems() {
        for (int i = Children.Count - 1; i >= 0; i--) {
            RemoveChild(Children[i]);
        }
    }

    public void AddMenuItem(int index, string text, Texture2D icon, Action action) {
        var btn = new Button(
            new Vector2(MenuPadding, MenuPadding + index * (MenuItemHeight + MenuPadding)), 
            new Vector2(Size.X - (MenuPadding * 2), MenuItemHeight), 
            text) 
        {
            OnClickAction = action,
            Icon = icon,
            BackgroundColor = Color.Transparent,
            HoverColor = new Color(60, 60, 60)
        };
        AddChild(btn);
    }
    
    protected override void DrawSelf(SpriteBatch spriteBatch, ShapeBatch batch) {
        var absPos = AbsolutePosition;
        batch.FillRectangle(absPos, Size, BackgroundColor * Opacity); // Apply opacity
        batch.BorderRectangle(absPos, Size, BorderColor * Opacity, thickness: 1f);
        
        // Buttons draw themselves, we need to ensure they inherit Opacity?
        // UIElement doesn't propagate Opacity automatically.
        // We can hack it? Button checks Parent's Opacity?
        // Or update Button DrawSelf to use Parent Opacity?
        // Or StartMenu sets children Opacity?
        // Let's rely on simple Panel opacity for now. If buttons don't fade, it looks weird.
        // I'll update Button.cs to multiply by Parent Opacity recursively or just check parent?
        // Standard: Cascade opacity.
        // For now, let's just make Panel fade.
    }
    
    public override void Update(GameTime gameTime) {
        base.Update(gameTime);
        
        // Auto-Close Logic
        if (IsVisible && InputManager.IsMouseButtonJustPressed(MouseButton.Left)) {
            // Check if click is outside our bounds
            if (!Bounds.Contains(InputManager.MousePosition)) {
                if (!InputManager.IsMouseConsumed) {
                    Toggle(); // Animate close
                }
            }
        }
    }
}
