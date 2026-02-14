using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Core.Input;
using TheGame.Core.UI;
using TheGame.Graphics;
using TheGame.Core.OS.DragDrop;
using TheGame.Core.Animation;  // For Tween

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
    private Dictionary<object, Vector2> _customPositions = new(); // For non-DesktopIcon snap-back
    private bool _isActive;
    private Vector2 _dragGrabOffset;
    private DragDropEffect _currentEffect = DragDropEffect.None;
    
    // Cached drag visuals
    private List<Texture2D> _cachedDragIcons = new();
    private string _cachedDragLabel;
    private UIElement _cachedCustomVisual;  // NEW: Custom UI element for drag visual
    
    // Visual customization
    private float _dragOpacity = 0.7f;
    private bool _showItemCount = true;
    
    // Snap-back animation
    private Tween _snapBackTween;
    private Vector2 _animatedDragPosition;  // Current position during snap-back
    private Dictionary<object, Vector2> _dropPreviews = new();

    public bool IsActive => _isActive;
    public object DragData => _dragData;
    public Vector2 DragGrabOffset => _dragGrabOffset;
    public Vector2 DragSourcePosition => _dragSourcePosition;
    public bool IsSnapBackAnimating => _snapBackTween != null;  // NEW: Check if snap-back is running
    
    /// <summary>
    /// Gets or sets the current drag-drop effect for visual feedback.
    /// This is automatically set by IDropTarget.OnDragOver calls.
    /// </summary>
    public DragDropEffect CurrentEffect {
        get => _currentEffect;
        set => _currentEffect = value;
    }
    
    /// <summary>
    /// Updates snap-back animation. Call each frame.
    /// </summary>
    public void Update(GameTime gameTime) {
        if (_snapBackTween != null) {
            _snapBackTween.Update((float)gameTime.ElapsedGameTime.TotalSeconds);
        }

        // Ensure custom visual is updated so labels/dynamic elements calculate their layout
        if (_isActive && _cachedCustomVisual != null) {
            _cachedCustomVisual.Update(gameTime);
        }
    }

    private DragDropManager() {
        _dragOriginalPositions = new Dictionary<DesktopIcon, Vector2>();
    }

    /// <summary>
    /// Begins a drag operation with the specified data.
    /// </summary>
    /// <param name="data">The data being dragged (DesktopIcon, string path, List<string>, etc.)</param>
    /// <param name="sourcePosition">Original position for snap-back if drop fails</param>
    /// <param name="grabOffset">Offset of the mouse from the icon's top-left at start of drag</param>
    public void BeginDrag(object data, Vector2 sourcePosition, Vector2 grabOffset) {
        if (_isActive) {
            DebugLogger.Log("Warning: BeginDrag called while drag already active");
            return;
        }

        _dragData = data;
        _dragSourcePosition = sourcePosition;
        _dragGrabOffset = grabOffset;
        _isActive = true;
        _dragOriginalPositions.Clear();
        _dropPreviews.Clear();
        
        // Cache icons and label at drag start - NOT in DrawDragVisual()
        _cachedDragIcons.Clear();
        _cachedDragLabel = null;
        _cachedCustomVisual = null;  // Clear custom visual
        
        // Check for IDraggable first (new interface-based API)
        if (data is IDraggable draggable) {
            // Try to get custom visual first
            _cachedCustomVisual = draggable.GetCustomDragVisual();
            
            // Fall back to icon/label if no custom visual
            if (_cachedCustomVisual == null) {
                var icon = draggable.GetDragIcon();
                if (icon != null) {
                    _cachedDragIcons.Add(icon);
                }
                _cachedDragLabel = draggable.GetDragLabel();
            }
            
            // Notify drag start
            draggable.OnDragStart(grabOffset);
        }
        // Legacy types
        else if (data is string path) {
            _cachedDragIcons.Add(Shell.GetIcon(path));
            _cachedDragLabel = System.IO.Path.GetFileName(path);
        } else if (data is List<string> list && list.Count > 0) {
            // Cache up to 3 icons for the visual stack
            for (int i = 0; i < Math.Min(3, list.Count); i++) {
                _cachedDragIcons.Add(Shell.GetIcon(list[i]));
            }
            _cachedDragLabel = ""; // Don't show "X items" label since we have the badge
        } else if (data is DesktopIcon dIcon) {
            _cachedDragIcons.Add(dIcon.Icon);
            _cachedDragLabel = dIcon.Label;
        }
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
        _currentEffect = DragDropEffect.None;
        _dragOriginalPositions.Clear();
        _customPositions.Clear();
        _dropPreviews.Clear();
    }

    /// <summary>
    /// Cancels the current drag operation with snap-back animation.
    /// </summary>
    public void CancelDrag() {
        if (!_isActive) return;

        // Notify draggable source IMMEDIATELY to restore visual state
        if (_dragData is IDraggable draggable) {
            draggable.OnDragCancel();
        }

        // Calculate snap-back target (where we grabbed from)
        Vector2 targetPos = _dragSourcePosition + _dragGrabOffset;
        Vector2 currentPos = InputManager.MousePosition.ToVector2();
        
        // Create snap-back tween with EaseOutBack for nice overshoot effect
        _animatedDragPosition = currentPos;
        _snapBackTween = new Tween(
            this,
            (Vector2 pos) => _animatedDragPosition = pos,
            currentPos,
            targetPos,
            0.3f,  // 300ms
            Easing.EaseOutQuad
        ).OnCompleteAction(() => {
            // Animation complete - cleanup only (draggable already notified)
            _snapBackTween = null;
            EndDrag();
        });
    }
    
    /// <summary>
    /// Gets information about the current drag operation.
    /// Returns null if no drag is active.
    /// </summary>
    public DragInfo GetDragInfo() {
        if (!_isActive) return null;
        return new DragInfo {
            Data = _dragData,
            SourcePosition = _dragSourcePosition,
            GrabOffset = _dragGrabOffset,
            IsMultiItem = _dragData is List<string>
        };
    }
    
    /// <summary>
    /// Stores a custom position for snap-back behavior.
    /// Useful for non-DesktopIcon draggable items.
    /// </summary>
    public void StoreCustomPosition(object key, Vector2 position) {
        if (key != null) {
            _customPositions[key] = position;
        }
    }
    
    /// <summary>
    /// Gets the current mouse position adjusted for drag offset.
    /// Useful for calculating drop positions.
    /// </summary>
    public Vector2 GetAdjustedMousePosition() {
        return InputManager.MousePosition.ToVector2() - _dragGrabOffset;
    }
    
    /// <summary>
    /// Sets the opacity for drag visuals.
    /// </summary>
    internal void SetDragOpacity(float opacity) {
        _dragOpacity = MathHelper.Clamp(opacity, 0.1f, 1.0f);
    }
    
    /// <summary>
    /// Sets whether to show item count for multi-item drags.
    /// </summary>
    internal void SetShowItemCount(bool show) {
        _showItemCount = show;
    }
    
    /// <summary>
    /// Sets the position where drop preview should be rendered.
    /// Set to null to hide the preview.
    /// </summary>
    internal void SetDropPreviewPosition(object id, Vector2? position) {
        if (position == null) _dropPreviews.Remove(id);
        else _dropPreviews[id] = position.Value;
    }

    /// <summary>
    /// Draws the drag visual feedback at the mouse cursor.
    /// Should be called during the main draw loop.
    /// </summary>
    public void DrawDragVisual(SpriteBatch sb, ShapeBatch sbatch) {
        if (!_isActive || _dragData == null) return;

        // === CUSTOM VISUAL RENDERING ===
        if (_cachedCustomVisual != null) {
            Shell.IsRenderingDrag = true;
            try {
                // Use animated position during snap-back, otherwise use mouse
                Vector2 currentPos = _snapBackTween != null ? _animatedDragPosition : InputManager.MousePosition.ToVector2();
                Vector2 drawPos = currentPos - _dragGrabOffset;
                _cachedCustomVisual.Position = drawPos;
                
                // Render the custom UI element
                _cachedCustomVisual.Draw(sb, sbatch);
                
                // Draw effect indicator (but not during snap-back)
                if (_snapBackTween == null) {
                    DrawEffectIndicator(sb, sbatch, drawPos);
                }
            } finally {
                Shell.IsRenderingDrag = false;
            }
            return;  // Skip default icon rendering
        }

        // === DEFAULT ICON/LABEL RENDERING ===
        if (_cachedDragIcons.Count == 0 || _cachedDragIcons[0] == null) return;

        Shell.IsRenderingDrag = true;
        try {
            // Draw drop previews at target positions
            foreach (var kvp in _dropPreviews) {
                // Determine icon based on data type (path or DesktopIcon)
                Texture2D previewIcon = _cachedDragIcons[0];
                string label = _cachedDragLabel;
                
                if (kvp.Key is string path) {
                    previewIcon = Shell.GetIcon(path) ?? _cachedDragIcons[0];
                    label = System.IO.Path.GetFileName(path.TrimEnd('\\'));
                } else if (kvp.Key is DesktopIcon di) {
                    previewIcon = di.Icon ?? _cachedDragIcons[0];
                    label = di.Label;
                }

                float iconSize = 48f; // Assuming standard icon size for calculation
                float previewScale = iconSize / (float)previewIcon.Width;
                
                // If this preview is for the item at the cursor, skip the main visual to avoid double-drawing
                bool isLeader = IsLeader(kvp.Key);

                // Draw the ghost with label matching DesktopIcon style
                // Use uniform opacity for all icons in the group
                DrawDragIcon(sb, sbatch, kvp.Value, _dragOpacity, false, showLabel: true, iconOverride: previewIcon, labelOverride: label);
            }

            // Only draw the cursor ghost if no leader was drawn as a preview
            // This prevents "two icons drawing near mouse"
            bool leaderDrawn = false;
            foreach(var kvp in _dropPreviews) {
                if (IsLeader(kvp.Key)) {
                    leaderDrawn = true;
                    break;
                }
            }

            if (!leaderDrawn) {
                // Use animated position during snap-back, otherwise use mouse
                Vector2 currentPos = _snapBackTween != null ? _animatedDragPosition : InputManager.MousePosition.ToVector2();
                Vector2 drawPos = currentPos - _dragGrabOffset;
                float iconSize = 48f;
                float scale = iconSize / (float)_cachedDragIcons[0].Width;
                bool isMultiItem = _dragData is List<string>;
                
                // Only show badge (item count) if we AREN'T currently showing drop previews (ghosts)
                // This prevents the flickering "count circle" distraction on the desktop
                bool showBadge = _dropPreviews.Count == 0;
                DrawDragIcon(sb, sbatch, drawPos, _dragOpacity, isMultiItem, showLabel: showBadge);
                
                // Draw effect indicator based on current effect (but not during snap-back)
                if (_snapBackTween == null) {
                    DrawEffectIndicator(sb, sbatch, drawPos);
                }
            }


        } finally {
            Shell.IsRenderingDrag = false;
        }
    }

    private void DrawDragIcon(SpriteBatch sb, ShapeBatch sbatch, Vector2 position, float opacity, bool isMultiItem, bool showLabel, Texture2D iconOverride = null, string labelOverride = null) {
        float iconSize = 48f;
        Texture2D icon = iconOverride ?? (_cachedDragIcons.Count > 0 ? _cachedDragIcons[0] : null);
        if (icon == null) return;
        
        float mainScale = iconSize / Math.Max(icon.Width, icon.Height);
        
        // Exact 16-pixel offset to match DesktopIcon alignment ( (80-48)/2 = 16 )
        Vector2 iconDrawPos = position + new Vector2(16, 5);

        if (isMultiItem && _dragData is List<string> list) {
            // Draw stack for multiple items with offset - using 10px for better visibility
            int stackCount = Math.Min(3, list.Count);
            for (int i = stackCount - 1; i >= 0; i--) {
                Vector2 offset = new Vector2(i * 10, i * 10);
                // Subtle dimming for background icons in stack (0.07 per layer)
                float stackOpacity = opacity * (1.0f - (i * 0.07f));
                
                // Use varied icons if available, otherwise fallback to the first one
                Texture2D stackIcon = (i < _cachedDragIcons.Count) ? _cachedDragIcons[i] : _cachedDragIcons[0];
                float stackScale = iconSize / Math.Max(stackIcon.Width, stackIcon.Height);
                sb.Draw(stackIcon, iconDrawPos + offset, null, Color.White * stackOpacity, 0f, Vector2.Zero, stackScale, SpriteEffects.None, 0f);
            }

            // Draw count badge only if showLabel is true (for cursor, not for drop preview)
            if (_showItemCount && showLabel) {
                string countText = list.Count.ToString();
                var font = GameContent.FontSystem.GetFont(14);
                if (font != null) {
                    // Shift badge more to the right (iconSize - 8 instead of -16)
                    Vector2 badgePos = position + new Vector2(iconSize - 8, -8);
                    Vector2 textSize = font.MeasureString(countText);
                    float badgeSize = Math.Max(20, textSize.X + 8);

                    // Badge background
                    sbatch.FillCircle(badgePos + new Vector2(badgeSize / 2, badgeSize / 2), badgeSize / 2, new Color(255, 69, 58) * opacity);
                    sbatch.BorderCircle(badgePos + new Vector2(badgeSize / 2, badgeSize / 2), badgeSize / 2, Color.White * opacity, 2f);

                    // Badge text
                    font.DrawText(sbatch, countText, badgePos + new Vector2((badgeSize - textSize.X) / 2, (badgeSize - textSize.Y) / 2), Color.White * opacity);
                }
            }
        } else {
            // Single item
            sb.Draw(icon, iconDrawPos, null, Color.White * opacity, 0f, Vector2.Zero, mainScale, SpriteEffects.None, 0f);
        }

        string labelText = labelOverride ?? _cachedDragLabel;
        if (showLabel && !string.IsNullOrEmpty(labelText)) {
            var font = GameContent.FontSystem?.GetFont(14);
            if (font != null) {
                // Replicate DesktopIcon.DrawWrappedLabel exactly
                DrawWrappedLabel(sbatch, font, labelText, position, iconSize, opacity);
            }
        }
    }

    private void DrawWrappedLabel(ShapeBatch batch, FontStashSharp.DynamicSpriteFont font, string text, Vector2 absPos, float iconSize, float opacity) {
        const int maxLines = 2;
        const float maxWidth = 80f; 
        const float lineHeight = 16f;
        
        string[] words = text.Split(' ');
        var lines = new List<string>();
        string currentLine = "";
        
        foreach (var word in words) {
            string testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
            Vector2 testSize = font.MeasureString(testLine);
            
            if (testSize.X > maxWidth && !string.IsNullOrEmpty(currentLine)) {
                lines.Add(currentLine);
                currentLine = word;
                if (lines.Count >= maxLines) break;
            } else {
                currentLine = testLine;
            }
        }
        
        if (!string.IsNullOrEmpty(currentLine) && lines.Count < maxLines) {
            lines.Add(currentLine);
        }
        
        // Truncate logic removed for brevity in drag visuals, but can be added if needed
        // For now, just draw the lines
        float startY = iconSize + 10;
        for (int i = 0; i < lines.Count; i++) {
            Vector2 lineSize = font.MeasureString(lines[i]);
            // (80 - lineSize.X) / 2 where 80 is Size.X of an icon
            Vector2 labelPos = absPos + new Vector2((80f - lineSize.X) / 2, startY + i * lineHeight);
            
            // Draw text with shadow
            font.DrawText(batch, lines[i], labelPos + new Vector2(1, 1), Color.Black * (opacity * 0.5f));
            font.DrawText(batch, lines[i], labelPos, Color.White * opacity);
        }
    }
    /// <summary>
    /// Draws the visual effect indicator (+ for copy, → for move, etc.)
    /// </summary>
    private void DrawEffectIndicator(SpriteBatch sb, ShapeBatch sbatch, Vector2 position) {
        // Show default MOVE effect if no target is hovered
        var effectToShow = _currentEffect == DragDropEffect.None ? DragDropEffect.Move : _currentEffect;
        
        var font = GameContent.FontSystem?.GetFont(20);  // Larger font
        if (font == null) return;
        
        string indicator = effectToShow switch {
            DragDropEffect.Copy => "+",  // Plus sign for copy
            DragDropEffect.Move => "→",  // Arrow for move
            DragDropEffect.Link => "&",  // Ampersand for link
            _ => null
        };
        
        if (indicator != null) {
            // Draw indicator offset from drag icon (bottom-right)
            Vector2 indicatorPos = position + new Vector2(48, 48);  // Offset from icon bottom-right
            float circleRadius = 16f;  // Larger circle
            Vector2 circleCenter = indicatorPos + new Vector2(circleRadius, circleRadius);
            
            // Background circle with shadow
            sbatch.FillCircle(circleCenter + new Vector2(2, 2), circleRadius, Color.Black * 0.5f);  // Shadow
            sbatch.FillCircle(circleCenter, circleRadius, Color.Black * 0.8f);  // Background
            sbatch.BorderCircle(circleCenter, circleRadius, Color.White, 2.5f);  // Border
            
            // Indicator text centered in circle
            var size = font.MeasureString(indicator);
            Vector2 textPos = circleCenter - new Vector2(size.X / 2, size.Y / 2);
            font.DrawText(sbatch, indicator, textPos, Color.White);
        }
    }

    private bool IsLeader(object item) {
        if (_dragData == item) return true;
        
        if (_dragData is DesktopIcon di && item is DesktopIcon diKey && di == diKey) return true;

        if (_dragData is List<string> list && list.Count > 0) {
            string leaderPath = list[0]?.ToUpper().TrimEnd('\\');
            if (string.IsNullOrEmpty(leaderPath)) return false;

            if (item is string s && s.ToUpper().TrimEnd('\\') == leaderPath) return true;
            if (item is DesktopIcon di2 && di2.VirtualPath?.ToUpper().TrimEnd('\\') == leaderPath) return true;
        }
        
        if (_dragData is string path && item is DesktopIcon di3 && di3.VirtualPath?.ToUpper().TrimEnd('\\') == path.ToUpper().TrimEnd('\\')) return true;
        
        return false;
    }
}

