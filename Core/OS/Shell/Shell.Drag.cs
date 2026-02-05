using Microsoft.Xna.Framework;
using System.Collections.Generic;
using TheGame.Core.OS.DragDrop;
using TheGame.Core.UI;

namespace TheGame.Core.OS;

public static partial class Shell {
    /// <summary>
    /// Drag and drop API for the shell.
    /// Supports both simple object-based dragging and interface-based type-safe dragging.
    /// </summary>
    public static class Drag {
        // ==================== Core Operations ====================
        
        /// <summary>
        /// Gets whether a drag operation is currently active.
        /// </summary>
        public static bool IsActive => DragDropManager.Instance.IsActive;
        
        /// <summary>
        /// Gets or sets the currently dragged item. Setting to null ends the drag.
        /// Note: For proper snap-back behavior, use Begin() with source position.
        /// </summary>
        public static object DraggedItem {
            get => DragDropManager.Instance.DragData;
            set {
                if (value != null) DragDropManager.Instance.BeginDrag(value, Vector2.Zero, Vector2.Zero);
                else DragDropManager.Instance.EndDrag();
            }
        }
        
        /// <summary>
        /// Begins a drag operation with source position for snap-back.
        /// </summary>
        /// <param name="data">Data to drag (string path, List&lt;string&gt;, IDraggable, etc.)</param>
        /// <param name="sourcePosition">Original position for snap-back if cancelled</param>
        /// <param name="grabOffset">Offset from top-left where mouse grabbed the item</param>
        public static void Begin(object data, Vector2 sourcePosition, Vector2 grabOffset)
            => DragDropManager.Instance.BeginDrag(data, sourcePosition, grabOffset);
        
        /// <summary>
        /// Begins a drag operation with an IDraggable source.
        /// Automatically calls OnDragStart on the source.
        /// </summary>
        public static void BeginDraggable(IDraggable source, Vector2 sourcePosition, Vector2 grabOffset) {
            source.OnDragStart(grabOffset);
            DragDropManager.Instance.BeginDrag(source, sourcePosition, grabOffset);
        }
        
        /// <summary>
        /// Ends the current drag operation (successful drop).
        /// </summary>
        public static void End() {
            // Notify draggable source if applicable
            if (DragDropManager.Instance.DragData is IDraggable draggable) {
                draggable.OnDragEnd();
            }
            DragDropManager.Instance.EndDrag();
        }
        
        /// <summary>
        /// Cancels the drag and restores original positions.
        /// </summary>
        public static void Cancel()
            => DragDropManager.Instance.CancelDrag(); // Manager handles IDraggable.OnDragCancel
        
        /// <summary>
        /// Checks if a specific item is currently being dragged.
        /// </summary>
        public static bool IsItemDragged(object item)
            => DragDropManager.Instance.IsItemDragged(item);
        
        // ==================== Drag State Access ====================
        
        /// <summary>
        /// Gets or sets the current drag-drop effect for visual feedback.
        /// Automatically set by IDropTarget.OnDragOver calls.
        /// </summary>
        public static DragDropEffect CurrentEffect {
            get => DragDropManager.Instance.CurrentEffect;
            set => DragDropManager.Instance.CurrentEffect = value;
        }
        
        /// <summary>
        /// Gets the original source position where drag started.
        /// </summary>
        public static Vector2 SourcePosition 
            => DragDropManager.Instance.DragSourcePosition;
        
        /// <summary>
        /// Gets the offset from top-left where mouse grabbed the item.
        /// </summary>
        public static Vector2 GrabOffset 
            => DragDropManager.Instance.DragGrabOffset;
        
        /// <summary>
        /// Gets the current mouse position adjusted for the grab offset.
        /// Useful for calculating drop positions.
        /// </summary>
        public static Vector2 AdjustedMousePosition
            => DragDropManager.Instance.GetAdjustedMousePosition();
        
        /// <summary>
        /// Gets complete information about the current drag operation.
        /// Returns null if no drag is active.
        /// </summary>
        public static DragInfo GetInfo()
            => DragDropManager.Instance.GetDragInfo();
        
        // ==================== Position Management ====================
        
        /// <summary>
        /// Stores a position for custom snap-back behavior.
        /// </summary>
        /// <param name="key">Identifier for the position (e.g., icon, path)</param>
        /// <param name="position">Position to store</param>
        public static void StorePosition(object key, Vector2 position)
            => DragDropManager.Instance.StoreCustomPosition(key, position);
        
        /// <summary>
        /// Gets all stored positions for snap-back.
        /// Note: This returns DesktopIcon positions. For custom keys, use StorePosition.
        /// </summary>
        public static Dictionary<DesktopIcon, Vector2> GetStoredPositions()
            => DragDropManager.Instance.GetStoredPositions();
        
        // ==================== Visual Customization ====================
        
        /// <summary>
        /// Sets the opacity for drag visuals (0.1 to 1.0).
        /// </summary>
        public static void SetVisualOpacity(float opacity) 
            => DragDropManager.Instance.SetDragOpacity(opacity);
        
        /// <summary>
        /// Sets whether to show item count badge for multi-item drags.
        /// </summary>
        public static void SetShowItemCount(bool show) 
            => DragDropManager.Instance.SetShowItemCount(show);
        
        /// <summary>
        /// Sets the position where drop preview should be rendered.
        /// Set position to null to hide the preview for this id.
        /// </summary>
        /// <param name="id">Identifier for the preview (icon, path, etc.)</param>
        /// <param name="position">Position to show preview, or null to hide</param>
        public static void SetDropPreview(object id, Vector2? position)
            => DragDropManager.Instance.SetDropPreviewPosition(id, position);
        
        // ==================== Drop Target Helpers ====================
        
        /// <summary>
        /// Checks if the current drag can be accepted by a drop target.
        /// Also sets the current effect based on OnDragOver return value.
        /// Use this in your Update() method for continuous hover feedback.
        /// </summary>
        public static DragDropEffect CheckDropTarget(IDropTarget target, Vector2 position) {
            if (!IsActive || target == null) {
                CurrentEffect = DragDropEffect.None;
                return DragDropEffect.None;
            }
            
            if (!target.CanAcceptDrop(DraggedItem)) {
                CurrentEffect = DragDropEffect.None;
                return DragDropEffect.None;
            }
            
            var effect = target.OnDragOver(DraggedItem, position);
            CurrentEffect = effect;
            return effect;
        }
        
        /// <summary>
        /// Checks if the current drag can be accepted by a drop target (legacy).
        /// For new code, prefer CheckDropTarget which also updates visual feedback.
        /// </summary>
        public static bool CanDropOn(IDropTarget target) {
            if (!IsActive || target == null) return false;
            return target.CanAcceptDrop(DraggedItem);
        }
        
        /// <summary>
        /// Attempts to drop on a target if it can accept the drop.
        /// </summary>
        /// <returns>True if drop was handled successfully.</returns>
        public static bool TryDropOn(IDropTarget target, Vector2 dropPosition) {
            if (!CanDropOn(target)) return false;
            bool handled = target.OnDrop(DraggedItem, dropPosition);
            if (handled) End();
            else CurrentEffect = DragDropEffect.None; // Reset if drop failed
            return handled;
        }
    }
}
