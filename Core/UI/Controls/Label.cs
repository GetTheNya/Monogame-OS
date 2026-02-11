using System;
using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FontStashSharp;
using TheGame.Core.Designer;
using TheGame.Graphics;

namespace TheGame.Core.UI.Controls;

public class Label : UIElement {
    public string Text { get; set; }
    public Color Color { get; set; } = Color.White;
    [DesignerIgnoreProperty] [DesignerIgnoreJsonSerialization]
    public Color TextColor { get => Color; set => Color = value; }
    public int FontSize { get; set; } = 20;
    public bool UseBoldFont { get; set; } = false;
    public bool WordWrap { get; set; } = false;
    public float MaxWidth { get; set; } = 0;
    private string[] _wrappedLines = Array.Empty<string>();

    [Obsolete("For Designer/Serialization use only")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public Label() : this(Vector2.Zero, "Label") { }

    public Label(Vector2 position, string text) : base(position, Vector2.Zero) {
        Text = text;
        ConsumesInput = false;
    }

    public override void Update(GameTime gameTime) {
        base.Update(gameTime);
        
        // Update size based on text measurement
        if (!string.IsNullOrEmpty(Text) && GameContent.FontSystem != null) {
            var font = GameContent.FontSystem.GetFont(FontSize);
            if (font != null) {
                if (WordWrap && MaxWidth > 0) {
                    _wrappedLines = WrapText(font, Text, MaxWidth);
                    float height = _wrappedLines.Length * font.LineHeight;
                    Size = new Vector2(MaxWidth, height);
                } else {
                    _wrappedLines = new[] { Text };
                    Size = font.MeasureString(Text);
                }
            }
        } else {
            _wrappedLines = Array.Empty<string>();
            Size = Vector2.Zero;
        }
    }

    private string[] WrapText(DynamicSpriteFont font, string text, float maxWidth) {
        if (string.IsNullOrEmpty(text)) return Array.Empty<string>();
        
        // Normalize line endings and split by explicit newlines
        string normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");
        var sourceLines = normalized.Split('\n');
        var resultLines = new List<string>();

        foreach (var sourceLine in sourceLines) {
            if (string.IsNullOrWhiteSpace(sourceLine)) {
                resultLines.Add("");
                continue;
            }

            var words = sourceLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var currentLine = new System.Text.StringBuilder();

            foreach (var word in words) {
                if (currentLine.Length > 0 && font.MeasureString(currentLine.ToString() + " " + word).X > maxWidth) {
                    resultLines.Add(currentLine.ToString());
                    currentLine.Clear();
                }

                if (currentLine.Length > 0) currentLine.Append(" ");
                currentLine.Append(word);
            }

            if (currentLine.Length > 0) resultLines.Add(currentLine.ToString());
            else resultLines.Add(""); // Handle lines with only whitespace
        }

        return resultLines.ToArray();
    }

    public override void Draw(SpriteBatch spriteBatch, ShapeBatch batch) {
        if (!IsVisible || _wrappedLines.Length == 0) return;

        var activeFontSystem = UseBoldFont ? GameContent.BoldFontSystem : GameContent.FontSystem;

        if (activeFontSystem != null) {
            var font = activeFontSystem.GetFont(FontSize);
    
            if (font != null) {
                for (int i = 0; i < _wrappedLines.Length; i++) {
                    font.DrawText(batch, _wrappedLines[i], AbsolutePosition + new Vector2(0, i * font.LineHeight), Color * AbsoluteOpacity);
                }
            }
        }
    }
}
