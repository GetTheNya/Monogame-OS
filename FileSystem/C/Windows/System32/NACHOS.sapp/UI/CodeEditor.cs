using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TheGame.Core.OS;
using TheGame.Core.UI.Controls;
using TheGame.Core.Input;
using TheGame;
using TheGame.Graphics;

namespace NACHOS;

public class CodeEditor : TextArea {
    private Gutter _gutter;
    private bool _isDirty = false;
    private string _filePath;

    private Stack<SnippetSession> _snippetSessions = new();
    public SnippetSession ActiveSnippetSession {
        get => _snippetSessions.Count > 0 ? _snippetSessions.Peek() : null;
        set {
            if (value == null) {
                if (_snippetSessions.Count > 0) _snippetSessions.Pop();
            } else {
                _snippetSessions.Push(value);
            }
        }
    }

    public bool IsDirty => _isDirty;
    public string FilePath => _filePath;
    public string FileName => string.IsNullOrEmpty(_filePath) ? "Untitled" : Path.GetFileName(_filePath);
    public bool IsFetchingCompletions { get; set; } = false;

    public List<TokenSegment> Tokens { get; set; } = new();
    public List<DiagnosticInfo> Diagnostics { get; set; } = new();

    // Expose necessary protected members from TextArea via public properties if needed, 
    // or just use them directly if inheriting in the same assembly. 
    // Since this is a .sapp app compiled at runtime, it might not have access to protected members 
    // if it's considered a different assembly. 
    // WAIT, sapp apps ARE compiled into separate assemblies. 
    // I need to check if TextArea's fields are internal or protected.
    // I saw they are protected. Protected members are accessible to derived classes even in different assemblies.

    public IReadOnlyList<VisualLine> VisualLines => _visualLines;
    public float ScrollOffset => _scrollOffset;
    public int CursorLine => _cursorLine;
    public int CursorCol => _cursorCol;
    public IReadOnlyList<string> Lines => _lines;

    public Action OnDirtyChanged;

    public CodeEditor(Vector2 position, Vector2 size, string filePath = null) : base(position, size) {
        _filePath = filePath;
        _gutter = new Gutter(this);
        AddChild(_gutter);
        
        BackgroundColor = new Color(30, 30, 30);
        TextColor = new Color(220, 220, 220);
        FontSize = 14;
        
        UpdateLayout();
        
        if (!string.IsNullOrEmpty(_filePath) && VirtualFileSystem.Instance.Exists(_filePath)) {
            Value = VirtualFileSystem.Instance.ReadAllText(_filePath);
            _isDirty = false;
        }

        _charEnteredHandler = (c) => {
            if (!IsFocused) return;
            
            // 1. Wrap Selection
            if (HasSelection()) {
                char closeWrap = '\0';
                if (c == '(') closeWrap = ')';
                else if (c == '{') closeWrap = '}';
                else if (c == '[') closeWrap = ']';
                else if (c == '"') closeWrap = '"';
                else if (c == '\'') closeWrap = '\'';

                if (closeWrap != '\0') {
                    WrapSelectionBy(c, closeWrap);
                    return;
                }
            }

            // 2. Overtyping logic
            char charAtCursor = _cursorCol < _lines[_cursorLine].Length ? _lines[_cursorLine][_cursorCol] : '\0';
            if (charAtCursor != '\0' && charAtCursor == c) {
                if (c == ')' || c == '}' || c == ']' || c == '"' || c == '\'' || c == ';') {
                    // Just move cursor instead of inserting
                    _cursorCol++;
                    OnCursorMoved?.Invoke();
                    return;
                }
            }

            // Standard character insertion occurs in base.Update (via InputManager.GetTypedChars)
            // But we already have a closure in _charEnteredHandler. 
            // The shell's CodeEditor uses InputManager.OnCharEntered. 
            // Let's add the auto-closer here:
            
            char autoClose = '\0';
            if (c == '(') autoClose = ')';
            else if (c == '{') autoClose = '}';
            else if (c == '[') autoClose = ']';
            else if (c == '"') autoClose = '"';
            else if (c == '\'') autoClose = '\'';

            if (autoClose != '\0') {
                // Peek at current line. Insertion has already happened in TextArea (which reacts to CharEntered)
                // Wait, TextArea also listens to CharEntered? 
                // No, TextArea's base.Update iterates GetTypedChars().
                // CodeEditor is subscribing to OnCharEntered which fires immediately.
                // This means OnCharEntered might fire BEFORE base.Update inserts it.
                // Let's ensure consistency.
            }
        };
        InputManager.OnCharEntered += _charEnteredHandler;

        OnResize += UpdateLayout;

        OnValueChanged += (val) => {
            if (!_isDirty) {
                _isDirty = true;
                OnDirtyChanged?.Invoke();
            }
        };
    }

