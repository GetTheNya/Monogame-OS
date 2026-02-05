using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NACHOS;

public enum CompletionContext {
    StatementStart,        // Beginning of line
    MemberAccess,          // After `object.`
    Argument,              // Inside method parentheses
    Assignment,            // After `int x =`
    ObjectInitializer,     // Inside `new Class { | }`
    TypeDeclaration,       // After `<`, `private`, `List<`
    Override,              // After `override` keyword
    Attribute,             // Inside `[...]`
    UsingDirective,        // After `using`
    StringLiteral,         // Inside string (suppress)
    Comment                // Inside comment (suppress)
}

public static class ContextResolver {
    public static CompletionContext DetermineContext(SyntaxTree tree, SemanticModel model, int position) {
        try {
            var root = tree.GetRoot();
            
            // Check for comments first
            if (IsInComment(tree, position)) {
                return CompletionContext.Comment;
            }
            
            // Check for string literals
            if (IsInStringLiteral(tree, position)) {
                return CompletionContext.StringLiteral;
            }
            
            var token = root.FindToken(position);
            var node = root.FindNode(new Microsoft.CodeAnalysis.Text.TextSpan(position, 0), findInsideTrivia: false, getInnermostNodeForTie: true);
            
            // Check for attribute context
            if (IsInAttributeList(node)) {
                return CompletionContext.Attribute;
            }
            
            // Check for using directive
            if (IsInUsingDirective(node)) {
                return CompletionContext.UsingDirective;
            }
            
            // Check for override keyword
            if (IsAfterOverrideKeyword(token, position)) {
                return CompletionContext.Override;
            }
            
            // Check for type declaration context (generics, modifiers)
            if (IsInTypeDeclaration(node, token, position)) {
                return CompletionContext.TypeDeclaration;
            }
            
            // Check for object initializer
            if (IsInObjectInitializer(node)) {
                return CompletionContext.ObjectInitializer;
            }
            
            // Check for member access (after dot)
            if (IsInMemberAccess(node, token, position)) {
                return CompletionContext.MemberAccess;
            }
            
            // Check for argument context
            if (IsInArgumentList(node, token, position)) {
                return CompletionContext.Argument;
            }
            
            // Check for assignment context
            if (IsInAssignment(node, token, position)) {
                return CompletionContext.Assignment;
            }
            
            // Check for statement start
            if (IsAtStatementStart(node, token, position)) {
                return CompletionContext.StatementStart;
            }
            
            // Default to statement start if we can't determine
            return CompletionContext.StatementStart;
        } catch {
            return CompletionContext.StatementStart;
        }
    }
    
