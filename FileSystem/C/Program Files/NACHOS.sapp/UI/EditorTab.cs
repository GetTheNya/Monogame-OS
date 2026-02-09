using System;
using Microsoft.Xna.Framework;
using TheGame.Core.UI.Controls;
using TheGame.Core.OS.History;

namespace NACHOS;

public class EditorTab : NachosTab {
    private CodeEditor _editor;
    public CodeEditor Editor => _editor;
    public override CommandHistory History => _editor.History;

    public override bool IsDirty => History.IsDirty;
    public override string DisplayTitle => _editor.FileName + (IsDirty ? "*" : "");

    public EditorTab(Vector2 position, Vector2 size, string filePath) : base(position, size, filePath) {
        _editor = new CodeEditor(Vector2.Zero, size, filePath);
        _editor.OnDirtyChanged += () => OnDirtyChanged?.Invoke();
        History.OnHistoryChanged += () => OnDirtyChanged?.Invoke();
        _editor.OnValueChanged += (val) => OnContentChanged?.Invoke();
        _editor.OnCursorMoved += () => OnSelectionChanged?.Invoke();
        _editor.UseInternalScrolling = false;
        AddChild(_editor);
    }

    public override void Save() => _editor.Save();

    public override void Undo() => _editor.Undo();
    public override void Redo() => _editor.Redo();

    public override void UpdateLayout() {
        if (Parent == null) return;
        
        Vector2 viewportSize = Parent.Size;
        _editor.Size = new Vector2(
            Math.Max(viewportSize.X, _editor.GetTotalWidth()),
            Math.Max(viewportSize.Y, _editor.GetTotalHeight())
        );
        this.Size = _editor.Size;
    }

    public override void Dispose() {
        _editor?.Dispose();
        base.Dispose();
    }
}
