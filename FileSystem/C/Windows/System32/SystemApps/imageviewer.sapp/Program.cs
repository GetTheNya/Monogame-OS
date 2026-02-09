using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using TheGame.Core.OS;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;
using TheGame.Core.Input;
using TheGame.Graphics;
using TheGame.Core;
using TheGame;

namespace ImageViewerApp;

public class Program : Application {
    public static string[] SupportedExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif" };

    public static Application Main(string[] args) => new Program();

    protected override void OnLoad(string[] args) {
        foreach (var ext in SupportedExtensions) {
            var extNoDot = ext.Substring(1);
            Shell.File.RegisterFileTypeHandler(Process, ext, $"FileIcons/{extNoDot}.png", $"Image Viewer {extNoDot}");
        }

        string filePath = args != null && args.Length > 0 ? args[0] : null;
        var window = CreateWindow<ImageViewerWindow>();
        if (!string.IsNullOrEmpty(filePath)) {
            window.LoadFile(filePath);
        }
        MainWindow = window;
    }
}

/// <summary>
/// Custom control for rendering the image with zoom and pan support.
/// This ensures it's drawn behind other UI elements like MenuBar.
/// </summary>
public class ImageControl : UIControl {
    public Texture2D Image { get; set; }
    public float Zoom { get; set; } = 1f;
    public Vector2 Offset { get; set; } = Vector2.Zero;
    public bool PixelPerfect { get; set; } = false;

    public ImageControl(Vector2 pos, Vector2 size) : base(pos, size) {
        BackgroundColor = Color.Transparent;
        ConsumesInput = false; // Background element
    }

    protected override void DrawSelf(SpriteBatch spriteBatch, ShapeBatch batch) {
        if (Image == null) return;

        // Custom drawing with specific SamplerState
        batch.End();
        spriteBatch.End();

        var sampler = PixelPerfect ? SamplerState.PointClamp : SamplerState.LinearClamp;
        
        // Use Scissor to clip image to control bounds
        var gd = G.GraphicsDevice;
        var oldScissor = gd.ScissorRectangle;
        gd.ScissorRectangle = Bounds;

        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, sampler, DepthStencilState.None, gd.RasterizerState, null);
        
        spriteBatch.Draw(Image, AbsolutePosition + Offset, null, Color.White * AbsoluteOpacity, 0f, Vector2.Zero, Zoom, SpriteEffects.None, 0f);

        spriteBatch.End();
        gd.ScissorRectangle = oldScissor;
        
        batch.Begin();
        spriteBatch.Begin();
    }
}

public class ImageViewerWindow : Window {
    private ImageControl _imageControl;
    private string _currentFilePath;
    private List<string> _folderImages = new();
    private int _currentFileIndex = -1;

    // Interaction State
    private Vector2 _velocity = Vector2.Zero;
    private bool _isPanning = false;
    private Point _lastMousePos;
    private bool _justBecameActive = true;

    // UI Controls
    private MenuBar _menuBar;
    private Panel _statusBar;
    private Label _infoLabel;
    private LoadingSpinner _loadingSpinner;
    private const float MenuBarHeight = 26f;
    private const float StatusBarHeight = 22f;

    public ImageViewerWindow() {
        Title = "Image Viewer";
        Size = new Vector2(800, 600);
        AppId = "IMAGEVIEWER";
        OnResize += LayoutUI;
    }

    protected override void OnLoad() {
        SetupUI();
        _lastMousePos = InputManager.MousePosition;
    }

    private void SetupUI() {
        // Image Control (Background)
        _imageControl = new ImageControl(Vector2.Zero, ClientSize);
        AddChild(_imageControl);

        // Menu Bar
        _menuBar = new MenuBar(Vector2.Zero, new Vector2(ClientSize.X, MenuBarHeight));
        _menuBar.AddMenu("File", m => {
            m.AddItem("Open...", OpenFile, "Ctrl+O");
            m.AddSeparator();
            m.AddItem("Exit", Close);
        });
        _menuBar.AddMenu("View", m => {
            m.AddItem("Fit to Window", FitToWindow, "F1");
            m.AddItem("Actual Size (1:1)", () => SetZoom(1f), "F2");
            m.AddSeparator();
            m.AddItem("Pixel Perfect (Nearest)", () => _imageControl.PixelPerfect = !_imageControl.PixelPerfect, "P");
        });
        AddChild(_menuBar);
        _menuBar.RegisterHotkeys(OwnerProcess);

        // Status Bar
        _statusBar = new Panel(new Vector2(0, ClientSize.Y - StatusBarHeight), new Vector2(ClientSize.X, StatusBarHeight)) {
            BackgroundColor = new Color(35, 35, 35),
            BorderColor = new Color(60, 60, 60),
            BorderThickness = 1
        };
        _infoLabel = new Label(new Vector2(10, 3), "No image loaded") { FontSize = 14, Color = Color.LightGray };
        _statusBar.AddChild(_infoLabel);
        AddChild(_statusBar);

        // Loading Spinner
        _loadingSpinner = new LoadingSpinner(Vector2.Zero, new Vector2(40, 40)) {
            IsVisible = false,
            Thickness = 4f
        };
        AddChild(_loadingSpinner);

        // Register Navigation Keys
        Shell.Hotkeys.RegisterLocal(OwnerProcess, Keys.Left, HotkeyModifiers.None, PreviousImage);
        Shell.Hotkeys.RegisterLocal(OwnerProcess, Keys.Right, HotkeyModifiers.None, NextImage);
        Shell.Hotkeys.RegisterLocal(OwnerProcess, "Ctrl+O", OpenFile);
        Shell.Hotkeys.RegisterLocal(OwnerProcess, Keys.F1, HotkeyModifiers.None, FitToWindow);
        Shell.Hotkeys.RegisterLocal(OwnerProcess, Keys.F2, HotkeyModifiers.None, () => SetZoom(1f));
        Shell.Hotkeys.RegisterLocal(OwnerProcess, Keys.P, HotkeyModifiers.None, () => _imageControl.PixelPerfect = !_imageControl.PixelPerfect);
    }

