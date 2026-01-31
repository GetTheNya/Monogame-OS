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
