using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using TheGame.Core.OS;
using TheGame.Core.UI;
using TheGame.Graphics;
using TheGame.Core.UI.Controls;
using TheGame.Core;
using TheGame;
using TheGame.Core.OS.DragDrop;
using TheGame.Core.Input;

namespace DragDropTestApp;

/// <summary>
/// Comprehensive test app demonstrating all new Drag & Drop API features:
/// - Shell.Drag.* unified API
/// - IDraggable interface for type-safe dragging
/// - IDropTarget interface with DragDropEffect
/// - Visual feedback (Copy/Move/Link indicators)
/// - Keyboard modifiers (Ctrl/Shift/Alt)
/// - Drop previews
/// - Multi-item dragging
/// - Snap-back behavior
/// </summary>
public class Program : Application {
    public static Application Main(string[] args) => new Program();

    protected override void OnLoad(string[] args) {
        DebugLogger.Log("DragDropTestApp: OnLoad called!");
        
        ExitOnMainWindowClose = true;
        Priority = ProcessPriority.Normal;
        
        MainWindow = CreateWindow<DragDropTestWindow>();
        MainWindow.Title = "Drag & Drop API Test";
        MainWindow.Position = new Vector2(100, 100);
        MainWindow.Size = new Vector2(700, 550);
    }
}

/// <summary>
/// Main test window with draggable items and drop targets.
/// </summary>
public class DragDropTestWindow : Window {
    private Label _statusLabel;
    private List<DraggableBox> _draggables = new();
    private List<DropZone> _dropZones = new();
    
    public DragDropTestWindow() {
        Title = "Drag & Drop API Test";
        Size = new Vector2(700, 550);
    }
    
    protected override void OnLoad() {
        // Header
        AddChild(new Label(new Vector2(20, 10), "Drag & Drop API Test") { 
            Color = Color.White, 
            FontSize = 24 
        });
        
        // Instructions
        var instructions = new Panel(new Vector2(20, 50), new Vector2(660, 80)) {
            BackgroundColor = new Color(40, 40, 60, 200)
        };
        AddChild(instructions);
        
        instructions.AddChild(new Label(new Vector2(10, 5), "Instructions:") { 
            Color = Color.Yellow, 
            FontSize = 16 
        });
        instructions.AddChild(new Label(new Vector2(10, 25), 
            "• Drag boxes to drop zones to see visual feedback\n" +
            "• Hold Ctrl = Copy (+), Shift = Move (→), Alt = Link (&)\n" +
            "• Grab offset is intentional - icon follows where you grabbed it")  {
            Color = Color.LightGray, 
            FontSize = 14 
        });
        
        // Status label
        _statusLabel = new Label(new Vector2(20, 140), "Drag something to see status...") { 
            Color = Color.Lime,
            FontSize = 14
        };
        AddChild(_statusLabel);
        
        // === Draggable Items Section ===
        var dragSection = new Panel(new Vector2(20, 170), new Vector2(320, 350)) {
            BackgroundColor = new Color(30, 30, 30, 200)
        };
        AddChild(dragSection);
        
        dragSection.AddChild(new Label(new Vector2(10, 10), "Draggable Items") { 
            Color = Color.Cyan, 
            FontSize = 18 
        });
        
        // Create draggable boxes (IDraggable interface)
        string[] items = {  "Document.txt", "Image.png", "Video.mp4", "App.sapp" };
        Color[] colors = { Color.Orange, Color.DeepSkyBlue, Color.MediumPurple, Color.LimeGreen };
        
        for (int i = 0; i < items.Length; i++) {
            var box = new DraggableBox(
                new Vector2(10, 40 + i * 70),
                new Vector2(300, 60),
                items[i],
                colors[i]
            );
            dragSection.AddChild(box);
            _draggables.Add(box);
        }
        
        // === Drop Zones Section ===
        var dropSection = new Panel(new Vector2(360, 170), new Vector2(320, 350)) {
            BackgroundColor = new Color(30, 30, 30, 200)
        };
        AddChild(dropSection);
        
        dropSection.AddChild(new Label(new Vector2(10, 10), "Drop Zones (IDropTarget)") { 
            Color = Color.Cyan, 
            FontSize = 18 
        });
        
        // Create drop zones (IDropTarget interface)
        var copyZone = new DropZone(
            new Vector2(10, 50),
            new Vector2(300, 80),
            "Copy Zone",
            Color.Green,
            DragDropEffect.Copy
        );
        dropSection.AddChild(copyZone);
        _dropZones.Add(copyZone);
        
        var moveZone = new DropZone(
            new Vector2(10, 145),
            new Vector2(300, 80),
            "Move Zone",
            Color.Blue,
            DragDropEffect.Move
        );
        dropSection.AddChild(moveZone);
        _dropZones.Add(moveZone);
        
        var linkZone = new DropZone(
            new Vector2(10, 240),
            new Vector2(300, 80),
            "Link Zone (Shortcuts)",
            Color.Purple,
            DragDropEffect.Link
        );
        dropSection.AddChild(linkZone);
        _dropZones.Add(linkZone);
    }
    
