using Microsoft.Xna.Framework;

namespace NACHOS;

public record struct TokenSegment(int Start, int Length, Color Color);

public enum DiagnosticSeverity {
    Hidden,
    Info,
    Warning,
    Error
}

public record struct DiagnosticInfo(int Start, int Length, string Message, DiagnosticSeverity Severity);
