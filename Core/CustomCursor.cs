using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Core.Input;

namespace TheGame.Core;

/// <summary>
/// Custom cursor types available in the application.
/// </summary>
public enum CursorType {
    Pointer,     // Default arrow
    Beam,        // Text input
    Link,        // Clickable/hand
    Move,        // Move/drag
    Horizontal,  // Horizontal resize
    Vertical,    // Vertical resize
    DiagonalNE,  // NE-SW resize (dgn1)
    DiagonalNW,  // NW-SE resize (dgn2)
    Unavailable  // Disabled/not allowed
}

/// <summary>
/// Custom cursor manager that renders a sprite cursor instead of Windows cursor.
/// Draws on top of everything and changes based on context.
/// </summary>
public class CustomCursor {
    private static CustomCursor _instance;
    public static CustomCursor Instance => _instance ??= new CustomCursor();
    
    private Dictionary<CursorType, Texture2D> _cursors = new();
    private Dictionary<CursorType, Vector2> _hotspots = new();
    
    private CursorType _currentType = CursorType.Pointer;
    private CursorType _nextFrameType = CursorType.Pointer;
    private bool _isInitialized = false;
    private GraphicsDevice _graphicsDevice;
    private SpriteBatch _spriteBatch;
    
    public bool IsVisible { get; set; } = true;
    public float Scale { get; set; } = 1f;
    
    private CustomCursor() { }
    
    /// <summary>
    /// Initialize the cursor system. Call once during game initialization.
    /// </summary>
    public void Initialize(GraphicsDevice graphicsDevice) {
        _graphicsDevice = graphicsDevice;
        _spriteBatch = new SpriteBatch(graphicsDevice);
        
        // Load cursor textures from FileSystem
        // For now lets just hardcode path and hotspot
        LoadCursor(CursorType.Beam, "C:\\Windows\\SystemResources\\Cursors\\beam.png", new Vector2(15, 15));
        LoadCursor(CursorType.DiagonalNW, "C:\\Windows\\SystemResources\\Cursors\\dgn1.png", new Vector2(12, 11));
        LoadCursor(CursorType.DiagonalNE, "C:\\Windows\\SystemResources\\Cursors\\dgn2.png", new Vector2(12, 11));
        LoadCursor(CursorType.Horizontal, "C:\\Windows\\SystemResources\\Cursors\\horz.png", new Vector2(12, 11));
        LoadCursor(CursorType.Link, "C:\\Windows\\SystemResources\\Cursors\\link.png", new Vector2(9, 3));
        LoadCursor(CursorType.Move, "C:\\Windows\\SystemResources\\Cursors\\move.png", new Vector2(12, 11));
        LoadCursor(CursorType.Pointer, "C:\\Windows\\SystemResources\\Cursors\\pointer.png", new Vector2(0, 0));
        LoadCursor(CursorType.Unavailable, "C:\\Windows\\SystemResources\\Cursors\\unavailable.png", new Vector2(0, 0));
        LoadCursor(CursorType.Vertical, "C:\\Windows\\SystemResources\\Cursors\\vert.png", new Vector2(12, 11));

        // Hide Windows cursor
        Game1.Instance.IsMouseVisible = false;
        
        _isInitialized = true;
    }
    
    private void LoadCursor(CursorType type, string virtualPath, Vector2 hotspot) {
        try {
            string hostPath = OS.VirtualFileSystem.Instance.ToHostPath(virtualPath);
            var texture = ImageLoader.Load(_graphicsDevice, hostPath, useCache: true);
            _cursors[type] = texture;
            _hotspots[type] = hotspot;
        } catch (Exception ex) {
            Console.WriteLine($"Failed to load cursor {type}: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Set the cursor type for this frame. Call this during Update based on hover state.
    /// The cursor resets to Pointer each frame, so set it every frame you need a different cursor.
    /// </summary>
    public void SetCursor(CursorType type) {
        _nextFrameType = type;
    }
    
    /// <summary>
    /// Call at the START of each Update to reset cursor to default.
    /// UI elements will then set their preferred cursor during their Update.
    /// </summary>
    public void BeginFrame() {
        _currentType = _nextFrameType;
        _nextFrameType = CursorType.Pointer; // Reset for next frame
    }
    
    /// <summary>
    /// Draw the cursor. Call this LAST in your Draw method to ensure it's on top.
    /// </summary>
    public void Draw() {
        if (!_isInitialized || !IsVisible) return;
        
        if (!_cursors.TryGetValue(_currentType, out var texture)) {
            // Fallback to pointer
            if (!_cursors.TryGetValue(CursorType.Pointer, out texture)) return;
        }
        
        var hotspot = _hotspots.GetValueOrDefault(_currentType, Vector2.Zero);
        var mousePos = InputManager.MousePosition.ToVector2();
        var drawPos = mousePos - (hotspot * Scale);
        
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        _spriteBatch.Draw(texture, drawPos, null, Color.White, 0f, Vector2.Zero, Scale, SpriteEffects.None, 0f);
        _spriteBatch.End();
    }
    
    /// <summary>
    /// Get the current cursor type.
    /// </summary>
    public CursorType CurrentType => _currentType;
}
