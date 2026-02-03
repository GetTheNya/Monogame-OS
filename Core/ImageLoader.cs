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
    private static readonly object _cacheLock = new();

    private struct CachedTexture {
        public Texture2D Texture;
        public int RefCount;
    }

    private static Dictionary<string, CachedTexture> _cache = new();
    
    /// <summary>
    /// Load a texture from a file path.
    /// </summary>
    /// <param name="graphicsDevice">The GraphicsDevice to create the texture on.</param>
    /// <param name="filePath">Path to the image file (PNG, JPG, or GIF).</param>
    /// <param name="useCache">If true, caches the texture for future loads of the same path.</param>
    /// <returns>The loaded Texture2D.</returns>
    public static Texture2D Load(GraphicsDevice graphicsDevice, string filePath, bool useCache = true) {
        if (useCache) {
            lock (_cacheLock) {
                if (_cache.TryGetValue(filePath, out var cached)) {
                    cached.RefCount++;
                    _cache[filePath] = cached;
                    return cached.Texture;
                }
            }
        }
        
        if (!File.Exists(filePath)) {
            throw new FileNotFoundException($"Image file not found: {filePath}");
        }
        
        using (FileStream stream = File.OpenRead(filePath)) {
            var texture = Texture2D.FromStream(graphicsDevice, stream);
            
            if (useCache) {
                lock (_cacheLock) {
                    // Double-check after IO
                    if (_cache.TryGetValue(filePath, out var existing)) {
                        existing.RefCount++;
                        _cache[filePath] = existing;
                        texture.Dispose();
                        return existing.Texture;
                    }
                    _cache[filePath] = new CachedTexture { Texture = texture, RefCount = 1 };
                }
            }
            
            return texture;
        }
    }

    /// <summary>
    /// Asynchronously load a texture from a file path.
    /// </summary>
    public static async System.Threading.Tasks.Task<Texture2D> LoadAsync(GraphicsDevice graphicsDevice, string filePath, bool useCache = true) {
        if (useCache) {
            lock (_cacheLock) {
                if (_cache.TryGetValue(filePath, out var cached)) {
                    cached.RefCount++;
                    _cache[filePath] = cached;
                    return cached.Texture;
                }
            }
        }
        
        if (!File.Exists(filePath)) {
            throw new FileNotFoundException($"Image file not found: {filePath}");
        }
        
        // Decoding on background thread
        var texture = await System.Threading.Tasks.Task.Run(() => {
            using (FileStream stream = File.OpenRead(filePath)) {
                return Texture2D.FromStream(graphicsDevice, stream);
            }
        });
            
        if (useCache) {
            lock (_cacheLock) {
                if (_cache.TryGetValue(filePath, out var existing)) {
                    existing.RefCount++;
                    _cache[filePath] = existing;
                    texture.Dispose();
                    return existing.Texture;
                }
                _cache[filePath] = new CachedTexture { Texture = texture, RefCount = 1 };
            }
        }
        
        return texture;
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
        lock (_cacheLock) {
            _cache.Clear();
        }
    }
    
    /// <summary>
    /// Decrement reference count and dispose if it reaches zero.
    /// </summary>
    public static void Unload(string filePath) {
        lock (_cacheLock) {
            if (_cache.TryGetValue(filePath, out var cached)) {
                cached.RefCount--;
                if (cached.RefCount <= 0) {
                    cached.Texture.Dispose();
                    _cache.Remove(filePath);
                } else {
                    _cache[filePath] = cached;
                }
            }
        }
    }
    
    /// <summary>
    /// Dispose all cached textures and clear the cache.
    /// </summary>
    public static void UnloadAll() {
        lock (_cacheLock) {
            foreach (var cached in _cache.Values) {
                cached.Texture.Dispose();
            }
            _cache.Clear();
        }
    }
}
