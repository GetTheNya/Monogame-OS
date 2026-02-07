using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.CodeAnalysis;
using TheGame;
using TheGame.Core;
using TheGame.Core.OS;
using TheGame.Core.Input;

namespace NACHOS;

public class CodeIntelligenceManager {
    private readonly CodeEditor _editor;
    private CancellationTokenSource _completionCts;
    private CancellationTokenSource _signatureCts;
    private CancellationTokenSource _analysisCts;
    
    private CompletionPopup _activePopup;
    private SignaturePopup _activeSignaturePopup;
    private readonly Queue<Action> _pendingUiActions = new();
    private Dictionary<string, System.Timers.Timer> _highlightTimers = new();

    // Workspace Context Providers
    private Func<Dictionary<string, string>> _getSources;
    private Func<IEnumerable<string>> _getReferences;

    public CodeIntelligenceManager(CodeEditor editor) {
        _editor = editor;
        _editor.OnValueChanged = OnValueChanged;
        _editor.OnCursorMoved = OnCursorMoved;
    }

    public void SetWorkspaceContext(Func<Dictionary<string, string>> getSources, Func<IEnumerable<string>> getReferences) {
        _getSources = getSources;
        _getReferences = getReferences;
        TriggerAnalysis();
    }

    public void Update() {
        while (_pendingUiActions.Count > 0) {
            _pendingUiActions.Dequeue().Invoke();
        }
    }

    private void OnValueChanged(string value) {
        TriggerAnalysis();
        
        // Trigger IntelliSense logic
        int cursorIdx = _editor.GetIndexFromPosition(_editor.CursorLine, _editor.CursorCol);
        string text = _editor.Value;

        if (cursorIdx >= 0 && cursorIdx <= text.Length) {
            char lastChar = cursorIdx > 0 ? text[cursorIdx - 1] : '\0';
            
            // Get current word
            int start = _editor.CursorCol;
            var lines = _editor.Lines;
            if (lines == null || _editor.CursorLine < 0 || _editor.CursorLine >= lines.Count) return;
            string currentLine = lines[_editor.CursorLine];
            while (start > 0 && start <= currentLine.Length && (char.IsLetterOrDigit(currentLine[start - 1]) || currentLine[start - 1] == '_')) start--;
            string word = currentLine.Substring(start, Math.Min(_editor.CursorCol, currentLine.Length) - start);

            // Check if we're after a keyword that needs IntelliSense
            bool afterKeyword = false;
            if (lastChar == ' ' && start > 0) {
                int kwStart = start - 1;
                while (kwStart > 0 && currentLine[kwStart - 1] == ' ') kwStart--;
                int kwEnd = kwStart;
                while (kwStart > 0 && (char.IsLetterOrDigit(currentLine[kwStart - 1]) || currentLine[kwStart - 1] == '_')) kwStart--;
                string previousWord = currentLine.Substring(kwStart, kwEnd - kwStart);
                if (previousWord == "override" || previousWord == "new" || previousWord == "partial") afterKeyword = true;
            }

            if (lastChar == '.' || lastChar == '(' || lastChar == ',' || lastChar == '<' || lastChar == '{' || afterKeyword) {
                RequestCompletions(true);
            } else if (word.Length > 0) {
                if (_activePopup == null && word.Length >= 1 && !_editor.IsFetchingCompletions) {
                    RequestCompletions(false);
                } else if (_activePopup != null) {
                    _activePopup.SearchQuery = word;
                    if (_activePopup != null) {
                        var caretPos = _editor.GetCaretPosition();
                        if (caretPos != null) _activePopup.Position = caretPos.Value + new Vector2(0, 18);
                    }
                }
            } else {
                if (_activePopup != null && lastChar != ' ') CloseCompletions();
            }
        }
    }

    private void OnCursorMoved() {
        // Trigger signature help or other context-aware features
        ShowSignatureHelp();
    }