    private Action<char> _charEnteredHandler;
    public void Dispose() {
        InputManager.OnCharEntered -= _charEnteredHandler;
    }

    public void WrapSelectionBy(char opening, char closing) {
        GetSelectionRange(out int sl, out int sc, out int el, out int ec);
        
        // Single line wrap is easiest
        if (sl == el) {
            string line = _lines[sl];
            _lines[sl] = line.Insert(ec, closing.ToString()).Insert(sc, opening.ToString());
            _cursorCol = ec + 2; 
            _selStartCol = sc; // Keep selection or update it? User usually wants it wrapped.
        } else {
            // Multi-line wrap
            _lines[sl] = _lines[sl].Insert(sc, opening.ToString());
            _lines[el] = _lines[el].Insert(ec + (sl == el ? 1 : 0), closing.ToString());
        }
        NotifyUserChanged();
    }

    public override Vector2 TextOffset => new Vector2(_gutter.Width + 5, 10);

    private void UpdateLayout() {
        _gutter.UpdateWidth(_lines.Count);
        _gutter.Position = Vector2.Zero;
        _gutter.Size = new Vector2(_gutter.Width, Size.Y);
    }

    protected override void OnEnterPressed() {
        if (HasSelection()) DeleteSelection();
        
        // Auto-indent: copy leading whitespace from current line
        string currentLine = _lines[_cursorLine];
        string indent = "";
        for (int i = 0; i < currentLine.Length; i++) {
            if (char.IsWhiteSpace(currentLine[i])) indent += currentLine[i];
            else break;
        }

        bool needsExtraIndent = currentLine.TrimEnd().EndsWith("{");

        string remainder = _lines[_cursorLine].Substring(_cursorCol);
        _lines[_cursorLine] = _lines[_cursorLine].Substring(0, _cursorCol);
        
        string newIndent = indent + (needsExtraIndent ? "    " : "");
        _lines.Insert(_cursorLine + 1, newIndent + remainder);
        _cursorLine++;
        _cursorCol = newIndent.Length;
        ResetSelection();
        NotifyUserChanged();
    }

    public void SwapLineUp() {
        if (HasSelection()) {
            GetSelectionRange(out int sl, out int sc, out int el, out int ec);
            if (sl > 0) {
                string lineBefore = _lines[sl - 1];
                _lines.RemoveAt(sl - 1);
                _lines.Insert(el, lineBefore);
                _cursorLine--;
                _selStartLine--;
                NotifyUserChanged();
            }
        } else if (_cursorLine > 0) {
            string current = _lines[_cursorLine];
            _lines[_cursorLine] = _lines[_cursorLine - 1];
            _lines[_cursorLine - 1] = current;
            _cursorLine--;
            ResetSelection();
            NotifyUserChanged();
        }
    }

    public void SwapLineDown() {
        if (HasSelection()) {
            GetSelectionRange(out int sl, out int sc, out int el, out int ec);
            if (el < _lines.Count - 1) {
                string lineAfter = _lines[el + 1];
                _lines.RemoveAt(el + 1);
                _lines.Insert(sl, lineAfter);
                _cursorLine++;
                _selStartLine++;
                NotifyUserChanged();
            }
        } else if (_cursorLine < _lines.Count - 1) {
            string current = _lines[_cursorLine];
            _lines[_cursorLine] = _lines[_cursorLine + 1];
            _lines[_cursorLine + 1] = current;
            _cursorLine++;
            ResetSelection();
            NotifyUserChanged();
        }
    }

    public void DuplicateCurrentLine() {
        if (HasSelection()) {
            GetSelectionRange(out int sl, out int sc, out int el, out int ec);
            List<string> linesToDup = new();
            for (int i = sl; i <= el; i++) linesToDup.Add(_lines[i]);
            _lines.InsertRange(el + 1, linesToDup);
            _cursorLine += linesToDup.Count;
            _selStartLine += linesToDup.Count;
        } else {
            _lines.Insert(_cursorLine + 1, _lines[_cursorLine]);
            _cursorLine++;
        }
        NotifyUserChanged();
    }

