using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using TheGame;
using TheGame.Core;
using TheGame.Core.Input;
using TheGame.Core.OS;
using TheGame.Core.OS.DragDrop;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;
using TheGame.Graphics;

namespace NACHOS.Designer;

public class HierarchyNode : Panel, IDraggable, IDropTarget {
    public UIElement TargetElement { get; private set; }
    public DesignerSurface Surface { get; private set; }
    public HierarchyPanel Panel { get; private set; }
    public int Depth { get; private set; }
    public bool IsExpanded { get; set; } = true;
    public List<HierarchyNode> ChildNodes { get; } = new();

    private Button _expandBtn;
    private Label _label;
    private Vector2 _dragStartPos;
    private bool _isHoveredByDrag;
    private float _dropHitY; // 0-1 relative to height

    public HierarchyNode(UIElement target, DesignerSurface surface, HierarchyPanel panel, int depth) : base(Vector2.Zero, new Vector2(0, 24)) {
        TargetElement = target;
        Surface = surface;
        Panel = panel;
        Depth = depth;
        
        BackgroundColor = Color.Transparent;
        BorderThickness = 0;
        ConsumesInput = true;
        
        SetupUI();
    }

    private void SetupUI() {
        float indent = Depth * 16;
        
        if (TargetElement.Children.Count > 0) {
            _expandBtn = new Button(new Vector2(indent + 2, 2), new Vector2(20, 20), IsExpanded ? "▼" : "▶") {
                BackgroundColor = Color.Transparent,
                BorderColor = Color.Transparent,
                FontSize = 10,
                OnClickAction = () => {
                    IsExpanded = !IsExpanded;
                    _expandBtn.Text = IsExpanded ? "▼" : "▶";
                    Panel.UpdateLayout();
                }
            };
            AddChild(_expandBtn);
        }

        _label = new Label(new Vector2(indent + 24, 4), GetDisplayName()) {
            FontSize = 14,
            Color = Color.LightGray
        };
        AddChild(_label);
    }

    private string GetDisplayName() {
        string typeName = TargetElement.GetType().Name.Replace("Designer", ""); // Clean up DesignerWindow etc
        string name = TargetElement.Name;
        
        // If no name, try to use 'Text' property if available (for Labels/Buttons)
        if (string.IsNullOrEmpty(name)) {
            var textProp = TargetElement.GetType().GetProperty("Text");
            if (textProp != null) {
                var textVal = textProp.GetValue(TargetElement) as string;
                if (!string.IsNullOrEmpty(textVal)) return $"{textVal} ({typeName})";
            }
            return typeName;
        }
        
        return $"{name} ({typeName})";
    }

    protected override void UpdateInput() {
        base.UpdateInput();
        if (!IsVisible) return;

        // Selection
        if (IsMouseOver && (InputManager.IsMouseButtonJustPressed(MouseButton.Left) || InputManager.IsMouseButtonJustPressed(MouseButton.Right))) {
            Surface.SelectElement(TargetElement);
            if (InputManager.IsMouseButtonJustPressed(MouseButton.Left)) {
                _dragStartPos = InputManager.MousePosition.ToVector2();
            }
        }

        // Drag Start
        if (InputManager.IsMouseButtonDown(MouseButton.Left) && !Shell.Drag.IsActive && _dragStartPos != Vector2.Zero) {
            if (Vector2.Distance(_dragStartPos, InputManager.MousePosition.ToVector2()) > 5f) {
                Vector2 grabOffset = _dragStartPos - AbsolutePosition;
                Shell.Drag.BeginDraggable(this, AbsolutePosition, grabOffset);
                _dragStartPos = Vector2.Zero;
            }
        }

        if (InputManager.IsMouseButtonJustReleased(MouseButton.Left)) {
            _dragStartPos = Vector2.Zero;
            if (Shell.Drag.IsActive && IsMouseOver) {
                Shell.Drag.TryDropOn(this, InputManager.MousePosition.ToVector2());
            }
        }
    }

    public override void Update(GameTime gameTime) {
        base.Update(gameTime);
        
        if (Shell.Drag.IsActive && IsMouseOver) {
            Shell.Drag.CheckDropTarget(this, InputManager.MousePosition.ToVector2());
        }

        bool isSelected = Surface.SelectedElement == TargetElement;
        
        if (_isHoveredByDrag) BackgroundColor = new Color(60, 60, 60);
        else if (isSelected) BackgroundColor = new Color(45, 45, 45);
        else BackgroundColor = Color.Transparent;

        _label.Text = GetDisplayName();
        _label.Color = isSelected ? Color.White : Color.LightGray;
    }

    protected override void DrawSelf(SpriteBatch spriteBatch, ShapeBatch batch) {
        base.DrawSelf(spriteBatch, batch);

        // Draw drop indicators
        if (_isHoveredByDrag) {
            var absPos = AbsolutePosition;
            if (_dropHitY < 0.25f) {
                batch.FillRectangle(absPos, new Vector2(Size.X, 2), Color.Cyan); // Top
            } else if (_dropHitY > 0.75f) {
                batch.FillRectangle(new Vector2(absPos.X, absPos.Y + Size.Y - 2), new Vector2(Size.X, 2), Color.Cyan); // Bottom
            } else {
                batch.BorderRectangle(absPos, Size, Color.Cyan, 1f); // Middle (Child)
            }
        }
    }

