using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace TheGame.Core.OS;

/// <summary>
/// Statistics for an application's network usage.
/// </summary>
public class NetworkStats {
    public string AppId { get; set; }
    public string ProcessId { get; set; }
    public long BytesDownloaded { get; set; }
    public long BytesUploaded { get; set; }
    public DateTime LastActivity { get; set; }
}

/// <summary>
/// Manages OS-level network state, bandwidth tracking, and firewall rules.
/// </summary>
public class NetworkManager {
    private static NetworkManager _instance;
    public static NetworkManager Instance => _instance ??= new NetworkManager();

    private readonly HttpClient _httpClient;
    private readonly ConcurrentDictionary<string, NetworkStats> _stats = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _processCts = new();
    
    private bool _isEnabled = true;
    public bool IsEnabled {
        get => _isEnabled;
        set {
            if (_isEnabled != value) {
                _isEnabled = value;
                
                if (!_isEnabled) {
                    // Cancel all active requests
                    foreach (var cts in _processCts.Values) {
                        try { cts.Cancel(); } catch { }
                    }
                } else {
                    // Re-create CTS for all registered processes so they can make new requests
                    var processIds = _processCts.Keys.ToList();
                    foreach (var id in processIds) {
                        if (_processCts.TryRemove(id, out var oldCts)) {
                            oldCts.Dispose();
                        }
                        _processCts.TryAdd(id, new CancellationTokenSource());
                    }
                }

                OnStateChanged?.Invoke();
            }
        }
    }
    public NetworkFirewall Firewall { get; set; } = new();

    public event Action OnStateChanged;

    private NetworkManager() {
        var handler = new HttpClientHandler {
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
            UseCookies = false, // Let the browser manage its own cookies
            AllowAutoRedirect = false // Let the browser handle redirects
        };
        _httpClient = new HttpClient(handler) {
            Timeout = TimeSpan.FromSeconds(30),
            DefaultRequestVersion = System.Net.HttpVersion.Version20,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower
        };
        
        // Only set a generic UA as a fallback; BrowserControl should provide its own
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36");
    }

    public void RegisterProcess(Process process) {
        if (process == null) return;
        _processCts.TryAdd(process.ProcessId, new CancellationTokenSource());
        _stats.TryAdd(process.ProcessId, new NetworkStats {
            AppId = process.AppId,
            ProcessId = process.ProcessId,
            LastActivity = DateTime.Now
        });
    }

    public void UnregisterProcess(Process process) {
        if (process == null) return;
        if (_processCts.TryRemove(process.ProcessId, out var cts)) {
            cts.Cancel();
            cts.Dispose();
        }
    }

    private CancellationToken GetLinkedToken(Process process, CancellationToken appToken) {
        if (process != null && _processCts.TryGetValue(process.ProcessId, out var processCts)) {
            return CancellationTokenSource.CreateLinkedTokenSource(processCts.Token, appToken).Token;
        }
        return appToken;
    }

    private void TrackBandwidth(string processId, long downloaded, long uploaded) {
        if (_stats.TryGetValue(processId, out var stat)) {
            stat.BytesDownloaded += downloaded;
            stat.BytesUploaded += uploaded;
            stat.LastActivity = DateTime.Now;
        }
    }

    private bool IsUrlAllowed(string url) {
        if (!IsEnabled) return false;
        try {
            var uri = new Uri(url);
            string host = uri.Host.ToLower();

            // Firewall check
            if (Firewall.BlacklistedDomains.Contains(host)) return false;
            if (Firewall.WhitelistedDomains.Count > 0 && !Firewall.WhitelistedDomains.Contains(host)) return false;

            // if (!Firewall.AllowLocalhost) {
            //     if (host == "localhost" || host == "127.0.0.1" || host == "::1") return false;
            // }

            // // Simple private network check (can be expanded)
            // if (!Firewall.AllowPrivateNetwork) {
            //     if (host.StartsWith("192.168.") || host.StartsWith("10.") || host.StartsWith("172.16.")) return false;
            // }

            return true;
        } catch {
            return false;
        }
    }