    public void ToggleComment() {
        if (!HasSelection()) {
            // Line based toggle for current line
            string line = _lines[_cursorLine];
            int indent = 0;
            while (indent < line.Length && char.IsWhiteSpace(line[indent])) indent++;
            
            string trimmed = line.TrimStart();
            if (trimmed.StartsWith("// ")) {
                _lines[_cursorLine] = line.Remove(line.IndexOf("// "), 3);
                _cursorCol = Math.Max(indent, _cursorCol - 3);
            } else if (trimmed.StartsWith("//")) {
                _lines[_cursorLine] = line.Remove(line.IndexOf("//"), 2);
                _cursorCol = Math.Max(indent, _cursorCol - 2);
            } else {
                _lines[_cursorLine] = line.Insert(indent, "// ");
                _cursorCol += 3;
            }
            // Ensure no ghost selection remains
            ResetSelection();
        } else {
            GetSelectionRange(out int sl, out int sc, out int el, out int ec);
            
            // Check if it's an "inline" selection (partial line)
            bool isFullLineSelection = sc == 0 && ec == _lines[el].Length;
            
            if (!isFullLineSelection && sl == el) {
                // Block comment for single-line partial selection
                string line = _lines[sl];
                string selectedText = line.Substring(sc, ec - sc);
                
                if (selectedText.StartsWith("/*") && selectedText.EndsWith("*/")) {
                    // Uncomment
                    _lines[sl] = line.Remove(ec - 2, 2).Remove(sc, 2);
                    
                    // Maintain selection on the content
                    if (_cursorCol == ec) {
                        _cursorCol -= 4;
                        _selStartCol = sc;
                    } else {
                        _cursorCol = sc;
                        _selStartCol = ec - 4;
                    }
                } else {
                    // Comment
                    _lines[sl] = line.Insert(ec, "*/").Insert(sc, "/*");
                    
                    // Maintain selection on the original content (now wrapped)
                    if (_cursorCol == ec) {
                        _cursorCol += 2;
                        _selStartCol = sc + 2;
                    } else {
                        _cursorCol = sc + 2;
                        _selStartCol = ec + 2;
                    }
                }
            } else {
                // Line based toggle for multiple lines
                bool allCommented = true;
                for (int i = sl; i <= el; i++) {
                    string trimmed = _lines[i].TrimStart();
                    if (!trimmed.StartsWith("//")) {
                        allCommented = false;
                        break;
                    }
                }

                for (int i = sl; i <= el; i++) {
                    string line = _lines[i];
                    if (allCommented) {
                        int idx = line.IndexOf("//");
                        int len = 2;
                        if (line.Length > idx + 2 && line[idx + 2] == ' ') len = 3;
                        _lines[i] = line.Remove(idx, len);
                        
                        if (i == _cursorLine) _cursorCol = Math.Max(0, _cursorCol - len);
                        if (i == _selStartLine) _selStartCol = Math.Max(0, _selStartCol - len);
                    } else {
                        int indent = 0;
                        while (indent < line.Length && char.IsWhiteSpace(line[indent])) indent++;
                        _lines[i] = line.Insert(indent, "// ");
                        
                        // Shift cursor/selection start if they were after the insert point
                        if (i == _cursorLine && _cursorCol >= indent) _cursorCol += 3;
                        if (i == _selStartLine && _selStartCol >= indent) _selStartCol += 3;
                    }
                }
            }
        }

        NotifyUserChanged();
    }

    protected override void NotifyUserChanged() {
        base.NotifyUserChanged();
        _gutter.UpdateWidth(_lines.Count);
    }

    public void Save() {
        if (string.IsNullOrEmpty(_filePath)) return;
        VirtualFileSystem.Instance.WriteAllText(_filePath, Value);
        _isDirty = false;
        OnDirtyChanged?.Invoke();
    }

    public void SaveAs(string path) {
        _filePath = path;
        Save();
    }

    public void SetCursor(int line, int col) {
        _cursorLine = Math.Clamp(line, 0, _lines.Count - 1);
        _cursorCol = Math.Clamp(col, 0, _lines[_cursorLine].Length);
        ResetSelection();
        EnsureCursorVisible();
        NotifyUserChanged();
    }

    public void SetSelection(int startLine, int startCol, int endLine, int endCol) {
        _selStartLine = Math.Clamp(startLine, 0, _lines.Count - 1);
        _selStartCol = Math.Clamp(startCol, 0, _lines[_selStartLine].Length);
        _cursorLine = Math.Clamp(endLine, 0, _lines.Count - 1);
        _cursorCol = Math.Clamp(endCol, 0, _lines[_cursorLine].Length);
        EnsureCursorVisible();
        NotifyUserChanged();
    }

