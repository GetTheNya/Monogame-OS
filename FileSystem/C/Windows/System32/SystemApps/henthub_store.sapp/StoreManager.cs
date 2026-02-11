using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
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
            var headers = new Dictionary<string, string> {
                { "Accept", "application/json" }
            };
            var response = await Shell.Network.SendRequestAsync(process, "https://getthenya.github.io/HentHub-Store/manifests/store-manifest.json", HttpMethod.Get, null, headers);

            if (response.IsSuccessStatusCode) {
                string json = response.BodyText;
                Manifest = await Task.Run(() => JsonSerializer.Deserialize<StoreManifest>(json));
                if (Manifest == null) DebugLogger.Log("[StoreManager] Manifest deserialization returned null.");
                return Manifest != null;
            } else {
                DebugLogger.Log($"[StoreManager] Manifest fetch failed: {response.StatusCode} - {response.ErrorMessage} - URL: https://getthenya.github.io/HentHub-Store/manifests/store-manifest.json");
            }
        } catch (Exception ex) {
            DebugLogger.Log($"[StoreManager] Exception loading manifest: {ex.Message}");
        }
        return false;
    }

    public StoreApp GetApp(string appId) {
        if (Manifest == null) return null;
        return Manifest.Apps.Find(a => a.AppId.Equals(appId, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Recursively resolves all missing dependencies for a given app.
    /// </summary>
    public DependencyNode ResolveDependencyTree(StoreApp app) {
        var root = new DependencyNode {
            AppId = app.AppId,
            Name = app.Name,
            IsInstalled = AppInstaller.Instance.IsAppInstalled(app.AppId)
        };

        if (app.Dependencies != null) {
            foreach (var depId in app.Dependencies) {
                var depApp = GetApp(depId);
                if (depApp != null) {
                    var childNode = ResolveDependencyTree(depApp);
                    // Only add if it's missing or if any of its dependencies are missing
                    if (!childNode.IsInstalled || childNode.HasMissingDependencies) {
                        root.Dependencies.Add(childNode);
                    }
                } else {
                    // Dependency not found in manifest
                    root.Dependencies.Add(new DependencyNode {
                        AppId = depId,
                        Name = $"{depId} (Not Found)",
                        IsInstalled = false
                    });
                }
            }
        }

        return root;
    }
}

public class DependencyNode {
    public string AppId { get; set; }
    public string Name { get; set; }
    public bool IsInstalled { get; set; }
    public List<DependencyNode> Dependencies { get; set; } = new();

    public bool HasMissingDependencies => Dependencies.Any(d => !d.IsInstalled || d.HasMissingDependencies);

    /// <summary>
    /// Returns a flat list of all unique missing AppIDs in the tree, in installation order (bottom-up).
    /// </summary>
    public List<string> GetFlatMissingList() {
        var list = new List<string>();
        CollectMissingRecursive(this, list);
        // Remove the root app itself from the missing list if it's the one being installed
        // (The caller usually handles the root app separately or as the last step)
        return list.Distinct().ToList();
    }

    private void CollectMissingRecursive(DependencyNode node, List<string> list) {
        foreach (var dep in node.Dependencies) {
            CollectMissingRecursive(dep, list);
        }
        if (!node.IsInstalled) {
            list.Add(node.AppId);
        }
    }
}
