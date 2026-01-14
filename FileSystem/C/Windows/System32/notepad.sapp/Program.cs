using System;
using Microsoft.Xna.Framework;
using TheGame;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;
using TheGame.Core.OS;

namespace NotepadApp;

public class AppSettings {
    public string LastText { get; set; } = "";
    public float WindowX { get; set; } = 200;
    public float WindowY { get; set; } = 150;
    public float WindowWidth { get; set; } = 500;
    public float WindowHeight { get; set; } = 400;
}

public class Program {
    public static Window CreateWindow() {
        var settings = Shell.LoadSettings<AppSettings>();
        var window = new Window(new Vector2(settings.WindowX, settings.WindowY), new Vector2(settings.WindowWidth, settings.WindowHeight));
        window.Title = "Notepad";
        
        var edit = new TextInput(Vector2.Zero, window.ClientSize) {
            Placeholder = "Start typing...",
            Value = settings.LastText
        };
        window.AddChild(edit);
        
        edit.OnValueChanged += (val) => {
            settings.LastText = val;
            Shell.SaveSettings(settings);
        };
        
        window.OnResize += () => {
            edit.Size = window.ClientSize;
            settings.WindowWidth = window.Size.X;
            settings.WindowHeight = window.Size.Y;
            Shell.SaveSettings(settings);
        };

        window.OnMove += () => {
            if (window.Opacity < 0.9f) return; // Ignore animations
            settings.WindowX = window.Position.X;
            settings.WindowY = window.Position.Y;
            Shell.SaveSettings(settings);
        };

        // We also need to save position. I'll add OnMove event to Window.
        
        return window;
    }
}