    protected override void OnUpdate(GameTime gameTime) {
        // Update status label with current drag state
        if (Shell.Drag.IsActive) {
            var info = Shell.Drag.GetInfo();
            string effectStr = Shell.Drag.CurrentEffect switch {
                DragDropEffect.Copy => "COPY (+)",
                DragDropEffect.Move => "MOVE (→)",
                DragDropEffect.Link => "LINK (&)",
                _ => "NONE"
            };
            
            string dataStr = info.Data is IDraggable d ? d.GetDragLabel() : info.Data?.ToString() ?? "null";
            
            // Show keyboard modifiers status
            bool ctrlPressed = InputManager.IsKeyDown(Keys.LeftControl) || InputManager.IsKeyDown(Keys.RightControl);
            bool shiftPressed = InputManager.IsKeyDown(Keys.LeftShift) || InputManager.IsKeyDown(Keys.RightShift);
            bool altPressed = InputManager.IsKeyDown(Keys.LeftAlt) || InputManager.IsKeyDown(Keys.RightAlt);
            string modifiers = $"[{(ctrlPressed ? "CTRL " : "")}{(shiftPressed ? "SHIFT " : "")}{(altPressed ? "ALT" : "")}]".Trim();
            if (modifiers == "[]") modifiers = "[No modifiers]";
            
            _statusLabel.Text = $"Dragging: {dataStr} | Effect: {effectStr} | Keys: {modifiers}";
            _statusLabel.Color = Color.Yellow;
        } else {
            _statusLabel.Text = "Not dragging. Drag a box to a zone! Try holding Ctrl/Shift/Alt while hovering.";
            _statusLabel.Color = Color.Lime;
        }
    }
}

/// <summary>
/// A draggable box that implements IDraggable interface.
/// Demonstrates lifecycle hooks and drag data provision.
/// </summary>
public class DraggableBox : Panel, IDraggable {
    private string _label;
    private Color _baseColor;
    private bool _isDragging;
    private Vector2 _dragStartPos;
    
    public DraggableBox(Vector2 pos, Vector2 size, string label, Color color) 
        : base(pos, size) {
        _label = label;
        _baseColor = color;
        BackgroundColor = color;
        BorderColor = Color.White;
        BorderThickness = 2;
        
        // Label
        AddChild(new Label(new Vector2(10, size.Y / 2 - 10), label) { 
            Color = Color.White,
            FontSize = 16
        });
    }
    
    // === IDraggable Implementation ===
    
    public object GetDragData() => _label;
    
    public Texture2D GetDragIcon() => GameContent.FolderIcon;  // Use any icon
    
    public string GetDragLabel() => _label;
    
    /// <summary>
    /// Returns a custom visual - a smaller version of this box!
    /// </summary>
    public UIElement GetCustomDragVisual() {
        // Create a mini version of the box (60% size)
        float scale = 0.6f;
        var miniBox = new Panel(Vector2.Zero, Size * scale) {
            BackgroundColor = _baseColor * 0.8f,  // Slightly transparent
            BorderColor = Color.White,
            BorderThickness = 2
        };
        
        // Add label
        miniBox.AddChild(new Label(new Vector2(10, (Size.Y * scale) / 2 - 10), _label) { 
            Color = Color.White,
            FontSize = 14  // Smaller font
        });
        
        return miniBox;
    }
    
    public void OnDragStart(Vector2 grabPosition) {
        _isDragging = true;
        BackgroundColor = _baseColor * 0.5f;  // Dim while dragging
        DebugLogger.Log($"Started dragging: {_label}");
    }
    
    public void OnDragEnd() {
        _isDragging = false;
        BackgroundColor = _baseColor;  // Restore color
        DebugLogger.Log($"Dropped successfully: {_label}");
    }
    
    public void OnDragCancel() {
        _isDragging = false;
        BackgroundColor = _baseColor;  // Restore color
        DebugLogger.Log($"Drag cancelled (snap-back): {_label}");
    }
    
    // === Input Handling ===
    
    protected override void UpdateInput() {
        base.UpdateInput();
        
        if (!IsVisible) return;
        
        // Start drag on mouse down
        if (IsMouseOver && InputManager.IsMouseButtonJustPressed(MouseButton.Left)) {
            _dragStartPos = InputManager.MousePosition.ToVector2();
        }
        
        // Begin drag after small movement (prevents accidental drags)
        if (InputManager.IsMouseButtonDown(MouseButton.Left) && 
            !Shell.Drag.IsActive && 
            _dragStartPos != Vector2.Zero) {
            
            var currentPos = InputManager.MousePosition.ToVector2();
            if (Vector2.Distance(_dragStartPos, currentPos) > 5) {
                // Calculate grab offset from WHERE WE INITIALLY CLICKED
                Vector2 panelOffset = _dragStartPos - AbsolutePosition;
                
                // SCALE the offset! Panel is 300x60 but icon is 48x48
                // So we need to proportionally scale where the click was
                float iconSize = 48f;
                Vector2 scaledOffset = new Vector2(
                    panelOffset.X * (iconSize / Size.X),
                    panelOffset.Y * (iconSize / Size.Y)
                );
                
                // Use Shell.Drag.BeginDraggable for IDraggable sources
                Shell.Drag.BeginDraggable(this, AbsolutePosition, scaledOffset);
                _dragStartPos = Vector2.Zero;
            }
        }
        
        // Reset on release
        if (InputManager.IsMouseButtonJustReleased(MouseButton.Left)) {
            _dragStartPos = Vector2.Zero;
        }
    }
}

