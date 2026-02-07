using System;
using TheGame.Core.OS.History;

namespace TheGame.Core.UI.Controls;

public struct SelectionState {
    public int CursorLine;
    public int CursorCol;
    public int SelStartLine;
    public int SelStartCol;

    public SelectionState(TextArea area) {
        CursorLine = area.CursorLine;
        CursorCol = area.CursorCol;
        SelStartLine = area.SelStartLine;
        SelStartCol = area.SelStartCol;
    }

    public void Restore(TextArea area) {
        area.SetCursorAndSelection(CursorLine, CursorCol, SelStartLine, SelStartCol);
    }
}

public abstract class TextCommand : ICommand {
    protected TextArea _target;
    protected SelectionState _before;
    protected SelectionState _after;
    protected DateTime _timestamp;

    public string Description { get; protected set; }
    public bool ModifiesDocument => true;

    public TextCommand(TextArea target, string description) {
        _target = target;
        Description = description;
        _before = new SelectionState(target);
        _timestamp = DateTime.Now;
    }

    public abstract void Execute();
    public abstract void Undo();

    public virtual bool CanMerge(ICommand other) => false;
    public virtual void MergeWith(ICommand other) => throw new NotSupportedException();

    public void Dispose() {
        _target = null;
    }

    protected void SetAfterState() {
        _after = new SelectionState(_target);
    }
}

public class InsertTextCommand : TextCommand {
    private string _text;
    private int _line;
    private int _col;

    public InsertTextCommand(TextArea target, string text, int line, int col) : base(target, "Typing") {
        _text = (text ?? "").Replace("\r\n", "\n").Replace("\r", "\n");
        _line = line;
        _col = col;
    }

    public override void Execute() {
        if (_target.IsReadOnly) return;
        _target.InternalInsertText(_line, _col, _text);
        SetAfterState();
    }

    public override void Undo() {
        _target.InternalDeleteRange(_line, _col, _text.Length);
        _before.Restore(_target);
    }

    public override bool CanMerge(ICommand other) {
        if (other is InsertTextCommand next && next._line == _line && next._col == _col + _text.Length) {
            // Only merge simple characters, break on whitespace or special chars if needed, 
            // but for simplicity let's just use a time limit.
            return (DateTime.Now - _timestamp).TotalSeconds < 2.0;
        }
        return false;
    }

    public override void MergeWith(ICommand other) {
        var next = (InsertTextCommand)other;
        _text += next._text;
        _after = next._after;
        _timestamp = DateTime.Now;
    }
}

public class DeleteTextCommand : TextCommand {
    private string _deletedText;
    private int _line;
    private int _col;
    private bool _isBackspace;

    public DeleteTextCommand(TextArea target, int line, int col, string text, bool isBackspace) 
        : base(target, isBackspace ? "Backspace" : "Delete") {
        _line = line;
        _col = col;
        _deletedText = (text ?? "").Replace("\r\n", "\n").Replace("\r", "\n");
        _isBackspace = isBackspace;
    }

    public override void Execute() {
        if (_target.IsReadOnly) return;
        _target.InternalDeleteRange(_line, _col, _deletedText.Length);
        SetAfterState();
    }

    public override void Undo() {
        _target.InternalInsertText(_line, _col, _deletedText);
        _before.Restore(_target);
    }

    public override bool CanMerge(ICommand other) {
        if (other is DeleteTextCommand next) {
            if (_isBackspace && next._isBackspace && next._line == _line && next._col == _col - next._deletedText.Length) {
                return (DateTime.Now - _timestamp).TotalSeconds < 2.0;
            }
            if (!_isBackspace && !next._isBackspace && next._line == _line && next._col == _col) {
                return (DateTime.Now - _timestamp).TotalSeconds < 2.0;
            }
        }
        return false;
    }

    public override void MergeWith(ICommand other) {
        var next = (DeleteTextCommand)other;
        if (_isBackspace) {
            _deletedText = next._deletedText + _deletedText;
            _line = next._line;
            _col = next._col;
        } else {
            _deletedText += next._deletedText;
        }
        _after = next._after;
        _timestamp = DateTime.Now;
    }
}

public class ReplaceTextCommand : TextCommand {
    private string _oldText;
    private string _newText;
    private int _startLine;
    private int _startCol;
    private int _endLine;
    private int _endCol;

    public ReplaceTextCommand(TextArea target, string description, int sl, int sc, int el, int ec, string newText) 
        : base(target, description) {
        target.SortRange(ref sl, ref sc, ref el, ref ec);
        _startLine = sl; _startCol = sc;
        _endLine = el; _endCol = ec;
        _newText = (newText ?? "").Replace("\r\n", "\n").Replace("\r", "\n");
        _oldText = target.InternalGetRange(_startLine, _startCol, _endLine, _endCol);
    }

    public override void Execute() {
        if (_target.IsReadOnly) return;
        _target.InternalReplaceRange(_startLine, _startCol, _endLine, _endCol, _newText);
        SetAfterState();
    }

    public override void Undo() {
        int newEndLine, newEndCol;
        _target.InternalGetPositionAfter(_startLine, _startCol, _newText, out newEndLine, out newEndCol);
        
        // Ensure the range we are replacing is valid and sorted
        int sl = _startLine;
        int sc = _startCol;
        int el = newEndLine;
        int ec = newEndCol;
        
        _target.InternalReplaceRange(sl, sc, el, ec, _oldText);
        _before.Restore(_target);
    }
}