    private static bool IsInComment(SyntaxTree tree, int position) {
        var trivia = tree.GetRoot().FindTrivia(position);
        return trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
               trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) ||
               trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
               trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia);
    }
    
    private static bool IsInStringLiteral(SyntaxTree tree, int position) {
        var token = tree.GetRoot().FindToken(position);
        return token.IsKind(SyntaxKind.StringLiteralToken) ||
               token.IsKind(SyntaxKind.InterpolatedStringTextToken);
    }
    
    private static bool IsInAttributeList(SyntaxNode node) {
        return node?.AncestorsAndSelf().OfType<AttributeListSyntax>().Any() ?? false;
    }
    
    private static bool IsInUsingDirective(SyntaxNode node) {
        return node?.AncestorsAndSelf().OfType<UsingDirectiveSyntax>().Any() ?? false;
    }
    
    private static bool IsAfterOverrideKeyword(SyntaxToken token, int position) {
        // Check if previous token is "override"
        if (token.IsKind(SyntaxKind.OverrideKeyword)) {
            return position >= token.Span.End;
        }
        
        // Look back to find override keyword
        var prevToken = token;
        for (int i = 0; i < 5; i++) {
            prevToken = prevToken.GetPreviousToken();
            if (prevToken.IsKind(SyntaxKind.None)) break;
            if (prevToken.IsKind(SyntaxKind.OverrideKeyword)) {
                return true;
            }
            // Stop if we hit something that would end the override declaration
            if (prevToken.IsKind(SyntaxKind.SemicolonToken) ||
                prevToken.IsKind(SyntaxKind.OpenBraceToken) ||
                prevToken.IsKind(SyntaxKind.CloseBraceToken)) {
                break;
            }
        }
        return false;
    }
    
    private static bool IsInTypeDeclaration(SyntaxNode node, SyntaxToken token, int position) {
        // After < in generic type: List<|>
        if (token.IsKind(SyntaxKind.LessThanToken)) {
            var parent = token.Parent;
            if (parent is TypeArgumentListSyntax || parent is TypeParameterListSyntax) {
                return true;
            }
        }
        
        // Between < and > in generics
        if (node?.AncestorsAndSelf().OfType<TypeArgumentListSyntax>().Any() ?? false) {
            return true;
        }
        
        // After access modifiers: private |, public |, protected |
        if (token.IsKind(SyntaxKind.PrivateKeyword) ||
            token.IsKind(SyntaxKind.PublicKeyword) ||
            token.IsKind(SyntaxKind.ProtectedKeyword) ||
            token.IsKind(SyntaxKind.InternalKeyword) ||
            token.IsKind(SyntaxKind.StaticKeyword) ||
            token.IsKind(SyntaxKind.ReadOnlyKeyword)) {
            return position >= token.Span.End;
        }
        
        return false;
    }
    
    private static bool IsInObjectInitializer(SyntaxNode node) {
        var initializer = node?.AncestorsAndSelf().OfType<InitializerExpressionSyntax>()
            .FirstOrDefault(init => init.IsKind(SyntaxKind.ObjectInitializerExpression));
        
        if (initializer != null) {
            // Make sure we're inside the braces
            return true;
        }
        return false;
    }
    
    private static bool IsInMemberAccess(SyntaxNode node, SyntaxToken token, int position) {
        // After a dot: object.|
        if (token.IsKind(SyntaxKind.DotToken)) {
            return position >= token.Span.End;
        }
        
        // Inside a member access expression
        var memberAccess = node?.AncestorsAndSelf().OfType<MemberAccessExpressionSyntax>().FirstOrDefault();
        if (memberAccess != null) {
            // Make sure we're after the dot
            return position > memberAccess.OperatorToken.Span.End;
        }
        
        return false;
    }
    
    private static bool IsInArgumentList(SyntaxNode node, SyntaxToken token, int position) {
        // Inside method arguments: Method(|)
        var argList = node?.AncestorsAndSelf().OfType<ArgumentListSyntax>().FirstOrDefault();
        if (argList != null) {
            // Make sure we're between the parentheses
            return position > argList.OpenParenToken.Span.End &&
                   (argList.CloseParenToken.IsMissing || position <= argList.CloseParenToken.Span.Start);
        }
        
        // After ( or , in argument list
        if (token.IsKind(SyntaxKind.OpenParenToken) || token.IsKind(SyntaxKind.CommaToken)) {
            var parent = token.Parent as ArgumentListSyntax;
            if (parent != null) {
                return true;
            }
        }
        
        return false;
    }
    
    private static bool IsInAssignment(SyntaxNode node, SyntaxToken token, int position) {
        // After = in assignment: int x = |
        if (token.IsKind(SyntaxKind.EqualsToken)) {
            var parent = token.Parent;
            if (parent is VariableDeclaratorSyntax ||
                parent is AssignmentExpressionSyntax ||
                parent is EqualsValueClauseSyntax) {
                return position >= token.Span.End;
            }
        }
        
        // Inside the right side of an assignment
        var assignment = node?.AncestorsAndSelf().OfType<AssignmentExpressionSyntax>().FirstOrDefault();
        if (assignment != null && position > assignment.OperatorToken.Span.End) {
            return true;
        }
        
        var variableDecl = node?.AncestorsAndSelf().OfType<VariableDeclaratorSyntax>().FirstOrDefault();
        if (variableDecl?.Initializer != null) {
            return position > variableDecl.Initializer.EqualsToken.Span.End;
        }
        
        return false;
    }
    
    private static bool IsAtStatementStart(SyntaxNode node, SyntaxToken token, int position) {
        // At the beginning of a line or after {
        if (token.IsKind(SyntaxKind.OpenBraceToken) ||
            token.IsKind(SyntaxKind.SemicolonToken)) {
            return true;
        }
        
        // Inside a block but not in any specific expression
        var block = node?.AncestorsAndSelf().OfType<BlockSyntax>().FirstOrDefault();
        if (block != null) {
            // Not in any of the other contexts
            return true;
        }
        
        return false;
    }
}
