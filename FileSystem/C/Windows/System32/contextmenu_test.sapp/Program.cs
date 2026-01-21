using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;
using TheGame.Core.OS;
using TheGame.Core;
using TheGame;

namespace ContextTesting;

public class ContextMenuTest : Window {
    public static Window CreateWindow() {
        return new ContextMenuTest(new Vector2(100, 100), new Vector2(500, 450));
    }

    private bool _featureA = true;
    private readonly Texture2D _appIcon;

    public ContextMenuTest(Vector2 pos, Vector2 size) : base(pos, size) {
        Title = "Context Menu & Bubbling Test";
        AppId = "CONTEXTTEST";

        _appIcon = Shell.Images.LoadAppImage("icon.png");

        var label = new Label(new Vector2(20, 20), "1. Basic Menu Test") {
            TextColor = Color.Yellow,
            FontSize = 18
        };
        AddChild(label);

        var btn = new Button(new Vector2(20, 50), new Vector2(160, 35), "Show Base Menu") {
            OnClickAction = () => {
                Shell.ContextMenu.Show(this);
            }
        };
        AddChild(btn);

        var longBtn = new Button(new Vector2(20, 90), new Vector2(160, 35), "This is a very long text that should definitely scroll when hovered") {
            OnClickAction = () => {
                Shell.Notifications.Show("Button Test", "Long button clicked!");
            }
        };
        AddChild(longBtn);

        // --- Bubbling Logic Section ---
        var bubblingLabel = new Label(new Vector2(20, 140), "2. Bubbling Logic Test (Right-click colored boxes)") {
            TextColor = Color.Yellow,
            FontSize = 18
        };
        AddChild(bubblingLabel);

        // Outer Panel (Red)
        var outerPanel = new BubblingPanel(new Vector2(20, 170), new Vector2(300, 200), "Level 1 (Outer)", Color.Red * 0.3f);
        AddChild(outerPanel);

        // Middle Panel (Green)
        var middlePanel = new BubblingPanel(new Vector2(30, 40), new Vector2(240, 130), "Level 2 (Middle)", Color.Green * 0.3f);
        outerPanel.AddChild(middlePanel);

        // Inner Panel (Blue)
        var innerPanel = new BubblingPanel(new Vector2(30, 40), new Vector2(180, 60), "Level 3 (Inner)", Color.Blue * 0.3f);
        middlePanel.AddChild(innerPanel);
        
        var helpLabel = new Label(new Vector2(20, 380), "Tip: Right-click the Inner (Blue) box to see items from\nall three panels and the Window itself bubbling up.") {
            TextColor = Color.Gray,
            FontSize = 14
        };
        AddChild(helpLabel);
    }

    public override void PopulateContextMenu(ContextMenuContext context, List<MenuItem> items) {
        // Context menu items with some long text
        items.Add(new MenuItem { 
            Text = "Window Action With Very Long Text That Should Scroll On Hover", 
            Icon = GameContent.PCIcon,
            Action = () => Shell.Notifications.Show("Context Test", "Window level action triggered") 
        });

        items.Add(new MenuItem { Type = MenuItemType.Separator });

        // Add checkboxes
        items.Add(new MenuItem { 
            Text = "Feature A (Global-ish)", 
            Type = MenuItemType.Checkbox, 
            IsChecked = _featureA,
            Action = () => { _featureA = !_featureA; Shell.Notifications.Show("Feature A", _featureA ? "Enabled" : "Disabled"); }
        });

        // Add submenus
        var subMenu = new MenuItem { 
            Text = "Advanced Options", 
            SubItems = new List<MenuItem> {
                new MenuItem { Text = "Sub-option 1", Action = () => Shell.Notifications.Show("Submenu", "Option 1") },
                new MenuItem { 
                    Text = "Deep Menu", 
                    SubItems = new List<MenuItem> {
                        new MenuItem { Text = "Level 3 Item", IsDefault = true, Action = () => Shell.Notifications.Show("Level 3", "You found it!") }
                    }
                }
            } 
        };
        items.Add(subMenu);

        items.Add(new MenuItem { Type = MenuItemType.Separator });
        items.Add(new MenuItem { Text = "Close App", Action = () => Close() });

        base.PopulateContextMenu(context, items);
    }
}

public class BubblingPanel : Panel {
    private string _levelName;
    private Color _tint;

    public BubblingPanel(Vector2 pos, Vector2 size, string levelName, Color tint) : base(pos, size) {
        _levelName = levelName;
        _tint = tint;
        BackgroundColor = tint;
        BorderColor = Color.White * 0.5f;
        BorderThickness = 1f;
        ConsumesInput = true;

        var label = new Label(new Vector2(5, 5), levelName) {
            TextColor = Color.White,
            FontSize = 14
        };
        AddChild(label);
    }

    public override void PopulateContextMenu(ContextMenuContext context, List<MenuItem> items) {
        // Add a unique item for this level
        items.Add(new MenuItem { 
            Text = $"Action from {_levelName}", 
            Icon = GameContent.FolderIcon,
            Action = () => Shell.Notifications.Show("Bubbling Test", $"Clicked item from {_levelName}") 
        });
        
        // Don't add separator here, let the manager handle visual separation or bubble up
        base.PopulateContextMenu(context, items);
    }
}
