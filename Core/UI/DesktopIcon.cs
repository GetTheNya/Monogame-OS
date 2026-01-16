using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Core.Input;
using TheGame.Core.OS;
using TheGame.Graphics;
using TheGame.Core.UI.Controls;

namespace TheGame.Core.UI;

/// <summary>
/// A desktop icon component that displays an image, text, and handles double-click actions.
/// </summary>
public class DesktopIcon : UIElement {
    public Texture2D Icon { get; set; }
    private string _label;
    public string Label { 
        get => _label; 
        set {
            if (_label != value) {
                _label = value;
                _cachedLabelSize = Vector2.Zero;
            }
        }
    }
    public string VirtualPath { get; set; }
    public Action<DesktopIcon> OnSelectedAction { get; set; }
    public Action<DesktopIcon, Vector2> OnDragAction { get; set; }
    public Action OnDropAction { get; set; }
    
    public bool IsSelected { get; set; }
    private bool _isDragging;
    private Vector2 _dragOffset;
    private Vector2 _dragStartPos;
    
    public Color LabelColor { get; set; } = Color.White;
    public Color SelectionColor { get; set; } = new Color(0, 102, 204, 100); // semi-transparent blue
    public Color SelectionBorderColor { get; set; } = new Color(0, 102, 204);
    
    private Vector2 _cachedLabelSize = Vector2.Zero;
    
    // Rename functionality
    private TextInput _renameInput;
    private bool _isRenaming = false;
    public Action OnRenamed;

    public DesktopIcon(Vector2 position, string label, Texture2D icon = null) {
        Position = position;
        Size = new Vector2(80, 90); // Default size for an icon
        Label = label;
        Icon = icon;
    }

