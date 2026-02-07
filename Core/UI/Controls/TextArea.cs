using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using TheGame.Core.Input;
using TheGame.Core.OS;
using TheGame.Graphics;
using System.ComponentModel;
using TheGame.Core.Designer;
using TheGame.Core.OS.History;

namespace TheGame.Core.UI.Controls;

/// <summary>
/// A multiline text editing control similar to a Windows TextBox with multiline support.
/// </summary>
public class TextArea : ValueControl<string> {
    protected List<string> _lines = new() { "" };
    public record struct VisualLine(int LogicalLineIndex, int StartIndex, int Length);
    protected List<VisualLine> _visualLines = new();
    
    protected int _cursorLine = 0;
    protected int _cursorCol = 0;
    
    private bool _wordWrap = false;
    public bool WordWrap {
        get => _wordWrap;
        set {
            if (_wordWrap == value) return;
            _wordWrap = value;
            _maxWidthDirty = true;
            UpdateVisualLines();
        }
    }
    
    protected int _selStartLine = 0;
    protected int _selStartCol = 0;
    protected float _scrollOffset = 0f;
    protected float _targetScrollOffset = 0f;
    protected float _scrollOffsetX = 0f;
    protected float _targetScrollOffsetX = 0f;
    protected float _cursorTimer = 0f;
    protected bool _showCursor = true;
    [DesignerIgnoreProperty] [DesignerIgnoreJsonSerialization]
    public bool UseInternalScrolling { get; set; } = true;
    public bool IsReadOnly { get; set; } = false;
    public Action OnCursorMoved;
    protected bool _isWordSelecting = false;
    public int CursorLine => _cursorLine;
    public int CursorCol => _cursorCol;
    public int SelStartLine => _selStartLine;
    public int SelStartCol => _selStartCol;
    protected int _wordSelectAnchorStartLine = 0;
    protected int _wordSelectAnchorStartCol = 0;
    protected int _wordSelectAnchorEndLine = 0;
    protected int _wordSelectAnchorEndCol = 0;
    protected float _cachedMaxWidth = 0f;
    protected bool _maxWidthDirty = true;
    [DesignerIgnoreProperty] [DesignerIgnoreJsonSerialization]
    public CommandHistory History { get; set; } = new();
    
    protected int _notifySkipCount = 0;
    protected bool _needsNotify = false;
    
    protected static readonly RasterizerState _scissorRasterizer = new RasterizerState { ScissorTestEnable = true, CullMode = CullMode.None };

    [DesignerIgnoreProperty] [DesignerIgnoreJsonSerialization]
    public virtual Vector2 TextOffset => new Vector2(10, 10);

    protected virtual bool CanEditAt(int line, int col) => !IsReadOnly;
    protected virtual void OnEnterPressed() {
        if (History != null) {
            GetSelectionRange(out int sl, out int sc, out int el, out int ec);
            History.Execute(new ReplaceTextCommand(this, "New Line", sl, sc, el, ec, "\n"));
        } else {
            if (HasSelection()) DeleteSelection();
            string remainder = _lines[_cursorLine].Substring(_cursorCol);
            _lines[_cursorLine] = _lines[_cursorLine].Substring(0, _cursorCol);
            _lines.Insert(_cursorLine + 1, remainder);
            _cursorLine++;
            _cursorCol = 0;
            ResetSelection();
            NotifyUserChanged();
        }
    }
    protected virtual void OnTabPressed() {
        if (History != null) {
             GetSelectionRange(out int sl, out int sc, out int el, out int ec);
             History.Execute(new ReplaceTextCommand(this, "Tab", sl, sc, el, ec, "    "));
        } else {
            if (HasSelection()) DeleteSelection();
            _lines[_cursorLine] = _lines[_cursorLine].Insert(_cursorCol, "    ");
            _cursorCol += 4;
            ResetSelection();
            NotifyUserChanged();
        }
    }

    public override void SetValue(string value, bool notify = true) {
        string newValue = (value ?? "").Replace("\r\n", "\n").Replace("\r", "\n");
        if (base.Value == newValue) return;

        _lines = new List<string>(newValue.Split('\n'));
        if (_lines.Count == 0) _lines.Add("");
        _cursorLine = 0;
        _cursorCol = 0;
        _selStartLine = 0;
        _selStartCol = 0;
        _maxWidthDirty = true;
        
        UpdateVisualLines();
        base.SetValue(newValue, notify);
    }

    protected virtual void NotifyUserChanged() {
        if (_notifySkipCount > 0) {
            _needsNotify = true;
            return;
        }
        _maxWidthDirty = true;
        _needsNotify = false;
        UpdateVisualLines();
        base.SetValue(string.Join("\n", _lines), true);
        EnsureCursorVisible();
        OnCursorMoved?.Invoke();
    }

