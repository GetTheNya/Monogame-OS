using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using TheGame.Core.OS;
using TheGame.Core;

namespace TheGame.Core.OS {
    /// <summary>
    /// Central manager for checking, downloading, and facilitating system updates.
    /// </summary>
    public enum UpdateState {
        Idle,
        Checking,
        UpdateAvailable,
        NoUpdateAvailable,
        Downloading,
        Downloaded,
        Error
    }

    /// <summary>
    /// Central manager for checking, downloading, and facilitating system updates.
    /// </summary>
    public class UpdateManager {
        private static UpdateManager _instance;
        public static UpdateManager Instance => _instance ??= new UpdateManager();

        private const string LatestReleaseUrl = "https://api.github.com/repos/GetTheNya/Monogame-OS/releases/latest";
        
        public UpdateState State { get; private set; } = UpdateState.Idle;
        public UpdateCheckResult LastResult { get; private set; }
        public float DownloadProgress { get; private set; }
        
        public event Action OnStateChanged;
        public event Action<float> OnDownloadProgress;

        private CancellationTokenSource _downloadCts;

        private UpdateManager() { }

        private void SetState(UpdateState newState) {
            if (State == newState) return;
            State = newState;
            OnStateChanged?.Invoke();
        }

        public void CancelDownload() {
            if (State == UpdateState.Downloading) {
                _downloadCts?.Cancel();
            }
        }

        public async Task<UpdateCheckResult> CheckForUpdatesAsync(Process process) {
            SetState(UpdateState.Checking);
            try {
                var response = await Shell.Network.GetAsync(process, LatestReleaseUrl);
                
                if (!response.IsSuccessStatusCode) {
                    LastResult = new UpdateCheckResult { Success = false, ErrorMessage = $"HTTP {response.StatusCode}" };
                    SetState(UpdateState.Error);
                    return LastResult;
                }

                var release = JsonSerializer.Deserialize<GitHubRelease>(response.BodyText);
                if (release == null || string.IsNullOrEmpty(release.TagName)) {
                    LastResult = new UpdateCheckResult { Success = false, ErrorMessage = "Invalid release data received." };
                    SetState(UpdateState.Error);
                    return LastResult;
                }

                bool isNewer = IsNewerVersion(release.TagName, SystemVersion.Current);
                var zipAsset = release.Assets.Find(a => a.BrowserDownloadUrl.EndsWith(".zip"));

                LastResult = new UpdateCheckResult {
                    Success = true,
                    LatestVersion = release.TagName,
                    IsUpdateAvailable = isNewer,
                    DownloadUrl = zipAsset?.BrowserDownloadUrl
                };

                SetState(isNewer ? UpdateState.UpdateAvailable : UpdateState.NoUpdateAvailable);
                return LastResult;
            } catch (Exception ex) {
                LastResult = new UpdateCheckResult { Success = false, ErrorMessage = ex.Message };
                SetState(UpdateState.Error);
                return LastResult;
            }
        }

        public async Task StartDownloadAsync(Process process) {
            if (LastResult == null || string.IsNullOrEmpty(LastResult.DownloadUrl)) return;
            
            _downloadCts?.Cancel();
            _downloadCts = new CancellationTokenSource();
            CancellationToken token = _downloadCts.Token;

            SetState(UpdateState.Downloading);
            DownloadProgress = 0;
            OnDownloadProgress?.Invoke(0);

            try {
                string downloadPath = @"C:\Temp\update.zip";
                
                // Ensure temp folder exists in VFS
                if (!VirtualFileSystem.Instance.Exists(@"C:\Temp")) {
                    VirtualFileSystem.Instance.CreateDirectory(@"C:\Temp");
                }
                
                await Shell.Network.DownloadToFileAsync(process, LastResult.DownloadUrl, downloadPath, new Progress<float>(p => {
                    DownloadProgress = p;
                    OnDownloadProgress?.Invoke(p);
                }), token);
                
                token.ThrowIfCancellationRequested();

                SetState(UpdateState.Downloaded);
            } catch (OperationCanceledException) {
                DebugLogger.Log("UpdateManager: Download cancelled by user.");
                CleanupPartialDownload();
                SetState(UpdateState.UpdateAvailable);
            } catch (Exception ex) {
                CleanupPartialDownload();
                LastResult = new UpdateCheckResult { 
                    Success = false, 
                    ErrorMessage = $"Download failed: {ex.Message}",
                    LatestVersion = LastResult.LatestVersion, // Preserve version info
                    IsUpdateAvailable = true,
                    DownloadUrl = LastResult.DownloadUrl
                };
                SetState(UpdateState.Error);
                DebugLogger.Log($"UpdateManager Download Error: {ex}");
            } finally {
                _downloadCts?.Dispose();
                _downloadCts = null;
            }
        }

