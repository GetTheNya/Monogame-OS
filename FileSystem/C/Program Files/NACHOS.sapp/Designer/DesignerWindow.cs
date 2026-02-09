using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Graphics;
using TheGame.Core;
using TheGame;
using FontStashSharp;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;
using TheGame.Core.Input;
using TheGame.Core.OS;
using TheGame.Core.OS.DragDrop;
using System.Linq;
using System.Collections.Generic;

namespace NACHOS.Designer;

/// <summary>
/// A "Fake" window component used in the Designer to simulate a Window 
/// without the RenderTarget redirection and OS-level window logic.
/// </summary>
public class DesignerWindow : Panel, IDropTarget {
    private const int TitleBarHeight = 30;
    public string Title { get; set; } = "New Window";
    public Texture2D Icon { get; set; }

    public override Vector2 RawAbsolutePosition {
        get {
            // If we are the root window in a DesignerSurface, pin us to top-left (5,5)
            // Parent is the ContentLayer, Parent.Parent is the DesignerSurface
            if (Parent?.Parent is DesignerSurface) {
                return Parent.RawAbsolutePosition + new Vector2(5, 5);
            }
            return base.RawAbsolutePosition;
        }
    }

    public DesignerWindow() : this(Vector2.Zero, new Vector2(400, 300)) {  }

    public DesignerWindow(Vector2 position, Vector2 size) : base(position, size) {
        BackgroundColor = new Color(30, 30, 30, 240);
        BorderColor = Color.White;
        CornerRadius = 5f;
    }

    public override Vector2 GetChildOffset(UIElement child) {
        return new Vector2(0, TitleBarHeight);
    }

    public override UIElement GetElementAt(Vector2 pos) {
        if (!IsVisible || !Bounds.Contains(pos)) return null;

        // Check children first (offset by title bar)
        var snapshot = Children;
        for (int i = snapshot.Count - 1; i >= 0; i--) {
            var found = snapshot[i].GetElementAt(pos);
            if (found != null) return found;
        }

        return ConsumesInput ? this : null;
    }

    protected override void DrawSelf(SpriteBatch spriteBatch, ShapeBatch batch) {
        var absPos = AbsolutePosition;
        float opacity = AbsoluteOpacity;

        // Draw background and border (from Panel)
        base.DrawSelf(spriteBatch, batch);

        // Draw Title Bar
        Color baseTitleBarColor = new Color(60, 60, 60);
        batch.DrawBlurredRectangle(absPos, new Vector2(Size.X, TitleBarHeight), baseTitleBarColor * 0.8f * opacity, Color.Transparent, 0f, CornerRadius, opacity);

        // Draw Title Bar Separator
        batch.DrawLine(
            new Vector2(absPos.X + 5, absPos.Y + TitleBarHeight),
            new Vector2(absPos.X + Size.X - 5, absPos.Y + TitleBarHeight),
            1f, BorderColor * 0.5f * opacity, BorderColor * 0.5f * opacity, 1f
        );

        // Draw Icon
        float titleXOffset = 10;
        if (Icon != null) {
            float iconSize = 18f;
            float iconY = absPos.Y + (TitleBarHeight - iconSize) / 2f;
            float scale = iconSize / Icon.Width;
            batch.DrawTexture(Icon, new Vector2(absPos.X + 10, iconY), Color.White * opacity, scale);
            titleXOffset += iconSize + 8;
        }

        // Draw Title Text
        if (GameContent.FontSystem != null) {
            var font = GameContent.FontSystem.GetFont(20);
            if (font != null) {
                font.DrawText(batch, Title, absPos + new Vector2(titleXOffset, 5), Color.White * opacity);
            }
        }

        // Draw fake chrome buttons
        float rightOffset = 5;
        Vector2 btnSize = new Vector2(20, 20);
        
        // Close Button (X)
        DrawFakeButton(batch, "X", new Vector2(absPos.X + Size.X - btnSize.X - rightOffset, absPos.Y + (TitleBarHeight - btnSize.Y) / 2), btnSize, opacity);
        rightOffset += btnSize.X + 2;
        
        // Max Button (O)
        DrawFakeButton(batch, "O", new Vector2(absPos.X + Size.X - btnSize.X - rightOffset, absPos.Y + (TitleBarHeight - btnSize.Y) / 2), btnSize, opacity);
        rightOffset += btnSize.X + 2;
        
        // Min Button (_)
        DrawFakeButton(batch, "_", new Vector2(absPos.X + Size.X - btnSize.X - rightOffset, absPos.Y + (TitleBarHeight - btnSize.Y) / 2), btnSize, opacity);
    }

