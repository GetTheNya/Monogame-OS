using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Graphics;
using System;
using TheGame.Core.Animation;

namespace TheGame.Core.UI;

public class Tooltip : UIElement {
    private string _text = "";
    private string _wrappedText = "";
    private Vector2 _textSize;
    private const float Padding = 6f;
    private const float MaxWidth = 300f;
    
    private float _slideOffset = 10f;
    private float _currentOpacity = 0f;

    public Tooltip() {
        IsVisible = false;
        ConsumesInput = false;
    }

    public void SetText(string text) {
        if (_text == text) return;
        _text = text;
        UpdateLayout();
    }

    public void AnimateIn() {
        Tweener.CancelAll(this);
        // Reset state BEFORE making visible
        _slideOffset = 10f;
        _currentOpacity = 0f;
        IsVisible = true;

        Tweener.To(this, v => _slideOffset = v, _slideOffset, 0f, 0.2f, Easing.EaseOutQuad);
        Tweener.To(this, v => _currentOpacity = v, _currentOpacity, 1f, 0.2f, Easing.Linear);
    }

    public void AnimateOut(Action onComplete) {
        Tweener.CancelAll(this);
        if (_currentOpacity < 0.01f) {
            IsVisible = false;
            onComplete?.Invoke();
            return;
        }

        Tweener.To(this, v => _slideOffset = v, _slideOffset, 10f, 0.15f, Easing.EaseInQuad);
        Tweener.To(this, v => _currentOpacity = v, _currentOpacity, 0f, 0.15f, Easing.Linear)
            .OnCompleteAction(() => {
                IsVisible = false;
                onComplete?.Invoke();
            });
    }

    private void UpdateLayout() {
        if (string.IsNullOrEmpty(_text)) {
            _wrappedText = "";
            Size = Vector2.Zero;
            return;
        }

        var font = GameContent.FontSystem?.GetFont(14);
        if (font == null) return;

        _wrappedText = TextHelper.WrapText(font, _text, MaxWidth);
        _textSize = font.MeasureString(_wrappedText);
        Size = _textSize + new Vector2(Padding * 2);
    }

    protected override void DrawSelf(SpriteBatch spriteBatch, ShapeBatch batch) {
        if (string.IsNullOrEmpty(_wrappedText) || _currentOpacity < 0.01f) return;

        var absPos = AbsolutePosition + new Vector2(0, _slideOffset);
        var font = GameContent.FontSystem?.GetFont(14);
        var opacity = _currentOpacity;

        // Background (properly premultiplied)
        batch.FillRectangle(absPos, Size, new Color(30, 30, 30, 240) * opacity, rounded: 4f);
        // Border
        batch.BorderRectangle(absPos, Size, new Color(100, 100, 100) * opacity, thickness: 1f, rounded: 4f);

        // Text
        if (font != null) {
            font.DrawText(batch, _wrappedText, absPos + new Vector2(Padding), Color.White * opacity);
        }
    }
}
