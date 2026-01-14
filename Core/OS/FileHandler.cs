using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TheGame.Core.OS;

public abstract class FileHandler {
    public abstract string Extension { get; }
    public abstract void Execute(string virtualPath, Rectangle? startBounds = null);
    public virtual Texture2D GetIcon(string virtualPath) => GameContent.FileIcon;
}

public class Shortcut {
    [System.Text.Json.Serialization.JsonPropertyName("targetPath")]
    public string TargetPath { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("arguments")]
    public string Arguments { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("iconPath")]
    public string CustomIconPath { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("label")]
    public string Label { get; set; }

    public static Shortcut FromJson(string json) {
        try {
            return System.Text.Json.JsonSerializer.Deserialize<Shortcut>(json, new System.Text.Json.JsonSerializerOptions {
                PropertyNameCaseInsensitive = true
            });
        } catch {
            return null;
        }
    }

    public string ToJson() {
        return System.Text.Json.JsonSerializer.Serialize(this);
    }
}
