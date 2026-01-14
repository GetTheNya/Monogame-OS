using Microsoft.Xna.Framework;
using System;
using TheGame.Core.Input;
using TheGame.Core.OS;

namespace TheGame.Core.UI.Controls;

public class DropTargetButton : Button {
    public Action<object> OnDropAction { get; set; }

    public DropTargetButton(Vector2 position, Vector2 size, string text = "") : base(position, size, text) {
    }

    public override void Update(GameTime gameTime) {
        base.Update(gameTime);

        if (!IsVisible) return;

        // If something is released over us that was being dragged
        if (InputManager.IsMouseButtonJustReleased(MouseButton.Left) && IsMouseOver) {
            if (Shell.DraggedItem != null) {
                OnDropAction?.Invoke(Shell.DraggedItem);
                Shell.DraggedItem = null;
            }
        }
    }
}
