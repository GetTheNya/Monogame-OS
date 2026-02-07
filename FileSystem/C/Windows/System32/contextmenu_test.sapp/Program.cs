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
        return new ContextMenuTest(new Vector2(100, 100), new Vector2(550, 500));
    }

    private bool _featureA = true;
    private int _testPriority = 10;
    private readonly Label _priorityLabel;
    private readonly Texture2D _appIcon;

    public ContextMenuTest(Vector2 pos, Vector2 size) : base(pos, size) {
        Title = "Advanced Context Menu Test";
        AppId = "CONTEXTTEST";

        _appIcon = Shell.Images.LoadAppImage(OwnerProcess, "icon.png");

        var label = new Label(new Vector2(20, 20), "1. Priority & Sorting Test") {
            TextColor = Color.Yellow,
            FontSize = 18
        };
        AddChild(label);

        _priorityLabel = new Label(new Vector2(250, 55), $"Priority: {_testPriority}") {
            TextColor = Color.White,
            FontSize = 16
        };
        AddChild(_priorityLabel);

        var btnMinus = new Button(new Vector2(20, 50), new Vector2(40, 35), "-") {
            OnClickAction = () => { _testPriority -= 5; _priorityLabel.Text = $"Priority: {_testPriority}"; }
        };
        AddChild(btnMinus);

        var btnPlus = new Button(new Vector2(70, 50), new Vector2(40, 35), "+") {
            OnClickAction = () => { _testPriority += 5; _priorityLabel.Text = $"Priority: {_testPriority}"; }
        };
        AddChild(btnPlus);

        var btnMenu = new Button(new Vector2(120, 50), new Vector2(120, 35), "Show Menu") {
            OnClickAction = () => Shell.ContextMenu.Show(this)
        };
        AddChild(btnMenu);

        // --- Bubbling Logic Section ---
        var bubblingLabel = new Label(new Vector2(20, 110), "2. Bubbling & Termination (Blue sets Handled=true)") {
            TextColor = Color.Yellow,
            FontSize = 18
        };
        AddChild(bubblingLabel);

        // Outer Panel (Red)
        var outerPanel = new BubblingPanel(new Vector2(20, 140), new Vector2(300, 150), "Level 1 (Outer)", Color.Red * 0.3f, false);
        AddChild(outerPanel);

        // Middle Panel (Green)
        var middlePanel = new BubblingPanel(new Vector2(30, 40), new Vector2(240, 90), "Level 2 (Middle)", Color.Green * 0.3f, false);
        outerPanel.AddChild(middlePanel);

        // Inner Panel (Blue)
        var innerPanel = new BubblingPanel(new Vector2(30, 30), new Vector2(180, 40), "Level 3 (Terminator)", Color.Blue * 0.5f, true);
        middlePanel.AddChild(innerPanel);

        // --- File Simulation ---
        var fileLabel = new Label(new Vector2(20, 310), "3. File Simulation (Reports as .txt file)") {
            TextColor = Color.Yellow,
            FontSize = 18
        };
        AddChild(fileLabel);

        var fileSim = new FileSimulatorPanel(new Vector2(20, 340), new Vector2(150, 60), "C:\\Test.txt", Color.SlateGray);
        AddChild(fileSim);
        
        var helpLabel = new Label(new Vector2(20, 420), "Tip: Check visuals for Disabled, Hotkey, and Default items.") {
            TextColor = Color.Gray,
            FontSize = 14
        };
        AddChild(helpLabel);
    }

    public override void PopulateContextMenu(ContextMenuContext context, List<MenuItem> items) {
        // High priority item
        items.Add(new MenuItem { 
            Text = "Priority-Adjustable Item", 
            Priority = _testPriority,
            Icon = GameContent.DiskIcon,
            Action = () => Shell.Notifications.Show("Priority Test", $"Clicked with priority {_testPriority}")
        });

        items.Add(new MenuItem { Type = MenuItemType.Separator });

        // Advanced states
        items.Add(new MenuItem { 
            Text = "Default Bold Item", 
            IsDefault = true,
            Action = () => Shell.Notifications.Show("Visual Test", "Default item clicked")
        });

        items.Add(new MenuItem { 
            Text = "Disabled Item", 
            IsEnabled = false,
            Action = () => {} // Should not be callable
        });

        items.Add(new MenuItem { 
            Text = "Item with Hotkey", 
            ShortcutText = "Ctrl+Shift+T",
            Action = () => Shell.Notifications.Show("Hotkey Test", "Action triggered")
        });

        items.Add(new MenuItem { Type = MenuItemType.Separator });

        // Checkboxes
        items.Add(new MenuItem { 
            Text = "Feature A Toggle", 
            Type = MenuItemType.Checkbox, 
            IsChecked = _featureA,
            Action = () => { _featureA = !_featureA; }
        });

        // Nested Submenu
        items.Add(new MenuItem { 
            Text = "More Sub-Deep", 
            SubItems = new List<MenuItem> {
                new MenuItem { Text = "Level 2 Sub", Action = () => {} },
                new MenuItem { 
                    Text = "Level 3 Sub", 
                    SubItems = new List<MenuItem> {
                        new MenuItem { Text = "The deepest layer", Action = () => Shell.Notifications.Show("Deep", "Bottom reached!") }
                    }
                }
            }
        });

        items.Add(new MenuItem { Text = "Close", Action = () => Close(), Priority = -100 });

        base.PopulateContextMenu(context, items);
    }
}

public class BubblingPanel : Panel {
    private string _levelName;
    private bool _terminate;

    public BubblingPanel(Vector2 pos, Vector2 size, string levelName, Color tint, bool terminate) : base(pos, size) {
        _levelName = levelName;
        _terminate = terminate;
        BackgroundColor = tint;
        BorderColor = Color.White * 0.5f;
        BorderThickness = 1f;
        ConsumesInput = true;

        var label = new Label(new Vector2(5, 5), levelName) { TextColor = Color.White, FontSize = 12 };
        AddChild(label);
    }

    public override void PopulateContextMenu(ContextMenuContext context, List<MenuItem> items) {
        items.Add(new MenuItem { 
            Text = $"Item from {_levelName}", 
            Icon = GameContent.FolderIcon,
            Action = () => Shell.Notifications.Show("Bubbling", $"Source: {_levelName}") 
        });

        if (_terminate) {
            context.Handled = true;
            items.Add(new MenuItem { Text = "--- BUBBLING STOPPED HERE ---", IsEnabled = false });
        }
        
        base.PopulateContextMenu(context, items);
    }
}

public class FileSimulatorPanel : Panel {
    private string _path;

    public FileSimulatorPanel(Vector2 pos, Vector2 size, string path, Color color) : base(pos, size) {
        _path = path;
        BackgroundColor = color;
        BorderColor = Color.White;
        BorderThickness = 2f;
        ConsumesInput = true;

        var label = new Label(new Vector2(10, 10), "Right-click me\nSimulates: .txt") { TextColor = Color.White, FontSize = 12 };
        AddChild(label);
    }

    public override void PopulateContextMenu(ContextMenuContext context, List<MenuItem> items) {
        context.SetProperty("VirtualPath", _path);
        
        items.Add(new MenuItem { 
            Text = $"Simulating: {System.IO.Path.GetFileName(_path)}", 
            IsEnabled = false,
            Priority = 1000 // Ensure it's at top
        });

        base.PopulateContextMenu(context, items);
    }
}
