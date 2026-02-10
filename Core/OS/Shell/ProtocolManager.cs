using System;
using System.Collections.Generic;
using System.Linq;
using TheGame.Core.OS;
using TheGame.Core.UI;

namespace TheGame.Core.OS;

/// <summary>
/// Manages custom URI schemes (protocols) and their associated applications.
/// </summary>
public static class ProtocolManager {
    /// <summary>
    /// Registers a custom URI scheme in the system registry.
    /// </summary>
    public static void RegisterProtocol(string scheme, string appId, string description, string icon) {
        string path = $"{Shell.Registry.Protocols}\\{scheme}";
        Registry.Instance.SetValue($"{path}\\(Default)", description);
        Registry.Instance.SetValue($"{path}\\AppID", appId);
        Registry.Instance.SetValue($"{path}\\Icon", icon);
        Registry.Instance.FlushToDisk();
    }

    /// <summary>
    /// Checks if a URI scheme is registered and returns the associated AppID.
    /// </summary>
    public static string GetAppForProtocol(string scheme) {
        if (string.IsNullOrEmpty(scheme)) return null;
        string path = $"{Shell.Registry.Protocols}\\{scheme}";
        return Registry.Instance.GetValue<string>($"{path}\\AppID", null);
    }

    /// <summary>
    /// Launches the application associated with a URI scheme after user confirmation.
    /// </summary>
    public static void LaunchProtocol(string uri, string sourceAppId) {
        if (string.IsNullOrEmpty(uri)) return;

        int colonIndex = uri.IndexOf(':');
        if (colonIndex <= 0) return;

        string scheme = uri.Substring(0, colonIndex).ToLower();
        string path = $"{Shell.Registry.Protocols}\\{scheme}";
        
        string targetAppId = Registry.Instance.GetValue<string>($"{path}\\AppID", null);
        if (string.IsNullOrEmpty(targetAppId)) return;

        string description = Registry.Instance.GetValue<string>($"{path}\\(Default)", "Unknown Application");
        // Remove "URL:" prefix if present in description
        if (description.StartsWith("URL:", StringComparison.OrdinalIgnoreCase)) {
            description = description.Substring(4);
        }

        string sourceName = sourceAppId;
        // In a more advanced system, we'd lookup the pretty name of the source app from registry
        // For now, AppId should suffice or we can try to find a running process
        
        string message = $"\"{sourceName}\" wants to open \"{description}\".\n\nURI: {uri}";
        
        Shell.UI.OpenWindow(new MessageBox("Open Application?", message, MessageBoxButtons.YesNo, (confirmed) => {
            if (confirmed) {
                // URL Decoding ensures spaces (%20) etc. are handled before passing to the app
                string cleanUri = Uri.UnescapeDataString(uri);
                ProcessManager.Instance.StartProcess(targetAppId, new[] { cleanUri });
            }
        }));
    }
}
