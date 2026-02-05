using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TheGame.Core.OS.DragDrop;

/// <summary>
/// Immutable data class that provides information about the current drag operation.
/// </summary>
public class DragInfo {
    /// <summary>The data being dragged (string path, List&lt;string&gt;, IDraggable, etc.)</summary>
    public object Data { get; init; }
    
    /// <summary>Original position where drag started (for snap-back)</summary>
    public Vector2 SourcePosition { get; init; }
    
    /// <summary>Offset from top-left where the mouse grabbed the item</summary>
    public Vector2 GrabOffset { get; init; }
    
    /// <summary>True if dragging multiple items (List&lt;string&gt;)</summary>
    public bool IsMultiItem { get; init; }
    
    /// <summary>Number of items being dragged</summary>
    public int ItemCount {
        get {
            if (Data is List<string> list) return list.Count;
            return 1;
        }
    }
    
    /// <summary>
    /// Gets the file paths being dragged.
    /// Works with string, List&lt;string&gt;, and IDraggable sources.
    /// </summary>
    public IReadOnlyList<string> GetPaths() {
        if (Data is List<string> list) return list.AsReadOnly();
        if (Data is string path) return new[] { path };
        if (Data is IDraggable draggable) {
            var dragData = draggable.GetDragData();
            if (dragData is string p) return new[] { p };
            if (dragData is List<string> l) return l.AsReadOnly();
        }
        return Array.Empty<string>();
    }
    
    /// <summary>Gets the draggable source if applicable</summary>
    public IDraggable GetDraggable() => Data as IDraggable;
}