    public void RequestCompletions(bool initialSearch = true) {
        _completionCts?.Cancel();
        _completionCts = new CancellationTokenSource();
        var token = _completionCts.Token;

        var sources = _getSources?.Invoke() ?? new Dictionary<string, string> { { _editor.FilePath ?? "Untitled.cs", _editor.Value } };
        var refs = _getReferences?.Invoke();
        int pos = _editor.GetIndexFromPosition(_editor.CursorLine, _editor.CursorCol);

        _editor.IsFetchingCompletions = true;

        Task.Run(async () => {
            try {
                await Task.Delay(20, token);
                if (token.IsCancellationRequested) return;

                var items = await IntelliSenseProvider.GetCompletionsAsync(sources, _editor.FilePath, pos, refs);
                if (token.IsCancellationRequested) return;

                if (items.Count > 0) {
                    _pendingUiActions.Enqueue(() => {
                        if (token.IsCancellationRequested) return;

                        var caretPos = _editor.GetCaretPosition();
                        if (caretPos != null) {
                            Vector2 popupPos = caretPos.Value + new Vector2(0, 18);
                            
                            // Get current word for initial filtering
                            int start = _editor.CursorCol;
                            string currentLine = _editor.Lines[_editor.CursorLine];
                            while (start > 0 && start <= currentLine.Length && (char.IsLetterOrDigit(currentLine[start - 1]) || currentLine[start - 1] == '_')) start--;
                            string word = currentLine.Substring(start, Math.Min(_editor.CursorCol, currentLine.Length) - start);

                            var popup = new CompletionPopup(popupPos, items, (item) => {
                                _editor.History?.BeginTransaction("Completion: " + item.Label);
                                try {
                                    if (item.Kind == "M") {
                                        InsertMethodCompletion(item);
                                    } else if (item.Kind == "SN") {
                                        var snippet = SnippetManager.GetSnippets().FirstOrDefault(s => s.Shortcut == item.Label);
                                        if (snippet != null) _editor.InsertSnippet(snippet);
                                    } else {
                                        InsertGenericCompletion(item);
                                    }
                                    TriggerAnalysis();
                                    CloseCompletions();
                                } finally {
                                    _editor.History?.EndTransaction();
                                }
                            }, initialSearch ? "" : word);

                            if (popup.VisibleItemsCount == 0) return;

                            popup.OnClosed = () => { if (_activePopup == popup) _activePopup = null; };
                            CloseCompletions();
                            _activePopup = popup;
                            Shell.AddOverlayElement(popup);
                        }
                    });
                } else {
                    _pendingUiActions.Enqueue(CloseCompletions);
                }
            } catch (OperationCanceledException) { 
            } catch (Exception ex) {
                DebugLogger.Log("IntelliSense Error: " + ex.Message);
            } finally {
                if (!token.IsCancellationRequested) {
                    _pendingUiActions.Enqueue(() => {
                        if (token.IsCancellationRequested) return;
                        if (_editor != null) _editor.IsFetchingCompletions = false;
                    });
                }
            }
        }, token);
    }

    private void InsertMethodCompletion(CompletionItem item) {
        string line = _editor.Lines[_editor.CursorLine];
        int col = _editor.CursorCol;
        int parens = 0;
        for (int i = 0; i < col; i++) {
            if (line[i] == '(') parens++;
            else if (line[i] == ')') parens--;
        }
        bool inExpression = parens > 0;
        bool hasParenAfter = false;
        bool hasSemicolonAfter = false;
        for (int i = col; i < line.Length; i++) {
            if (char.IsWhiteSpace(line[i])) continue;
            if (line[i] == '(') hasParenAfter = true;
            if (line[i] == ';') hasSemicolonAfter = true;
            break;
        }

        string suffix = "";
        int back = 0;
        if (!hasParenAfter) { suffix += "()"; back = 1; }
        if (!inExpression && !hasSemicolonAfter) { suffix += ";"; if (back > 0) back++; }

        _editor.ReplaceCurrentWord(item.Label + suffix);
        if (back > 0) _editor.MoveCursor(-back, 0, false);
    }

    private void InsertGenericCompletion(CompletionItem item) {
        bool isGeneric = (item.Kind == "C" || item.Kind == "S" || item.Kind == "I" || item.Kind == "D") && 
                         (item.Detail.Contains("<") || item.Detail.Contains(">"));
        
        if (isGeneric) {
            _editor.ReplaceCurrentWord(item.Label + "<>");
            _editor.MoveCursor(-1, 0, false);
        } else {
            _editor.ReplaceCurrentWord(item.Label); 
        }
    }

    public void CloseCompletions() {
        if (_activePopup != null) {
            Shell.RemoveOverlayElement(_activePopup);
            _activePopup = null;
        }
    }