    protected void UpdateVisualLines() {
        _visualLines.Clear();
        if (GameContent.FontSystem == null) {
            for (int i = 0; i < _lines.Count; i++) _visualLines.Add(new VisualLine(i, 0, _lines[i].Length));
            return;
        }

        if (!WordWrap) {
            for (int i = 0; i < _lines.Count; i++) _visualLines.Add(new VisualLine(i, 0, _lines[i].Length));
            return;
        }

        var font = GameContent.FontSystem.GetFont(FontSize);
        float maxWidth = Size.X - 20; // 5 padding each side + some safety
        if (maxWidth <= 0) {
             for (int i = 0; i < _lines.Count; i++) _visualLines.Add(new VisualLine(i, 0, _lines[i].Length));
             return;
        }

        for (int i = 0; i < _lines.Count; i++) {
            string line = _lines[i];
            if (string.IsNullOrEmpty(line)) {
                _visualLines.Add(new VisualLine(i, 0, 0));
                continue;
            }

            int start = 0;
            while (start < line.Length) {
                int count = 1;
                while (start + count <= line.Length && font.MeasureString(line.Substring(start, count)).X <= maxWidth) {
                    count++;
                }
                
                // If the first character is already too wide, we force it and move on
                if (count == 1) {
                    _visualLines.Add(new VisualLine(i, start, 1));
                    start += 1;
                } else {
                    _visualLines.Add(new VisualLine(i, start, count - 1));
                    start += count - 1;
                }
            }
        }
    }
    
    // Backwards compatibility alias
    [DesignerIgnoreProperty] [DesignerIgnoreJsonSerialization]
    public string Text {
        get => Value;
        set => Value = value;
    }

    public string Placeholder { get; set; } = "";
    public Color TextColor { get; set; } = Color.White;
    public Color FocusedBorderColor { get; set; } = new Color(0, 120, 215);
    public int FontSize { get; set; } = 16;
    public bool DrawBackground { get; set; } = true;