    public async Task<NetworkResponse> SendRequestAsync(Process process, string url, HttpMethod method, byte[] body = null, Dictionary<string, string> headers = null, CancellationToken cancellationToken = default) {
        var response = await SendRequestStreamAsync(process, url, method, body, headers, cancellationToken);
        if (response.StatusCode == 0 || response.StatusCode >= 400 || response.Stream == null) return response;

        try {
            using var ms = new MemoryStream();
            await response.Stream.CopyToAsync(ms, cancellationToken);
            response.BodyBytes = ms.ToArray();
            response.Stream.Dispose();
            response.Stream = null;
            
            TrackBandwidth(process?.ProcessId, response.BodyBytes.Length, 0);
            return response;
        } catch (Exception ex) {
            response.Stream?.Dispose();
            return new NetworkResponse { StatusCode = 0, ErrorMessage = ex.Message };
        }
    }

    public async Task<NetworkResponse> SendRequestStreamAsync(Process process, string url, HttpMethod method, byte[] body = null, Dictionary<string, string> headers = null, CancellationToken cancellationToken = default) {
        if (!IsUrlAllowed(url)) {
            return new NetworkResponse { StatusCode = 403, ErrorMessage = IsEnabled ? "Firewall blocked" : "Network disabled" };
        }

        var linkedToken = GetLinkedToken(process, cancellationToken);
        
        try {
            var request = new HttpRequestMessage(method, url);
            if (body != null) {
                request.Content = new ByteArrayContent(body);
                TrackBandwidth(process?.ProcessId, 0, body.Length);
            }

            if (headers != null) {
                if (headers.ContainsKey("User-Agent")) request.Headers.UserAgent.Clear();

                foreach (var header in headers) {
                    if (header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value)) {
                        request.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                }
            }

            // Use ResponseHeadersRead to avoid buffering the whole body
            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linkedToken);
            
            var rawStream = await response.Content.ReadAsStreamAsync(linkedToken);
            // Wrap in a tracking stream to count bandwidth as it's read by the browser/app
            var trackingStream = new NetworkTrackingStream(rawStream, (bytes) => TrackBandwidth(process?.ProcessId, bytes, 0));

            var result = new NetworkResponse {
                StatusCode = (int)response.StatusCode,
                Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                Stream = trackingStream
            };

            foreach (var h in response.Headers) result.Headers[h.Key] = string.Join(", ", h.Value);
            if (response.Content != null) {
                foreach (var h in response.Content.Headers) {
                    if (h.Key.Equals("Content-Encoding", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!result.Headers.ContainsKey(h.Key)) result.Headers[h.Key] = string.Join(", ", h.Value);
                }
            }

            return result;
        } catch (OperationCanceledException) {
            return new NetworkResponse { StatusCode = 0, ErrorMessage = "Request cancelled" };
        } catch (Exception ex) {
            return new NetworkResponse { StatusCode = 0, ErrorMessage = ex.Message };
        }
    }

    /// <summary>
    /// Helper stream that calls a callback whenever bytes are read.
    /// Used for real-time bandwidth tracking without buffering.
    /// </summary>
    private class NetworkTrackingStream : Stream {
        private readonly Stream _innerStream;
        private readonly Action<int> _onRead;

        public NetworkTrackingStream(Stream innerStream, Action<int> onRead) {
            _innerStream = innerStream;
            _onRead = onRead;
        }

        public override bool CanRead => _innerStream.CanRead;
        public override bool CanSeek => _innerStream.CanSeek;
        public override bool CanWrite => _innerStream.CanWrite;
        public override long Length => _innerStream.Length;
        public override long Position { get => _innerStream.Position; set => _innerStream.Position = value; }

        public override void Flush() => _innerStream.Flush();
        public override long Seek(long offset, SeekOrigin origin) => _innerStream.Seek(offset, origin);
        public override void SetLength(long value) => _innerStream.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => _innerStream.Write(buffer, offset, count);

        public override int Read(byte[] buffer, int offset, int count) {
            int read = _innerStream.Read(buffer, offset, count);
            if (read > 0) _onRead(read);
            return read;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
            int read = await _innerStream.ReadAsync(buffer, offset, count, cancellationToken);
            if (read > 0) _onRead(read);
            return read;
        }

