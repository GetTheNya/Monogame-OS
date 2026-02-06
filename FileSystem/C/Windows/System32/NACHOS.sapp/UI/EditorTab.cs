using System;
using Microsoft.Xna.Framework;
using TheGame.Core.UI.Controls;

namespace NACHOS;

public class EditorTab : NachosTab {
    private CodeEditor _editor;
    public CodeEditor Editor => _editor;

    public override bool IsDirty => _editor.IsDirty;
    public override string DisplayTitle => _editor.FileName + (IsDirty ? "*" : "");

    public EditorTab(Vector2 position, Vector2 size, string filePath) : base(position, size, filePath) {
        _editor = new CodeEditor(Vector2.Zero, size, filePath);
        _editor.OnDirtyChanged += () => OnDirtyChanged?.Invoke();
        _editor.OnValueChanged += (val) => OnContentChanged?.Invoke();
        _editor.OnCursorMoved += () => OnSelectionChanged?.Invoke();
        _editor.UseInternalScrolling = false;
        AddChild(_editor);
    }

    public override void Save() => _editor.Save();

    public override void UpdateLayout() {
        if (Parent == null) return;
        
        _editor.Size = new Vector2(
            Math.Max(Size.X, _editor.GetTotalWidth()),
            Math.Max(Size.Y, _editor.GetTotalHeight())
        );
    }

    public override void Dispose() {
        _editor?.Dispose();
        base.Dispose();
    }
}
