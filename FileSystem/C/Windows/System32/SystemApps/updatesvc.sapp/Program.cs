using System;
using System.Threading.Tasks;
using TheGame.Core;
using TheGame.Core.OS;
using TheGame.Core.UI;

namespace UpdateService;

public class App : Application {
    public static Application Main(string[] args) => new App();

    public override bool IsAsync => true;

    protected override async Task OnLoadAsync(string[] args) {
        DebugLogger.Log("[UpdateService] Service started.");
        
        // Wait a bit to not slow down the boot/login process too much
        await Task.Delay(5000);
        
        await CheckForUpdates();
    }

    private async Task CheckForUpdates() {
        try {
            DebugLogger.Log("[UpdateService] Checking for updates...");
            var result = await UpdateManager.Instance.CheckForUpdatesAsync(Process);
            
            if (result.Success && result.IsUpdateAvailable) {
                DebugLogger.Log($"[UpdateService] Update available: {result.LatestVersion}");
                
                SystemAPI.NotificationsAPI.Show(
                    "System Update Available",
                    $"A new version ({result.LatestVersion}) is available. Click to open Settings.",
                    onClick: () => {
                        // Launch Settings app with --updates flag
                        ProcessManager.Instance.StartProcess("SETTINGS", new[] { "--updates" });
                    }
                );
            } else if (!result.Success) {
                DebugLogger.Log($"[UpdateService] Check failed: {result.ErrorMessage}");
            } else {
                DebugLogger.Log("[UpdateService] System is up to date.");
            }
        } catch (Exception ex) {
            DebugLogger.Log($"[UpdateService] Error: {ex.Message}");
        }
        
        // Since this is a "run once at startup" service in this implementation, 
        // we could terminate here, or stay alive and check every 24h.
        // For now, let's keep it simple and terminate after check.
        Exit();
    }
}

