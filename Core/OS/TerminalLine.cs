using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;

namespace TheGame.Core.OS;

public struct TerminalSegment {
    public string Text;
    public Color Color;

    public TerminalSegment(string text, Color color) {
        Text = text;
        Color = color;
    }
}

/// <summary>
/// Represents a single line in the terminal buffer with metadata and potentially multiple colored segments.
/// </summary>
public struct TerminalLine {
    public List<TerminalSegment> Segments;
    public string Source; // e.g., "STDOUT", "STDERR", or Process name

    public string Text => Segments == null ? "" : string.Concat(Segments.Select(s => s.Text));

    public TerminalLine(string text, Color color, string source = null) {
        Segments = new List<TerminalSegment> { new TerminalSegment(text, color) };
        Source = source;
    }

    public TerminalLine(List<TerminalSegment> segments, string source = null) {
        Segments = segments;
        Source = source;
    }
}

public static class AnsiCodes {
    public const string Reset = "\x1b[0m";
    
    public static string GetColorCode(Color color) {
        // Direct matches first
        if (color == Color.Red) return "31";
        if (color == Color.Green) return "32";
        if (color == Color.Yellow) return "33";
        if (color == Color.Blue) return "34";
        if (color == Color.Magenta) return "35";
        if (color == Color.Cyan) return "36";
        if (color == Color.White) return "37";
        if (color == Color.Gray) return "90";
        if (color == Color.Black) return "30";
        
        // RGB based matching for resilience
        if (color.R > 200 && color.G > 200 && color.B < 100) return "33"; // Yellow
        if (color.R > 200 && color.G < 100 && color.B < 100) return "31"; // Red
        if (color.G > 200 && color.R < 100 && color.B < 100) return "32"; // Green
        if (color.B > 200 && color.R < 100 && color.G < 100) return "34"; // Blue
        if (color.R > 200 && color.B > 200 && color.G < 100) return "35"; // Magenta
        if (color.G > 200 && color.B > 200 && color.R < 100) return "36"; // Cyan
        
        return null;
    }
    
    public static string Wrap(string text, Color color) {
        string code = GetColorCode(color);
        if (code == null) return text;
        return $"\x1b[{code}m{text}{Reset}";
    }
}
