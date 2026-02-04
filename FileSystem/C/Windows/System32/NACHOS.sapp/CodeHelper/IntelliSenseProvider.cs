using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TheGame.Core;
using TheGame.Core.OS;

namespace NACHOS;

public static class IntelliSenseProvider {
private static readonly string[] _keywords = {
    "abstract", "add", "alias", "and", "as", "ascending", "async", "await",
    "base", "bool", "break", "byte", "case", "catch", "char", "checked",
    "class", "const", "continue", "decimal", "default", "delegate", "descending",
    "do", "double", "dynamic", "else", "enum", "equals", "event", "explicit",
    "extern", "false", "file", "finally", "fixed", "float", "for", "foreach",
    "from", "get", "global", "goto", "group", "if", "implicit", "in", "init",
    "int", "interface", "internal", "into", "is", "join", "let", "lock",
    "long", "namespace", "new", "not", "notnull", "null", "object", "on",
    "operator", "or", "orderby", "out", "override", "params", "partial",
    "private", "protected", "public", "readonly", "record", "ref", "remove",
    "required", "return", "sbyte", "scoped", "sealed", "select", "set", "short",
    "sizeof", "stackalloc", "static", "string", "struct", "switch", "this",
    "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unmanaged",
    "unsafe", "ushort", "using", "value", "var", "virtual", "void", "volatile",
    "when", "where", "while", "with", "yield"
};

