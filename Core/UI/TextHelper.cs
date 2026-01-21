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
    public static string WrapText(SpriteFontBase font, string text, float maxWidth) {
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
    public static Vector2 MeasureWrappedText(SpriteFontBase font, string text, float maxWidth) {
        if (string.IsNullOrEmpty(text)) return Vector2.Zero;
        if (font == null) return Vector2.Zero;

        string wrapped = WrapText(font, text, maxWidth);
        return font.MeasureString(wrapped);
    }

    /// <summary>
    /// Truncates text with an ellipsis if it exceeds the specified width, with a tolerance to prevent aggressive truncation.
    /// </summary>
    public static string TruncateWithEllipsis(SpriteFontBase font, string text, float maxWidth, float tolerance = 2f) {
        if (string.IsNullOrEmpty(text)) return "";
        if (font == null) return text;

        var size = font.MeasureString(text);
        if (size.X <= maxWidth + tolerance) return text;

        // Try to find the longest substring that fits with "..."
        string ellipsis = "...";
        float ellipsisWidth = font.MeasureString(ellipsis).X;
        
        // If even the ellipsis doesn't fit, return empty or as much of the ellipsis as possible
        if (ellipsisWidth > maxWidth) {
            return "";
        }

        // Binary search for efficiency on long strings, or just a backwards loop if we expect it to be close
        // Since we are usually close to the limit, a backwards loop is often faster than binary search overhead
        int low = 0;
        int high = text.Length;
        string result = "";

        while (low <= high) {
            int mid = (low + high) / 2;
            string sub = text.Substring(0, mid);
            float subWidth = font.MeasureString(sub).X;

            if (subWidth + ellipsisWidth <= maxWidth + tolerance) {
                result = sub + ellipsis;
                low = mid + 1;
            } else {
                high = mid - 1;
            }
        }

        return result;
    }
}
