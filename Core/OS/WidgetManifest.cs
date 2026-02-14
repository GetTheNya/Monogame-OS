using System;
using System.Text.Json.Serialization;

namespace TheGame.Core.OS;

public class WidgetManifest {
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("extensionType")]
    public string ExtensionType { get; set; } = "widget";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("author")]
    public string Author { get; set; } = "Unknown";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    [JsonPropertyName("widgetClass")]
    public string WidgetClass { get; set; }

    [JsonPropertyName("defaultSize")]
    public WidgetSize DefaultSize { get; set; } = new WidgetSize { Width = 200, Height = 200 };

    [JsonPropertyName("isResizable")]
    public bool IsResizable { get; set; } = false;

    [JsonPropertyName("refreshPolicy")]
    public string RefreshPolicy { get; set; } = "Interval"; // "Interval" or "OnEvent"

    [JsonPropertyName("intervalMs")]
    public int IntervalMs { get; set; } = 0;

    [JsonPropertyName("subscriptions")]
    public string[] Subscriptions { get; set; } = Array.Empty<string>();

    [JsonPropertyName("permissions")]
    public string[] Permissions { get; set; } = Array.Empty<string>();

    [JsonPropertyName("references")]
    public string[] References { get; set; } = Array.Empty<string>();

    public static WidgetManifest FromJson(string json) {
        return System.Text.Json.JsonSerializer.Deserialize<WidgetManifest>(json);
    }

    public string ToJson() {
        return System.Text.Json.JsonSerializer.Serialize(this, new System.Text.Json.JsonSerializerOptions { 
            WriteIndented = true 
        });
    }

    public class WidgetSize {
        [JsonPropertyName("width")]
        public float Width { get; set; }
        [JsonPropertyName("height")]
        public float Height { get; set; }
    }
}
