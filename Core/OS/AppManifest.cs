using System;
using System.Text.Json.Serialization;

namespace TheGame.Core.OS;

/// <summary>
/// Represents an app manifest file (manifest.json) containing metadata and configuration.
/// </summary>
public class AppManifest {
    [JsonPropertyName("appId")]
    public string AppId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("extensionType")]
    public string ExtensionType { get; set; } = "application";

    [JsonPropertyName("icon")]
    public string Icon { get; set; } = "icon.png";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    [JsonPropertyName("minOSVersion")]
    public string MinOSVersion { get; set; } = "1.0.0";

    [JsonPropertyName("author")]
    public string Author { get; set; } = "Unknown";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("entryPoint")]
    public string EntryPoint { get; set; } = "Program.cs";

    [JsonPropertyName("entryClass")]
    public string EntryClass { get; set; }

    [JsonPropertyName("entryMethod")]
    public string EntryMethod { get; set; } = "Main";

    [JsonPropertyName("permissions")]
    public string[] Permissions { get; set; } = Array.Empty<string>();

    [JsonPropertyName("dependencies")]
    public string[] Dependencies { get; set; } = Array.Empty<string>();

    [JsonPropertyName("singleInstance")]
    public bool SingleInstance { get; set; } = false;

    [JsonPropertyName("terminalOnly")]
    public bool TerminalOnly { get; set; } = false;

    [JsonPropertyName("references")]
    public string[] References { get; set; } = Array.Empty<string>();

    public static AppManifest FromJson(string json) {
        return System.Text.Json.JsonSerializer.Deserialize<AppManifest>(json);
    }

    public string ToJson() {
        return System.Text.Json.JsonSerializer.Serialize(this, new System.Text.Json.JsonSerializerOptions { 
            WriteIndented = true 
        });
    }
}
