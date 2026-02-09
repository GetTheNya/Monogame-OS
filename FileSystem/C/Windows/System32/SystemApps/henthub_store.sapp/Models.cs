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
    public string Version { get; set; }

    [JsonPropertyName("author")]
    public string Author { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; }

    [JsonPropertyName("terminalOnly")]
    public bool TerminalOnly { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("icon")]
    public string Icon { get; set; }

    [JsonPropertyName("screenshotCount")]
    public int ScreenshotCount { get; set; }

    [JsonPropertyName("dependencies")]
    public List<string> Dependencies { get; set; } = new();

    [JsonPropertyName("references")]
    public List<string> References { get; set; } = new();

    [JsonPropertyName("downloadUrl")]
    public string DownloadUrl { get; set; }

    [JsonPropertyName("iconUrl")]
    public string IconUrl { get; set; }

    [JsonPropertyName("publishedDate")]
    public DateTime PublishedDate { get; set; }
}
