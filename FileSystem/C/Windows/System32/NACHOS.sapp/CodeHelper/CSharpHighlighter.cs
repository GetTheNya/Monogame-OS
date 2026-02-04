using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Xna.Framework;
using System.Linq;

namespace NACHOS;

public static class CSharpHighlighter {
    public static List<TokenSegment> Highlight(string code) {
        var segments = new List<TokenSegment>();
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();

        foreach (var token in root.DescendantTokens(descendIntoTrivia: true)) {
            Color? color = GetTokenColor(token);
            if (color.HasValue) {
                segments.Add(new TokenSegment(token.Span.Start, token.Span.Length, color.Value));
            }
        }

        return segments;
    }

    private static Color? GetTokenColor(SyntaxToken token) {
        if (token.IsKeyword()) return new Color(86, 156, 214); // Blue keywords
        
        var kind = token.Kind();
        
        switch (kind) {
            case SyntaxKind.StringLiteralToken:
            case SyntaxKind.CharacterLiteralToken:
            case SyntaxKind.InterpolatedStringToken:
                return new Color(214, 157, 133); // Orange strings
            case SyntaxKind.NumericLiteralToken:
                return new Color(181, 206, 168); // Green numbers
            case SyntaxKind.SingleLineCommentTrivia:
            case SyntaxKind.MultiLineCommentTrivia:
            case SyntaxKind.SingleLineDocumentationCommentTrivia:
                return new Color(106, 153, 85); // Dark green comments
        }

        // We can't easily get SemanticModel here without a full Compilation.
        // For now, let's just do basics. 
        // We can detect some things by parent.
        var parent = token.Parent;
        if (parent != null) {
            if (parent is ClassDeclarationSyntax ||
                parent is InterfaceDeclarationSyntax ||
                parent is StructDeclarationSyntax ||
                parent is EnumDeclarationSyntax) {
                if (token.IsKind(SyntaxKind.IdentifierToken))
                    return new Color(78, 201, 176); // Teal for type definitions
            }
        }

        return null;
    }
}
