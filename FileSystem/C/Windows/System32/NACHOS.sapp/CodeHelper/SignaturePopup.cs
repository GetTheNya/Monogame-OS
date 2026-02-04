using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using TheGame;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;
using TheGame.Graphics;
using TheGame.Core.OS;

namespace NACHOS;

public class SignaturePopup : UIElement {
    private string _methodName;
    private List<(string Type, string Name)> _parameters;
    private int _activeIndex = -1;
    private Color _backgroundColor = new Color(37, 37, 38);
    private Color _borderColor = new Color(63, 63, 70);
    private Color _textColor = new Color(220, 220, 220);
    private Color _highlightColor = new Color(0, 122, 204);

    public int ActiveIndex {
        get => _activeIndex;
        set => _activeIndex = value;
    }

    public SignaturePopup(Vector2 position, string methodName, List<(string Type, string Name)> parameters) 
        : base(position, Vector2.Zero) {
        _methodName = methodName;
        _parameters = parameters;
        ConsumesInput = false; // It's just an info popup
        UpdateSize();
    }

    private void UpdateSize() {
        if (GameContent.FontSystem == null) return;
        var font = GameContent.FontSystem.GetFont(14);
        string fullText = GetFullText();
        var size = font.MeasureString(fullText);
        Size = new Vector2(size.X + 20, size.Y + 10);
    }

    private string GetFullText() {
        string text = _methodName + "(";
        for (int i = 0; i < _parameters.Count; i++) {
            text += _parameters[i].Type + " " + _parameters[i].Name;
            if (i < _parameters.Count - 1) text += ", ";
        }
        text += ")";
        return text;
    }

    public void SetSignature(string methodName, List<(string Type, string Name)> parameters) {
        if (_methodName == methodName && _parameters.Count == parameters.Count) {
             bool match = true;
             for (int i = 0; i < parameters.Count; i++) {
                 if (_parameters[i].Type != parameters[i].Type || _parameters[i].Name != parameters[i].Name) {
                     match = false;
                     break;
                 }
             }
             if (match) return;
        }

        _methodName = methodName;
        _parameters = parameters;
        UpdateSize();
    }

    public void UpdateParameters(int activeIndex) {
        _activeIndex = activeIndex;
    }

    protected override void DrawSelf(SpriteBatch spriteBatch, ShapeBatch batch) {
        var absPos = AbsolutePosition;
        batch.FillRectangle(absPos, Size, _backgroundColor * AbsoluteOpacity);
        batch.BorderRectangle(absPos, Size, _borderColor * AbsoluteOpacity, 1f);

        if (GameContent.FontSystem == null) return;
        var font = GameContent.FontSystem.GetFont(14);
        
        float x = absPos.X + 10;
        float y = absPos.Y + 5;

        // Draw method name
        font.DrawText(batch, _methodName + "(", new Vector2(x, y), _textColor * AbsoluteOpacity);
        x += font.MeasureString(_methodName + "(").X;

        for (int i = 0; i < _parameters.Count; i++) {
            string paramText = _parameters[i].Type + " " + _parameters[i].Name;
            bool isActive = (i == _activeIndex);
            
            Color color = isActive ? _highlightColor : _textColor;
            font.DrawText(batch, paramText, new Vector2(x, y), color * AbsoluteOpacity);
            
            x += font.MeasureString(paramText).X;

            if (i < _parameters.Count - 1) {
                font.DrawText(batch, ", ", new Vector2(x, y), _textColor * AbsoluteOpacity);
                x += font.MeasureString(", ").X;
            }
        }

        font.DrawText(batch, ")", new Vector2(x, y), _textColor * AbsoluteOpacity);
    }
}