    // This method is used by external components (like snippets) to get the absolute screen position of a text coordinate.
    public Vector2 GetTextPosition(int line, int col) {
        var font = GameContent.FontSystem.GetFont(FontSize);
        float lineHeight = font.LineHeight;
        
        // Find the visual line corresponding to the logical line and column
        int visualLineIdx = GetVisualLineIndex(line, col);
        if (visualLineIdx == -1) return Vector2.Zero; // Should not happen if line/col are valid

        var vl = _visualLines[visualLineIdx];
        string lineText = _lines[vl.LogicalLineIndex];
        
        // Calculate X position
        int start = Math.Clamp(vl.StartIndex, 0, lineText.Length);
        int actualCol = Math.Clamp(col, start, start + vl.Length);
        string visualPart = lineText.Substring(start, actualCol - start);
        float x = font.MeasureString(visualPart).X;

        // Calculate Y position
        float y = visualLineIdx * lineHeight;

        // Apply editor's absolute position, text offset, and scroll offset
        var absPos = AbsolutePosition;
        var offset = TextOffset;
        float textX = absPos.X + offset.X - _scrollOffsetX;
        float textY = absPos.Y + offset.Y - _scrollOffset;

        return new Vector2(textX + x, textY + y);
    }

    public void ModifyLine(int lineIndex, string newContent) {
        if (lineIndex >= 0 && lineIndex < _lines.Count) {
            _lines[lineIndex] = newContent;
            NotifyUserChanged();
        }
    }


    protected override void DrawSelf(SpriteBatch spriteBatch, ShapeBatch batch) {
        base.DrawSelf(spriteBatch, batch);
        
        var absPos = AbsolutePosition;
        float gutterWidth = _gutter.Width;
        var oldScissor = G.GraphicsDevice.ScissorRectangle;

        int scX = (int)Math.Floor(absPos.X + gutterWidth + 2);
        int scY = (int)Math.Floor(absPos.Y + 2);
        int scW = (int)Math.Ceiling(Size.X - gutterWidth - 4);
        int scH = (int)Math.Ceiling(Size.Y - 4);
        var scissor = new Rectangle(scX, scY, scW, scH);

        // We override DrawSelf to shift the text to the right of the gutter
        // Background
        if (DrawBackground) {
            batch.FillRectangle(absPos, Size, BackgroundColor * AbsoluteOpacity, rounded: 3f);
            batch.BorderRectangle(absPos, Size, (IsFocused ? FocusedBorderColor : BorderColor) * AbsoluteOpacity, thickness: 1f, rounded: 3f);
        }

        // Highlights need to be drawn with the same scissor as the text
        if (_snippetSessions.Count > 0) {
            // Re-apply scissor
            G.GraphicsDevice.ScissorRectangle = Rectangle.Intersect(oldScissor, scissor);
            // Draw from bottom of stack up to ensure active session is on top
            foreach (var session in _snippetSessions.Reverse()) {
                session.Draw(spriteBatch, batch, (line, col) => GetTextPosition(line, col));
            }
            G.GraphicsDevice.ScissorRectangle = oldScissor;
        }

        if (GameContent.FontSystem == null) return;
        var font = GameContent.FontSystem.GetFont(FontSize);
        float lineHeight = font.LineHeight;

        // Clip region for text
        var finalScissor = Rectangle.Intersect(oldScissor, scissor);
        
        if (finalScissor.Width <= 0 || finalScissor.Height <= 0) {
            // Still draw children (the gutter)
            foreach (var child in Children) {
                child.Draw(spriteBatch, batch);
            }
            return;
        }

        // Draw children first (including gutter)
        foreach (var child in Children) {
            child.Draw(spriteBatch, batch);
        }

        batch.End();
        spriteBatch.End();
        
        var oldState = G.GraphicsDevice.ScissorRectangle;
        G.GraphicsDevice.ScissorRectangle = finalScissor;
        var rasterizerState = _scissorRasterizer;
        G.GraphicsDevice.RasterizerState = rasterizerState;
        
        batch.Begin();
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, rasterizerState);

        var offset = TextOffset;
        float textX = absPos.X + offset.X - _scrollOffsetX;
        float textY = absPos.Y + offset.Y - _scrollOffset;

        // Draw selection
        if (HasSelection()) {
            GetSelectionRange(out int sl, out int sc, out int el, out int ec);
            for (int i = 0; i < _visualLines.Count; i++) {
                var vl = _visualLines[i];
                float y = textY + i * lineHeight;
                if (y + lineHeight < absPos.Y || y > absPos.Y + Size.Y) continue;
                int visualLogicalLine = vl.LogicalLineIndex;
                if (visualLogicalLine < sl || visualLogicalLine > el) continue;
                int startC = (visualLogicalLine == sl) ? Math.Max(0, sc - vl.StartIndex) : 0;
                int endC = (visualLogicalLine == el) ? Math.Min(vl.Length, ec - vl.StartIndex) : vl.Length;
                if (startC >= endC && visualLogicalLine == sl && visualLogicalLine == el) continue;
                if (startC >= vl.Length || endC <= 0) continue;
                string visualText = _lines[vl.LogicalLineIndex].Substring(vl.StartIndex, vl.Length);
                float x1 = startC <= 0 ? 0 : font.MeasureString(visualText.Substring(0, startC)).X;
                float x2 = endC >= vl.Length ? font.MeasureString(visualText).X : font.MeasureString(visualText.Substring(0, endC)).X;
                batch.FillRectangle(new Vector2(textX + x1, y), new Vector2(x2 - x1, lineHeight), FocusedBorderColor * 0.3f * AbsoluteOpacity);
            }
        }