        private void CleanupPartialDownload() {
            try {
                string downloadPath = @"C:\Temp\update.zip";
                if (VirtualFileSystem.Instance.Exists(downloadPath)) {
                    VirtualFileSystem.Instance.Delete(downloadPath);
                }
            } catch (Exception ex) {
                DebugLogger.Log($"UpdateManager: Failed to cleanup partial download: {ex.Message}");
            }
        }

        public void LaunchUpdater(string zipPath) {
            try {
                // Track update status in registry
                Registry.Instance.SetValue($"{Shell.Registry.Update}\\LastVersion", SystemVersion.Current);
                Registry.Instance.SetValue($"{Shell.Registry.Update}\\UpdatePending", true);
                Registry.Instance.FlushToDisk();

                string hostZipPath = VirtualFileSystem.Instance.ToHostPath(zipPath);
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                
                // Create a temp directory for the updater to run from
                string tempDir = Path.Combine(Path.GetTempPath(), "HentOSUpdater_" + Guid.NewGuid().ToString().Substring(0, 8));
                Directory.CreateDirectory(tempDir);
                
                DebugLogger.Log($"Copying updater files to temp: {tempDir}");
                
                // Copy files needed for the updater
                foreach (string file in Directory.GetFiles(baseDir)) {
                    string name = Path.GetFileName(file);
                    if (name.StartsWith("Updater", StringComparison.OrdinalIgnoreCase) || 
                        name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || 
                        name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) {
                        File.Copy(file, Path.Combine(tempDir, name), true);
                    }
                }
                
                // Copy runtimes folder for self-contained builds
                string runtimesSrc = Path.Combine(baseDir, "runtimes");
                if (Directory.Exists(runtimesSrc)) {
                    CopyDirectory(runtimesSrc, Path.Combine(tempDir, "runtimes"));
                }

                string exeName = "TheGame.exe";
                int processId = Environment.ProcessId;
                
                string updaterPath = Path.Combine(tempDir, "Updater.exe");
                string args = $"\"{hostZipPath}\" \"{baseDir.TrimEnd('\\')}\" \"{exeName}\" {processId}";
                
                DebugLogger.Log($"Launching Updater from Temp: {updaterPath} {args}");
                
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
                    FileName = updaterPath,
                    Arguments = args,
                    UseShellExecute = true,
                    WorkingDirectory = tempDir
                });
                
                // Start the shutdown sequence for update
                Shell.ShutdownForUpdate();
            } catch (Exception ex) {
                 DebugLogger.Log($"UpdateManager Updater Launch Error: {ex}");
                 LastResult = new UpdateCheckResult { Success = false, ErrorMessage = $"Failed to launch updater: {ex.Message}" };
                 SetState(UpdateState.Error);
            }
        }

        private void CopyDirectory(string sourceDir, string destinationDir) {
            Directory.CreateDirectory(destinationDir);
            foreach (string file in Directory.GetFiles(sourceDir)) {
                File.Copy(file, Path.Combine(destinationDir, Path.GetFileName(file)), true);
            }
            foreach (string subDir in Directory.GetDirectories(sourceDir)) {
                CopyDirectory(subDir, Path.Combine(destinationDir, Path.GetFileName(subDir)));
            }
        }

        public bool IsNewerVersion(string latest, string current) {
            try {
                string lStr = latest.TrimStart('v').Split('-')[0];
                string cStr = current.TrimStart('v').Split('-')[0];
                
                string[] lParts = lStr.Split('.');
                string[] cParts = cStr.Split('.');
                
                for (int i = 0; i < Math.Max(lParts.Length, cParts.Length); i++) {
                    int lVal = i < lParts.Length && int.TryParse(lParts[i], out int resL) ? resL : 0;
                    int cVal = i < cParts.Length && int.TryParse(cParts[i], out int resC) ? resC : 0;
                    
                    if (lVal > cVal) return true;
                    if (lVal < cVal) return false;
                }
            } catch { }
            return false;
        }

        private class GitHubRelease {
            [JsonPropertyName("tag_name")]
            public string TagName { get; set; }
            
            [JsonPropertyName("assets")]
            public List<GitHubAsset> Assets { get; set; }
        }

        private class GitHubAsset {
            [JsonPropertyName("browser_download_url")]
            public string BrowserDownloadUrl { get; set; }
        }
    }

    public class UpdateCheckResult {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public string LatestVersion { get; set; }
        public bool IsUpdateAvailable { get; set; }
        public string DownloadUrl { get; set; }
    }
}
