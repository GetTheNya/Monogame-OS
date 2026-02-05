using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;
using TheGame.Core.OS;
using TheGame.Core;
using TheGame.Core.Input;

namespace NACHOS.Designer;

public class DesignerTab : UIElement {
    public string FilePath { get; set; }
    private DesignerSurface _surface;
    private PropertyGrid _propertyGrid;
    private ToolboxPanel _toolbox;
    private Panel _toolbar;
    
    public bool IsDirty { get; set; }
    public Action OnDirtyChanged { get; set; }

    public DesignerTab(Vector2 position, Vector2 size, string filePath) : base(position, size) {
        FilePath = filePath;
        
        float sidebarWidth = 200;
        float propertyWidth = 250;
        float toolbarHeight = 35;
        
        _toolbar = new Panel(Vector2.Zero, new Vector2(size.X, toolbarHeight)) {
            BackgroundColor = new Color(35, 35, 35)
        };
        
        var saveBtn = new Button(new Vector2(5, 5), new Vector2(80, 25), "Save") {
            OnClickAction = () => Save()
        };
        _toolbar.AddChild(saveBtn);
        
        AddChild(_toolbar);
        
        _toolbox = new ToolboxPanel(new Vector2(0, toolbarHeight), new Vector2(sidebarWidth, size.Y - toolbarHeight));
        AddChild(_toolbox);
        
        _propertyGrid = new PropertyGrid(new Vector2(size.X - propertyWidth, toolbarHeight), new Vector2(propertyWidth, size.Y - toolbarHeight));
        AddChild(_propertyGrid);
        
        _surface = new DesignerSurface(new Vector2(sidebarWidth, toolbarHeight), new Vector2(size.X - sidebarWidth - propertyWidth, size.Y - toolbarHeight));
        _surface.OnSelectionChanged += (el) => _propertyGrid.Inspect(el);
        _surface.OnElementModified += (el) => {
            IsDirty = true;
            OnDirtyChanged?.Invoke();
        };
        AddChild(_surface);
        
        OnResize += () => {
            _toolbar.Size = new Vector2(Size.X, toolbarHeight);
            _toolbox.Size = new Vector2(sidebarWidth, Size.Y - toolbarHeight);
            _propertyGrid.Position = new Vector2(Size.X - propertyWidth, toolbarHeight);
            _propertyGrid.Size = new Vector2(propertyWidth, Size.Y - toolbarHeight);
            _surface.Position = new Vector2(sidebarWidth, toolbarHeight);
            _surface.Size = new Vector2(Size.X - sidebarWidth - propertyWidth, Size.Y - toolbarHeight);
            
            // Re-sync surface layers
            _surface.ContentLayer.Size = _surface.Size;
            _surface.AdornerLayer.Size = _surface.Size;
        };
        
        Load();
    }
    
    public void Save() {
        if (string.IsNullOrEmpty(FilePath)) return;
        
        try {
            var root = _surface.ContentLayer.Children.FirstOrDefault();
            if (root != null) {
                string json = UISerializer.Serialize(root);
                File.WriteAllText(FilePath, json);
                IsDirty = false;
                OnDirtyChanged?.Invoke();
                Shell.Notifications.Show("Designer", "UI Layout saved successfully.");
            }
        } catch (Exception ex) {
            Console.WriteLine($"Error saving UI to {FilePath}: {ex.Message}");
            Shell.Notifications.Show("Designer", "Error saving layout: " + ex.Message);
        }
    }
    
    public void Load() {
        if (File.Exists(FilePath)) {
            try {
                string json = File.ReadAllText(FilePath);
                var root = UISerializer.Deserialize(json);
                if (root != null) {
                    _surface.ContentLayer.ClearChildren();
                    _surface.ContentLayer.AddChild(root);
                }
            } catch (Exception ex) {
                Console.WriteLine($"Error loading UI from {FilePath}: {ex.Message}");
                Shell.Notifications.Show("Designer", "Error loading layout: " + ex.Message);
            }
        }
        
        // If empty / new layout, add default Window base component
        if (!_surface.ContentLayer.Children.Any()) {
            var defaultWindow = new Window(new Vector2(50, 50), new Vector2(400, 300)) {
                Title = "New Window",
                ShowChromeButtons = false
            };
            _surface.ContentLayer.AddChild(defaultWindow);
            _surface.SelectElement(defaultWindow);
            
            // Mark as dirty since we just added a base component
            IsDirty = true;
            OnDirtyChanged?.Invoke();
        }
    }
}
