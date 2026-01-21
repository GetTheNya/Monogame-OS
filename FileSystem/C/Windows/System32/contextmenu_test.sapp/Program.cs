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
        return new ContextMenuTest(new Vector2(150, 150), new Vector2(400, 300));
    }

    private bool _featureA = true;
    private bool _featureB = false;
    private readonly Texture2D _appIcon;

    public ContextMenuTest(Vector2 pos, Vector2 size) : base(pos, size) {
        Title = "Context Menu Test App";
        AppId = "CONTEXTTEST";

        _appIcon = Shell.Images.LoadAppImage("icon.png");

        var label = new Label(new Vector2(20, 20), "Right-click anywhere or use the button") {
            TextColor = Color.LightGray,
            FontSize = 18
        };
        AddChild(label);

        var btn = new Button(new Vector2(20, 60), new Vector2(160, 40), "Open Menu") {
            OnClickAction = () => {
                Shell.ContextMenu.Show(this);
            }
        };
        AddChild(btn);
    }

    public override void PopulateContextMenu(ContextMenuContext context, List<MenuItem> items) {
        // Add basic items
        items.Add(new MenuItem { 
            Text = "Refresh State", 
            Icon = GameContent.FolderIcon, // Using a system icon for test
            Action = () => Shell.Notifications.Show("Context Test", "State Refreshed", _appIcon, null) 
        });

        items.Add(new MenuItem { Type = MenuItemType.Separator });

        // Add checkboxes
        items.Add(new MenuItem { 
            Text = "Enable Feature A", 
            Type = MenuItemType.Checkbox, 
            IsChecked = _featureA,
            Action = () => { _featureA = !_featureA; Shell.Notifications.Show("Feature A", _featureA ? "Enabled" : "Disabled"); }
        });

        items.Add(new MenuItem { 
            Text = "Enable Feature B", 
            Type = MenuItemType.Checkbox, 
            IsChecked = _featureB,
            Action = () => { _featureB = !_featureB; Shell.Notifications.Show("Feature B", _featureB ? "Enabled" : "Disabled"); }
        });

        items.Add(new MenuItem { Type = MenuItemType.Separator });

        // Add submenus
        var subMenu = new MenuItem { 
            Text = "Advanced Options", 
            SubItems = new List<MenuItem> {
                new MenuItem { Text = "Sub-option 1", Action = () => Shell.Notifications.Show("Submenu", "Option 1 Selected") },
                new MenuItem { Text = "Sub-option 2", Action = () => Shell.Notifications.Show("Submenu", "Option 2 Selected") },
                new MenuItem { Type = MenuItemType.Separator },
                new MenuItem { 
                    Text = "Deep Menu", 
                    SubItems = new List<MenuItem> {
                        new MenuItem { Text = "Level 3 Item", IsDefault = true, Action = () => Shell.Notifications.Show("Level 3", "You went deep!") }
                    }
                }
            } 
        };
        items.Add(subMenu);

        items.Add(new MenuItem { Type = MenuItemType.Separator });

        items.Add(new MenuItem { 
            Text = "Close Application", 
            Action = () => Close() 
        });

        base.PopulateContextMenu(context, items);
    }
}
