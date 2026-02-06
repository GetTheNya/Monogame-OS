using System;
using TheGame.Core.OS.History;

namespace TheGame.Core.UI.Controls;

public struct TextInputSelectionState {
    public int CursorPos;
    public int SelectionEnd;

    public TextInputSelectionState(TextInput input) {
        CursorPos = input.CursorPos;
        SelectionEnd = input.SelectionEnd;
    }

    public void Restore(TextInput input) {
        input.SetCursorAndSelection(CursorPos, SelectionEnd);
    }
}

public abstract class TextInputCommand : ICommand {
    protected TextInput _target;
    protected TextInputSelectionState _before;
    protected TextInputSelectionState _after;
    protected DateTime _timestamp;

    public string Description { get; protected set; }
    public bool ModifiesDocument => true;

    public TextInputCommand(TextInput target, string description) {
        _target = target;
        Description = description;
        _before = new TextInputSelectionState(target);
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
        _after = new TextInputSelectionState(_target);
    }
}

public class TextInputInsertCommand : TextInputCommand {
    private string _text;
    private int _pos;

    public TextInputInsertCommand(TextInput target, string text, int pos) : base(target, "Typing") {
        _text = text;
        _pos = pos;
    }

    public override void Execute() {
        _target.InternalInsert(_pos, _text);
        SetAfterState();
    }

    public override void Undo() {
        _target.InternalDelete(_pos, _text.Length);
        _before.Restore(_target);
    }

    public override bool CanMerge(ICommand other) {
        if (other is TextInputInsertCommand next && next._pos == _pos + _text.Length) {
            return (DateTime.Now - _timestamp).TotalSeconds < 2.0;
        }
        return false;
    }

    public override void MergeWith(ICommand other) {
        var next = (TextInputInsertCommand)other;
        _text += next._text;
        _after = next._after;
        _timestamp = DateTime.Now;
    }
}

public class TextInputDeleteCommand : TextInputCommand {
    private string _deletedText;
    private int _pos;
    private bool _isBackspace;

    public TextInputDeleteCommand(TextInput target, int pos, string text, bool isBackspace) 
        : base(target, isBackspace ? "Backspace" : "Delete") {
        _pos = pos;
        _deletedText = text;
        _isBackspace = isBackspace;
    }

    public override void Execute() {
        _target.InternalDelete(_pos, _deletedText.Length);
        SetAfterState();
    }

    public override void Undo() {
        _target.InternalInsert(_pos, _deletedText);
        _before.Restore(_target);
    }

    public override bool CanMerge(ICommand other) {
        if (other is TextInputDeleteCommand next) {
            if (_isBackspace && next._isBackspace && next._pos == _pos - next._deletedText.Length) {
                return (DateTime.Now - _timestamp).TotalSeconds < 2.0;
            }
            if (!_isBackspace && !next._isBackspace && next._pos == _pos) {
                return (DateTime.Now - _timestamp).TotalSeconds < 2.0;
            }
        }
        return false;
    }

    public override void MergeWith(ICommand other) {
        var next = (TextInputDeleteCommand)other;
        if (_isBackspace) {
            _deletedText = next._deletedText + _deletedText;
            _pos = next._pos;
        } else {
            _deletedText += next._deletedText;
        }
        _after = next._after;
        _timestamp = DateTime.Now;
    }
}

public class TextInputReplaceCommand : TextInputCommand {
    private string _oldText;
    private string _newText;
    private int _pos;
    private int _length;

    public TextInputReplaceCommand(TextInput target, string description, int pos, int length, string newText) 
        : base(target, description) {
        _pos = pos;
        _length = length;
        _newText = newText;
        _oldText = target.Value.Substring(pos, length);
    }

    public override void Execute() {
        _target.InternalReplace(_pos, _length, _newText);
        SetAfterState();
    }

    public override void Undo() {
        _target.InternalReplace(_pos, _newText.Length, _oldText);
        _before.Restore(_target);
    }
}
