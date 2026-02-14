using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace HentHub;

public class StoreManifest {
    [JsonPropertyName("apps")]
    public List<StoreApp> Apps { get; set; } = new();

    [JsonPropertyName("lastUpdated")]
    public DateTime LastUpdated { get; set; }
}

public class StoreApp {
    [JsonPropertyName("appId")]
    public string AppId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";
    
    [JsonPropertyName("extensionType")]
    public string ExtensionType { get; set; } = "application";

    [JsonPropertyName("minOSVersion")]
    public string MinOSVersion { get; set; } = "1.0.0";

    [JsonPropertyName("author")]
    public string Author { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; }

    [JsonPropertyName("entryPoint")]
    public string EntryPoint { get; set; }

    [JsonPropertyName("entryClass")]
    public string EntryClass { get; set; }

    [JsonPropertyName("entryMethod")]
    public string EntryMethod { get; set; }

    [JsonPropertyName("terminalOnly")]
    public bool TerminalOnly { get; set; }

    [JsonPropertyName("singleInstance")]
    public bool SingleInstance { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("icon")]
    public string Icon { get; set; }

    [JsonPropertyName("screenshotCount")]
    public int ScreenshotCount { get; set; }

    [JsonPropertyName("widgetClass")]
    public string WidgetClass { get; set; }

    [JsonPropertyName("defaultSize")]
    public WidgetSize DefaultSize { get; set; }

    [JsonPropertyName("isResizable")]
    public bool IsResizable { get; set; }

    [JsonPropertyName("refreshPolicy")]
    public string RefreshPolicy { get; set; }

    [JsonPropertyName("intervalMs")]
    public int IntervalMs { get; set; }

    [JsonPropertyName("dependencies")]
    public List<string> Dependencies { get; set; } = new();

    [JsonPropertyName("permissions")]
    public List<string> Permissions { get; set; } = new();

    [JsonPropertyName("subscriptions")]
    public List<string> Subscriptions { get; set; } = new();

    [JsonPropertyName("references")]
    public List<string> References { get; set; } = new();

    [JsonPropertyName("downloadUrl")]
    public string DownloadUrl { get; set; }

    [JsonPropertyName("iconUrl")]
    public string IconUrl { get; set; }

    [JsonPropertyName("publishedDate")]
    public DateTime PublishedDate { get; set; }
}

public class WidgetSize {
    [JsonPropertyName("width")]
    public float Width { get; set; }
    [JsonPropertyName("height")]
    public float Height { get; set; }
}
