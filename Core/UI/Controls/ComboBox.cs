using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using TheGame.Core.Input;
using TheGame.Graphics;
using TheGame.Core.OS;

namespace TheGame.Core.UI.Controls;

public class ComboBox : ValueControl<int> {
    public List<string> Items { get; private set; } = new();
    public string SelectedItem => Value >= 0 && Value < Items.Count ? Items[Value] : "Select...";

    private bool _isOpen = false;
    private float _itemHeight = 25f;
    private ComboBoxDropdown _dropdown;

    public Color TextColor { get; set; } = Color.White;

    public ComboBox(Vector2 position, Vector2 size) : base(position, size, -1) {
    }

    private float _arrowRotation = 0f;

    public override void Update(GameTime gameTime) {
        base.Update(gameTime);
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // Close dropdown if parent is fading out (window closing)
        if (_isOpen && AbsoluteOpacity < 0.5f) {
            CloseDropdown();
        }

        float targetRot = _isOpen ? (float)Math.PI : 0f;
        _arrowRotation = MathHelper.Lerp(_arrowRotation, targetRot, MathHelper.Clamp(dt * 15f, 0, 1));
    }

    protected override void OnClick() {
        if (_isOpen) CloseDropdown();
        else OpenDropdown();
        base.OnClick();
    }

    private void OpenDropdown() {
        _isOpen = true;
        var absPos = AbsolutePosition;
        _dropdown = new ComboBoxDropdown(
            new Vector2(absPos.X, absPos.Y + Size.Y),
            new Vector2(Size.X, Items.Count * _itemHeight),
            Items,
            Value,
            (selectedIndex) => {
                Value = selectedIndex;
                CloseDropdown();
            },
            () => CloseDropdown()
        );
        _dropdown.TextColor = TextColor;
        _dropdown.BackgroundColor = BackgroundColor;
        _dropdown.BorderColor = BorderColor;
        _dropdown.HoverColor = HoverColor;
        Shell.AddOverlayElement(_dropdown);
    }

    private void CloseDropdown() {
        _isOpen = false;
        _dropdown?.MarkForRemoval();
        _dropdown = null;
    }

    public override void Draw(SpriteBatch spriteBatch, ShapeBatch batch) {
        if (!IsVisible) return;

        var absPos = AbsolutePosition;

        // Current Selection Box
        batch.FillRectangle(absPos, Size, CurrentBackgroundColor * AbsoluteOpacity, rounded: 3f);
        batch.BorderRectangle(absPos, Size, BorderColor * AbsoluteOpacity, thickness: 1f, rounded: 3f);

        if (GameContent.FontSystem != null) {
            var font = GameContent.FontSystem.GetFont(20);
            float textY = (Size.Y - font.LineHeight) / 2f;
            font.DrawText(batch, SelectedItem, absPos + new Vector2(10, textY), TextColor * AbsoluteOpacity);

            // Arrow icon
            font.DrawText(batch, "▼", absPos + new Vector2(Size.X - 20, Size.Y / 2f), Color.Gray * AbsoluteOpacity, rotation: _arrowRotation, origin: new Vector2(8, 8));
        }
    }
}

// Separate overlay panel for dropdown
internal class ComboBoxDropdown : UIElement {
    private List<string> _items;
    private int _selectedIndex;
    private Action<int> _onSelect;
    private Action _onClose;
    private float _itemHeight = 25f;
    private float _openAnim = 0f;
    private bool _markedForRemoval = false;

    public Color TextColor { get; set; } = Color.White;
    public Color BackgroundColor { get; set; } = new Color(40, 40, 40);
    public Color BorderColor { get; set; } = Color.Gray;
    public Color HoverColor { get; set; } = new Color(60, 60, 60);

    public ComboBoxDropdown(Vector2 position, Vector2 size, List<string> items, int selectedIndex, Action<int> onSelect, Action onClose) 
        : base(position, size) {
        _items = items;
        _selectedIndex = selectedIndex;
        _onSelect = onSelect;
        _onClose = onClose;
        ConsumesInput = true;
    }

    public void MarkForRemoval() {
        _markedForRemoval = true;
    }

    public override void Update(GameTime gameTime) {
        base.Update(gameTime);
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // Animate opening
        float targetAnim = _markedForRemoval ? 0f : 1f;
        _openAnim = MathHelper.Lerp(_openAnim, targetAnim, MathHelper.Clamp(dt * 15f, 0, 1));

        // Remove self when animation completes
        if (_markedForRemoval && _openAnim < 0.01f) {
            Parent?.RemoveChild(this);
            return;
        }

        // Close if clicking outside
        if (InputManager.IsAnyMouseButtonJustPressed(MouseButton.Left)) {
            bool clickedInside = false;
            for (int i = 0; i < _items.Count; i++) {
                var itemBounds = GetItemBounds(i);
                if (itemBounds.Contains(InputManager.MousePosition)) {
                    _onSelect?.Invoke(i);
                    InputManager.IsMouseConsumed = true;
                    clickedInside = true;
                    return;
                }
            }

            if (!clickedInside) {
                // If it's a click on the parent combobox, let the parent's OnClick handle it
                var parent = Parent as ComboBox; // Wait, ComboBox is the one that created this overlay via Shell.AddOverlay
                // The dropdown is added as a top-level child of UI root.
                
                _onClose?.Invoke();
                // Consume the click that closed the dropdown so it doesn't trigger other UI 
                InputManager.IsMouseConsumed = true;
            }
        }
    }

    private Rectangle GetItemBounds(int index) {
        var absPos = AbsolutePosition;
        return new Rectangle(
            (int)absPos.X,
            (int)(absPos.Y + index * (_itemHeight * _openAnim)),
            (int)Size.X,
            (int)(_itemHeight * _openAnim)
        );
    }

    public override void Draw(SpriteBatch spriteBatch, ShapeBatch batch) {
        if (!IsVisible || _openAnim < 0.01f) return;

        var absPos = AbsolutePosition;

        for (int i = 0; i < _items.Count; i++) {
            float itemY = absPos.Y + i * (_itemHeight * _openAnim);
            Vector2 itemPos = new Vector2(absPos.X, itemY);

            Color itemColor = BackgroundColor;
            Rectangle itemHitRect = GetItemBounds(i);
            if (itemHitRect.Contains(InputManager.MousePosition) && !InputManager.IsMouseConsumed) {
                itemColor = HoverColor;
            }

            batch.FillRectangle(itemPos, new Vector2(Size.X, _itemHeight * _openAnim), itemColor * _openAnim);
            batch.BorderRectangle(itemPos, new Vector2(Size.X, _itemHeight * _openAnim), BorderColor * (0.3f * _openAnim), thickness: 1f);

            if (GameContent.FontSystem != null && _openAnim > 0.5f) {
                var font = GameContent.FontSystem.GetFont(18);
                float itemTextY = (_itemHeight - font.LineHeight) / 2f;
                font.DrawText(batch, _items[i], itemPos + new Vector2(10, itemTextY), TextColor * _openAnim);
            }
        }
    }
}