    private void LayoutUI() {
        if (_imageControl != null) _imageControl.Size = ClientSize;
        if (_menuBar != null) _menuBar.Size = new Vector2(ClientSize.X, MenuBarHeight);
        if (_statusBar != null) {
            _statusBar.Position = new Vector2(0, ClientSize.Y - StatusBarHeight);
            _statusBar.Size = new Vector2(ClientSize.X, StatusBarHeight);
        }
        if (_loadingSpinner != null) {
            _loadingSpinner.Position = (ClientSize - _loadingSpinner.Size) / 2f;
        }
    }

    protected override void DisposeGraphicsResources() {
        base.DisposeGraphicsResources();
        if (_currentFilePath != null) Shell.Images.Unload(_currentFilePath);
    }

    private void OpenFile() {
        var picker = new FilePickerWindow(
            "Select Image",
            "C:\\Users\\Admin\\Documents\\",
            "",
            FilePickerMode.Open,
            LoadFile,
            Program.SupportedExtensions
        );
        Shell.UI.OpenWindow(picker, owner: this.OwnerProcess);
    }

    public async void LoadFile(string path) {
        if (string.IsNullOrEmpty(path)) return;
        
        _infoLabel.Text = $"Loading {Path.GetFileName(path)}...";
        _loadingSpinner.IsVisible = true;
        try {
            var newImage = await Shell.Images.LoadAsync(path);
            if (newImage != null) {
                // Safely unload old image via the caching system
                if (_currentFilePath != null) Shell.Images.Unload(_currentFilePath);
                
                _imageControl.Image = newImage;
                _currentFilePath = path;
                Title = $"{Path.GetFileName(path)} - Image Viewer";
                UpdateFolderList(path);
                UpdateStatusBar();
                FitToWindow();
            } else {
                _infoLabel.Text = "Failed to load image.";
            }
        } catch (Exception ex) {
            _infoLabel.Text = "Error loading image.";
            Shell.Notifications.Show("Image Viewer", $"Error loading image: {ex.Message}");
        } finally {
            _loadingSpinner.IsVisible = false;
        }
    }

    private void UpdateFolderList(string currentPath) {
        try {
            string dir = Path.GetDirectoryName(currentPath);
            if (string.IsNullOrEmpty(dir)) dir = "C:\\";
            
            var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            extensions.UnionWith(Program.SupportedExtensions);
            _folderImages = VirtualFileSystem.Instance.GetFiles(dir)
                .Where(f => extensions.Contains(Path.GetExtension(f)))
                .OrderBy(f => f)
                .ToList();
            
            _currentFileIndex = _folderImages.IndexOf(currentPath);
        } catch {
            _folderImages = new List<string> { currentPath };
            _currentFileIndex = 0;
        }
    }

    private void NextImage() {
        if (_folderImages.Count <= 1) return;
        _currentFileIndex = (_currentFileIndex + 1) % _folderImages.Count;
        LoadFile(_folderImages[_currentFileIndex]);
    }

    private void PreviousImage() {
        if (_folderImages.Count <= 1) return;
        _currentFileIndex = (_currentFileIndex - 1 + _folderImages.Count) % _folderImages.Count;
        LoadFile(_folderImages[_currentFileIndex]);
    }

    private void FitToWindow() {
        if (_imageControl.Image == null) return;
        float availableWidth = ClientSize.X;
        float availableHeight = ClientSize.Y - MenuBarHeight - StatusBarHeight;
        
        float zoomX = availableWidth / _imageControl.Image.Width;
        float zoomY = availableHeight / _imageControl.Image.Height;
        float zoom = Math.Min(zoomX, zoomY);
        if (zoom > 1f) zoom = 1f; // Don't upscale for "Fit"

        _imageControl.Zoom = zoom;
        // Center image in the viewport area (between menu and status bar)
        _imageControl.Offset = new Vector2(
            (availableWidth - _imageControl.Image.Width * zoom) / 2,
            MenuBarHeight + (availableHeight - _imageControl.Image.Height * zoom) / 2
        );
        _velocity = Vector2.Zero;
        UpdateStatusBar();
    }

