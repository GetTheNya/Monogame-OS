using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Core;
using TheGame.Core.OS;
using TheGame.Core.UI;

namespace TheGame.Scenes;

public class InstallerScene : Core.Scenes.Scene {
    private OSInstaller _installer;
    private UIManager _uiManager;
    private bool _isInitialized = false;
    private Texture2D _background;

    public override void LoadContent(ContentManager content) {
        _uiManager = new UIManager();
        // Load some background if needed, or just use a solid color
        _background = new Texture2D(G.GraphicsDevice, 1, 1);
        _background.SetData(new[] { new Color(15, 15, 20) });
    }

    public override void Update(GameTime gameTime) {
        if (!_isInitialized) {
            InitializeSetup();
            _isInitialized = true;
        }

        if (_uiManager != null) {
            _uiManager.Update(gameTime);
        }
    }

    private void InitializeSetup() {
        // Ensure Registry is initialized (should be done in Game1, but let's be safe)
        Registry.Instance.Initialize();

        bool isInstalled = Registry.Instance.GetValue("HKLM\\System\\Setup", "OSInstalled", false);

        if (isInstalled) {
            Finish();
        } else {
            StartInstaller();
        }
    }

    private void StartInstaller() {
        var data = new InstallerData();
        _installer = new OSInstaller(data);
        _installer.OnFinished += (finalData) => {
            // Apply data to SystemConfig
            SystemConfig.UpdateUser(finalData.Username, finalData.DisplayName, finalData.AccentColor);
            
            // Mark as installed
            Registry.Instance.SetValue("HKLM\\System\\Setup", "OSInstalled", true);
            Registry.Instance.FlushToDisk();
            
            Finish();
        };
        
        // Ensure it has focus and is managed by UIManager
        _uiManager.AddElement(_installer);
        _installer.IsFocused = true;
    }

    private void Finish() {
        // Initialize SystemConfig for the rest of the OS
        SystemConfig.Initialize();
        
        // Transition to app compilation
        SceneManager.TransitionTo(new LoadingScene());
    }

    public override void Draw(SpriteBatch spriteBatch, Graphics.ShapeBatch shapeBatch) {
        spriteBatch.Begin();
        spriteBatch.Draw(_background, new Rectangle(0, 0, G.GraphicsDevice.Viewport.Width, G.GraphicsDevice.Viewport.Height), Color.White);
        spriteBatch.End();

        if (_uiManager != null) {
            shapeBatch.Begin();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

            _uiManager.Draw(spriteBatch, shapeBatch);

            shapeBatch.End();
            spriteBatch.End();
        }
    }
}