/// <summary>
/// A drop zone that implements IDropTarget interface.
/// Demonstrates DragDropEffect usage and visual feedback.
/// </summary>
public class DropZone : Panel, IDropTarget {
    private string _label;
    private Color _baseColor;
    private DragDropEffect _preferredEffect;
    private bool _isHovered;
    private List<string> _droppedItems = new();
    private Label _countLabel;
    
    public DropZone(Vector2 pos, Vector2 size, string label, Color color, DragDropEffect effect) 
        : base(pos, size) {
        _label = label;
        _baseColor = color;
        _preferredEffect = effect;
        BackgroundColor = color * 0.3f;
        BorderColor = color;
        BorderThickness = 3;
        
        // Title label
        AddChild(new Label(new Vector2(10, 10), label) { 
            Color = Color.White,
            FontSize = 16
        });
        
        // Count label
        _countLabel = new Label(new Vector2(10, 35), "Dropped: 0 items") { 
            Color = Color.LightGray,
            FontSize = 14
        };
        AddChild(_countLabel);
        
        // Effect hint
        string effectHint = effect switch {
            DragDropEffect.Copy => "Accepts: Ctrl",
            DragDropEffect.Move => "Accepts: Shift (default)",
            DragDropEffect.Link => "Accepts: Alt",
            _ => ""
        };
        AddChild(new Label(new Vector2(10, 55), effectHint) { 
            Color = Color.Yellow,
            FontSize = 12
        });
    }
    
    // === IDropTarget Implementation ===
    
    public bool CanAcceptDrop(object dragData) {
        // Accept IDraggable or string data
        return dragData is IDraggable || dragData is string;
    }
    
    public DragDropEffect OnDragOver(object dragData, Vector2 position) {
        _isHovered = true;
        
        // Check keyboard modifiers (like Windows Explorer)
        bool ctrlPressed = InputManager.IsKeyDown(Keys.LeftControl) || InputManager.IsKeyDown(Keys.RightControl);
        bool shiftPressed = InputManager.IsKeyDown(Keys.LeftShift) || InputManager.IsKeyDown(Keys.RightShift);
        bool altPressed = InputManager.IsKeyDown(Keys.LeftAlt) || InputManager.IsKeyDown(Keys.RightAlt);
        
        // Override effect based on keyboard
        if (ctrlPressed) return DragDropEffect.Copy;
        if (shiftPressed) return DragDropEffect.Move;
        if (altPressed) return DragDropEffect.Link;
        
        // Default: use zone's preferred effect
        return _preferredEffect;
    }
    
    public void OnDragLeave() {
        _isHovered = false;
    }
    
    public bool OnDrop(object dragData, Vector2 position) {
        _isHovered = false;
        
        // Extract label
        string label = dragData is IDraggable draggable 
            ? draggable.GetDragLabel() 
            : dragData.ToString();
        
        _droppedItems.Add(label);
        _countLabel.Text = $"Dropped: {_droppedItems.Count} items";
        
        Shell.Notifications.Show("Drop Success!", 
            $"'{label}' dropped in {_label}\n" +
            $"Effect: {Shell.Drag.CurrentEffect}");
        
        return true;  // Drop handled successfully
    }
    
    public Rectangle GetDropBounds() => Bounds;
    
    // === Visual Feedback ===
    
    public override void Update(GameTime gameTime) {
        base.Update(gameTime);
        
        // Check if we're being hovered during drag
        bool mouseOver = Bounds.Contains(InputManager.MousePosition);
        
        if (Shell.Drag.IsActive && mouseOver) {
            // Update effect and set hover
            var effect = Shell.Drag.CheckDropTarget(this, InputManager.MousePosition.ToVector2());
            _isHovered = effect != DragDropEffect.None;
        } else {
            // Not dragging or mouse not over us - clear hover
            _isHovered = false;
        }
        
        // Update appearance based on hover state
        if (_isHovered) {
            BackgroundColor = _baseColor * 0.6f;  // Brighten on hover
            BorderColor = Color.White;
            BorderThickness = 4;
        } else {
            BackgroundColor = _baseColor * 0.3f;
            BorderColor = _baseColor;
            BorderThickness = 3;
        }
    }
    
    protected override void UpdateInput() {
        base.UpdateInput();
        
        // Handle drop on mouse release
        if (Shell.Drag.IsActive && 
            InputManager.IsMouseButtonJustReleased(MouseButton.Left) && 
            Bounds.Contains(InputManager.MousePosition)) {
            
            // Use Shell.Drag.TryDropOn helper
            Shell.Drag.TryDropOn(this, InputManager.MousePosition.ToVector2());
        }
    }
}