    public override void Update(GameTime gameTime) {
        base.Update(gameTime);

        // Visual feedback during drag
        if (Shell.Drag.IsActive && IsVisible && Bounds.Contains(InputManager.MousePosition)) {
            Shell.Drag.CheckDropTarget(this, InputManager.MousePosition.ToVector2());
        }
    }

    protected override void UpdateInput() {
        base.UpdateInput();

        // Handle dropping
        if (Shell.Drag.IsActive && IsVisible && 
            InputManager.IsMouseButtonJustReleased(MouseButton.Left) && 
            Bounds.Contains(InputManager.MousePosition)) {
            
            Shell.Drag.TryDropOn(this, InputManager.MousePosition.ToVector2());
        }
    }

    private void DrawFakeButton(ShapeBatch batch, string text, Vector2 pos, Vector2 size, float opacity) {
        // Just draw the text for the fake button
        if (GameContent.FontSystem != null) {
            var font = GameContent.FontSystem.GetFont(16);
            if (font != null) {
                var textSize = font.MeasureString(text);
                font.DrawText(batch, text, pos + (size - textSize) / 2, Color.White * 0.7f * opacity);
            }
        }
    }

    // IDropTarget implementation
    public bool CanAcceptDrop(object dragData) {
        if (dragData is ControlTypeDragData) return true;
        if (dragData is IDraggable draggable) {
            return draggable.GetDragData() is ControlTypeDragData;
        }
        return false;
    }

    public DragDropEffect OnDragOver(object data, Vector2 position) {
        // if (CanAcceptDrop(data)) {
        //     Shell.Drag.SetDropPreview("toolbox_preview", position);
        //     return DragDropEffect.Copy;
        // }
        return DragDropEffect.None;
    }

    public void OnDragLeave() {
        //Shell.Drag.SetDropPreview("toolbox_preview", null);
    }

    public Rectangle GetDropBounds() => Bounds;

    public bool OnDrop(object data, Vector2 dropPosition) {
        object actualData = data;
        if (data is IDraggable draggable) {
            actualData = draggable.GetDragData();
        }

        if (actualData is ControlTypeDragData dragData) {
            // Only allow non-window controls to be dropped onto this window
            if (dragData.ControlType == typeof(Window) || dragData.ControlType == typeof(DesignerWindow)) {
                return false;
            }

            var instance = System.Activator.CreateInstance(dragData.ControlType) as UIElement;
            if (instance != null) {
                // Find container to nest in (Panel or this DesignerWindow)
                UIElement targetContainer = GetElementAt(dropPosition);
                while (targetContainer != null && !(targetContainer is Panel || targetContainer is DesignerWindow)) {
                    targetContainer = targetContainer.Parent;
                }
                if (targetContainer == null) targetContainer = this;

                // Adjust position to local coordinates of target container
                instance.Position = dropPosition - (targetContainer.AbsolutePosition + targetContainer.GetChildOffset(instance));
                
                // Sensible default size
                if (instance.Size == Vector2.Zero) {
                    if (instance is Panel) instance.Size = new Vector2(200, 150);
                    else if (instance is Button) instance.Size = new Vector2(100, 30);
                    else if (instance is Label) instance.Size = new Vector2(100, 20);
                    else if (instance is Slider) instance.Size = new Vector2(150, 20);
                    else instance.Size = new Vector2(100, 30);
                }

                instance.ConsumesInput = true;
                
                targetContainer.AddChild(instance);
                
                // Notify surface if possible (via parent tree or static event)
                // For now, we assume the user will select it manually or we can trigger selection here if we find the surface.
                var surface = GetParent<DesignerSurface>();
                if (surface != null) {
                    surface.SelectElement(instance);
                    surface.NotifyElementModified(instance);
                }

                return true;
            }
        }
        return false;
    }

    public void CheckDropTarget(Vector2 pos) {
        if (Shell.Drag.IsActive && IsVisible && Bounds.Contains(pos.ToPoint())) {
            Shell.Drag.CheckDropTarget(this, pos);
        }
    }

    private T GetParent<T>() where T : class {
        UIElement current = Parent;
        while (current != null) {
            if (current is T typed) return typed;
            current = current.Parent;
        }
        return null;
    }
}
