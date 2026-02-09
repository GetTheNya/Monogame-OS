using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Xna.Framework;
using System.Linq;

namespace NACHOS;

public static class CSharpHighlighter {
    private static readonly Color[] BracketColors = {
        new Color(255, 215, 0),   // Gold
        new Color(218, 112, 214), // Orchid/Pink
        new Color(23, 159, 255)   // Bright Blue
    };

    public static List<TokenSegment> Highlight(string code, SemanticModel semanticModel = null) {
        var segments = new List<TokenSegment>();
        var tree = semanticModel?.SyntaxTree ?? CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();

        int bracketLevel = -1;

        // Pass 1: Tokens (Keywords, Brackets, Literals, Symbols)
        foreach (var token in root.DescendantTokens()) {
            Color? color = GetTokenColor(token, semanticModel, ref bracketLevel);
            if (color.HasValue) {
                segments.Add(new TokenSegment(token.Span.Start, token.Span.Length, color.Value));
            }
        }

        // Pass 2: Trivia (Comments)
        foreach (var trivia in root.DescendantTrivia()) {
            var kind = trivia.Kind();
            if (kind == SyntaxKind.SingleLineCommentTrivia || 
                kind == SyntaxKind.MultiLineCommentTrivia || 
                kind == SyntaxKind.SingleLineDocumentationCommentTrivia ||
                kind == SyntaxKind.MultiLineDocumentationCommentTrivia) {
                segments.Add(new TokenSegment(trivia.Span.Start, trivia.Span.Length, new Color(87, 166, 74))); // Brighter Green
            }
        }

        return segments.OrderBy(s => s.Start).ToList();
    }

    private static Color? GetTokenColor(SyntaxToken token, SemanticModel semanticModel, ref int bracketLevel) {
        var kind = token.Kind();

        // Bracket Pair Colorization
        if (kind == SyntaxKind.OpenParenToken || kind == SyntaxKind.OpenBraceToken || kind == SyntaxKind.OpenBracketToken) {
            bracketLevel++;
            return BracketColors[bracketLevel % BracketColors.Length];
        }
        if (kind == SyntaxKind.CloseParenToken || kind == SyntaxKind.CloseBraceToken || kind == SyntaxKind.CloseBracketToken) {
            if (bracketLevel < 0) return Color.White; // Unmatched
            var color = BracketColors[bracketLevel % BracketColors.Length];
            bracketLevel--;
            return color;
        }

        if (token.IsKeyword()) return new Color(86, 156, 214); // Blue keywords
        
        switch (kind) {
            case SyntaxKind.StringLiteralToken:
            case SyntaxKind.CharacterLiteralToken:
            case SyntaxKind.InterpolatedStringToken:
                return new Color(214, 157, 133); // Orange strings
            case SyntaxKind.NumericLiteralToken:
                return new Color(181, 206, 168); // Green numbers
        }

        if (semanticModel != null && token.IsKind(SyntaxKind.IdentifierToken)) {
            var symbol = semanticModel.GetSymbolInfo(token.Parent).Symbol ?? semanticModel.GetDeclaredSymbol(token.Parent);
            if (symbol != null) {
                return GetSymbolColor(symbol);
            }
        }

        // Fallback to basic heuristics if no semantic model or symbol found
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

    private static Color? GetSymbolColor(ISymbol symbol) {
        switch (symbol.Kind) {
            case SymbolKind.NamedType:
                return new Color(78, 201, 176); // Teal for Classes/Structs/Enums
            case SymbolKind.Method:
                return new Color(220, 220, 170); // Yellow for Methods
            case SymbolKind.Field:
            case SymbolKind.Property:
                return new Color(184, 215, 163); // Light Green for Fields/Properties
            case SymbolKind.Parameter:
            case SymbolKind.Local:
                return new Color(156, 220, 254); // Light Blue for Pos/Locals
            case SymbolKind.Namespace:
                return Color.Gray;
        }
        return null;
    }
}