        protected override void Dispose(bool disposing) {
            if (disposing) _innerStream.Dispose();
            base.Dispose(disposing);
        }
    }

    public async Task DownloadToFileAsync(Process process, string url, string virtualPath, IProgress<float> progress = null, CancellationToken cancellationToken = default) {
        if (!IsUrlAllowed(url)) throw new Exception(IsEnabled ? "Firewall blocked" : "Network disabled");

        var linkedToken = GetLinkedToken(process, cancellationToken);

        try {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, linkedToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            var readBytes = 0L;

            using var remoteStream = await response.Content.ReadAsStreamAsync(linkedToken);
            using var localStream = VirtualFileSystem.Instance.OpenWrite(virtualPath);

            var buffer = new byte[8192];
            int bytesRead;
            while ((bytesRead = await remoteStream.ReadAsync(buffer, 0, buffer.Length, linkedToken)) > 0) {
                await localStream.WriteAsync(buffer, 0, bytesRead, linkedToken);
                readBytes += bytesRead;
                TrackBandwidth(process?.ProcessId, bytesRead, 0);
                
                if (totalBytes != -1) {
                    progress?.Report((float)readBytes / totalBytes);
                }
            }
        } catch (OperationCanceledException) {
            // Silently absorb cancellation
        }
    }

    public async Task<NetworkResponse> UploadFileAsync(Process process, string url, string virtualPath, IProgress<float> progress = null, CancellationToken cancellationToken = default) {
        if (!IsUrlAllowed(url)) return new NetworkResponse { StatusCode = 403, ErrorMessage = "Firewall blocked" };

        var linkedToken = GetLinkedToken(process, cancellationToken);
        
        try {
            if (!VirtualFileSystem.Instance.Exists(virtualPath)) {
                return new NetworkResponse { StatusCode = 0, ErrorMessage = "File not found" };
            }

            using var localStream = VirtualFileSystem.Instance.OpenRead(virtualPath);
            var totalBytes = localStream.Length;
            
            // Note: For simplicity, we buffer for the HttpClient request here, 
            // but for very large uploads we should use a custom StreamContent.
            // However, typical app usage in this OS simulator likely fits in memory.
            // Let's implement an upload stream if possible for better OOM protection.
            
            var content = new StreamContent(localStream);
            // Wrap stream to track bandwidth if needed, but HttpClient will read it.
            // Simplified tracking for now:
            TrackBandwidth(process?.ProcessId, 0, totalBytes);

            using var response = await _httpClient.PostAsync(url, content, linkedToken);
            var responseBytes = await response.Content.ReadAsByteArrayAsync(linkedToken);
            TrackBandwidth(process?.ProcessId, responseBytes.Length, 0);

            return new NetworkResponse {
                StatusCode = (int)response.StatusCode,
                BodyBytes = responseBytes,
                Headers = response.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value))
            };
        } catch (OperationCanceledException) {
            return new NetworkResponse { StatusCode = 0, ErrorMessage = "Request cancelled" };
        } catch (Exception ex) {
            return new NetworkResponse { StatusCode = 0, ErrorMessage = ex.Message };
        }
    }

    public NetworkStats GetStats(string processId) {
        return _stats.TryGetValue(processId, out var stat) ? stat : null;
    }

    public Dictionary<string, NetworkStats> GetAllStats() {
        return _stats.ToDictionary(k => k.Key, v => v.Value);
    }

    public void ResetStats(string processId) {
        if (_stats.TryGetValue(processId, out var stat)) {
            stat.BytesDownloaded = 0;
            stat.BytesUploaded = 0;
        }
    }

    public void ResetAllStats() {
        foreach (var stat in _stats.Values) {
            stat.BytesDownloaded = 0;
            stat.BytesUploaded = 0;
        }
    }
}

public class NetworkFirewall {
    public HashSet<string> WhitelistedDomains { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> BlacklistedDomains { get; } = new(StringComparer.OrdinalIgnoreCase);
    public bool AllowLocalhost { get; set; } = false;
    public bool AllowPrivateNetwork { get; set; } = false;
}
