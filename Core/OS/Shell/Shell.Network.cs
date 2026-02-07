using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace TheGame.Core.OS;

public static partial class Shell {
    /// <summary>
    /// Advanced Network API for apps to communicate with external servers.
    /// </summary>
    public static class Network {
        /// <summary> Registers the calling process for network access. </summary>
        public static void RegisterForNetwork(OS.Process process) {
            NetworkManager.Instance.RegisterProcess(process);
        }

        /// <summary> Unregisters the calling process from network access. </summary>
        public static void UnregisterFromNetwork(OS.Process process) {
            NetworkManager.Instance.UnregisterProcess(process);
        }

        /// <summary> Makes a full-featured HTTP request. </summary>
        public static Task<NetworkResponse> SendRequestAsync(OS.Process process, string url, HttpMethod method, byte[] body = null, Dictionary<string, string> headers = null, CancellationToken cancellationToken = default) {
            return NetworkManager.Instance.SendRequestAsync(process, url, method, body, headers, cancellationToken);
        }

        /// <summary> HTTP GET shorthand. </summary>
        public static Task<NetworkResponse> GetAsync(OS.Process process, string url, CancellationToken cancellationToken = default) {
            return SendRequestAsync(process, url, HttpMethod.Get, null, null, cancellationToken);
        }

        /// <summary> HTTP POST shorthand. </summary>
        public static Task<NetworkResponse> PostAsync(OS.Process process, string url, byte[] body, CancellationToken cancellationToken = default) {
            return SendRequestAsync(process, url, HttpMethod.Post, body, null, cancellationToken);
        }

        /// <summary> HTTP PUT shorthand. </summary>
        public static Task<NetworkResponse> PutAsync(OS.Process process, string url, byte[] body, CancellationToken cancellationToken = default) {
            return SendRequestAsync(process, url, HttpMethod.Put, body, null, cancellationToken);
        }

        /// <summary> HTTP DELETE shorthand. </summary>
        public static Task<NetworkResponse> DeleteAsync(OS.Process process, string url, CancellationToken cancellationToken = default) {
            return SendRequestAsync(process, url, HttpMethod.Delete, null, null, cancellationToken);
        }

        /// <summary> Downloads a file directly to the virtual file system using streams. </summary>
        public static Task DownloadToFileAsync(OS.Process process, string url, string virtualPath, IProgress<float> progress = null, CancellationToken cancellationToken = default) {
            return NetworkManager.Instance.DownloadToFileAsync(process, url, virtualPath, progress, cancellationToken);
        }

        /// <summary> Uploads a file from the virtual file system using streams. </summary>
        public static Task<NetworkResponse> UploadFileAsync(OS.Process process, string url, string virtualPath, IProgress<float> progress = null, CancellationToken cancellationToken = default) {
            return NetworkManager.Instance.UploadFileAsync(process, url, virtualPath, progress, cancellationToken);
        }

        /// <summary> Checks if network is globally enabled. </summary>
        public static bool IsNetworkEnabled() => NetworkManager.Instance.IsEnabled;

        /// <summary> Enables or disables network globally. </summary>
        public static void SetNetworkEnabled(bool enabled) => NetworkManager.Instance.IsEnabled = enabled;

        /// <summary> Gets bandwidth stats for a process. </summary>
        public static NetworkStats GetStats(OS.Process process) => NetworkManager.Instance.GetStats(process?.ProcessId);

        /// <summary> Gets stats for all processes. </summary>
        public static Dictionary<string, NetworkStats> GetAllStats() => NetworkManager.Instance.GetAllStats();

        /// <summary> Resets stats for a process. </summary>
        public static void ResetStats(OS.Process process) => NetworkManager.Instance.ResetStats(process?.ProcessId);

        /// <summary> Resets all network statistics. </summary>
        public static void ResetAllStats() => NetworkManager.Instance.ResetAllStats();

        /// <summary> Gets the current firewall configuration. </summary>
        public static NetworkFirewall GetFirewall() => NetworkManager.Instance.Firewall;
        
        /// <summary> Sets a new firewall configuration. </summary>
        public static void SetFirewall(NetworkFirewall firewall) => NetworkManager.Instance.Firewall = firewall;
    }
}
