using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Core.OS;
using TheGame.Core.UI;
using TheGame.Graphics;
using TheGame.Core.UI.Controls;
using TheGame.Core;
using TheGame;

namespace UnifiedTestApp;

public class Program : Application {
    public static Application Main(string[] args) {
        return new Program();
    }

    private float _timer = 0;
    private int _updateCount = 0;

    public override void Initialize(string[] args) {
        DebugLogger.Log("UnifiedTestApp: Initialize called!");
        MainWindow = CreateWindow<TestWindow>();
        MainWindow.Title = "Unified API - Main Window";
        
        // Test multi-window
        var secondary = CreateWindow<Window>();
        secondary.Title = "Unified API - Secondary";
        secondary.Position = new Vector2(100, 100);
        secondary.Size = new Vector2(300, 200);
        OpenWindow(secondary);
    }

    public override void Update(GameTime gameTime) {
        _timer += (float)gameTime.ElapsedGameTime.TotalSeconds;
        _updateCount++;

        if (_timer >= 5.0f) {
            _timer = 0;
            Shell.Notifications.Show("UnifiedTestApp", $"UnifiedTestApp: Background Update running... Updates={_updateCount}");
        }
    }

    public override void Draw(SpriteBatch spriteBatch, ShapeBatch shapeBatch) {
        // Draw a global indicator in the corner
        shapeBatch.Begin();
        shapeBatch.FillRectangle(new Vector2(10, 50), new Vector2(200, 30), new Color(0, 0, 0, 150));
        shapeBatch.End();

        spriteBatch.Begin();
        var font = GameContent.FontSystem.GetFont(20);
        font.DrawText(spriteBatch, $"Global App Hook: {_updateCount}", new Vector2(20, 55), Color.Lime);
        spriteBatch.End();
    }

    public override void Terminate() {
        DebugLogger.Log("UnifiedTestApp: Terminate called!");
    }
}

public class TestWindow : Window {
    public TestWindow() : base(Vector2.Zero, new Vector2(400, 300)) {
        Title = "Unified Test Window";

        AddChild(new Label(new Vector2(20, 50), "I am the MainWindow") { Color = Color.White });
        AddChild(new Label(new Vector2(20, 80), "Managed by Application class") { Color = Color.LightGray });
    
        var modalWindow = new Modal();

        var button = new Button(new Vector2(20, 120), new Vector2(100, 30), "Test");
        button.OnClickAction += () => {
            OwnerProcess.Application.OpenModal(modalWindow, button.Bounds);
        };
        AddChild(button);
    }
}

public class Modal : Window {
    public Modal() : base(Vector2.Zero, new Vector2(400, 300)) {
        AddChild(new Label(Vector2.Zero, "Modal"));
    }
}
