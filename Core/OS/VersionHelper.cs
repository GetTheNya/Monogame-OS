using System;

namespace TheGame.Core.OS;

public static class VersionHelper {
    public static bool IsCompatible(string required) {
        string min = string.IsNullOrEmpty(required) ? "1.0.0" : required;
        return IsNewer(min, SystemVersion.Current) || Normalize(min) == Normalize(SystemVersion.Current);
    }

    public static bool IsNewer(string current, string remote) {
        if (string.IsNullOrEmpty(remote)) return false;
        if (string.IsNullOrEmpty(current) || Normalize(current) == "0.0.0") return true;

        try {
            var v1 = new Version(Normalize(current));
            var v2 = new Version(Normalize(remote));
            return v2 > v1;
        } catch {
            return Normalize(current) != Normalize(remote);
        }
    }

    private static string Normalize(string version) {
        if (string.IsNullOrEmpty(version)) return "0.0.0";
        
        // Remove 'v' prefix
        if (version.StartsWith("v", StringComparison.OrdinalIgnoreCase)) {
            version = version.Substring(1);
        }

        // Remove suffixes like '-local'
        int dashIndex = version.IndexOf('-');
        if (dashIndex > 0) {
            version = version.Substring(0, dashIndex);
        }

        // Ensure it's a valid version string (x.y.z)
        // System.Version expects at least major.minor
        string[] parts = version.Split('.');
        if (parts.Length == 1) return $"{version}.0.0";
        if (parts.Length == 2) return $"{version}.0";

        return version;
    }
}
