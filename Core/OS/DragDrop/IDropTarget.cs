using Microsoft.Xna.Framework;

namespace TheGame.Core.OS.DragDrop;

/// <summary>
/// Interface for UI elements that can accept drag-drop operations.
/// Provides hooks for hover feedback and drop handling.
/// </summary>
public interface IDropTarget {
    /// <summary>
    /// Checks if this target can accept the dragged data.
    /// Called once when drag enters the target bounds.
    /// </summary>
    /// <param name="dragData">The data being dragged</param>
    /// <returns>True if this target can accept the data</returns>
    bool CanAcceptDrop(object dragData);
    
    /// <summary>
    /// Called continuously while dragged item hovers over this target.
    /// Use this to show visual feedback and determine the drop effect.
    /// </summary>
    /// <param name="dragData">The data being dragged</param>
    /// <param name="position">Current mouse/drag position in screen coordinates</param>
    /// <returns>The effect that will occur (None, Copy, Move, Link). This determines the cursor visual indicator.</returns>
    DragDropEffect OnDragOver(object dragData, Vector2 position);
    
    /// <summary>
    /// Called when dragged item leaves this target's bounds.
    /// Use this to hide visual feedback.
    /// </summary>
    void OnDragLeave();
    
    /// <summary>
    /// Called when item is dropped on this target.
    /// Perform the actual drop operation here.
    /// </summary>
    /// <param name="dragData">The data being dragged</param>
    /// <param name="position">Drop position in screen coordinates</param>
    /// <returns>True if drop was handled successfully, false to cancel the drag</returns>
    bool OnDrop(object dragData, Vector2 position);
    
    /// <summary>
    /// Gets the bounds of this drop target for hit testing.
    /// </summary>
    /// <returns>Bounding rectangle in screen coordinates</returns>
    Rectangle GetDropBounds();
}
