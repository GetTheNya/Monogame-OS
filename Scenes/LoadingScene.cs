using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Core;
using TheGame.Core.OS;
using TheGame.Core.UI.Controls;

namespace TheGame.Scenes;

public class LoadingScene : Core.Scenes.Scene {

    private readonly LoadingSpinner _spinner;
    private Texture2D _hentosLogo;
    private Vector2 _hentosLogoPos;
    private float _loadingProgress = 0f;
    private int _compiledApps;
    private int _allApps;
    private bool _startedLoading = false;

    public LoadingScene() {
        _spinner = new LoadingSpinner(Vector2.Zero, new Vector2(80, 80)) {
            Color = Color.White,
            Thickness = 5,
        };
        var viewport = G.GraphicsDevice.Viewport;
        var spinnerPos = new Vector2((viewport.Width / 2) - (_spinner.Size.X / 2), ((viewport.Height / 2) - (_spinner.Size.Y / 2)) * 1.8f);
        _spinner.Position = spinnerPos;
    }

    public override void LoadContent(ContentManager content) { 
        _hentosLogo = ImageLoader.Load(G.GraphicsDevice, "Icon.png");
        var viewport = G.GraphicsDevice.Viewport;
        _hentosLogoPos = new Vector2((viewport.Width / 2) - (_hentosLogo.Width / 2), ((viewport.Height / 2) - (_hentosLogo.Height / 2)) * 0.5f);
    }

    private async void StartLoading() {
        _startedLoading = true;
        await AppLoader.Instance.LoadAppsFromDirectoryAsync(new[] {
            "C:\\Windows\\System32\\",
            "C:\\Windows\\System32\\TerminalApps\\"
        });
    }

    public override void UnloadContent() { 

    }

    public override void Update(GameTime gameTime) { 
        if (!_startedLoading) StartLoading();

        _spinner.Update(gameTime);

        if (AppLoader.Instance.TotalAppsToLoad > 0) {
            _loadingProgress = (float)AppLoader.Instance.AppsLoadedCount / AppLoader.Instance.TotalAppsToLoad;
            _compiledApps = AppLoader.Instance.AppsLoadedCount;
            _allApps = AppLoader.Instance.TotalAppsToLoad;
        }

        if (AppLoader.Instance.IsLoadingComplete) {
            SceneManager.TransitionTo(new LoginScene());
        }
    }

    public override void Draw(SpriteBatch spriteBatch, Graphics.ShapeBatch shapeBatch) {
        spriteBatch.Begin();

        spriteBatch.Draw(_hentosLogo, _hentosLogoPos, Color.White);

        // Draw progress text
        var viewport = G.GraphicsDevice.Viewport;
        var font = GameContent.FontSystem.GetFont(24);

        string loadingText = $"Loading System... {(int)(_loadingProgress * 100)}%";
        var loadingTextSize = font.MeasureString(loadingText);
        font.DrawText(spriteBatch, loadingText, new Vector2((viewport.Width / 2) - (loadingTextSize.X / 2), (viewport.Height / 2) + 100), Color.White * 0.7f);
        
        string additionalText = $"Compiling JIT apps {_compiledApps}/{_allApps}";
        var additionalTextSize = font.MeasureString(additionalText);
        font.DrawText(spriteBatch, additionalText, new Vector2((viewport.Width / 2) - (additionalTextSize.X / 2), (viewport.Height / 2) + 125), Color.White * 0.7f);

        spriteBatch.End();
        
        _spinner.Draw(spriteBatch, shapeBatch);
    }   
}