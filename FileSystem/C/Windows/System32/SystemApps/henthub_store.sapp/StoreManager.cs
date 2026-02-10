using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using TheGame.Core;
using TheGame.Core.OS;

namespace HentHub;

public class StoreManager {
    private static StoreManager _instance;
    public static StoreManager Instance => _instance ??= new StoreManager();

    public StoreManifest Manifest { get; private set; }
    public bool IsLoaded => Manifest != null;

    private StoreManager() { }

    public async Task<bool> LoadManifestAsync(Process process, bool forceRefresh = false) {
        if (!forceRefresh && Manifest != null) return true;

        try {
            // Try 127.0.0.1 as it's often more reliable than 'localhost' in simulator environments
            var response = await Shell.Network.GetAsync(process, "http://127.0.0.1:3000/manifests/store-manifest.json");

            if (response.IsSuccessStatusCode) {
                string json = response.BodyText;
                Manifest = await Task.Run(() => JsonSerializer.Deserialize<StoreManifest>(json));
                return Manifest != null;
            }
        } catch (Exception ex) {
            DebugLogger.Log($"[StoreManager] Failed to load manifest: {ex.Message}");
        }
        return false;
    }

    public StoreApp GetApp(string appId) {
        if (Manifest == null) return null;
        return Manifest.Apps.Find(a => a.AppId.Equals(appId, StringComparison.OrdinalIgnoreCase));
    }
}
