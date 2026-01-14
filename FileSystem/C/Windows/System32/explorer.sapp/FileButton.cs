using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Linq;
using System.Collections.Generic;
using TheGame.Core.Input;
using TheGame.Core.OS;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;
using TheGame.Graphics;
using TheGame;

namespace ExplorerApp;

public class FileButton : Button {
    public string VirtualPath { get; set; }
    private bool _isDragging;
    private Vector2 _dragStart;
    private bool _wasPressed; // Track if we initiated a potential drag
    public bool IsSelected { get; set; }
    public Color SelectionColor { get; set; } = new Color(0, 102, 204, 100);
    public Color SelectionBorderColor { get; set; } = new Color(0, 102, 204);

    public FileButton(Vector2 position, Vector2 size, string text, string virtualPath) 
        : base(position, size, text) {
        VirtualPath = virtualPath;
        BackgroundColor = Color.Transparent;
        HoverColor = new Color(60, 60, 60);
    }

    public override void Update(GameTime gameTime) {
        // --- Input Discovery Phase (Must happen BEFORE base.Update because that consumes input) ---
        bool isInBounds = Bounds.Contains(InputManager.MousePosition);
        bool justPressed = isInBounds && InputManager.IsMouseButtonJustPressed(MouseButton.Left);
        bool justRightPressed = isInBounds && InputManager.IsMouseButtonJustPressed(MouseButton.Right);
        var currentMouse = Microsoft.Xna.Framework.Input.Mouse.GetState();
        bool isMouseDown = currentMouse.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed;

        base.Update(gameTime);

        if (!IsVisible) return;

        // Detect press start
        if (justPressed || justRightPressed) {
            _dragStart = InputManager.MousePosition.ToVector2();
            _wasPressed = true;
            
            // Logic: If not selected, select self and clear others.
            if (!IsSelected) {
                IsSelected = true;
                if (Parent != null) {
                    foreach (var child in Parent.Children) {
                        if (child is FileButton other && other != this) other.IsSelected = false;
                    }
                }
            }
        }

        // Start dragging if we were pressed and mouse moves far enough
        if (_wasPressed && isMouseDown && !_isDragging) {
            if (Vector2.Distance(InputManager.MousePosition.ToVector2(), _dragStart) > 10f) {
                _isDragging = true;
                
                // Check for multi-select drag
                if (IsSelected && Parent != null) {
                      var selected = Parent.Children.OfType<FileButton>().Where(x => x.IsSelected).Select(x => x.VirtualPath).ToList();
                      if (selected.Count > 1) {
                          DragDropManager.Instance.BeginDrag(selected, AbsolutePosition);
                      } else {
                          DragDropManager.Instance.BeginDrag(VirtualPath, AbsolutePosition);
                      }
                } else {
                    DragDropManager.Instance.BeginDrag(VirtualPath, AbsolutePosition);
                }
            }
        }

        // End drag/press when mouse released
        if (!isMouseDown && (_isDragging || _wasPressed)) {
            // Clicked (no drag) on existing selection -> Clear others now
            if (!_isDragging && _wasPressed && IsSelected) {
                if (Parent != null) {
                    foreach (var child in Parent.Children) {
                        if (child is FileButton other && other != this) other.IsSelected = false;
                    }
                }
            }

            if (_isDragging) {
                // Drag ended
                _isDragging = false;
            }
            _wasPressed = false;
        }
    }

    protected override void DrawSelf(SpriteBatch spriteBatch, ShapeBatch batch) {
        if (IsSelected) {
            batch.FillRectangle(AbsolutePosition, Size, SelectionColor, rounded: 3f);
            batch.BorderRectangle(AbsolutePosition, Size, SelectionBorderColor, thickness: 1f, rounded: 3f);
        }
        base.DrawSelf(spriteBatch, batch);
    }
}
