using System;
using Microsoft.Xna.Framework;
using TheGame.Core.UI.Controls;
using TheGame.Core.OS.History;

namespace NACHOS;

public abstract class NachosTab : UIControl, IDisposable {
    public string FilePath { get; set; }
    public abstract bool IsDirty { get; }
    public Action OnDirtyChanged { get; set; }

    public abstract string DisplayTitle { get; }

    public Action OnContentChanged { get; set; }
    public Action OnSelectionChanged { get; set; }
    
    public abstract CommandHistory History { get; }

    protected NachosTab(Vector2 position, Vector2 size, string filePath) : base(position, size) {
        FilePath = filePath;
    }

    public abstract void Save();
    
    public virtual void UpdateLayout() {
        // Base implementation does nothing, can be overridden by tabs to handle internal resizing
    }

    public virtual void Dispose() {
        // Cleanup if needed in subclasses
    }
}
