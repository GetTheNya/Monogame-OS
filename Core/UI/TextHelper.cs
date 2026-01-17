using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using FontStashSharp;

namespace TheGame.Core.UI;

public static class TextHelper {
    /// <summary>
    /// Wraps text to fit within a specified width.
    /// </summary>
    public static string WrapText(DynamicSpriteFont font, string text, float maxWidth) {
        if (string.IsNullOrEmpty(text)) return "";
        if (font == null) return text;

        string[] words = text.Split(' ');
        StringBuilder sb = new StringBuilder();
        float spaceWidth = font.MeasureString(" ").X;
        float currentLineWidth = 0;

        for (int i = 0; i < words.Length; i++) {
            Vector2 size = font.MeasureString(words[i]);

            if (currentLineWidth + size.X > maxWidth) {
                if (currentLineWidth > 0) {
                    sb.Append("\n");
                    currentLineWidth = 0;
                }
                
                // If a single word is wider than maxWidth, it will still take a full line
                sb.Append(words[i]);
                currentLineWidth = size.X;
            } else {
                if (currentLineWidth > 0) {
                    sb.Append(" ");
                    currentLineWidth += spaceWidth;
                }
                sb.Append(words[i]);
                currentLineWidth += size.X;
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Measures the size of a string after it has been wrapped.
    /// </summary>
    public static Vector2 MeasureWrappedText(DynamicSpriteFont font, string text, float maxWidth) {
        if (string.IsNullOrEmpty(text)) return Vector2.Zero;
        if (font == null) return Vector2.Zero;

        string wrapped = WrapText(font, text, maxWidth);
        return font.MeasureString(wrapped);
    }
}
