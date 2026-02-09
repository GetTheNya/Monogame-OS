using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using TheGame;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;
using TheGame.Core.OS;
using TheGame.Graphics;

namespace NACHOS;

public class SplashScreen : BorderlessWindow {
    private Texture2D _banner;
    private ProgressBar _progressBar;

    public float Progress {
        get => _progressBar?.Value ?? 0f;
        set { if (_progressBar != null) _progressBar.Value = value; }
    }

    public SplashScreen() : base(Vector2.Zero, new Vector2(600, 320)) {
        Title = "NACHOS - Loading...";
        
        BackgroundColor = Color.Transparent;
        ShowInTaskbar = false;
    }

    protected override void OnLoad() {
        // Load image using the helper provided by the user
        _banner = Shell.Images.LoadAppImage(OwnerProcess, "splash.png");
        
        if (_banner != null) {
            Size = new Vector2(_banner.Width, _banner.Height);

            // Center on screen again now that size is known
            var viewport = G.GraphicsDevice.Viewport;
            Position = new Vector2(
                (viewport.Width - Size.X) / 2,
                (viewport.Height - Size.Y) / 2
            );
        }

        _progressBar = new ProgressBar(new Vector2(10, Size.Y - 30), new Vector2(Size.X - 20, 20)) {
            ProgressColor = new Color(0, 150, 255), // Nice blue
            BackgroundColor = new Color(20, 20, 20, 150),
            BorderColor = Color.White * 0.2f,
            FillPadding = 0f,
            TextFormat = "Loading..."
        };
        AddChild(_progressBar);
    }

    protected override void OnDraw(SpriteBatch spriteBatch, ShapeBatch batch) {
        if (_banner != null) {
            batch.DrawTexture(_banner, AbsolutePosition, Color.White * AbsoluteOpacity);
        } else {
            // Fallback if image missing
            batch.FillRectangle(AbsolutePosition, Size, new Color(40, 40, 40) * AbsoluteOpacity, rounded: 10f);
            batch.BorderRectangle(AbsolutePosition, Size, Color.White * 0.5f * AbsoluteOpacity, thickness: 2f, rounded: 10f);
            
            if (GameContent.FontSystem != null) {
                var font = GameContent.FontSystem.GetFont(24);
                font?.DrawText(batch, "NACHOS IDE", AbsolutePosition + new Vector2(20, 20), Color.White * AbsoluteOpacity);
                font?.DrawText(batch, "Loading...", AbsolutePosition + new Vector2(20, 60), Color.Gray * AbsoluteOpacity);
            }
        }
    }
}