    public override void Update(GameTime gameTime) {
        base.Update(gameTime);
        
        // Update rename input
        if (_isRenaming && _renameInput != null) {
            _renameInput.Position = AbsolutePosition + new Vector2(0, 58);
            _renameInput.Update(gameTime);
            
            // Cancel on Escape or click outside
            if (InputManager.IsKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.Escape)) {
                CancelRename();
            } else if (InputManager.IsMouseButtonJustPressed(MouseButton.Left) && 
                       !_renameInput.Bounds.Contains(InputManager.MousePosition)) {
                CompleteRename(_renameInput.Value);
            }
        }
    }
    
    protected override void UpdateInput() {
        if (!IsVisible || _isRenaming) return;

        // Input capture order:
        // 1. Check for mouse hovering and basic clicks (left, right, double)
        // 2. Handle dragging separately as it can span multiple frames and requires mouse button held down.

        bool isHovering = InputManager.IsMouseHovering(Bounds);
        bool isJustPressed = isHovering && InputManager.IsMouseButtonJustPressed(MouseButton.Left);
        bool isRightPressed = isHovering && InputManager.IsMouseButtonJustPressed(MouseButton.Right);
        bool isDoubleClick = isHovering && InputManager.IsDoubleClick(MouseButton.Left);

        if (isHovering && ConsumesInput && !_isDragging)
            InputManager.IsMouseConsumed = true;
        
        if (isHovering) {
            CustomCursor.Instance.SetCursor(CursorType.Link);
            
            if (isJustPressed) {
                if (!IsSelected) {
                    OnSelectedAction?.Invoke(this);
                    IsSelected = true;
                }
                
                // Store drag start position but don't start dragging yet
                // (wait to see if it's a double-click)
                _dragStartPos = InputManager.MousePosition.ToVector2();
                _dragOffset = InputManager.MousePosition.ToVector2() - Position;

                InputManager.IsMouseConsumed = true;
                Parent?.BringToFront(this);
            }
            
            if (isDoubleClick) {
                OnDoubleClickAction?.Invoke();
                InputManager.IsMouseConsumed = true;
                IsSelected = true;
                _isDragging = false; 
            } else if (isJustPressed) {
                // Start dragging only if it's not a double-click
                _isDragging = true;
            }

            if (isRightPressed) {
                OnRightClickAction?.Invoke();
                InputManager.IsMouseConsumed = true;
                IsSelected = true;
            }
        }

        if (_isDragging) {
            if (InputManager.IsMouseButtonDown(MouseButton.Left)) {
                Vector2 currentMousePos = InputManager.MousePosition.ToVector2();
                
                // Only start drag if mouse has moved >5 pixels
                if (!DragDropManager.Instance.IsActive && Vector2.Distance(currentMousePos, _dragStartPos) > 5f) {
                    // If dragging a selected icon, drag all selected icons
                    if (IsSelected && Parent != null) {
                        // Manual iteration to avoid LINQ allocations
                        var selectedPaths = new System.Collections.Generic.List<string>();
                        for (int i = 0; i < Parent.Children.Count; i++) {
                            if (Parent.Children[i] is DesktopIcon di && di.IsSelected && !string.IsNullOrEmpty(di.VirtualPath)) {
                                selectedPaths.Add(di.VirtualPath);
                            }
                        }
                        
                        if (selectedPaths.Count > 1) {
                             DragDropManager.Instance.BeginDrag(selectedPaths, Position);
                        } else {
                             DragDropManager.Instance.BeginDrag(this, Position);
                        }
                    } else {
                        DragDropManager.Instance.BeginDrag(this, Position);
                    }
                }
                
                Vector2 oldPos = Position;
                Position = currentMousePos - _dragOffset;
                Vector2 delta = Position - oldPos;

                if (delta != Vector2.Zero && IsSelected) {
                    OnDragAction?.Invoke(this, delta);
                }

                InputManager.IsMouseConsumed = true;
            } else {
                _isDragging = false;
                OnDropAction?.Invoke();
            }
        }
    }
    
    public void StartRename() {
        if (_isRenaming || string.IsNullOrEmpty(VirtualPath)) return;
        
        _isRenaming = true;
        string currentName = System.IO.Path.GetFileName(VirtualPath.TrimEnd('\\'));
        
        // For files, remove extension (will be added back)
        bool isFile = !VirtualFileSystem.Instance.IsDirectory(VirtualPath);
        if (isFile) {
            currentName = System.IO.Path.GetFileNameWithoutExtension(currentName);
        }
        
        // Create text input positioned over the label
        var absPos = AbsolutePosition;
        float iconSize = 48f;
        float labelY = absPos.Y + iconSize + 10;
        
        _renameInput = new TextInput(new Vector2(absPos.X, labelY), new Vector2(Size.X, 20)) {
            Value = currentName,
            BackgroundColor = Color.White,
            TextColor = Color.Black
        };
        _renameInput.OnSubmit += CompleteRename;
        _renameInput.IsFocused = true;
    }
    
    private void CompleteRename(string newName) {
        if (!_isRenaming) return;
        
        try {
            // Validate
            if (string.IsNullOrWhiteSpace(newName)) {
                CancelRename();
                return;
            }
            
            char[] invalidChars = System.IO.Path.GetInvalidFileNameChars();
            if (newName.Any(c => invalidChars.Contains(c))) {
                Shell.Notifications.Show("Invalid Name", "Filename contains invalid characters.");
                CancelRename();
                return;
            }
            
            string directory = System.IO.Path.GetDirectoryName(VirtualPath.TrimEnd('\\'));
            bool isFile = !VirtualFileSystem.Instance.IsDirectory(VirtualPath);
            
            // For files, preserve extension
            if (isFile) {
                string extension = System.IO.Path.GetExtension(VirtualPath);
                if (!newName.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) {
                    newName += extension;
                }
            }
            
            string newPath = System.IO.Path.Combine(directory, newName);
            
            // Check if already exists
            if (VirtualFileSystem.Instance.Exists(newPath) && 
                !newPath.Equals(VirtualPath, StringComparison.OrdinalIgnoreCase)) {
                Shell.Notifications.Show("Name Conflict", "A file or folder with that name already exists.");
                CancelRename();
                return;
            }
            
            // Don't rename if name didn't change
            if (newPath.Equals(VirtualPath, StringComparison.OrdinalIgnoreCase)) {
                CancelRename();
                return;
            }
            
            // Perform rename
            VirtualFileSystem.Instance.Move(VirtualPath, newPath);
            
            // Update icon
            VirtualPath = newPath;
            Label = System.IO.Path.GetFileName(newPath.TrimEnd('\\'));
            
            // Notify parent to refresh
            OnRenamed?.Invoke();
            
        } catch (Exception ex) {
            Shell.Notifications.Show("Rename Error", ex.Message);
        } finally {
            _isRenaming = false;
            _renameInput = null;
        }
    }
    
    private void CancelRename() {
        _isRenaming = false;
        _renameInput = null;
    }

    protected override void DrawSelf(SpriteBatch spriteBatch, ShapeBatch batch) {
        // Hide icon from normal layer if it's being dragged (so it can be drawn in overlay)
        if (DragDropManager.Instance.IsItemDragged(this) && !Shell.IsRenderingDrag) return;
        
        // Also hide if its path is being dragged in a list
        if (!string.IsNullOrEmpty(VirtualPath) && DragDropManager.Instance.IsItemDragged(VirtualPath) && !Shell.IsRenderingDrag) return;

        var absPos = AbsolutePosition;
        
        // Selection highlight
        if (IsSelected) {
            batch.FillRectangle(absPos, Size, SelectionColor, rounded: 3f);
            batch.BorderRectangle(absPos, Size, SelectionBorderColor, thickness: 1f, rounded: 3f);
        }

        // Draw Icon
        float iconSize = 48f;
        Vector2 iconPos = absPos + new Vector2((Size.X - iconSize) / 2, 5);
        
        if (Icon != null) {
            float scale = iconSize / Icon.Width;
            batch.DrawTexture(Icon, iconPos, Color.White * AbsoluteOpacity, scale);
        } else {
            // Placeholder icon
            batch.FillRectangle(iconPos, new Vector2(iconSize, iconSize), new Color(200, 200, 200, 150));
            batch.BorderRectangle(iconPos, new Vector2(iconSize, iconSize), Color.White, thickness: 1f);
        }

        // Draw Label or Rename Input
        if (_isRenaming && _renameInput != null) {
            _renameInput.Draw(spriteBatch, batch);
        } else if (!string.IsNullOrEmpty(Label) && GameContent.FontSystem != null) {
            var font = GameContent.FontSystem.GetFont(14);
            if (font != null) {
                if (_cachedLabelSize == Vector2.Zero) {
                    _cachedLabelSize = font.MeasureString(Label);
                }
                
                // Wrap text if too long? For now just center it and allow overlap
                Vector2 labelPos = absPos + new Vector2((Size.X - _cachedLabelSize.X) / 2, iconSize + 10);
                
                // Shadow/Outline for readability on any background
                font.DrawText(batch, Label, labelPos + new Vector2(1, 1), Color.Black);
                font.DrawText(batch, Label, labelPos, LabelColor);
            }
        }
    }
}
