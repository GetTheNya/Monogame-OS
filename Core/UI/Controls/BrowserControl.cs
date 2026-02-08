using System;
using System.ComponentModel;
using CefSharp;
using CefSharp.OffScreen;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Core.Input;
using TheGame.Graphics;

namespace TheGame.Core.UI.Controls;

/// <summary>
/// A UI control that embeds a real Chromium browser using CefSharp offscreen rendering.
/// </summary>
public class BrowserControl : UIElement {
    private ChromiumWebBrowser _browser;
    private Texture2D _browserTexture;
    private byte[] _pixelBuffer;
    private bool _isDirty;
    private readonly object _textureLock = new();
    
    public string Url { get; set; } = "about:blank";
    public Color BackgroundColor { get; set; } = new Color(30, 30, 30);
    
    [Obsolete("For Designer/Serialization use only")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public BrowserControl() : this(Vector2.Zero, new Vector2(800, 600)) { }
    
    public BrowserControl(Vector2 position, Vector2 size) : base(position, size) {
        ConsumesInput = true;
    }
    
    /// <summary>
    /// Call this after the GraphicsDevice is available to create the browser instance.
    /// </summary>
    public void InitializeBrowser() {
        if (_browser != null) return;
        
        int width = Math.Max(1, (int)Size.X);
        int height = Math.Max(1, (int)Size.Y);
        
        _browserTexture = new Texture2D(G.GraphicsDevice, width, height, false, SurfaceFormat.Color);
        _pixelBuffer = new byte[width * height * 4];
        
        var browserSettings = new BrowserSettings {
            WindowlessFrameRate = 60 // Match game framerate
        };
        
        _browser = new ChromiumWebBrowser(Url, browserSettings);
        _browser.Size = new System.Drawing.Size(width, height);
        
        // Handle paint events
        _browser.Paint += OnBrowserPaint;
        
        // Handle loading events
        _browser.LoadingStateChanged += (sender, args) => {
            if (!args.IsLoading) {
                // Page finished loading
            }
        };
        
        OnResize += HandleResize;
    }
    
    private void HandleResize() {
        if (_browser == null) return;
        
        int width = Math.Max(1, (int)Size.X);
        int height = Math.Max(1, (int)Size.Y);
        
        _browser.Size = new System.Drawing.Size(width, height);
        
        lock (_textureLock) {
            // Recreate texture with new size
            var gd = _browserTexture?.GraphicsDevice;
            if (gd != null) {
                _browserTexture?.Dispose();
                _browserTexture = new Texture2D(gd, width, height, false, SurfaceFormat.Color);
                _pixelBuffer = new byte[width * height * 4];
                _isDirty = false; // Reset dirty flag as we have a new texture
            }
        }
    }
    
    private void OnBrowserPaint(object sender, OnPaintEventArgs e) {
        lock (_textureLock) {
            var info = e.BufferHandle;
            var width = e.Width;
            var height = e.Height;
            
            if (_pixelBuffer.Length != width * height * 4) {
                _pixelBuffer = new byte[width * height * 4];
            }
            
            // Copy from CefSharp buffer to our pixel buffer
            // CefSharp uses BGRA format, we need to convert to RGBA
            System.Runtime.InteropServices.Marshal.Copy(info, _pixelBuffer, 0, _pixelBuffer.Length);
            
            // Convert BGRA to RGBA
            for (int i = 0; i < _pixelBuffer.Length; i += 4) {
                byte b = _pixelBuffer[i];
                byte r = _pixelBuffer[i + 2];
                _pixelBuffer[i] = r;
                _pixelBuffer[i + 2] = b;
            }
            
            _isDirty = true;
        }
    }
    
    public override void Update(GameTime gameTime) {
        base.Update(gameTime);
        
        // Update texture on main thread
        if (_isDirty && _browserTexture != null && _pixelBuffer != null) {
            lock (_textureLock) {
                // Safety check: Ensure buffer size matches texture exactly to avoid ArgumentException
                if (_pixelBuffer.Length == _browserTexture.Width * _browserTexture.Height * 4) {
                    _browserTexture.SetData(_pixelBuffer);
                }
                _isDirty = false;
            }
        }
    }
    
    protected override void UpdateInput() {
        base.UpdateInput();
        
        if (_browser == null || !IsVisible) return;
        
        var host = _browser.GetBrowserHost();
        if (host == null) return;
        
        // Forward mouse events
        if (IsMouseOver) {
            var localPos = InputManager.MousePosition.ToVector2() - AbsolutePosition;
            var mouseEvent = new MouseEvent((int)localPos.X, (int)localPos.Y, CefEventFlags.None);
            
            // Mouse move
            host.SendMouseMoveEvent(mouseEvent, false);
            
            // Mouse clicks
            if (InputManager.IsMouseButtonJustPressed(MouseButton.Left)) {
                host.SendMouseClickEvent(mouseEvent, CefSharp.MouseButtonType.Left, false, 1);
            }
            if (InputManager.IsMouseButtonJustReleased(MouseButton.Left)) {
                host.SendMouseClickEvent(mouseEvent, CefSharp.MouseButtonType.Left, true, 1);
            }
            
            // Mouse wheel
            var scroll = InputManager.ScrollDelta;
            if (scroll != 0) {
                host.SendMouseWheelEvent(mouseEvent, 0, scroll * 10);
            }
        }
    }
    
    protected override void DrawSelf(SpriteBatch spriteBatch, ShapeBatch shapeBatch) {
        if (_browserTexture != null) {
            spriteBatch.Draw(_browserTexture, AbsolutePosition, Color.White * AbsoluteOpacity);
        } else {
            // Draw placeholder if browser not initialized
            shapeBatch.FillRectangle(AbsolutePosition, Size, BackgroundColor * AbsoluteOpacity);
        }
    }
    
    /// <summary>
    /// Navigate to a URL
    /// </summary>
    public void Navigate(string url) {
        Url = url;
        _browser?.Load(url);
    }
    
    /// <summary>
    /// Reload the current page
    /// </summary>
    public void Reload() {
        _browser?.Reload();
    }
    
    /// <summary>
    /// Go back in history
    /// </summary>
    public void GoBack() {
        if (_browser?.CanGoBack == true) {
            _browser.Back();
        }
    }
    
    /// <summary>
    /// Go forward in history
    /// </summary>
    public void GoForward() {
        if (_browser?.CanGoForward == true) {
            _browser.Forward();
        }
    }
    
    /// <summary>
    /// Execute JavaScript in the browser
    /// </summary>
    public void ExecuteJavaScript(string script) {
        _browser?.ExecuteScriptAsync(script);
    }
    
    /// <summary>
    /// Dispose of browser resources
    /// </summary>
    public void Dispose() {
        _browser?.Dispose();
        _browserTexture?.Dispose();
    }
}
