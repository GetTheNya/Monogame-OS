using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using TheGame.Core;
using TheGame.Core.Input;
using TheGame.Core.Scenes;
using TheGame.Graphics;
using TheGame.Core.OS;
using System;
using System.IO;
using System.Threading.Tasks;

namespace TheGame;

public class Game1 : Game {
    public static Game1 Instance { get; private set; }
    public static GraphicsDeviceManager _graphics;
    private ShapeBatch _shapeBatch;
    private SpriteBatch _spriteBatch;

    private SceneManager _sceneManager;
    public SceneManager SceneManager => _sceneManager;

    private WindowsKeyHook _winKeyHook;

    private Fps _fps;

    private static bool _screenshotRequested;
    private static TaskCompletionSource<Texture2D> _screenshotTcs;

    public Game1() {
        Instance = this;
        _graphics = new GraphicsDeviceManager(this) {
            GraphicsProfile = GraphicsProfile.HiDef
        };

        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.AllowUserResizing = true;

        Window.AllowAltF4 = false;
        
        Window.TextInput += (s, e) => {
            InputManager.AddChar(e.Character);
        };
    }

    protected override void Initialize() {
        _graphics.PreferredBackBufferWidth = 1500;
        _graphics.PreferredBackBufferHeight = 1000;

        _graphics.SynchronizeWithVerticalRetrace = false;
        IsFixedTimeStep = false;

        _graphics.ApplyChanges();

        G.GraphicsDevice = GraphicsDevice;
        G.ContentManager = Content;

        _winKeyHook = new WindowsKeyHook();

        this.Activated += (s, e) => _winKeyHook.SetActive(true);
        this.Deactivated += (s, e) => _winKeyHook.SetActive(false);

        base.Initialize();
    }

    protected override void LoadContent() {
        _shapeBatch = new ShapeBatch(GraphicsDevice, Content);
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        G.ShapeBatch = _shapeBatch;

        // Initialize Virtual File System
        string fsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FileSystem");
        VirtualFileSystem.Instance.Initialize(fsPath);

        GameContent.InitContent();

        // All apps are now loaded dynamically from System32 via AppLoader

        _sceneManager = new SceneManager(Content, GraphicsDevice);
        _sceneManager.LoadScene(new TheGame.Scenes.LoadingScene());
        // DesktopScene is loaded via transition in LoginScene (which is loaded via transition in LoadingScene)

        _fps = new Fps();
        
        // Initialize custom cursor
        CustomCursor.Instance.Initialize(GraphicsDevice);
    }

    protected override void Update(GameTime gameTime) {
        if (!IsActive) {
            base.Update(gameTime);
            return;
        }

        InputManager.Update(gameTime);
        CustomCursor.Instance.BeginFrame();

        // Update Shell and Processes FIRST so they can consume input for the frame
        Shell.Update(gameTime);
        
        _sceneManager.Update(gameTime);
        _fps.Update(gameTime);

        AudioManager.Instance.Update((float)gameTime.ElapsedGameTime.TotalSeconds);
        Registry.Instance.Update((float)gameTime.ElapsedGameTime.TotalSeconds);
        DebugLogger.Update((float)gameTime.ElapsedGameTime.TotalSeconds);

        // The drag cleanup is now handled by DragDropManager through Shell.DraggedItem wrapper

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime) {
        GraphicsDevice.Clear(Color.Black);

        _sceneManager.Draw(_spriteBatch, _shapeBatch);

        // Draw global application overlays
        ProcessManager.Instance.Draw(_spriteBatch, _shapeBatch);

        _shapeBatch.Begin();
        _spriteBatch.Begin();

        // Debug FPS
        SpriteFontBase font = GameContent.FontSystem.GetFont(30);
        var posText = $"FPS: {_fps.CurrentFps:F2}";
        font.DrawText(_shapeBatch, posText, new Vector2(10, 10), Color.White);

        // Draw Drag Overlay (files, icons being moved)
        Shell.DrawDrag(_spriteBatch, _shapeBatch);

        _shapeBatch.End();
        _spriteBatch.End();

        _fps.UpdateDrawFps();
        
        // Draw custom cursor LAST (on top of everything)
        CustomCursor.Instance.Draw();

        base.Draw(gameTime);

        if (_screenshotRequested) {
            DebugLogger.Log("Screenshot requested!");
            _screenshotRequested = false;
            
            int width = GraphicsDevice.PresentationParameters.BackBufferWidth;
            int height = GraphicsDevice.PresentationParameters.BackBufferHeight;
            
            Color[] data = new Color[width * height];
            GraphicsDevice.GetBackBufferData(data);
            
            Texture2D screenshot = new Texture2D(GraphicsDevice, width, height);
            screenshot.SetData(data);
            
            DebugLogger.Log("Screenshot made!");
            
            _screenshotTcs?.SetResult(screenshot);
            _screenshotTcs = null;
        }

    }

    public static Task<Texture2D> CaptureScreenshotAsync() {
        if (_screenshotRequested) return _screenshotTcs.Task;
        
        _screenshotRequested = true;
        _screenshotTcs = new TaskCompletionSource<Texture2D>();
        return _screenshotTcs.Task;
    }

    protected override void OnExiting(object sender, EventArgs args) {
        AudioManager.Instance.Shutdown();

        _winKeyHook?.Dispose();

        base.OnExiting(sender, args);
    }

    public static float Map(float value, float fromLow, float fromHigh, float toLow, float toHigh) {
        float normalized = (value - fromLow) / (fromHigh - fromLow);
        return toLow + normalized * (toHigh - toLow);
    }
}