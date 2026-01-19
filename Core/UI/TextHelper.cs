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

        string[] lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        StringBuilder sb = new StringBuilder();
        float spaceWidth = font.MeasureString(" ").X;

        for (int lineIdx = 0; lineIdx < lines.Length; lineIdx++) {
            if (lineIdx > 0) sb.Append("\n");

            string line = lines[lineIdx];
            if (string.IsNullOrEmpty(line)) continue;

            string[] words = line.Split(' ');
            float currentLineWidth = 0;

            for (int i = 0; i < words.Length; i++) {
                string word = words[i];
                if (string.IsNullOrEmpty(word)) {
                    // Handle multiple spaces
                    if (i < words.Length - 1) { // Only if not the last word
                        sb.Append(" ");
                        currentLineWidth += spaceWidth;
                    }
                    continue;
                }

                Vector2 size = font.MeasureString(word);

                if (size.X > maxWidth) {
                    // This word is too long, break it character by character
                    if (currentLineWidth > 0) {
                        sb.Append("\n");
                        currentLineWidth = 0;
                    }

                    for (int j = 0; j < word.Length; j++) {
                        char c = word[j];
                        float charWidth = font.MeasureString(c.ToString()).X;

                        if (currentLineWidth + charWidth > maxWidth) {
                            if (currentLineWidth > 0) {
                                sb.Append("\n");
                                currentLineWidth = 0;
                            }
                        }

                        sb.Append(c);
                        currentLineWidth += charWidth;
                    }
                } else if (currentLineWidth + size.X > maxWidth) {
                    if (currentLineWidth > 0) {
                        sb.Append("\n");
                        currentLineWidth = 0;
                    }
                    
                    sb.Append(word);
                    currentLineWidth = size.X;
                } else {
                    if (currentLineWidth > 0) {
                        sb.Append(" ");
                        currentLineWidth += spaceWidth;
                    }
                    sb.Append(word);
                    currentLineWidth += size.X;
                }
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
