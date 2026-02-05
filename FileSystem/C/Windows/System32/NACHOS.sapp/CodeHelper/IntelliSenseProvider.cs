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
                
                // Determine completion context
                var context = ContextResolver.DetermineContext(currentTree, semanticModel, cursorPosition);
                
                // Suppress IntelliSense in comments and strings
                if (context == CompletionContext.Comment || context == CompletionContext.StringLiteral) {
                    return new List<CompletionItem>();
                }
                
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

                // Object initializer container detection
                if (container == null && context == CompletionContext.ObjectInitializer) {
                    var targetNode = nodeAtCursor ?? root.FindNode(new Microsoft.CodeAnalysis.Text.TextSpan(cursorPosition, 0));
                    var initializer = targetNode?.AncestorsAndSelf().OfType<InitializerExpressionSyntax>()
                        .FirstOrDefault(init => init.IsKind(SyntaxKind.ObjectInitializerExpression));
                    
                    if (initializer?.Parent is ObjectCreationExpressionSyntax oce) {
                        var typeInfo = semanticModel.GetTypeInfo(oce);
                        container = typeInfo.Type;
                    } else if (initializer?.Parent is ImplicitObjectCreationExpressionSyntax ioce) {
                        var typeInfo = semanticModel.GetTypeInfo(ioce);
                        container = typeInfo.Type;
                    }
                }

                // Find visible symbols at cursor, potentially filtered by container
                var symbolsArray = semanticModel.LookupSymbols(cursorPosition, container: container, includeReducedExtensionMethods: true);

                // Strict filtering based on context
                var filteredSymbols = symbolsArray.AsEnumerable();
                
                if (context == CompletionContext.TypeDeclaration) {
                    // Only show types and namespaces
                    filteredSymbols = filteredSymbols.Where(s => s is ITypeSymbol || s is INamespaceSymbol);
                } else if (context == CompletionContext.ObjectInitializer && container != null) {
                    // Only show properties/fields of the container
                    filteredSymbols = filteredSymbols.Where(s => (s.Kind == SymbolKind.Property || s.Kind == SymbolKind.Field) && 
                                                               s.ContainingType != null && 
                                                               SymbolEqualityComparer.Default.Equals(s.ContainingType, container));
                } else if (context == CompletionContext.Override) {
                    // Filter to virtual/abstract methods from base classes
                    filteredSymbols = filteredSymbols.Where(s => s.Kind == SymbolKind.Method && (s.IsVirtual || s.IsAbstract || s.IsOverride));
                }

                var symbols = filteredSymbols.ToArray();
                
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

                var items = new List<CompletionItem>();
                foreach (var symbol in symbols) {
                    string kind = "V"; // Default to variable/local
                    bool isLocalContext = symbol.Kind == SymbolKind.Local || symbol.Kind == SymbolKind.Parameter;
                    
                    // Scoring System (Tiered)
                    // 1. Scope (Locals > Class > Globals)
                    int score = 0;

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
                        score += 100; // Small base boost for types
                    }
                    else if (symbol.Kind == SymbolKind.Namespace) kind = "N";

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
                                 score += 5000; // HIGHEST PRIORITY: exact type match
                             } else if (compilation.ClassifyConversion(symbolType, expectedType).IsImplicit) {
                                 score += 3500; // Compatible type
                             }
                        }
                    }
                    
                    // 2b. Type Declaration Context - Boost type symbols
                    if (context == CompletionContext.TypeDeclaration && symbol is INamedTypeSymbol) {
                        score += 4000; // Prioritize types in type contexts
                    }

                    // 3. Penalty (Base Object)
                    if (symbol.ContainingType?.SpecialType == SpecialType.System_Object) {
                        score -= 5000;
                    }
                    
                    // 4. Recent Usage Tracking
                    int usageScore = UsageTracker.GetScore(symbol.Name);
                    score += usageScore;

                    // Use ToMinimalDisplayString to keep it short and context-aware
                    string detail = symbol.ToMinimalDisplayString(semanticModel, cursorPosition, format);
                    bool isPreferred = score >= 500;

                    // Improve detail for variables/fields to show type
                    if (symbol is ILocalSymbol ls) detail = $"{ls.Type.ToMinimalDisplayString(semanticModel, cursorPosition)} {symbol.Name}";
                    else if (symbol is IFieldSymbol fs) detail = $"{fs.Type.ToMinimalDisplayString(semanticModel, cursorPosition)} {symbol.Name}";
                    else if (symbol is IPropertySymbol ps) detail = $"{ps.Type.ToMinimalDisplayString(semanticModel, cursorPosition)} {symbol.Name}";
                    else if (symbol is IParameterSymbol pas) detail = $"{pas.Type.ToMinimalDisplayString(semanticModel, cursorPosition)} {symbol.Name}";

                    items.Add(new CompletionItem(
                        symbol.Name,
                        detail,
                        kind,
                        score,
                        isPreferred
                    ));
                }

                // Add Keywords and Snippets based on context
                // NEVER add in: MemberAccess, Argument, Attribute, UsingDirective, Override, ObjectInitializer, TypeDeclaration
                if (context != CompletionContext.MemberAccess && 
                    context != CompletionContext.Argument && 
                    context != CompletionContext.Attribute && 
                    context != CompletionContext.UsingDirective &&
                    context != CompletionContext.Override &&
                    context != CompletionContext.ObjectInitializer &&
                    context != CompletionContext.TypeDeclaration) {
                    
                    // Add keywords at StatementStart and Assignment
                    if (context == CompletionContext.StatementStart || 
                        context == CompletionContext.Assignment) {
                        foreach (var kw in _keywords) {
                            items.Add(new CompletionItem(kw, "keyword", "K", 0, false));
                        }
                    }

                    // Add Snippets ONLY at StatementStart and Assignment
                    var snippets = SnippetManager.GetSnippets();
                    
                    if (context == CompletionContext.StatementStart) {
                        // Add ALL snippets at statement start
                        foreach (var sn in snippets) {
                            int snippetScore = sn.Category == SnippetCategory.Statement ? 2000 : 500;
                            items.Add(new CompletionItem(sn.Shortcut, sn.Description, "SN", snippetScore, true));
                        }
                    } else if (context == CompletionContext.Assignment) {
                        // Add ONLY expression snippets at assignment
                        foreach (var sn in snippets.Where(s => s.Category == SnippetCategory.Expression)) {
                            items.Add(new CompletionItem(sn.Shortcut, sn.Description, "SN", 500, true));
                        }
                    }
                }

                // Sorting and deduplication (manually to avoid JIT issues with complex Linq)
                var deduplicated = new Dictionary<string, CompletionItem>();
                foreach (var item in items) {
                    if (!deduplicated.ContainsKey(item.Label) || item.Score > deduplicated[item.Label].Score) {
                        deduplicated[item.Label] = item;
                    }
                }

                var final = deduplicated.Values
                    .OrderByDescending((CompletionItem c) => c.Score)
                    .ThenByDescending((CompletionItem c) => c.IsPreferred)
                    .ThenBy((CompletionItem c) => c.Label)
                    .ToList();
                
                DebugLogger.Log($"IntelliSense: {final.Count} items. Preferred: {final.Count(i => i.IsPreferred)}. Context: {expectedType?.ToDisplayString() ?? "None"}");
                return final;
            } catch (Exception ex) {
                DebugLogger.Log("IntelliSense Error: " + ex.Message);
                return new List<CompletionItem>();
            }
        });
    }

    public static async Task<(string MethodName, List<(string Type, string Name)> Parameters, int ActiveIndex)?> GetSignatureHelpAsync(Dictionary<string, string> sourceFiles, string currentFile, int cursorPosition) {
        return await Task.Run<(string MethodName, List<(string Type, string Name)> Parameters, int ActiveIndex)?>(() => {
            try {
                var syntaxTrees = sourceFiles.Select(kvp => 
                    CSharpSyntaxTree.ParseText(kvp.Value, path: kvp.Key)
                ).ToArray();

                var currentTree = syntaxTrees.FirstOrDefault(t => t.FilePath == currentFile);
                if (currentTree == null) return null;

                var compilation = CSharpCompilation.Create(
                    "SignatureHelpCompilation",
                    syntaxTrees: syntaxTrees,
                    references: AppCompiler.Instance.GetType().GetMethod("GetFullReferences", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                        .Invoke(AppCompiler.Instance, new object[] { null }) as IEnumerable<MetadataReference>
                );

                var semanticModel = compilation.GetSemanticModel(currentTree);
                var root = currentTree.GetRoot();
                
                var nodeAtCursor = root.FindNode(new Microsoft.CodeAnalysis.Text.TextSpan(cursorPosition, 0), findInsideTrivia: true);

                // Find the innermost ArgumentList that the cursor is strictly inside
                var argList = nodeAtCursor
                    .AncestorsAndSelf()
                    .OfType<ArgumentListSyntax>()
                    .FirstOrDefault(al => {
                        bool afterOpen = cursorPosition >= al.OpenParenToken.Span.End;
                        bool beforeClose = al.CloseParenToken.IsMissing || cursorPosition <= al.CloseParenToken.Span.Start;
                        return afterOpen && beforeClose;
                    });

                // Final fallback: look back one char if we are exactly at the end of a token or trivia
                if (argList == null && cursorPosition > 0) {
                    argList = root.FindNode(new Microsoft.CodeAnalysis.Text.TextSpan(cursorPosition - 1, 0), findInsideTrivia: true)
                        .AncestorsAndSelf()
                        .OfType<ArgumentListSyntax>()
                        .FirstOrDefault(al => cursorPosition >= al.OpenParenToken.Span.End && (al.CloseParenToken.IsMissing || cursorPosition <= al.CloseParenToken.Span.Start));
                }

                if (argList != null) {
                    var invocation = argList.Parent;
                    var symbolInfo = semanticModel.GetSymbolInfo(invocation);
                    var method = symbolInfo.Symbol as IMethodSymbol;
                    
                    if (method == null && symbolInfo.CandidateSymbols.Length > 0) {
                        method = symbolInfo.CandidateSymbols[0] as IMethodSymbol;
                    }

                    if (method == null && invocation is ObjectCreationExpressionSyntax oce) {
                        var oceInfo = semanticModel.GetSymbolInfo(oce);
                        method = oceInfo.Symbol as IMethodSymbol ?? oceInfo.CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();
                    }

                    if (method != null) {
                        var parameters = method.Parameters.Select(p => (p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat), p.Name)).ToList();
                        
                        // Find active index
                        int activeIndex = 0;
                        if (argList.Arguments.Count > 0) {
                            for (int i = 0; i < argList.Arguments.Count; i++) {
                                if (cursorPosition > argList.Arguments[i].Span.End) activeIndex = i + 1;
                                else break;
                            }
                        }
                        
                        return (method.Name == ".ctor" ? method.ContainingType.Name : method.Name, parameters, activeIndex);
                    }
                }
                
                return null;
            } catch (Exception ex) {
                DebugLogger.Log("SignatureHelp Error: " + ex.Message);
                return null;
            }
        });
    }

    // Fuzzy Matching Helpers
    private static bool MatchesUppercaseAcronym(string query, string label) {
        // "MNGM" matches "MyNewGoodMethod"
        // Requirements:
        //   1. Query length >= 2 characters
        //   2. All characters in query must be uppercase
        //   3. Extract PascalCase/camelCase initials
        if (query.Length < 2 || !query.All(char.IsUpper)) return false;
        
        string initials = string.Concat(label.Where(char.IsUpper));
        return string.Equals(query, initials, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesMixedAcronym(string query, string label) {
        // "sb" matches "SpriteBatch", "spriteBatch"
        // Requirements:
        //   1. Query length >= 2 characters
        //   2. Extract uppercase letters from label
        if (query.Length < 2) return false;
        
        string initials = string.Concat(label.Where(char.IsUpper));
        return string.Equals(query, initials, StringComparison.OrdinalIgnoreCase);
    }

    private static int CalculateStringMatchScore(string query, string label) {
        if (string.IsNullOrEmpty(query)) return 0;
        
        int score = 0;
        
        // Uppercase acronym match: +200
        if (MatchesUppercaseAcronym(query, label)) {
            score += 200;
        }
        // Mixed-case acronym match: +50
        else if (MatchesMixedAcronym(query, label)) {
            score += 50;
        }
        
        // Exact prefix match (case-sensitive): +100
        if (label.StartsWith(query, StringComparison.Ordinal)) {
            score += 100;
        }
        // Prefix match (case-insensitive): +75
        else if (label.StartsWith(query, StringComparison.OrdinalIgnoreCase)) {
            score += 75;
        }
        
        // Case-sensitive match anywhere: +50
        if (label.Contains(query, StringComparison.Ordinal)) {
            score += 50;
        }
        
        // Contains match: +5
        if (label.Contains(query, StringComparison.OrdinalIgnoreCase)) {
            score += 5;
        }
        
        return score;
    }
}