    public void ShowSignatureHelp() {
        _signatureCts?.Cancel();
        _signatureCts = new CancellationTokenSource();
        var token = _signatureCts.Token;

        var sources = _getSources?.Invoke() ?? new Dictionary<string, string> { { _editor.FilePath ?? "Untitled.cs", _editor.Value } };
        var refs = _getReferences?.Invoke();
        int pos = _editor.GetIndexFromPosition(_editor.CursorLine, _editor.CursorCol);

        Task.Run(async () => {
            try {
                var sig = await IntelliSenseProvider.GetSignatureHelpAsync(sources, _editor.FilePath, pos, refs);
                if (token.IsCancellationRequested) return;

                _pendingUiActions.Enqueue(() => {
                    if (token.IsCancellationRequested) return;

                    if (sig == null) {
                        CloseSignatureHelp();
                        return;
                    }

                    var caretPos = _editor.GetCaretPosition();
                    if (caretPos != null) {
                        Vector2 popupPos = caretPos.Value - new Vector2(0, 35);
                        if (_activeSignaturePopup == null) {
                            _activeSignaturePopup = new SignaturePopup(popupPos, sig.Value.MethodName, sig.Value.Parameters);
                            _activeSignaturePopup.ActiveIndex = sig.Value.ActiveIndex;
                            Shell.AddOverlayElement(_activeSignaturePopup);
                        } else {
                            _activeSignaturePopup.Position = popupPos;
                            _activeSignaturePopup.SetSignature(sig.Value.MethodName, sig.Value.Parameters);
                            _activeSignaturePopup.UpdateParameters(sig.Value.ActiveIndex);
                        }
                    }
                });
            } catch (Exception ex) {
                DebugLogger.Log("SignatureHelp Error: " + ex.Message);
            }
        }, token);
    }

    public void CloseSignatureHelp() {
        if (_activeSignaturePopup != null) {
            Shell.RemoveOverlayElement(_activeSignaturePopup);
            _activeSignaturePopup = null;
        }
    }

    public void TriggerAnalysis() {
        string path = _editor.FilePath ?? "Untitled.cs";
        bool isCSharp = path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);

        if (!_highlightTimers.TryGetValue(path, out var timer)) {
            timer = new System.Timers.Timer(500) { AutoReset = false };
            timer.Elapsed += (s, e) => {
                _pendingUiActions.Enqueue(() => {
                    string text = _editor.Value;
                    Task.Run(() => {
                        var tokens = isCSharp ? CSharpHighlighter.Highlight(text) : new List<TokenSegment>();
                        _pendingUiActions.Enqueue(() => {
                            if (_editor.Value == text) _editor.Tokens = tokens;
                        });
                    });
                });
            };
            _highlightTimers[path] = timer;
        }
        timer.Stop();
        timer.Start();

        if (!isCSharp) {
            _editor.Diagnostics = new List<NACHOS.DiagnosticInfo>();
            return;
        }

        _analysisCts?.Cancel();
        _analysisCts = new CancellationTokenSource();
        var token = _analysisCts.Token;

        var sources = _getSources?.Invoke() ?? new Dictionary<string, string> { { path, _editor.Value } };
        var refs = _getReferences?.Invoke();

        Task.Run(async () => {
            try {
                await Task.Delay(1000, token);
                if (token.IsCancellationRequested) return;

                var compilation = AppCompiler.Instance.Validate(sources, "NACHOS_ANALYSIS", out var diagnostics, refs);
                
                _pendingUiActions.Enqueue(() => {
                    if (token.IsCancellationRequested) return;
                    
                    var tree = compilation.SyntaxTrees.FirstOrDefault(t => t.FilePath == _editor.FilePath);
                    if (tree != null) {
                        var semanticModel = compilation.GetSemanticModel(tree);
                        string content = _editor.Value;
                        Task.Run(() => {
                            var semanticTokens = CSharpHighlighter.Highlight(content, semanticModel);
                            _pendingUiActions.Enqueue(() => {
                                if (_editor.Value == content) _editor.Tokens = semanticTokens;
                            });
                        });
                    }

                    var fileDiags = diagnostics.Where(d => d.Location.SourceTree?.FilePath == _editor.FilePath);
                    _editor.Diagnostics = fileDiags.Select(d => {
                        var span = d.Location.SourceSpan;
                        return new NACHOS.DiagnosticInfo(span.Start, span.Length, d.GetMessage(), (NACHOS.DiagnosticSeverity)d.Severity);
                    }).ToList();
                });
            } catch (TaskCanceledException) { 
            } catch (Exception ex) {
                DebugLogger.Log("Analysis Error: " + ex.Message);
            }
        }, token);
    }

    public void Shutdown() {
        CloseCompletions();
        CloseSignatureHelp();
        _completionCts?.Cancel();
        _signatureCts?.Cancel();
        _analysisCts?.Cancel();
    }
}
