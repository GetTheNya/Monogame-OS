using System;
using Microsoft.Xna.Framework;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;
using TheGame.Core.OS;
using TheGame.Core;

namespace SettingsApp.Panels;

public class PersonalizationPanel : Panel {
    public PersonalizationPanel() : base(Vector2.Zero, Vector2.Zero) {
        BackgroundColor = Color.Transparent;
        SetupUI();
    }
    
    private void SetupUI() {
        float y = 20;
        
        // Wallpaper Section
        AddChild(new Label(new Vector2(20, y), "Wallpaper") { FontSize = 20 });
        y += 35;
        
        var pathInput = new TextInput(new Vector2(20, y), new Vector2(400, 30)) {
            Value = Settings.Personalization.WallpaperPath
        };
        AddChild(pathInput);
        
        var browseBtn = new Button(new Vector2(430, y), new Vector2(100, 30), "Browse") {
            OnClickAction = () => {
                var picker = new FilePickerWindow(
                    "Select Wallpaper",
                    "C:\\",
                    "",
                    FilePickerMode.Open,
                    (selectedPath) => {
                        Settings.Personalization.WallpaperPath = selectedPath;
                        pathInput.Value = selectedPath;
                    },
                    new[] { ".jpg", ".jpeg", ".png", ".bmp" } // Image files only
                );
                Shell.UI.OpenWindow(picker);
            }
        };
        AddChild(browseBtn);
        y += 50;
        
        // Draw Mode
        AddChild(new Label(new Vector2(20, y), "Draw Mode"));
        y += 30;
        
        var modeCombo = new ComboBox(new Vector2(20, y), new Vector2(200, 30));
        modeCombo.Items.Add("Fill");
        modeCombo.Items.Add("Fit");
        modeCombo.Items.Add("Stretch");
        modeCombo.Items.Add("Tile");
        modeCombo.Items.Add("Center");
        
        // Set current value
        string currentMode = Settings.Personalization.WallpaperDrawMode;
        int currentIndex = modeCombo.Items.IndexOf(currentMode);
        if (currentIndex >= 0) modeCombo.Value = currentIndex;
        
        modeCombo.OnValueChanged += (newValue) => {
            if (newValue >= 0 && newValue < modeCombo.Items.Count) {
                Settings.Personalization.WallpaperDrawMode = modeCombo.Items[newValue];
            }
        };
        AddChild(modeCombo);
        y += 50;
        
        // Description
        AddChild(new Label(new Vector2(20, y), "Fill - Cover screen (may crop)") { FontSize = 14, TextColor = Color.Gray });
        y += 20;
        AddChild(new Label(new Vector2(20, y), "Fit - Show entire image (may have bars)") { FontSize = 14, TextColor = Color.Gray });
        y += 20;
        AddChild(new Label(new Vector2(20, y), "Stretch - Distort to fill screen") { FontSize = 14, TextColor = Color.Gray });
        y += 20;
        AddChild(new Label(new Vector2(20, y), "Tile - Repeat pattern") { FontSize = 14, TextColor = Color.Gray });
        y += 20;
        AddChild(new Label(new Vector2(20, y), "Center - Original size, centered") { FontSize = 14, TextColor = Color.Gray });
    }
}
