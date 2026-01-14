using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Core.Input;
using TheGame.Core.UI;
using TheGame.Graphics;

namespace TheGame.Core.OS;

/// <summary>
/// Centralized drag and drop manager for the OS simulator.
/// Handles drag initiation, drop targets, snap-back, and visual feedback.
/// </summary>
public class DragDropManager {
    private static DragDropManager _instance;
    public static DragDropManager Instance => _instance ??= new DragDropManager();

    private object _dragData;
    private Vector2 _dragSourcePosition;
    private Dictionary<DesktopIcon, Vector2> _dragOriginalPositions;
    private bool _isActive;

    public bool IsActive => _isActive;
    public object DragData => _dragData;
    public Vector2 DragSourcePosition => _dragSourcePosition;

    private DragDropManager() {
        _dragOriginalPositions = new Dictionary<DesktopIcon, Vector2>();
    }

    /// <summary>
    /// Begins a drag operation with the specified data.
    /// </summary>
    /// <param name="data">The data being dragged (DesktopIcon, string path, List<string>, etc.)</param>
    /// <param name="sourcePosition">Original position for snap-back if drop fails</param>
    public void BeginDrag(object data, Vector2 sourcePosition) {
        if (_isActive) {
            DebugLogger.Log("Warning: BeginDrag called while drag already active");
            return;
        }

        _dragData = data;
        _dragSourcePosition = sourcePosition;
        _isActive = true;
        _dragOriginalPositions.Clear();
    }

    /// <summary>
    /// Stores original positions for icons involved in a multi-drag operation.
    /// Used for snap-back if the drop is invalid.
    /// </summary>
    public void StoreIconPosition(DesktopIcon icon, Vector2 position) {
        if (!_dragOriginalPositions.ContainsKey(icon)) {
            _dragOriginalPositions[icon] = position;
        }
    }

    /// <summary>
    /// Gets stored positions for snap-back.
    /// </summary>
    public Dictionary<DesktopIcon, Vector2> GetStoredPositions() => _dragOriginalPositions;
    
    /// <summary>
    /// Checks if a specific item is currently being dragged.
    /// </summary>
    public bool IsItemDragged(object item) {
        if (!_isActive || _dragData == null) return false;
        if (_dragData == item) return true;
        
        // Check if item is a path inside a dragged list
        if (item is string path && _dragData is List<string> list) {
            return list.Contains(path);
        }
        
        return false;
    }

    /// <summary>
    /// Ends the drag operation successfully (drop was handled).
    /// </summary>
    public void EndDrag() {
        _dragData = null;
        _isActive = false;
        _dragOriginalPositions.Clear();
    }

    /// <summary>
    /// Cancels the drag operation and triggers snap-back behavior.
    /// </summary>
    public void CancelDrag() {
        if (!_isActive) return;

        // Restore original positions for any stored icons
        foreach (var kvp in _dragOriginalPositions) {
            kvp.Key.Position = kvp.Value;
        }

        EndDrag();
    }

    /// <summary>
    /// Draws the drag visual feedback at the mouse cursor.
    /// Should be called during the main draw loop.
    /// </summary>
    public void DrawDragVisual(SpriteBatch sb, ShapeBatch sbatch) {
        if (!_isActive || _dragData == null) return;

        Shell.IsRenderingDrag = true;
        try {
            Vector2 mousePos = InputManager.MousePosition.ToVector2();

            if (_dragData is string path) {
                Texture2D icon = Shell.GetIcon(path);
                float scale = 48f / (float)icon.Width;
                sb.Draw(icon, mousePos, null, Color.White * 0.8f, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                
                var name = System.IO.Path.GetFileName(path);
                if (!string.IsNullOrEmpty(name)) {
                     var font = GameContent.FontSystem.GetFont(18);
                     font.DrawText(sbatch, name, mousePos + new Vector2(40, 10), Color.White);
                }
            } 
            else if (_dragData is DesktopIcon dIcon) {
                // Draw icon texture at mouse
                float iconSize = 48f;
                Vector2 iconPos = mousePos;
                
                if (dIcon.Icon != null) {
                    sb.Draw(dIcon.Icon, new Rectangle((int)iconPos.X, (int)iconPos.Y, (int)iconSize, (int)iconSize), Color.White * 0.7f);
                } else {
                    sbatch.FillRectangle(iconPos, new Vector2(iconSize, iconSize), new Color(200, 200, 200, 100));
                }

                // Draw label at mouse
                if (!string.IsNullOrEmpty(dIcon.Label)) {
                    var font = GameContent.FontSystem.GetFont(14);
                    var textSize = font.MeasureString(dIcon.Label);
                    Vector2 labelPos = mousePos + new Vector2((iconSize - textSize.X) / 2, iconSize + 5);
                    font.DrawText(sbatch, dIcon.Label, labelPos, Color.White * 0.9f);
                }
            }
            else if (_dragData is List<string> list && list.Count > 0) {
                 var first = list[0];
                 Texture2D icon = Shell.GetIcon(first);
                 float scale = 48f / (float)icon.Width;
                 
                 // Draw stack
                 for(int i=2; i>=0; i--) {
                      Vector2 offset = new Vector2(i*5, i*5);
                      sb.Draw(icon, mousePos + offset, null, Color.White * (0.8f - (i*0.2f)), 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                 }
                 
                 var font = GameContent.FontSystem.GetFont(18);
                 font.DrawText(sbatch, $"{list.Count} items", mousePos + new Vector2(50, 10), Color.White);
            }
        } finally {
            Shell.IsRenderingDrag = false;
        }
    }
}
