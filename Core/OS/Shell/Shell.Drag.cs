using Microsoft.Xna.Framework;

namespace TheGame.Core.OS;

public static partial class Shell {
    /// <summary>
    /// Drag visual customization API
    /// </summary>
    public static class Drag {
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
        /// Set to null to hide the preview.
        /// </summary>
        public static void SetDropPreview(object id, Vector2? position)
            => DragDropManager.Instance.SetDropPreviewPosition(id, position);
    }
}
