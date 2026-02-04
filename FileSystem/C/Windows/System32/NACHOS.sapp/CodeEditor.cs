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
using TheGame.Core.Input;

namespace NACHOS;

public class CodeEditor : TextArea {
    private Gutter _gutter;
    private bool _isDirty = false;
    private string _filePath;

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
            
            char close = '\0';
            if (c == '(') close = ')';
            else if (c == '{') close = '}';
            else if (c == '[') close = ']';
            else if (c == '"') close = '"';
            else if (c == '\'') close = '\'';

            if (close != '\0') {
                // Peek at current line
                string line = _lines[_cursorLine];
                _lines[_cursorLine] = line.Insert(_cursorCol, close.ToString());
                NotifyUserChanged();
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

        string remainder = _lines[_cursorLine].Substring(_cursorCol);
        _lines[_cursorLine] = _lines[_cursorLine].Substring(0, _cursorCol);
        _lines.Insert(_cursorLine + 1, indent + remainder);
        _cursorLine++;
        _cursorCol = indent.Length;
        ResetSelection();
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

    protected override void DrawSelf(SpriteBatch spriteBatch, ShapeBatch batch) {
        // We override DrawSelf to shift the text to the right of the gutter
        var absPos = AbsolutePosition;

        // Background
        if (DrawBackground) {
            batch.FillRectangle(absPos, Size, BackgroundColor * AbsoluteOpacity, rounded: 3f);
            batch.BorderRectangle(absPos, Size, (IsFocused ? FocusedBorderColor : BorderColor) * AbsoluteOpacity, thickness: 1f, rounded: 3f);
        }

        if (GameContent.FontSystem == null) return;
        var font = GameContent.FontSystem.GetFont(FontSize);
        float lineHeight = font.LineHeight;

        // Offset for gutter
        float gutterWidth = _gutter.Width;
        
        // Clip region for text
        var oldScissor = G.GraphicsDevice.ScissorRectangle;
        int scX = (int)Math.Floor(absPos.X + gutterWidth + 2);
        int scY = (int)Math.Floor(absPos.Y + 2);
        int scW = (int)Math.Ceiling(Size.X - gutterWidth - 4);
        int scH = (int)Math.Ceiling(Size.Y - 4);
        var scissor = new Rectangle(scX, scY, scW, scH);
        scissor = Rectangle.Intersect(oldScissor, scissor);
        
        if (scissor.Width <= 0 || scissor.Height <= 0) {
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
        G.GraphicsDevice.ScissorRectangle = scissor;
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
        // Handle auto-brackets
        foreach (var c in InputManager.GetTypedChars()) {
            char close = '\0';
            if (c == '(') close = ')';
            else if (c == '{') close = '}';
            else if (c == '[') close = ']';
            else if (c == '"') close = '"';
            else if (c == '\'') close = '\'';

            if (close != '\0') {
                // If it's a quote, check if we are just "typing over" an existing one
                string line = _lines[_cursorLine];
                if ((c == '"' || c == '\'') && _cursorCol < line.Length && line[_cursorCol] == c) {
                    // TextArea will insert another one, but we want to avoid double quotes if just typing over.
                    // This is tricky because TextArea.Update processes input. 
                    // For now, let's just insert the closing pair.
                }

                // Insert the character normally (via base) then insert the closer
                // Actually, let's wait until base.Update inserts the character.
                // But base.Update consumes characters. 
            }
        }
        
        base.Update(gameTime);

        // Post-update: if one of the triggers was just typed, insert the closer
        foreach (var c in InputManager.GetTypedChars()) {
             // We need to know IF the character was just inserted.
             // But GetTypedChars() is a queue that we just emptied?
             // No, usually it's a snapshot or we consume it.
        }
    }

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
}
