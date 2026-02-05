using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Core.UI;

namespace TheGame.Core.OS.DragDrop;

/// <summary>
/// Interface for objects that can be dragged.
/// Provides drag data, visual representation, and lifecycle hooks.
/// </summary>
public interface IDraggable {
    /// <summary>
    /// Gets the data to be transferred in the drag operation.
    /// This can be a file path, list of paths, or custom data.
    /// </summary>
    object GetDragData();
    
    /// <summary>
    /// Returns the icon to display while dragging (can be null).
    /// Only used if GetCustomDragVisual() returns null.
    /// </summary>
    Texture2D GetDragIcon();
    
    /// <summary>
    /// Returns the text label to display below the icon (can be null).
    /// Only used if GetCustomDragVisual() returns null.
    /// </summary>
    string GetDragLabel();
    
    /// <summary>
    /// Returns a custom UIElement to render during drag (optional).
    /// If this returns a non-null element, it will be used instead of GetDragIcon/GetDragLabel.
    /// The element should be self-contained and positioned at (0,0) - it will be moved to cursor.
    /// </summary>
    UIElement GetCustomDragVisual() => null;
    
    /// <summary>
    /// Called when drag operation begins.
    /// Use this to provide visual feedback (e.g., make item semi-transparent).
    /// </summary>
    /// <param name="grabOffset">Offset from element's origin where mouse grabbed</param>
    void OnDragStart(Vector2 grabOffset);
    
    /// <summary>
    /// Called when drag operation completes successfully (item was dropped).
    /// Use this to restore visual state.
    /// </summary>
    void OnDragEnd();
    
    /// <summary>
    /// Called when drag operation is cancelled (dropped on invalid target).
    /// Use this to restore visual state and trigger snap-back animation.
    /// </summary>
    void OnDragCancel();
}