        // Draw lines
        int firstVis, lastVis;
        if (!UseInternalScrolling) {
            float relativeTop = G.GraphicsDevice.ScissorRectangle.Top - absPos.Y;
            float relativeBottom = G.GraphicsDevice.ScissorRectangle.Bottom - absPos.Y;
            firstVis = (int)Math.Floor(relativeTop / lineHeight);
            lastVis = (int)Math.Ceiling(relativeBottom / lineHeight);
        } else {
            firstVis = (int)Math.Floor(_scrollOffset / lineHeight);
            lastVis = (int)Math.Ceiling((_scrollOffset + Size.Y) / lineHeight);
        }

        int maxVis = Math.Max(0, _visualLines.Count - 1);
        firstVis = Math.Clamp(firstVis, 0, maxVis);
        lastVis = Math.Clamp(lastVis, 0, maxVis);

        for (int i = firstVis; i <= lastVis; i++) {
            var vl = _visualLines[i];
            float y = textY + i * lineHeight;
            string lineText = _lines[vl.LogicalLineIndex];
            
            // Syntax Highlighting Pass
            // Get tokens that overlap this visual line range
            int lineStartIdx = GetIndexFromPosition(vl.LogicalLineIndex, vl.StartIndex);
            int lineEndIdx = lineStartIdx + vl.Length;

            var lineTokens = Tokens.Where(t => t.Start < lineEndIdx && t.Start + t.Length > lineStartIdx).OrderBy(t => t.Start).ToList();
            
            if (lineTokens.Count == 0) {
                // Fallback to normal text if no tokens
                string visualPart = lineText.Substring(vl.StartIndex, vl.Length);
                if (!string.IsNullOrEmpty(visualPart)) {
                    font.DrawText(batch, visualPart, new Vector2(textX, y), TextColor * AbsoluteOpacity);
                }
            } else {
                float currentX = textX;
                int currentIdx = lineStartIdx;

                foreach (var token in lineTokens) {
                    // Draw untokenized text before this token
                    if (token.Start > currentIdx) {
                        int untokenizedLength = token.Start - currentIdx;
                        string untokenizedText = lineText.Substring(currentIdx - GetIndexFromPosition(vl.LogicalLineIndex, 0), untokenizedLength);
                        font.DrawText(batch, untokenizedText, new Vector2(currentX, y), TextColor * AbsoluteOpacity);
                        currentX += font.MeasureString(untokenizedText).X;
                        currentIdx = token.Start;
                    }

                    // Draw tokenized text
                    int tokenStartInLine = Math.Max(0, token.Start - lineStartIdx);
                    int tokenLengthInLine = Math.Min(vl.Length - tokenStartInLine, token.Length - Math.Max(0, lineStartIdx - token.Start));
                    
                    if (tokenLengthInLine > 0) {
                        string tokenText = lineText.Substring(vl.StartIndex + tokenStartInLine, tokenLengthInLine);
                        font.DrawText(batch, tokenText, new Vector2(currentX, y), token.Color * AbsoluteOpacity);
                        currentX += font.MeasureString(tokenText).X;
                        currentIdx += tokenLengthInLine;
                    }

                    if (currentIdx >= lineEndIdx) break;
                }

                // Draw remaining untokenized text
                if (currentIdx < lineEndIdx) {
                    int remainingLength = lineEndIdx - currentIdx;
                    string remainingText = lineText.Substring(vl.StartIndex + (currentIdx - lineStartIdx), remainingLength);
                    font.DrawText(batch, remainingText, new Vector2(currentX, y), TextColor * AbsoluteOpacity);
                }
            }
        }

