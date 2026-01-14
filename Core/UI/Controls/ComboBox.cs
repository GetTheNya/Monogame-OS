using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using TheGame.Core.Input;
using TheGame.Graphics;

namespace TheGame.Core.UI.Controls;

public class ComboBox : ValueControl<int> {
    public List<string> Items { get; private set; } = new();
    public string SelectedItem => Value >= 0 && Value < Items.Count ? Items[Value] : "Select...";

    private bool _isOpen = false;
    private float _itemHeight = 25f;

    public Color TextColor { get; set; } = Color.White;

    public ComboBox(Vector2 position, Vector2 size) : base(position, size, -1) {
    }

    private float _openAnim = 0f;
    private float _arrowRotation = 0f;

    public override void Update(GameTime gameTime) {
        base.Update(gameTime);
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        float targetOpen = _isOpen ? 1f : 0f;
        _openAnim = MathHelper.Lerp(_openAnim, targetOpen, MathHelper.Clamp(dt * 15f, 0, 1));

        float targetRot = _isOpen ? (float)Math.PI : 0f;
        _arrowRotation = MathHelper.Lerp(_arrowRotation, targetRot, MathHelper.Clamp(dt * 15f, 0, 1));
    }

    protected override void UpdateInput() {
        base.UpdateInput();

        if (InputManager.IsAnyMouseButtonJustPressed(MouseButton.Left)) {
            if (IsMouseOver) {
                _isOpen = !_isOpen;
                InputManager.IsMouseConsumed = true;
            } else if (_isOpen) {
                // Check if clicking an item
                var absPos = AbsolutePosition;
                bool itemClicked = false;
                for (int i = 0; i < Items.Count; i++) {
                    var itemBounds = new Rectangle(
                        (int)absPos.X,
                        (int)(absPos.Y + Size.Y + i * (_itemHeight * _openAnim)),
                        (int)Size.X,
                        (int)(_itemHeight * _openAnim)
                    );

                    if (itemBounds.Contains(InputManager.MousePosition)) {
                        Value = i;
                        _isOpen = false;
                        InputManager.IsMouseConsumed = true;
                        itemClicked = true;
                        break;
                    }
                }

                if (!itemClicked) {
                    _isOpen = false;
                }
            }
        }
    }

    public override void Draw(SpriteBatch spriteBatch, ShapeBatch batch) {
        if (!IsVisible) return;

        var absPos = AbsolutePosition;

        // Current Selection Box
        batch.FillRectangle(absPos, Size, CurrentBackgroundColor * Opacity, rounded: 3f);
        batch.BorderRectangle(absPos, Size, BorderColor * Opacity, thickness: 1f, rounded: 3f);

        if (GameContent.FontSystem != null) {
            var font = GameContent.FontSystem.GetFont(20);
            float textY = (Size.Y - font.LineHeight) / 2f;
            font.DrawText(batch, SelectedItem, absPos + new Vector2(10, textY), TextColor * Opacity);

            // Arrow icon
            font.DrawText(batch, "▼", absPos + new Vector2(Size.X - 20, Size.Y / 2f), Color.Gray * Opacity, rotation: _arrowRotation, origin: new Vector2(8, 8));
        }

        // Dropdown Items
        if (_openAnim > 0.01f) {
            for (int i = 0; i < Items.Count; i++) {
                float itemY = absPos.Y + Size.Y + i * (_itemHeight * _openAnim);
                Vector2 itemPos = new Vector2(absPos.X, itemY);

                Color itemColor = BackgroundColor;
                // Highlight if hovered
                Rectangle itemHitRect = new Rectangle((int)absPos.X, (int)itemY, (int)Size.X, (int)(_itemHeight * _openAnim));
                if (itemHitRect.Contains(InputManager.MousePosition) && !InputManager.IsMouseConsumed) {
                    itemColor = HoverColor;
                }

                batch.FillRectangle(itemPos, new Vector2(Size.X, _itemItemHeightAnimationFix()), itemColor * (Opacity * _openAnim));
                batch.BorderRectangle(itemPos, new Vector2(Size.X, _itemItemHeightAnimationFix()), BorderColor * (0.3f * _openAnim), thickness: 1f);

                if (GameContent.FontSystem != null && _openAnim > 0.5f) {
                    var font = GameContent.FontSystem.GetFont(18);
                    float itemTextY = (_itemHeight - font.LineHeight) / 2f;
                    font.DrawText(batch, Items[i], itemPos + new Vector2(10, itemTextY), TextColor * (Opacity * _openAnim));
                }
            }
        }
    }

    private float _itemItemHeightAnimationFix() => _itemHeight * _openAnim;
}
