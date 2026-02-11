using System;
using TheGame.Core.OS;

namespace TheGame.Core.OS;

public static class SystemConfig {
    public static string Username { get; private set; } = "Admin";
    public static string DisplayName { get; private set; } = "Administrator";
    public static string AccentColor { get; private set; } = "Blue";

    public static void Initialize() {
        // Load from registry if available
        Username = Registry.Instance.GetValue("HKCU\\Software\\HentOS\\Config", "Username", "Admin");
        DisplayName = Registry.Instance.GetValue("HKCU\\Software\\HentOS\\Config", "DisplayName", "Administrator");
        AccentColor = Registry.Instance.GetValue("HKCU\\Software\\HentOS\\Config", "AccentColor", "Blue");
    }

    public static void Save() {
        Registry.Instance.SetValue("HKCU\\Software\\HentOS\\Config", "Username", Username);
        Registry.Instance.SetValue("HKCU\\Software\\HentOS\\Config", "DisplayName", DisplayName);
        Registry.Instance.SetValue("HKCU\\Software\\HentOS\\Config", "AccentColor", AccentColor);
        Registry.Instance.FlushToDisk();
    }

    public static void UpdateUser(string username, string displayName, string accentColor) {
        Username = username;
        DisplayName = displayName;
        AccentColor = accentColor;
        Save();
    }
}