    public static async Task<List<CompletionItem>> GetCompletionsAsync(Dictionary<string, string> sourceFiles, string currentFile, int cursorPosition) {
        return await Task.Run(() => {
            try {
                var syntaxTrees = sourceFiles.Select(kvp => 
                    CSharpSyntaxTree.ParseText(kvp.Value, path: kvp.Key)
                ).ToArray();

                var currentTree = syntaxTrees.FirstOrDefault(t => t.FilePath == currentFile);
                if (currentTree == null) return new List<CompletionItem>();

                // Simplified: We need a compilation to get SemanticModel
                // In a real IDE we would cache this.
                var compilation = CSharpCompilation.Create(
                    "IntelliSenseCompilation",
                    syntaxTrees: syntaxTrees,
                    references: AppCompiler.Instance.GetType().GetMethod("GetFullReferences", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                        .Invoke(AppCompiler.Instance, new object[] { null }) as IEnumerable<MetadataReference>
                );

                var semanticModel = compilation.GetSemanticModel(currentTree);
                var root = currentTree.GetRoot();
                
                // Detect member access context first to determine symbols container
                INamespaceOrTypeSymbol container = null;
                var nodeAtCursor = root.FindNode(new Microsoft.CodeAnalysis.Text.TextSpan(cursorPosition, 0), findInsideTrivia: true, getInnermostNodeForTie: true);
                var memberAccess = nodeAtCursor?.AncestorsAndSelf().OfType<MemberAccessExpressionSyntax>().FirstOrDefault();
                
                if (memberAccess == null && cursorPosition > 0) {
                    var nodeBefore = root.FindNode(new Microsoft.CodeAnalysis.Text.TextSpan(cursorPosition - 1, 0), findInsideTrivia: true, getInnermostNodeForTie: true);
                    memberAccess = nodeBefore?.AncestorsAndSelf().OfType<MemberAccessExpressionSyntax>().FirstOrDefault();
                }

                if (memberAccess != null && (cursorPosition > memberAccess.Expression.Span.End)) {
                    var typeInfo = semanticModel.GetTypeInfo(memberAccess.Expression);
                    container = typeInfo.Type;
                    
                    // If the expression is a type symbol itself, ensure we treat it as such
                    var symbolInfo = semanticModel.GetSymbolInfo(memberAccess.Expression);
                    if (symbolInfo.Symbol is INamedTypeSymbol nts) {
                        container = nts;
                    }
                }

                // Find visible symbols at cursor, potentially filtered by container
                var symbols = semanticModel.LookupSymbols(cursorPosition, container: container, includeReducedExtensionMethods: true);
                
                var format = new SymbolDisplayFormat(
                    typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
                    genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
                    memberOptions: SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeType,
                    parameterOptions: SymbolDisplayParameterOptions.IncludeName | SymbolDisplayParameterOptions.IncludeType,
                    miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
                );

                ITypeSymbol expectedType = null;
                var node = nodeAtCursor;
                
                // If we didn't find a node exactly at cursor (e.g. cursor is at end of "("), look back slightly
                if (node == null || (node is ArgumentListSyntax al && cursorPosition == al.Span.Start)) {
                    if (cursorPosition > 0) {
                        node = root.FindNode(new Microsoft.CodeAnalysis.Text.TextSpan(cursorPosition - 1, 0), findInsideTrivia: true, getInnermostNodeForTie: true);
                    }
                }

                var argList = node?.AncestorsAndSelf().OfType<ArgumentListSyntax>().FirstOrDefault();
                if (argList == null && node != null) {
                    // Try looking at tokens
                    var token = root.FindToken(cursorPosition);
                    // If we are at ")", look at the previous token
                    if (token.IsKind(SyntaxKind.CloseParenToken) && cursorPosition > 0) {
                        token = root.FindToken(cursorPosition - 1);
                    }

                    if (token.IsKind(SyntaxKind.OpenParenToken) || token.IsKind(SyntaxKind.CommaToken)) {
                        argList = token.Parent as ArgumentListSyntax;
                    } else if (cursorPosition > 0) {
                        var prevToken = root.FindToken(cursorPosition - 1);
                        if (prevToken.IsKind(SyntaxKind.OpenParenToken) || prevToken.IsKind(SyntaxKind.CommaToken)) {
                            argList = prevToken.Parent as ArgumentListSyntax;
                        }
                    }
                }

                if (argList != null) {
                    var invocation = argList.Parent;
                    var symbolInfo = semanticModel.GetSymbolInfo(invocation);
                    
                    // All possible overloads
                    var candidates = symbolInfo.CandidateSymbols.OfType<IMethodSymbol>().ToList();
                    if (symbolInfo.Symbol is IMethodSymbol primary) candidates.Add(primary);
                    
                    if (candidates.Count == 0 && invocation is ObjectCreationExpressionSyntax oce) {
                         var oceInfo = semanticModel.GetSymbolInfo(oce);
                         candidates = oceInfo.CandidateSymbols.OfType<IMethodSymbol>().ToList();
                         if (oceInfo.Symbol is IMethodSymbol ocePrimary) candidates.Add(ocePrimary);
                    }

                    if (candidates.Count > 0) {
                        int argIndex = 0;
                        var pos = cursorPosition;
                        
                        // Find which argument we are in
                        if (argList.Arguments.Count > 0) {
                            for (int i = 0; i < argList.Arguments.Count; i++) {
                                var arg = argList.Arguments[i];
                                if (pos > arg.Span.End) {
                                     argIndex = i + 1;
                                } else if (pos >= arg.Span.Start) {
                                     argIndex = i;
                                     break;
                                }
                            }
                        }

                        // Suppression: If we are beyond the max parameter count of ALL overloads, don't show anything.
                        int maxParams = candidates.Max(c => c.Parameters.Length);
                        bool canAcceptMore = candidates.Any(c => c.Parameters.Any(p => p.IsParams) || (argIndex < c.Parameters.Length));

                        if (argIndex >= maxParams && !candidates.Any(c => c.Parameters.Length > 0 && c.Parameters.Last().IsParams)) {
                             DebugLogger.Log($"IntelliSense: Suppressing due to argIndex {argIndex} >= maxParams {maxParams}");
                             return new List<CompletionItem>();
                        }

                        // Find best matching expected type (e.g. from any overload that could accept this index)
                        var matchingOverload = candidates.FirstOrDefault(c => argIndex < c.Parameters.Length);
                        if (matchingOverload != null) {
                            expectedType = matchingOverload.Parameters[argIndex].Type;
                        }
                    }
                }

                DebugLogger.Log($"IntelliSense: Found {symbols.Length} symbols at {cursorPosition}. ExpectedType: {expectedType?.Name ?? "null"}");

                var enclosingSymbol = semanticModel.GetEnclosingSymbol(cursorPosition);
                var enclosingType = enclosingSymbol?.ContainingType;

                var items = symbols.Select(symbol => {
                    string kind = "V"; // Default to variable/local
                    bool isLocalContext = symbol.Kind == SymbolKind.Local || symbol.Kind == SymbolKind.Parameter;
                    
                    if (symbol.Kind == SymbolKind.Method) kind = "M";
                    else if (symbol.Kind == SymbolKind.Property) kind = "P";
                    else if (symbol.Kind == SymbolKind.Field) kind = "F";
                    else if (symbol.Kind == SymbolKind.Event) kind = "E";
                    else if (symbol is INamedTypeSymbol nt) {
                        if (nt.TypeKind == TypeKind.Class) {
                            if (nt.SpecialType != SpecialType.None) kind = "T";
                            else kind = "C";
                        }
                        else if (nt.TypeKind == TypeKind.Struct) kind = "S";
                        else if (nt.TypeKind == TypeKind.Interface) kind = "I";
                        else if (nt.TypeKind == TypeKind.Enum) kind = "E";
                        else if (nt.TypeKind == TypeKind.Delegate) kind = "D";
                    }
                    else if (symbol.Kind == SymbolKind.Namespace) kind = "N";

                    // Scoring System (Tiered)
                    // 1. Scope (Locals > Class > Globals)
                    int score = 0;
                    if (isLocalContext) score += 1000;
                    else if (enclosingType != null && symbol.ContainingSymbol is ITypeSymbol containerType) {
                        if (SymbolEqualityComparer.Default.Equals(containerType, enclosingType)) {
                            score += 500; // Defined in current class
                        } else if (compilation.ClassifyConversion(enclosingType, containerType).IsImplicit) {
                            score += 400; // Inherited member
                        }
                    }
                    
                    // 2. Semantic Context (Expected Type)
                    if (expectedType != null) {
                        ITypeSymbol symbolType = null;
                        if (symbol is ILocalSymbol local) symbolType = local.Type;
                        else if (symbol is IFieldSymbol field) symbolType = field.Type;
                        else if (symbol is IPropertySymbol prop) symbolType = prop.Type;
                        else if (symbol is IParameterSymbol param) symbolType = param.Type;
                        else if (symbol is IMethodSymbol method) symbolType = method.ReturnType;

                        if (symbolType != null) {
                             if (SymbolEqualityComparer.Default.Equals(symbolType, expectedType)) {
                                 score += 2000;
                             } else if (compilation.ClassifyConversion(symbolType, expectedType).IsImplicit) {
                                 score += 1500; // Compatible type
                             }
                        }
                    }

                    // 3. Penalty (Base Object)
                    if (symbol.ContainingType?.SpecialType == SpecialType.System_Object) {
                        score -= 5000;
                    }

                    // Use ToMinimalDisplayString to keep it short and context-aware
                    string detail = symbol.ToMinimalDisplayString(semanticModel, cursorPosition, format);
                    bool isPreferred = score >= 500;

                    // Improve detail for variables/fields to show type
                    if (symbol is ILocalSymbol ls) detail = $"{ls.Type.ToMinimalDisplayString(semanticModel, cursorPosition)} {symbol.Name}";
                    else if (symbol is IFieldSymbol fs) detail = $"{fs.Type.ToMinimalDisplayString(semanticModel, cursorPosition)} {symbol.Name}";
                    else if (symbol is IPropertySymbol ps) detail = $"{ps.Type.ToMinimalDisplayString(semanticModel, cursorPosition)} {symbol.Name}";
                    else if (symbol is IParameterSymbol pas) detail = $"{pas.Type.ToMinimalDisplayString(semanticModel, cursorPosition)} {symbol.Name}";

                    return new CompletionItem(
                        symbol.Name,
                        detail,
                        kind,
                        score,
                        isPreferred
                    );
                }).ToList();

                // Add Keywords only if we are NOT in a member access context
                if (container == null) {
                    foreach (var kw in _keywords) {
                        items.Add(new CompletionItem(kw, "keyword", "K", 0, false));
                    }
                }

                var final = items.DistinctBy(c => c.Label)
                    .OrderByDescending(c => c.Score)
                    .ThenByDescending(c => c.IsPreferred)
                    .ThenBy(c => c.Label)
                    .ToList();
                
                DebugLogger.Log($"IntelliSense: {final.Count} items. Preferred: {final.Count(i => i.IsPreferred)}. Context: {expectedType?.ToDisplayString() ?? "None"}");
                return final;
            } catch (Exception ex) {
                DebugLogger.Log("IntelliSense Error: " + ex.Message);
                return new List<CompletionItem>();
            }
        });
    }
}
