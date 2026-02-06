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
using TheGame.Core.Designer;

using NACHOS;

namespace NACHOS.Designer;

public class DesignerTab : NachosTab {
    public string FilePath { get => base.FilePath; set => base.FilePath = value; }
    private DesignerSurface _surface;
    private ScrollPanel _surfaceScrollPanel;
    private PropertyGrid _propertyGrid;
    private ToolboxPanel _toolbox;
    private HierarchyPanel _hierarchy;
    private Panel _toolbar;
    
    private bool _isDirty;
    public override bool IsDirty => _isDirty;
    public new Action OnDirtyChanged { get; set; }
    public override string DisplayTitle => Path.GetFileName(FilePath) + (IsDirty ? "*" : "");

    private Action<bool> _modeChangedHandler;

    public DesignerTab(Vector2 position, Vector2 size, string filePath) : base(position, size, filePath) {
        FilePath = filePath;
        
        float sidebarWidth = 170;
        float propertyWidth = 250;
        float toolbarHeight = 35;
        
        _toolbar = new Panel(Vector2.Zero, new Vector2(size.X, toolbarHeight)) {
            BackgroundColor = new Color(35, 35, 35)
        };
        
        var saveBtn = new Button(new Vector2(5, 5), new Vector2(80, 25), "Save") {
            OnClickAction = () => Save()
        };
        _toolbar.AddChild(saveBtn);
        
        var designToggle = new Checkbox(new Vector2(100, 7), "Design Mode") {
            Value = DesignMode.IsEnabled,
            OnValueChanged = (val) => DesignMode.SetEnabled(val)
        };
        _toolbar.AddChild(designToggle);

        // Keep checkbox in sync if mode is changed elsewhere (e.g. tab switch)
        _modeChangedHandler = (enabled) => {
            designToggle.SetValue(enabled, false); // Don't notify to avoid loops
        };
        DesignMode.OnModeChanged += _modeChangedHandler;
        
        AddChild(_toolbar);
        
        _toolbox = new ToolboxPanel(new Vector2(0, toolbarHeight), new Vector2(sidebarWidth, (size.Y - toolbarHeight) * 0.4f));
        AddChild(_toolbox);
 
        _surfaceScrollPanel = new ScrollPanel(new Vector2(sidebarWidth, toolbarHeight), new Vector2(size.X - sidebarWidth - propertyWidth, size.Y - toolbarHeight));
        _surface = new DesignerSurface(Vector2.Zero, _surfaceScrollPanel.Size);
        _surfaceScrollPanel.AddChild(_surface);
        AddChild(_surfaceScrollPanel);
        
        _hierarchy = new HierarchyPanel(new Vector2(0, toolbarHeight + _toolbox.Size.Y), new Vector2(sidebarWidth, size.Y - (toolbarHeight + _toolbox.Size.Y)), _surface);
        AddChild(_hierarchy);
        
        _propertyGrid = new PropertyGrid(new Vector2(size.X - propertyWidth, toolbarHeight), new Vector2(propertyWidth, size.Y - toolbarHeight));
        AddChild(_propertyGrid);
        
        _surface.OnSelectionChanged += (el) => _propertyGrid.Inspect(el);
        _surface.OnElementModified += (el) => {
            _isDirty = true;
            OnDirtyChanged?.Invoke();
            _propertyGrid.Inspect(el);
            _hierarchy.Refresh();
        };
        
        OnResize += () => {
            _toolbar.Size = new Vector2(Size.X, toolbarHeight);
            _toolbox.Size = new Vector2(sidebarWidth, (Size.Y - toolbarHeight) * 0.4f);
            
            _hierarchy.Position = new Vector2(0, toolbarHeight + _toolbox.Size.Y);
            _hierarchy.Size = new Vector2(sidebarWidth, Size.Y - (toolbarHeight + _toolbox.Size.Y));

            _propertyGrid.Position = new Vector2(Size.X - propertyWidth, toolbarHeight);
            _propertyGrid.Size = new Vector2(propertyWidth, Size.Y - toolbarHeight);
            
            _surfaceScrollPanel.Position = new Vector2(sidebarWidth, toolbarHeight);
            _surfaceScrollPanel.Size = new Vector2(Size.X - sidebarWidth - propertyWidth, Size.Y - toolbarHeight);
            
            // Note: DesignerSurface will automatically resize itself in its Update method
            // based on its children and the ScrollPanel's viewport size.
        };
        
        Load();
    }
    
    public override void Save() {
        if (string.IsNullOrEmpty(FilePath)) return;
        
        try {
            var root = _surface.ContentLayer.Children.FirstOrDefault();
            if (root != null) {
                string json = UISerializer.Serialize(root);
                VirtualFileSystem.Instance.WriteAllText(FilePath, json);
                _isDirty = false;
                OnDirtyChanged?.Invoke();
                Shell.Notifications.Show("Designer", "UI Layout saved successfully.");
            }
        } catch (Exception ex) {
            DebugLogger.Log($"Error saving UI to {FilePath}: {ex.Message}");
            Shell.Notifications.Show("Designer", "Error saving layout: " + ex.Message);
        }
    }
    
    public void Load() {
        if (VirtualFileSystem.Instance.Exists(FilePath)) {
            try {
                string json = VirtualFileSystem.Instance.ReadAllText(FilePath);
                var root = UISerializer.Deserialize(json);
                if (root != null) {
                    _surface.ContentLayer.ClearChildren();
                    _surface.ContentLayer.AddChild(root);
                }
            } catch (Exception ex) {
                DebugLogger.Log($"Error loading UI from {FilePath}: {ex.Message}");
                Shell.Notifications.Show("Designer", "Error loading layout: " + ex.Message);
            }
        }
        
        // If empty / new layout, add default Window base component
        if (!_surface.ContentLayer.Children.Any()) {
            var defaultWindow = new DesignerWindow(new Vector2(50, 50), new Vector2(400, 300)) {
                Title = "New Window"
            };
            _surface.ContentLayer.AddChild(defaultWindow);
            _surface.SelectElement(defaultWindow);
            
            // Mark as dirty since we just added a base component
            _isDirty = true;
            OnDirtyChanged?.Invoke();
        }
    }
    
    public override void Dispose() {
        if (_modeChangedHandler != null) {
            DesignMode.OnModeChanged -= _modeChangedHandler;
        }
        base.Dispose();
    }
}