        // Pass 3: Diagnostics Pass (Squigglies)
        foreach (var diag in Diagnostics) {
            int diagEnd = diag.Start + diag.Length;
            for (int i = firstVis; i <= lastVis; i++) {
                var vl = _visualLines[i];
                int lineStartIdx = GetIndexFromPosition(vl.LogicalLineIndex, vl.StartIndex);
                int lineEndIdx = lineStartIdx + vl.Length;

                if (diag.Start <= lineEndIdx && diagEnd >= lineStartIdx) {
                    int startInLine = Math.Max(0, diag.Start - lineStartIdx);
                    int endInLine = Math.Min(vl.Length, diagEnd - lineStartIdx);
                    
                    // If length is 0 (like missing semicolon at end of line), we still want to show a dot or small line
                    if (startInLine <= endInLine) {
                        string lineText = _lines[vl.LogicalLineIndex];
                        string beforeText = lineText.Substring(vl.StartIndex, Math.Min(startInLine, vl.Length));
                        
                        float x1 = font.MeasureString(beforeText).X;
                        float x2;
                        
                        if (startInLine == endInLine) {
                            // Point diagnostic. If at the end of line, give it some width.
                            x2 = x1 + font.MeasureString(" ").X; 
                        } else {
                            string diagText = lineText.Substring(vl.StartIndex + startInLine, endInLine - startInLine);
                            x2 = x1 + font.MeasureString(diagText).X;
                        }
                        float y = textY + i * lineHeight + lineHeight - 2;

                        Color diagColor = diag.Severity switch {
                            DiagnosticSeverity.Error => Color.Red,
                            DiagnosticSeverity.Warning => Color.Yellow,
                            _ => Color.Blue
                        };

                        // Draw wavy line (simplified as dots for now)
                        for (float wx = x1; wx < x2; wx += 2) {
                            float waveOffset = (float)Math.Sin(wx * 0.5f) * 1.5f;
                            batch.FillRectangle(new Vector2(textX + wx, y + waveOffset), new Vector2(1, 1), diagColor * AbsoluteOpacity);
                        }
                    }
                }
            }
        }

        // Cursor
        if (_showCursor && IsFocused) {
            int visualIdx = GetVisualLineIndex(_cursorLine, _cursorCol);
            if (visualIdx >= 0 && visualIdx < _visualLines.Count) {
                var vl = _visualLines[visualIdx];
                string lineText = _lines[vl.LogicalLineIndex];
                int start = Math.Clamp(vl.StartIndex, 0, lineText.Length);
                int col = Math.Clamp(_cursorCol, start, start + vl.Length);
                string visualPart = lineText.Substring(start, col - start);
                float cursorX = font.MeasureString(visualPart).X;
                float cursorY = textY + visualIdx * lineHeight;
                batch.FillRectangle(new Vector2(textX + cursorX, cursorY), new Vector2(2, lineHeight), FocusedBorderColor * AbsoluteOpacity);
            }
        }
        
        batch.End();
        spriteBatch.End();