    private void SetZoom(float zoom) {
        if (_imageControl.Image == null) return;
        float oldZoom = _imageControl.Zoom;
        _imageControl.Zoom = zoom;
        
        // Center zoom on current window center
        Vector2 center = new Vector2(ClientSize.X / 2, (ClientSize.Y - MenuBarHeight - StatusBarHeight) / 2 + MenuBarHeight);
        _imageControl.Offset = center - (center - _imageControl.Offset) * (_imageControl.Zoom / oldZoom);
        UpdateStatusBar();
    }

    private void UpdateStatusBar() {
        if (_imageControl.Image == null) {
            _infoLabel.Text = "No image loaded";
            return;
        }
        
        string sizeStr = "Unknown";
        try {
            var info = VirtualFileSystem.Instance.GetFileInfo(_currentFilePath);
            if (info != null) {
                long bytes = info.Size;
                if (bytes < 1024) sizeStr = $"{bytes} B";
                else if (bytes < 1024 * 1024) sizeStr = $"{bytes / 1024.0:F1} KB";
                else sizeStr = $"{bytes / (1024.0 * 1024.0):F1} MB";
            }
        } catch { }

        _infoLabel.Text = $"{_imageControl.Image.Width}x{_imageControl.Image.Height} | Zoom: {(int)(_imageControl.Zoom * 100)}% | {sizeStr} | {Path.GetFileName(_currentFilePath)}";
    }

    public override void Update(GameTime gameTime) {
        var mousePos = InputManager.MousePosition;
        if (_justBecameActive) {
            _lastMousePos = mousePos;
            _justBecameActive = false;
        }
        
        var mouseDelta = (mousePos - _lastMousePos).ToVector2();
        bool wasConsumed = InputManager.IsMouseConsumed;

        base.Update(gameTime);

        if (IsActive && _imageControl.Image != null) {
            // Check if mouse was consumed by a child (like MenuBar) during base.Update
            bool childConsumed = !wasConsumed && InputManager.IsMouseConsumed && UIManager.HoveredElement != this;
            bool canInteract = !wasConsumed && !childConsumed;
            
            HandleInput(gameTime, canInteract, mouseDelta);
            ApplyInertia(gameTime);
        } else {
            _justBecameActive = true;
        }

        _lastMousePos = mousePos;
    }

    private void HandleInput(GameTime gameTime, bool canInteract, Vector2 mouseDelta) {
        var mousePos = InputManager.MousePosition;
        
        // Zooming (Mouse Wheel)
        float scrollDelta = InputManager.ScrollDelta;
        if (scrollDelta != 0 && !InputManager.IsScrollConsumed && canInteract) {
            float oldZoom = _imageControl.Zoom;
            float zoomFactor = scrollDelta > 0 ? 1.15f : 0.85f;
            _imageControl.Zoom *= zoomFactor;
            _imageControl.Zoom = MathHelper.Clamp(_imageControl.Zoom, 0.01f, 50f);

            // Zoom to mouse - relative to image control absolute position
            Vector2 mPos = mousePos.ToVector2() - _imageControl.AbsolutePosition;
            _imageControl.Offset = mPos - (mPos - _imageControl.Offset) * (_imageControl.Zoom / oldZoom);
            _velocity = Vector2.Zero;
            UpdateStatusBar();
        }

        // Panning (Left Click)
        if (InputManager.IsMouseButtonDown(MouseButton.Left)) {
            if (!_isPanning && canInteract) {
                // Only start panning if clicking in the image area
                if (_imageControl.Bounds.Contains(mousePos)) {
                    _isPanning = true;
                }
            }
            
            if (_isPanning) {
                // Apply delta
                _imageControl.Offset += mouseDelta;
                
                float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (dt > 0) {
                    Vector2 currentVelocity = mouseDelta / dt;
                    
                    // If moving, interpolate towards current velocity. 
                    // If stopped, decay aggressively to avoid drift after a pause.
                    if (mouseDelta.LengthSquared() > 0) {
                        _velocity = Vector2.Lerp(_velocity, currentVelocity, 0.5f);
                    } else {
                        // Very fast decay when stationary (approx. 99% reduction in 0.2s)
                        _velocity *= (float)Math.Pow(0.01, dt * 5); 
                    }
                    
                    // Clamp velocity to avoid explosion
                    if (_velocity.Length() > 5000) _velocity = Vector2.Normalize(_velocity) * 5000;
                }
                
                CustomCursor.Instance.SetCursor(CursorType.Move);
                InputManager.IsMouseConsumed = true;
            }
        } else {
            _isPanning = false;
        }
    }

    private void ApplyInertia(GameTime gameTime) {
        if (_isPanning) return;

        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (_velocity.Length() > 0.5f) {
            _imageControl.Offset += _velocity * dt;
            _velocity *= (float)Math.Pow(0.9, dt * 60); // Friction (0.9 per 1/60s)
        } else {
            _velocity = Vector2.Zero;
        }
    }
}