    [Obsolete("For Designer/Serialization use only")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public TextArea() : this(Vector2.Zero, Vector2.Zero) { }

    // OnTextChanged is now inherited as OnValueChanged from ValueControl<string>

    public TextArea(Vector2 position, Vector2 size) : base(position, size, "") {
        BackgroundColor = new Color(30, 30, 30);
        BorderColor = new Color(60, 60, 60);
        OnResize += () => {
            UpdateVisualLines();
            EnsureCursorVisible();
        };
        UpdateVisualLines();
    }

    protected override void OnHover() {
        CustomCursor.Instance.SetCursor(CursorType.Beam);
        base.OnHover();
    }

    protected override void UpdateInput() {
        if (DesignMode.SuppressNormalInput(this)) return;

        _notifySkipCount++;
        try {
            InternalUpdateInput();
        } finally {
            _notifySkipCount--;
            if (_notifySkipCount == 0 && _needsNotify) {
                NotifyUserChanged();
            }
        }
    }

    private void InternalUpdateInput() {
        base.UpdateInput();

        if (InputManager.IsAnyMouseButtonJustPressed(MouseButton.Left)) {
            if (IsMouseOver) {
                SetCursorFromMouse();
                _selStartLine = _cursorLine;
                _selStartCol = _cursorCol;
                _isWordSelecting = false;
                InputManager.IsMouseConsumed = true; // Consume mouse input
            }
        }

        if (IsFocused) {
            // Drag selection: Use ignoreConsumed: true so we don't block ourselves if we already consumed the mouse this frame
            if (_isPressed && InputManager.IsMouseButtonDown(MouseButton.Left)) {
                InputManager.IsMouseConsumed = true;
                if (!_isWordSelecting) {
                    SetCursorFromMouse();
                } else {
                    // Word selection drag logic
                    int oldCursorLine = _cursorLine;
                    int oldCursorCol = _cursorCol;
                    SetCursorFromMouse();

                    // Find word boundaries at current position
                    string line = _lines[_cursorLine];
                    int start = _cursorCol;
                    int end = _cursorCol;
                    while (start > 0 && IsWordChar(line[start - 1])) start--;
                    while (end < line.Length && IsWordChar(line[end])) end++;

                    // If dragging forward from anchor
                    if (_cursorLine > _wordSelectAnchorStartLine || (_cursorLine == _wordSelectAnchorStartLine && _cursorCol >= _wordSelectAnchorStartCol)) {
                        _selStartLine = _wordSelectAnchorStartLine;
                        _selStartCol = _wordSelectAnchorStartCol;
                        _cursorCol = end;
                    } else {
                        // Dragging backward
                        _selStartLine = _wordSelectAnchorEndLine;
                        _selStartCol = _wordSelectAnchorEndCol;
                        _cursorCol = start;
                    }
                }
            } else {
                _isWordSelecting = false;
            }

            if (InputManager.IsDoubleClick(MouseButton.Left, ignoreConsumed: true) && IsMouseOver) {
                SelectWordAtCursor();
                _isWordSelecting = true;
                InputManager.IsMouseConsumed = true;
            }
        }

        if (IsFocused) {
            // Handle scroll
            int scroll = InputManager.ScrollDelta;
            if (scroll != 0 && IsMouseOver && UseInternalScrolling) {
                var font = GameContent.FontSystem?.GetFont(FontSize);
                float scrollAmount = (font?.LineHeight ?? 20) * 3;
                _targetScrollOffset -= (scroll / 120f) * scrollAmount;
                ClampScroll();
            }

            bool shift = InputManager.IsKeyDown(Keys.LeftShift) || InputManager.IsKeyDown(Keys.RightShift);
            bool ctrl = InputManager.IsKeyDown(Keys.LeftControl) || InputManager.IsKeyDown(Keys.RightControl);

            // Navigation
            if (InputManager.IsKeyRepeated(Keys.Left)) MoveCursor(-1, 0, shift);
            if (InputManager.IsKeyRepeated(Keys.Right)) MoveCursor(1, 0, shift);
            if (InputManager.IsKeyRepeated(Keys.Up)) MoveCursor(0, -1, shift);
            if (InputManager.IsKeyRepeated(Keys.Down)) MoveCursor(0, 1, shift);

            if (InputManager.IsKeyJustPressed(Keys.Home)) {
                _cursorCol = 0;
                if (!shift) { _selStartLine = _cursorLine; _selStartCol = _cursorCol; }
                EnsureCursorVisible();
            }
            if (InputManager.IsKeyJustPressed(Keys.End)) {
                _cursorCol = _lines[_cursorLine].Length;
                if (!shift) { _selStartLine = _cursorLine; _selStartCol = _cursorCol; }
                EnsureCursorVisible();
            }

            // Select all / Copy / Cut / Paste
            if (ctrl && InputManager.IsKeyJustPressed(Keys.A)) SelectAll();
            if (ctrl && InputManager.IsKeyJustPressed(Keys.C)) Copy();
            if (ctrl && InputManager.IsKeyJustPressed(Keys.X)) Cut();
            if (ctrl && InputManager.IsKeyJustPressed(Keys.V)) Paste();

            // Backspace
            if (InputManager.IsKeyRepeated(Keys.Back)) {
                if (HasSelection()) {
                    GetSelectionRange(out int sl, out int sc, out int el, out int ec);
                    History?.Execute(new ReplaceTextCommand(this, "Backspace", sl, sc, el, ec, ""));
                } else if (_cursorCol > 0) {
                    if (CanEditAt(_cursorLine, _cursorCol - 1)) {
                        string text = _lines[_cursorLine].Substring(_cursorCol - 1, 1);
                        History?.Execute(new DeleteTextCommand(this, _cursorLine, _cursorCol - 1, text, true));
                    }
                } else if (_cursorLine > 0) {
                    if (CanEditAt(_cursorLine - 1, _lines[_cursorLine - 1].Length)) {
                        History?.Execute(new DeleteTextCommand(this, _cursorLine - 1, _lines[_cursorLine - 1].Length, "\n", true));
                    }
                }
            }

            // Delete
            if (InputManager.IsKeyRepeated(Keys.Delete)) {
                if (HasSelection()) {
                    GetSelectionRange(out int sl, out int sc, out int el, out int ec);
                    History?.Execute(new ReplaceTextCommand(this, "Delete", sl, sc, el, ec, ""));
                } else if (_cursorCol < _lines[_cursorLine].Length) {
                    if (CanEditAt(_cursorLine, _cursorCol)) {
                        string text = _lines[_cursorLine].Substring(_cursorCol, 1);
                        History?.Execute(new DeleteTextCommand(this, _cursorLine, _cursorCol, text, false));
                    }
                } else if (_cursorLine < _lines.Count - 1) {
                    if (CanEditAt(_cursorLine, _cursorCol)) {
                        History?.Execute(new DeleteTextCommand(this, _cursorLine, _lines[_cursorLine].Length, "\n", false));
                    }
                }
            }

            // Enter
            if (InputManager.IsKeyJustPressed(Keys.Enter)) {
                OnEnterPressed();
            }

            // Tab
            if (InputManager.IsKeyJustPressed(Keys.Tab)) {
                OnTabPressed();
            }

            string typingBatch = "";
            foreach (var c in InputManager.GetTypedChars()) {
                if (char.IsControl(c)) continue;
                if (CanEditAt(_cursorLine, _cursorCol)) {
                    if (HasSelection()) {
                        if (typingBatch.Length > 0) {
                            History?.Execute(new InsertTextCommand(this, typingBatch, _cursorLine, _cursorCol));
                            typingBatch = "";
                        }
                        GetSelectionRange(out int sl, out int sc, out int el, out int ec);
                        History?.Execute(new ReplaceTextCommand(this, "Typing", sl, sc, el, ec, c.ToString()));
                    } else {
                        typingBatch += c;
                    }
                }
            }
            if (typingBatch.Length > 0) {
                History?.Execute(new InsertTextCommand(this, typingBatch, _cursorLine, _cursorCol));
            }

            InputManager.IsKeyboardConsumed = true;
        }
    }

    protected virtual void SetCursorFromMouse() {
        if (GameContent.FontSystem == null || _visualLines.Count == 0) return;
        var font = GameContent.FontSystem.GetFont(FontSize);
        float lineHeight = font.LineHeight;

        Vector2 local = InputManager.MousePosition.ToVector2() - AbsolutePosition - TextOffset;
        local.Y += _scrollOffset;
        local.X += _scrollOffsetX;

        int visualIdx = (int)(local.Y / lineHeight);
        visualIdx = Math.Clamp(visualIdx, 0, _visualLines.Count - 1);

        var vl = _visualLines[visualIdx];
        if (vl.LogicalLineIndex < 0 || vl.LogicalLineIndex >= _lines.Count) return;
        
        string lineText = _lines[vl.LogicalLineIndex];
        int start = Math.Clamp(vl.StartIndex, 0, lineText.Length);
        int length = Math.Clamp(vl.Length, 0, lineText.Length - start);
        string visualPart = lineText.Substring(start, length);

        int colInVisual = 0;
        float bestDist = float.MaxValue;
        for (int i = 0; i <= visualPart.Length; i++) {
            float w = i == 0 ? 0 : font.MeasureString(visualPart.Substring(0, i)).X;
            float dist = Math.Abs(local.X - w);
            if (dist < bestDist) { bestDist = dist; colInVisual = i; }
        }

        _cursorLine = vl.LogicalLineIndex;
        _cursorCol = vl.StartIndex + colInVisual;
        ResetCursorBlink();
        OnCursorMoved?.Invoke();
    }

    public override Vector2? GetCaretPosition() {
        if (GameContent.FontSystem == null || _visualLines.Count == 0) return null;
        var font = GameContent.FontSystem.GetFont(FontSize);
        float lineHeight = font.LineHeight;

        int visualIdx = GetVisualLineIndex(_cursorLine, _cursorCol);
        if (visualIdx < 0 || visualIdx >= _visualLines.Count) return null;
        var vl = _visualLines[visualIdx];
        
        if (vl.LogicalLineIndex < 0 || vl.LogicalLineIndex >= _lines.Count) return null;
        string lineText = _lines[vl.LogicalLineIndex];
        int start = Math.Clamp(vl.StartIndex, 0, lineText.Length);
        int col = Math.Clamp(_cursorCol, start, start + vl.Length);
        string visualPart = lineText.Substring(start, col - start);
        
        float cursorX = font.MeasureString(visualPart).X;
        float cursorY = visualIdx * lineHeight;
        
        // Return absolute screen position
        return AbsolutePosition + TextOffset - new Vector2(_scrollOffsetX, _scrollOffset) + new Vector2(cursorX, cursorY);
    }

    protected int GetVisualLineIndex(int logicalLine, int logicalCol) {
        for (int i = 0; i < _visualLines.Count; i++) {
            var vl = _visualLines[i];
            if (vl.LogicalLineIndex == logicalLine) {
                if (logicalCol >= vl.StartIndex && logicalCol < vl.StartIndex + vl.Length) return i;
                // Last visual line of a logical line - includes the very end
                if (logicalCol == vl.StartIndex + vl.Length) {
                    if (i == _visualLines.Count - 1 || _visualLines[i+1].LogicalLineIndex != logicalLine) return i;
                }
            }
        }
        return -1;
    }

    public virtual void SelectAll() {
        _selStartLine = 0;
        _selStartCol = 0;
        _cursorLine = _lines.Count - 1;
        _cursorCol = _lines[_cursorLine].Length;
    }

    public override void Copy() {
        if (!HasSelection()) return;
        GetSelectionRange(out int sl, out int sc, out int el, out int ec);
        
        StringBuilder sb = new();
        for (int i = sl; i <= el; i++) {
            int start = (i == sl) ? sc : 0;
            int end = (i == el) ? ec : _lines[i].Length;
            sb.Append(_lines[i].Substring(start, end - start));
            if (i < el) sb.Append("\n");
        }
        
        string appId = GetOwnerProcess()?.AppId ?? "Unknown";
        ClipboardManager.Instance.SetData(sb.ToString(), ClipboardContentType.Text, appId);
    }

    public void SetCursorAndSelection(int cursorLine, int cursorCol, int selLine, int selCol) {
        _cursorLine = Math.Clamp(cursorLine, 0, _lines.Count - 1);
        _cursorCol = Math.Clamp(cursorCol, 0, _lines[_cursorLine].Length);
        _selStartLine = Math.Clamp(selLine, 0, _lines.Count - 1);
        _selStartCol = Math.Clamp(selCol, 0, _lines[_selStartLine].Length);
        EnsureCursorVisible();
        NotifyUserChanged();
    }

    public void SortRange(ref int sl, ref int sc, ref int el, ref int ec) {
        if (sl > el || (sl == el && sc > ec)) {
            int tempL = sl; sl = el; el = tempL;
            int tempC = sc; sc = ec; ec = tempC;
        }
    }

    private void ClampIndices(ref int line, ref int col) {
        line = Math.Clamp(line, 0, _lines.Count - 1);
        col = Math.Clamp(col, 0, _lines[line].Length);
    }

    internal void InternalInsertText(int line, int col, string text) {
        ClampIndices(ref line, ref col);
        if (string.IsNullOrEmpty(text)) {
            NotifyUserChanged();
            return;
        }

        string normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");
        string[] newLines = normalized.Split('\n');
        
        string currentLine = _lines[line];
        string before = currentLine.Substring(0, col);
        string after = currentLine.Substring(col);

        if (newLines.Length == 1) {
            _lines[line] = before + newLines[0] + after;
            _cursorLine = line;
            _cursorCol = col + newLines[0].Length;
        } else {
            // First line
            _lines[line] = before + newLines[0];
            
            // Middle lines
            for (int i = 1; i < newLines.Length - 1; i++) {
                _lines.Insert(line + i, newLines[i]);
            }
            
            // Last line
            _lines.Insert(line + newLines.Length - 1, newLines[^1] + after);
            
            _cursorLine = line + newLines.Length - 1;
            _cursorCol = newLines[^1].Length;
        }
        ResetSelection();
        NotifyUserChanged();
    }

    internal void InternalDeleteRange(int sl, int sc, int el, int ec) {
        SortRange(ref sl, ref sc, ref el, ref ec);
        ClampIndices(ref sl, ref sc);
        ClampIndices(ref el, ref ec);
        // Console.WriteLine($"[DeleteRange] {sl},{sc} to {el},{ec}");

        if (sl == el) {
            int count = Math.Max(0, ec - sc);
            if (count > 0) {
                _lines[sl] = _lines[sl].Remove(sc, count);
            }
        } else {
            string before = _lines[sl].Substring(0, sc);
            string after = _lines[el].Substring(ec);
            _lines[sl] = before + after;
            int linesToRemove = el - sl;
            if (linesToRemove > 0) {
                _lines.RemoveRange(sl + 1, linesToRemove);
            }
        }
        _cursorLine = sl;
        _cursorCol = sc;
        ResetSelection();
        NotifyUserChanged();
    }

    internal void InternalDeleteRange(int line, int col, int count) {
        // Find end position for count characters
        int el = line;
        int ec = col;
        int remaining = count;
        while (remaining > 0 && el < _lines.Count) {
            int lineLeft = _lines[el].Length - ec;
            if (remaining <= lineLeft) {
                ec += remaining;
                remaining = 0;
            } else {
                remaining -= (lineLeft + 1); // +1 for newline
                el++;
                ec = 0;
            }
        }
        InternalDeleteRange(line, col, el, ec);
    }

    public string InternalGetRange(int sl, int sc, int el, int ec) {
        SortRange(ref sl, ref sc, ref el, ref ec);
        ClampIndices(ref sl, ref sc);
        ClampIndices(ref el, ref ec);

        if (sl == el) return _lines[sl].Substring(sc, ec - sc);

        StringBuilder sb = new();
        sb.Append(_lines[sl].Substring(sc));
        for (int i = sl + 1; i <= el; i++) {
            sb.Append("\n");
            int end = (i == el) ? ec : _lines[i].Length;
            sb.Append(_lines[i].Substring(0, end));
        }
        return sb.ToString();
    }

    internal void InternalReplaceRange(int sl, int sc, int el, int ec, string newText) {
        SortRange(ref sl, ref sc, ref el, ref ec);
        DebugLogger.Log($"TextArea.InternalReplaceRange: {sl},{sc} to {el},{ec} with text of length {newText?.Length}");
        _notifySkipCount++;
        try {
            InternalDeleteRange(sl, sc, el, ec);
            InternalInsertText(sl, sc, newText);
        } finally {
            _notifySkipCount--;
            if (_notifySkipCount == 0 && _needsNotify) {
                NotifyUserChanged();
            }
        }
    }

    internal void InternalGetPositionAfter(int sl, int sc, string text, out int el, out int ec) {
        string[] lines = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        if (lines.Length == 1) {
            el = sl;
            ec = sc + lines[0].Length;
        } else {
            el = sl + lines.Length - 1;
            ec = lines[^1].Length;
        }
    }

    public override void Cut() {
        if (IsReadOnly) return;
        if (!HasSelection()) return;
        Copy();
        DeleteSelection();
    }

    public override void Paste() {
        if (IsReadOnly) return;
        string text = Shell.Clipboard.GetText();
        if (string.IsNullOrEmpty(text)) return;
        
        GetSelectionRange(out int sl, out int sc, out int el, out int ec);
        History?.Execute(new ReplaceTextCommand(this, "Paste", sl, sc, el, ec, text));
    }

    public override void Undo() {
        if (IsReadOnly) return;
        if (History == null) DebugLogger.Log("TextArea.Undo: History is null");
        else {
            DebugLogger.Log($"TextArea.Undo: Undoing last command. History count: {History.CanUndo}");
            History.Undo();
        }
    }
    public override void Redo() {
        if (IsReadOnly) return;
        History?.Redo();
    }

    private void SelectWordAtCursor() {
        string line = _lines[_cursorLine];
        if (string.IsNullOrEmpty(line)) return;

        int start = _cursorCol;
        int end = _cursorCol;

        // Find word start
        while (start > 0 && IsWordChar(line[start - 1])) start--;
        // Find word end
        while (end < line.Length && IsWordChar(line[end])) end++;

        _selStartLine = _cursorLine;
        _selStartCol = start;
        _cursorCol = end;

        // Store anchors for word-selection dragging
        _wordSelectAnchorStartLine = _cursorLine;
        _wordSelectAnchorStartCol = start;
        _wordSelectAnchorEndLine = _cursorLine;
        _wordSelectAnchorEndCol = end;
    }

    private bool IsWordChar(char c) {
        return char.IsLetterOrDigit(c) || c == '_';
    }

    public virtual void MoveCursor(int dx, int dy, bool select) {
        if (dy != 0) {
            _cursorLine = Math.Clamp(_cursorLine + dy, 0, _lines.Count - 1);
            _cursorCol = Math.Min(_cursorCol, _lines[_cursorLine].Length);
        }
        if (dx != 0) {
            _cursorCol += dx;
            if (_cursorCol < 0 && _cursorLine > 0) {
                _cursorLine--;
                _cursorCol = _lines[_cursorLine].Length;
            } else if (_cursorCol > _lines[_cursorLine].Length && _cursorLine < _lines.Count - 1) {
                _cursorLine++;
                _cursorCol = 0;
            }
            _cursorCol = Math.Clamp(_cursorCol, 0, _lines[_cursorLine].Length);
        }
        if (!select) ResetSelection();
        EnsureCursorVisible();
        OnCursorMoved?.Invoke();
    }

    public override bool HasSelection() => _cursorLine != _selStartLine || _cursorCol != _selStartCol;

    protected virtual void ResetSelection() {
        _selStartLine = _cursorLine;
        _selStartCol = _cursorCol;
    }

    public override void DeleteSelection() {
        if (IsReadOnly) return;
        if (!HasSelection()) return;
        GetSelectionRange(out int startLine, out int startCol, out int endLine, out int endCol);

        if (History != null) {
            History.Execute(new ReplaceTextCommand(this, "Delete", startLine, startCol, endLine, endCol, ""));
        } else {
            InternalDeleteRange(startLine, startCol, endLine, endCol);
        }
    }

    protected void GetSelectionRange(out int startLine, out int startCol, out int endLine, out int endCol) {
        if (_cursorLine < _selStartLine || (_cursorLine == _selStartLine && _cursorCol < _selStartCol)) {
            startLine = _cursorLine; startCol = _cursorCol;
            endLine = _selStartLine; endCol = _selStartCol;
        } else {
            startLine = _selStartLine; startCol = _selStartCol;
            endLine = _cursorLine; endCol = _cursorCol;
        }
    }

    protected virtual void EnsureCursorVisible() {
        ResetCursorBlink();
        if (GameContent.FontSystem == null || _visualLines.Count == 0) return;
        var font = GameContent.FontSystem.GetFont(FontSize);
        float lineHeight = font.LineHeight;

        int visualIdx = GetVisualLineIndex(_cursorLine, _cursorCol);
        float cursorY = visualIdx * lineHeight;

        if (cursorY < _targetScrollOffset) _targetScrollOffset = cursorY;
        if (cursorY + lineHeight > _targetScrollOffset + Size.Y - 20) _targetScrollOffset = cursorY + lineHeight - Size.Y + 20;

        if (WordWrap) {
            _targetScrollOffsetX = 0;
        } else {
            int safeCol = Math.Clamp(_cursorCol, 0, _lines[_cursorLine].Length);
            float cursorX = safeCol == 0 ? 0 : font.MeasureString(_lines[_cursorLine].Substring(0, safeCol)).X;
            if (cursorX < _targetScrollOffsetX) _targetScrollOffsetX = cursorX;
            if (cursorX > _targetScrollOffsetX + Size.X - 20) _targetScrollOffsetX = cursorX - Size.X + 20;
        }

        ClampScroll();
    }

    protected virtual void ClampScroll() {
        if (GameContent.FontSystem == null || _visualLines.Count == 0) return;
        var font = GameContent.FontSystem.GetFont(FontSize);
        float totalHeight = _visualLines.Count * font.LineHeight;
        _targetScrollOffset = Math.Clamp(_targetScrollOffset, 0, Math.Max(0, totalHeight - Size.Y + 20));

        if (WordWrap) {
            _targetScrollOffsetX = 0;
            _scrollOffsetX = 0;
        } else {
            if (_maxWidthDirty) {
                float maxW = 0;
                foreach (var l in _lines) {
                    float w = font.MeasureString(l).X;
                    if (w > maxW) maxW = w;
                }
                _cachedMaxWidth = maxW;
                _maxWidthDirty = false;
            }
            _targetScrollOffsetX = Math.Clamp(_targetScrollOffsetX, 0, Math.Max(0, _cachedMaxWidth - Size.X + 20));
        }
    }

    protected void ResetCursorBlink() {
        _cursorTimer = 0f;
        _showCursor = true;
    }

    public override void Update(GameTime gameTime) {
        base.Update(gameTime);
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (IsFocused) {
            _cursorTimer += dt;
            if (_cursorTimer >= 0.5f) { _showCursor = !_showCursor; _cursorTimer = 0f; }

            // Auto-scroll logic while dragging
            if (_isPressed && InputManager.IsMouseButtonDown(MouseButton.Left) && !_isWordSelecting) {
                // Input consumption is handled in UpdateInput()
                var mousePos = InputManager.MousePosition.ToVector2();
                var absPos = AbsolutePosition;
                float scrollSpeed = 500f * dt;
                bool moved = false;

                if (mousePos.Y < absPos.Y) { _targetScrollOffset -= scrollSpeed; moved = true; }
                else if (mousePos.Y > absPos.Y + Size.Y) { _targetScrollOffset += scrollSpeed; moved = true; }
                
                if (mousePos.X < absPos.X) { _targetScrollOffsetX -= scrollSpeed; moved = true; }
                else if (mousePos.X > absPos.X + Size.X) { _targetScrollOffsetX += scrollSpeed; moved = true; }

                if (moved) {
                    ClampScroll();
                    // Set scroll immediately so SetCursorFromMouse uses current view
                    _scrollOffset = _targetScrollOffset;
                    _scrollOffsetX = _targetScrollOffsetX;
                    SetCursorFromMouse(); 
                }
            }
        } else {
            _showCursor = false;
        }

        // Smooth scroll
        _scrollOffset = MathHelper.Lerp(_scrollOffset, _targetScrollOffset, MathHelper.Clamp(dt * 15f, 0, 1));
        if (Math.Abs(_scrollOffset - _targetScrollOffset) < 0.1f) _scrollOffset = _targetScrollOffset;

        _scrollOffsetX = MathHelper.Lerp(_scrollOffsetX, _targetScrollOffsetX, MathHelper.Clamp(dt * 15f, 0, 1));
        if (Math.Abs(_scrollOffsetX - _targetScrollOffsetX) < 0.1f) _scrollOffsetX = _targetScrollOffsetX;
    }

    protected override void DrawSelf(SpriteBatch spriteBatch, ShapeBatch batch) {
        var absPos = AbsolutePosition;

        // Background (optional)
        if (DrawBackground) {
            batch.FillRectangle(absPos, Size, BackgroundColor * AbsoluteOpacity, rounded: 3f);
            batch.BorderRectangle(absPos, Size, (IsFocused ? FocusedBorderColor : BorderColor) * AbsoluteOpacity, thickness: 1f, rounded: 3f);
        }

        if (GameContent.FontSystem == null) return;
        var font = GameContent.FontSystem.GetFont(FontSize);
        float lineHeight = font.LineHeight;

        // Clip region using GraphicsDevice scissor
        var oldScissor = G.GraphicsDevice.ScissorRectangle;
        // Float to int conversion can be tricky, use Floor and Ceiling for safety
        int scX = (int)Math.Floor(absPos.X + 2);
        int scY = (int)Math.Floor(absPos.Y + 2);
        int scW = (int)Math.Ceiling(Size.X - 4);
        int scH = (int)Math.Ceiling(Size.Y - 4);
        var scissor = new Rectangle(scX, scY, scW, scH);
        
        // Intersect with existing scissor if any
        scissor = Rectangle.Intersect(oldScissor, scissor);
        
        // We ensure we don't have an empty/invalid scissor that would cause crash or no drawing
        if (scissor.Width <= 0 || scissor.Height <= 0) return;

        // End current batches to apply scissor
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

        // Draw selection highlight
        if (HasSelection()) {
            GetSelectionRange(out int sl, out int sc, out int el, out int ec);
            
            for (int i = 0; i < _visualLines.Count; i++) {
                var vl = _visualLines[i];
                float y = textY + i * lineHeight;
                if (y + lineHeight < absPos.Y || y > absPos.Y + Size.Y) continue;

                // Determine if this visual line is within selection range
                // A visual line is selected if its logical line is in (sl, el)
                // or if it's the sl/el logical line and its start/end offsets overlap with sc/ec.
                
                int visualLogicalLine = vl.LogicalLineIndex;
                if (visualLogicalLine < sl || visualLogicalLine > el) continue;

                int startC = 0;
                int endC = vl.Length;

                if (visualLogicalLine == sl) {
                    startC = Math.Max(0, sc - vl.StartIndex);
                }
                if (visualLogicalLine == el) {
                    endC = Math.Min(vl.Length, ec - vl.StartIndex);
                }

                if (startC >= endC && visualLogicalLine == sl && visualLogicalLine == el) continue;
                if (startC >= vl.Length) continue;
                if (endC <= 0) continue;

                string visualText = _lines[vl.LogicalLineIndex].Substring(vl.StartIndex, vl.Length);
                float x1 = startC <= 0 ? 0 : font.MeasureString(visualText.Substring(0, startC)).X;
                float x2 = endC >= vl.Length ? font.MeasureString(visualText).X : font.MeasureString(visualText.Substring(0, endC)).X;

                batch.FillRectangle(new Vector2(textX + x1, y), new Vector2(x2 - x1, lineHeight), FocusedBorderColor * 0.3f * AbsoluteOpacity);
            }
        }

        // Draw lines (optimized: only visible lines)
        float currentScroll = UseInternalScrolling ? _scrollOffset : 0;
        float viewportY = UseInternalScrolling ? Size.Y : (Parent != null ? Parent.Size.Y : Size.Y);
        
        int firstVis, lastVis;
        if (!UseInternalScrolling) {
            float relativeTop = G.GraphicsDevice.ScissorRectangle.Top - absPos.Y;
            float relativeBottom = G.GraphicsDevice.ScissorRectangle.Bottom - absPos.Y;
            firstVis = (int)Math.Floor(relativeTop / lineHeight);
            lastVis = (int)Math.Ceiling(relativeBottom / lineHeight);
        } else {
            firstVis = (int)Math.Floor(currentScroll / lineHeight);
            lastVis = (int)Math.Ceiling((currentScroll + viewportY) / lineHeight);
        }

        int maxVis = Math.Max(0, _visualLines.Count - 1);
        firstVis = Math.Clamp(firstVis, 0, maxVis);
        lastVis = Math.Clamp(lastVis, 0, maxVis);

        for (int i = firstVis; i <= lastVis; i++) {
            var vl = _visualLines[i];
            float y = textY + i * lineHeight;
            if (vl.LogicalLineIndex < 0 || vl.LogicalLineIndex >= _lines.Count) continue;
            
            string lineText = _lines[vl.LogicalLineIndex];
            int start = Math.Clamp(vl.StartIndex, 0, lineText.Length);
            int length = Math.Clamp(vl.Length, 0, lineText.Length - start);
            
            string visualPart = lineText.Substring(start, length);
            if (!string.IsNullOrEmpty(visualPart)) {
                font.DrawText(batch, visualPart, new Vector2(textX, y), TextColor * AbsoluteOpacity);
            }
        }

        // Placeholder
        if (_lines.Count == 1 && string.IsNullOrEmpty(_lines[0]) && !string.IsNullOrEmpty(Placeholder)) {
            font.DrawText(batch, Placeholder, new Vector2(textX, textY), Color.Gray * 0.5f * AbsoluteOpacity);
        }

        // Cursor
        if (_showCursor && IsFocused) {
            int visualIdx = GetVisualLineIndex(_cursorLine, _cursorCol);
            if (visualIdx >= 0 && visualIdx < _visualLines.Count) {
                var vl = _visualLines[visualIdx];
                if (vl.LogicalLineIndex >= 0 && vl.LogicalLineIndex < _lines.Count) {
                    string lineText = _lines[vl.LogicalLineIndex];
                    int start = Math.Clamp(vl.StartIndex, 0, lineText.Length);
                    int col = Math.Clamp(_cursorCol, start, start + vl.Length);
                    string visualPart = lineText.Substring(start, col - start);
                    
                    float cursorX = font.MeasureString(visualPart).X;
                    float cursorY = textY + visualIdx * lineHeight;

                    // Only draw cursor if it's visible within scissor
                    if (cursorY + lineHeight >= absPos.Y && cursorY <= absPos.Y + Size.Y) {
                        batch.FillRectangle(new Vector2(textX + cursorX, cursorY), new Vector2(2, lineHeight), FocusedBorderColor * AbsoluteOpacity);
                    }
                }
            }
        }
        
        batch.End();
        spriteBatch.End();

        G.GraphicsDevice.ScissorRectangle = oldState;
        
        // Resume batches for subsequent UI elements
        batch.Begin();
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
    }

    public virtual float GetTotalHeight() {
        if (GameContent.FontSystem == null) return 0;
        var font = GameContent.FontSystem.GetFont(FontSize);
        return _visualLines.Count * font.LineHeight + 20;
    }

    public virtual float GetTotalWidth() {
        if (GameContent.FontSystem == null) return 0;
        var font = GameContent.FontSystem.GetFont(FontSize);
        if (_maxWidthDirty) {
            float maxW = 0;
            foreach (var l in _lines) {
                float w = font.MeasureString(l).X;
                if (w > maxW) maxW = w;
            }
            _cachedMaxWidth = maxW;
            _maxWidthDirty = false;
        }
        return _cachedMaxWidth + 20;
    }
}