        G.GraphicsDevice.ScissorRectangle = oldState;
        batch.Begin();
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
    }

    protected override void OnHover() {
        base.OnHover();
        
        if (GameContent.FontSystem == null || Diagnostics.Count == 0) return;
        var font = GameContent.FontSystem.GetFont(FontSize);
        float lineHeight = font.LineHeight;

        Vector2 absPos = AbsolutePosition;
        Vector2 local = InputManager.MousePosition.ToVector2() - absPos - TextOffset;
        local.Y += _scrollOffset;
        local.X += _scrollOffsetX;

        int visualIdx = (int)Math.Floor(local.Y / lineHeight);
        if (visualIdx >= 0 && visualIdx < _visualLines.Count) {
            var vl = _visualLines[visualIdx];
            int lineStartIdx = GetIndexFromPosition(vl.LogicalLineIndex, vl.StartIndex);
            
            // Find column
            string lineText = _lines[vl.LogicalLineIndex];
            string visualPart = lineText.Substring(vl.StartIndex, vl.Length);
            
            int colInVisual = 0;
            float bestDist = float.MaxValue;
            for (int i = 0; i <= visualPart.Length; i++) {
                float w = i == 0 ? 0 : font.MeasureString(visualPart.Substring(0, i)).X;
                float dist = Math.Abs(local.X - w);
                if (dist < bestDist) { bestDist = dist; colInVisual = i; }
            }

            int charIdx = lineStartIdx + colInVisual;
            // Use Inclusive check for 0-length diags, and normal check for others
            var diag = Diagnostics.OrderByDescending(d => d.Length == 0).FirstOrDefault(d => 
                (d.Length == 0 && charIdx == d.Start) || 
                (d.Length > 0 && charIdx >= d.Start && charIdx < d.Start + d.Length));
            
            if (diag != null) {
                Tooltip = diag.Message;
            } else {
                Tooltip = null;
            }
        } else {
            Tooltip = null;
        }
    }

    protected override void UpdateInput() {
        if (InputManager.IsKeyboardConsumed) return;
        base.UpdateInput();
    }

    protected override void SetCursorFromMouse() {
        base.SetCursorFromMouse();
    }

    public int GetIndexFromPosition(int line, int col) {
        int index = 0;
        for (int i = 0; i < line && i < _lines.Count; i++) {
            index += _lines[i].Length + 1; // +1 for newline
        }
        return index + col;
    }

    public void GetPositionFromIndex(int index, out int line, out int col) {
        line = 0;
        int remaining = index;
        while (line < _lines.Count && remaining > _lines[line].Length) {
            remaining -= (_lines[line].Length + 1); // +1 for newline
            line++;
        }
        line = Math.Min(line, _lines.Count - 1);
        col = Math.Max(0, remaining);
    }

    public void InsertText(string text) {
        if (string.IsNullOrEmpty(text)) return;
        
        if (HasSelection()) DeleteSelection();
        
        string lineContent = _lines[_cursorLine];
        _lines[_cursorLine] = lineContent.Insert(_cursorCol, text);
        _cursorCol += text.Length;
        
        ResetSelection();
        NotifyUserChanged();
    }

    public void ReplaceCurrentWord(string newWord) {
        if (HasSelection()) DeleteSelection();

        string currentLine = _lines[_cursorLine];
        int start = _cursorCol;
        while (start > 0 && (char.IsLetterOrDigit(currentLine[start - 1]) || currentLine[start - 1] == '_')) start--;
        
        // Remove old word part
        _lines[_cursorLine] = currentLine.Remove(start, _cursorCol - start);
        _cursorCol = start;
        ResetSelection(); // Reset selection to avoid eating following characters (like brackets)

        // Insert new word
        InsertText(newWord);
        ResetSelection();
    }

    public override void Update(GameTime gameTime) {
        if (IsFocused) {
        // Hungry Backspace & Auto-delete brackets
        // Use IsKeyDown check first to avoid calling IsKeyRepeated for regular backspaces
        if (InputManager.IsKeyDown(Keys.Back) && !HasSelection()) {
            string line = _lines[_cursorLine];
            bool isHungry = _cursorCol >= 4 && line.Substring(_cursorCol - 4, 4) == "    " && string.IsNullOrWhiteSpace(line.Substring(0, _cursorCol));
            
            bool isPair = false;
            if (!isHungry && _cursorCol > 0 && _cursorCol < line.Length) {
                char before = line[_cursorCol - 1];
                char after = line[_cursorCol];
                isPair = (before == '(' && after == ')') || 
                         (before == '{' && after == '}') || 
                         (before == '[' && after == ']') || 
                         (before == '"' && after == '"') || 
                         (before == '\'' && after == '\'');
            }

            if (isHungry || isPair) {
                if (InputManager.IsKeyRepeated(Keys.Back)) {
                    if (isHungry) {
                        _lines[_cursorLine] = line.Remove(_cursorCol - 4, 4);
                        _cursorCol -= 4;
                    } else {
                        _lines[_cursorLine] = line.Remove(_cursorCol - 1, 2);
                        _cursorCol--;
                    }
                    NotifyUserChanged();
                    InputManager.IsKeyboardConsumed = true; // Block base backspace
                }
            }
        }
    }

        // Handle auto-brackets on typing
        foreach (var c in InputManager.GetTypedChars()) {
            char close = '\0';
            if (c == '(') close = ')';
            else if (c == '{') close = '}';
            else if (c == '[') close = ']';
            else if (c == '"') close = '"';
            else if (c == '\'') close = '\'';

            if (close != '\0' && !HasSelection()) {
                // Peek at current line. TextArea hasn't updated yet.
                // We'll insert the closer after base.Update has processed the opener.
                _pendingAutoClose = close;
            }
        }
        
        if (ActiveSnippetSession != null && ActiveSnippetSession.IsEnded) {
            _snippetSessions.Pop();
        }

        int oldLine = _cursorLine;
        int oldCol = _cursorCol;
        int oldLen = _lines.Count > oldLine ? _lines[oldLine].Length : 0;
        string oldLineContent = _lines.Count > oldLine ? _lines[oldLine] : null;

        int oldLineCount = _lines.Count;
        base.Update(gameTime);

        if (_snippetSessions.Count > 0) {
            int newLineCount = _lines.Count;
            int deltaLines = newLineCount - oldLineCount;
            
            int newLen = _lines.Count > oldLine ? _lines[oldLine].Length : 0;
            string newLineContent = _lines.Count > oldLine ? _lines[oldLine] : null;
            int delta = newLen - oldLen;
            bool lineChanged = oldLineContent != newLineContent;

            if (delta != 0 || _cursorCol != oldCol || lineChanged || deltaLines != 0) {
                foreach (var session in _snippetSessions.ToList()) {
                    session.UpdateMarkers(oldLine, _cursorCol, delta, deltaLines);
                }
            }
        }

        if (_pendingAutoClose != '\0') {
            string line = _lines[_cursorLine];
            _lines[_cursorLine] = line.Insert(_cursorCol, _pendingAutoClose.ToString());
            _pendingAutoClose = '\0';
            NotifyUserChanged();
        }
    }

    private char _pendingAutoClose = '\0';

    public override void DeleteSelection() {
        base.DeleteSelection();
    }

    public override Vector2? GetCaretPosition() {
         return base.GetCaretPosition();
    }

    public override float GetTotalWidth() {
        UpdateLayout(); // Ensure gutter width is current
        return base.GetTotalWidth() + _gutter.Width + 10;
    }

    public void InsertSnippet(SnippetItem snippet) {
        if (HasSelection()) DeleteSelection();

        string line = _lines[_cursorLine];
        string indent = "";
        for (int i = 0; i < _cursorCol && i < line.Length && char.IsWhiteSpace(line[i]); i++) indent += line[i];

        // Replace current word if any (for example if user typed 'cw' then selected the snippet)
        int wordStart = _cursorCol;
        while (wordStart > 0 && (char.IsLetterOrDigit(line[wordStart - 1]) || line[wordStart - 1] == '_')) wordStart--;
        
        string prefix = line.Substring(0, wordStart);
        string suffix = line.Substring(_cursorCol);
        
        _lines[_cursorLine] = prefix;
        _cursorCol = prefix.Length;
        int startCol = _cursorCol;

        string body = snippet.Body;
        body = body.Replace("$CLASSNAME$", CodeEditorHelper.GetEnclosingClassName(_lines, FileName, _cursorLine));
        body = body.Replace("$NAMESPACE$", CodeEditorHelper.GetNamespace(_lines));

        // Parse placeholders ${1:default}, $0 etc
        var markers = new List<SnippetSession.Marker>();
        var regex = new System.Text.RegularExpressions.Regex(@"\${(\d+)(?::([^}]+))?}|\$(\d+)");
        
        var snippetLines = body.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        int startLine = _cursorLine;

        var processedSnippetLines = new List<string>();
        for (int i = 0; i < snippetLines.Length; i++) {
            string currentSnippetLine = snippetLines[i];
            string processedLine = "";
            int colOffset = (i == 0) ? startCol : indent.Length;
            
            int lastIdx = 0;
            var matches = regex.Matches(currentSnippetLine);
            foreach (System.Text.RegularExpressions.Match match in matches) {
                processedLine += currentSnippetLine.Substring(lastIdx, match.Index - lastIdx);
                
                int index = 0;
                string val = "";
                if (match.Groups[3].Success) { // $0, $1
                    index = int.Parse(match.Groups[3].Value);
                } else { // ${1:default}
                    index = int.Parse(match.Groups[1].Value);
                    val = match.Groups[2].Value;
                }

                markers.Add(new SnippetSession.Marker {
                    Index = index,
                    Line = startLine + i,
                    StartCol = colOffset + processedLine.Length,
                    EndCol = colOffset + processedLine.Length + val.Length,
                    Value = val
                });
                
                processedLine += val;
                lastIdx = match.Index + match.Length;
            }
            processedLine += currentSnippetLine.Substring(lastIdx);
            processedSnippetLines.Add(processedLine);
        }

        // Apply lines back to editor
        _lines[_cursorLine] = prefix + processedSnippetLines[0];
        if (snippetLines.Length == 1) {
            _lines[_cursorLine] += suffix;
        } else {
            var intermediate = new List<string>();
            for (int i = 1; i < processedSnippetLines.Count - 1; i++) {
                intermediate.Add(indent + processedSnippetLines[i]);
            }
            // Last line gets the suffix
            intermediate.Add(indent + processedSnippetLines.Last() + suffix);
            
            _lines.InsertRange(_cursorLine + 1, intermediate);

            // Notify existing sessions about the new lines and column shifts
            if (intermediate.Count > 0) {
                int lastLinePrefixLen = indent.Length + processedSnippetLines.Last().Length;
                int colShift = lastLinePrefixLen - startCol;

                foreach (var session in _snippetSessions.ToList()) {
                    // UpdateMarkers handles line shift, but we need to manually adjust columns 
                    // for markers that moved to the new line (the suffix part)
                    foreach (var m in session.MarkersInternal) {
                        if (m.Line == startLine && m.StartCol >= startCol) {
                            m.StartCol += colShift;
                            m.EndCol += colShift;
                        }
                    }
                    session.UpdateMarkers(startLine, startCol, 0, intermediate.Count);
                }
            }
        }

        if (markers.Count > 0) {
            ActiveSnippetSession = new SnippetSession(this, markers);
        } else {
            // Just move cursor to end of snippet
            _cursorLine = startLine + snippetLines.Length - 1;
            _cursorCol = (snippetLines.Length == 1 ? startCol + processedSnippetLines[0].Length : indent.Length + processedSnippetLines.Last().Length);
        }

        NotifyUserChanged();
    }
}
