using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.Linq;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;
using TheGame.Graphics;
using TheGame.Core;
using TheGame.Core.OS;
using TheGame.Core.Input;
using TheGame.Core.OS.DragDrop;

namespace NACHOS.Designer;

public class HierarchyPanel : Panel, IDropTarget {
    public DesignerSurface Surface { get; private set; }
    private List<HierarchyNode> _rootNodes = new();
    private ScrollPanel _scroll;
    private Label _headerLabel;

    public HierarchyPanel(Vector2 position, Vector2 size, DesignerSurface surface) : base(position, size) {
        Surface = surface;
        BackgroundColor = new Color(30, 30, 30);
        
        _headerLabel = new Label(new Vector2(5, 4), "HIERARCHY") {
            FontSize = 12,
            Color = Color.Gray
        };
        AddChild(_headerLabel);

        _scroll = new ScrollPanel(new Vector2(0, 20), new Vector2(size.X, size.Y - 20));
        AddChild(_scroll);
        
        Surface.OnElementModified += (el) => Refresh();
        Surface.OnSelectionChanged += (el) => {
            // Optional: Auto-expand to selection?
        };
        
        Refresh();
    }

    public void Refresh() {
        _rootNodes.Clear();
        var root = Surface.ContentLayer.Children.FirstOrDefault();
        if (root != null) {
            if (root.GetType().Name == "DesignerWindow") {
                foreach (var child in root.Children) {
                    _rootNodes.Add(BuildNodeRecursive(child, 0));
                }
            } else {
                _rootNodes.Add(BuildNodeRecursive(root, 0));
            }
        }
        UpdateLayout();
    }

    private HierarchyNode BuildNodeRecursive(UIElement el, int depth) {
        var node = new HierarchyNode(el, Surface, this, depth);
        foreach (var child in el.Children) {
            node.ChildNodes.Add(BuildNodeRecursive(child, depth + 1));
        }
        return node;
    }

    public void UpdateLayout() {
        _scroll.ClearChildren();
        List<HierarchyNode> visibleNodes = new();
        foreach (var node in _rootNodes) {
            node.BuildDisplayList(visibleNodes);
        }

        float y = 5;
        foreach (var node in visibleNodes) {
            node.Position = new Vector2(0, y);
            node.Size = new Vector2(Size.X, 24);
            _scroll.AddChild(node);
            y += 24;
        }
        
        _scroll.UpdateContentSize(Size.X, y + 20);
    }

    public override void Update(GameTime gameTime) {
        base.Update(gameTime);
        if (Shell.Drag.IsActive && IsMouseOver) {
            // Only check background if NO child node is hovered
            var hit = GetElementAt(InputManager.MousePosition.ToVector2());
            if (hit == this || hit == _scroll) {
                Shell.Drag.CheckDropTarget(this, InputManager.MousePosition.ToVector2());
            }
        }
    }

    protected override void UpdateInput() {
        base.UpdateInput();
        if (Shell.Drag.IsActive && IsMouseOver && InputManager.IsMouseButtonJustReleased(MouseButton.Left)) {
            Shell.Drag.TryDropOn(this, InputManager.MousePosition.ToVector2());
        }
    }

    // === IDropTarget ===
    public bool CanAcceptDrop(object dragData) => dragData is HierarchyNode;
    public DragDropEffect OnDragOver(object dragData, Vector2 position) => DragDropEffect.Move;
    public void OnDragLeave() { }
    public bool OnDrop(object dragData, Vector2 position) {
        if (dragData is HierarchyNode node) {
            var el = node.TargetElement;
            var root = Surface.ContentLayer.Children.FirstOrDefault();
            if (root != null && root.GetType().Name == "DesignerWindow") {
                DebugLogger.Log($"Hierarchy: Reparenting {el.GetType().Name} to Root Window");
                // Reparent to window
                if (el.Parent != null) el.Parent.RemoveChild(el);
                root.AddChild(el);
                Surface.NotifyElementModified(el);
                Refresh();
                return true;
            }
        }
        return false;
    }
    public Rectangle GetDropBounds() => Bounds;

    protected override void DrawSelf(SpriteBatch spriteBatch, ShapeBatch batch) {
        base.DrawSelf(spriteBatch, batch);
        // Header background
        batch.FillRectangle(AbsolutePosition, new Vector2(Size.X, 20), new Color(40, 40, 40));
    }
}
