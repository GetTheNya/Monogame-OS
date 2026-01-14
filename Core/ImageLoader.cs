using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework.Graphics;

namespace TheGame.Core;

/// <summary>
/// Helper class for loading images from files without using MonoGame Content Pipeline.
/// Supports PNG, JPG, and GIF formats.
/// </summary>
public static class ImageLoader {
    // Optional cache to avoid reloading the same image
    private static Dictionary<string, Texture2D> _cache = new();
    
    /// <summary>
    /// Load a texture from a file path.
    /// </summary>
    /// <param name="graphicsDevice">The GraphicsDevice to create the texture on.</param>
    /// <param name="filePath">Path to the image file (PNG, JPG, or GIF).</param>
    /// <param name="useCache">If true, caches the texture for future loads of the same path.</param>
    /// <returns>The loaded Texture2D.</returns>
    public static Texture2D Load(GraphicsDevice graphicsDevice, string filePath, bool useCache = true) {
        if (useCache && _cache.TryGetValue(filePath, out var cached)) {
            return cached;
        }
        
        if (!File.Exists(filePath)) {
            throw new FileNotFoundException($"Image file not found: {filePath}");
        }
        
        using (FileStream stream = File.OpenRead(filePath)) {
            var texture = Texture2D.FromStream(graphicsDevice, stream);
            
            if (useCache) {
                _cache[filePath] = texture;
            }
            
            return texture;
        }
    }
    
    /// <summary>
    /// Load a texture from a stream (useful for embedded resources or network streams).
    /// </summary>
    public static Texture2D LoadFromStream(GraphicsDevice graphicsDevice, Stream stream) {
        return Texture2D.FromStream(graphicsDevice, stream);
    }
    
    /// <summary>
    /// Clear the texture cache. Call this when you want to free memory.
    /// Note: This does NOT dispose the textures, only removes references.
    /// </summary>
    public static void ClearCache() {
        _cache.Clear();
    }
    
    /// <summary>
    /// Dispose and remove a specific texture from cache.
    /// </summary>
    public static void Unload(string filePath) {
        if (_cache.TryGetValue(filePath, out var texture)) {
            texture.Dispose();
            _cache.Remove(filePath);
        }
    }
    
    /// <summary>
    /// Dispose all cached textures and clear the cache.
    /// </summary>
    public static void UnloadAll() {
        foreach (var texture in _cache.Values) {
            texture.Dispose();
        }
        _cache.Clear();
    }
}