    // === IDraggable ===
    public object GetDragData() => this;
    public Texture2D GetDragIcon() => null;
    public string GetDragLabel() => TargetElement.GetType().Name;
    public UIElement GetCustomDragVisual() {
        var panel = new Panel(Vector2.Zero, new Vector2(150, 24)) {
            BackgroundColor = new Color(0, 0, 0, 150),
            BorderColor = Color.Cyan * 0.5f,
            BorderThickness = 1
        };
        panel.AddChild(new Label(new Vector2(5, 4), GetDragLabel()) { FontSize = 14, Color = Color.White });
        return panel;
    }
    public void OnDragStart(Vector2 grabOffset) { Opacity = 0.5f; }
    public void OnDragEnd() { Opacity = 1f; }
    public void OnDragCancel() { Opacity = 1f; }

    // === IDropTarget ===
    public bool CanAcceptDrop(object dragData) {
        if (dragData is HierarchyNode node) {
            var el = node.TargetElement;
            if (el == null) return false;

            // Can't drop on ourselves or our children
            if (el == TargetElement) return false;
            
            // Check if TargetElement is a descendant of el
            UIElement p = TargetElement.Parent;
            while (p != null) {
                if (p == el) return false;
                p = p.Parent;
            }
            return true;
        }
        return false;
    }

    public DragDropEffect OnDragOver(object dragData, Vector2 position) {
        _isHoveredByDrag = true;
        _dropHitY = (position.Y - AbsolutePosition.Y) / Size.Y;
        return DragDropEffect.Move;
    }

    public void OnDragLeave() { _isHoveredByDrag = false; }

    public bool OnDrop(object dragData, Vector2 position) {
        _isHoveredByDrag = false;
        if (dragData is HierarchyNode node) {
            var el = node.TargetElement;
            var oldParent = el.Parent;

            if (_dropHitY < 0.25f) {
                // Insert Before
                var parent = TargetElement.Parent;
                if (parent != null) {
                    int idx = parent.Children.ToList().IndexOf(TargetElement);
                    Panel.History?.BeginTransaction($"Move {el.GetType().Name}");
                    if (oldParent != null) Panel.History?.AddOrExecute(new RemoveElementCommand(oldParent, el));
                    Panel.History?.AddOrExecute(new InsertElementCommand(parent, idx, el));
                    Panel.History?.EndTransaction();
                }
            } else if (_dropHitY > 0.75f) {
                // Insert After
                var parent = TargetElement.Parent;
                if (parent != null) {
                    int idx = parent.Children.ToList().IndexOf(TargetElement);
                    Panel.History?.BeginTransaction($"Move {el.GetType().Name}");
                    if (oldParent != null) Panel.History?.AddOrExecute(new RemoveElementCommand(oldParent, el));
                    Panel.History?.AddOrExecute(new InsertElementCommand(parent, idx + 1, el));
                    Panel.History?.EndTransaction();
                }
            } else {
                // Add as Child
                Panel.History?.BeginTransaction($"Move {el.GetType().Name}");
                if (oldParent != null) Panel.History?.AddOrExecute(new RemoveElementCommand(oldParent, el));
                Panel.History?.AddOrExecute(new AddElementCommand(TargetElement, el));
                Panel.History?.EndTransaction();
            }

            Surface.NotifyElementModified(el);
            return true;
        }
        return false;
    }

    public Rectangle GetDropBounds() => Bounds;

    public void BuildDisplayList(List<HierarchyNode> list) {
        list.Add(this);
        if (IsExpanded) {
            foreach (var child in ChildNodes) {
                child.BuildDisplayList(list);
            }
        }
    }

    public override void PopulateContextMenu(ContextMenuContext context, List<MenuItem> items) {
        base.PopulateContextMenu(context, items);

        items.Add(new MenuItem { Text = "Rename", Action = () => {
            string currentName = TargetElement.Name;
            string typeName = TargetElement.GetType().Name.Replace("Designer", "");
            string placeholder = string.IsNullOrEmpty(currentName) ? typeName : currentName;

            var dialog = new InputDialog("Rename Element", "Enter new name:", placeholder, (newName) => {
                if (newName != null) {
                    Panel.History?.Execute(new SetPropertyCommand(TargetElement, "Name", newName));
                    Surface.NotifyElementModified(TargetElement);
                }
            });
            GetOwnerProcess().Application.OpenModal(dialog);
        }});

        items.Add(new MenuItem { Text = "Duplicate", Action = () => {
            var clone = UISerializer.CloneElement(TargetElement);
            if (clone != null) {
                // Add to same parent with offset
                TargetElement.Parent?.AddChild(clone);
                clone.Position += new Vector2(20, 20);
                Surface.NotifyElementModified(clone);
            }
        }});

        // Move to Parent (up hierarchy levels)
        if (TargetElement.Parent != null && TargetElement.Parent.Parent != null && TargetElement.Parent.GetType().Name != "ContentLayer") {
            items.Add(new MenuItem { 
                Text = "Move to Parent",
                IsEnabled = Depth > 0,
                Action = () => {
                    var el = TargetElement;
                    var grandParent = el.Parent.Parent;
                    el.Parent.RemoveChild(el);
                    grandParent.AddChild(el);
                    Surface.NotifyElementModified(el);
                }
            });
        }

        items.Add(new MenuItem{Type = MenuItemType.Separator});

        items.Add(new MenuItem { Text = "Delete", 
        Icon = GameContent.TrashFullIcon,
        Action = () => {
            if (TargetElement.Children.Count > 0) {
                var box = new MessageBox("Delete Element", $"Are you sure you want to delete '{GetDisplayName()}' and all its children?", MessageBoxButtons.YesNo, (res) => {
                    if (res) {
                        PerformDelete();
                    }
                });
                GetOwnerProcess().Application.OpenModal(box);
            } else {
                PerformDelete();
            }
        }});
    }

    private void PerformDelete() {
        if (TargetElement.Parent != null) {
            Panel.History?.Execute(new RemoveElementCommand(TargetElement.Parent, TargetElement));
        }
        Surface.SelectElement(null);
        Surface.NotifyElementModified(null);
    }
}
