using System;
using System.IO;
using System.ComponentModel;
using CefSharp;
using CefSharp.OffScreen;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.Threading.Tasks;
using TheGame.Core.Input;
using TheGame.Core.OS;
using TheGame.Graphics;
using System.Linq;
using CefSharp.Callback;
using CefSharp.Structs;
using TheGame.Core.OS.DragDrop;
using CefSharp.Enums;

namespace TheGame.Core.UI.Controls;

/// <summary>
/// A UI control that embeds a real Chromium browser using CefSharp offscreen rendering.
/// </summary>
public class BrowserControl : UIElement, IDisposable, IDropTarget {
    private ChromiumWebBrowser _browser;
    private Texture2D _browserTexture;
    private byte[] _pixelBuffer;
    private bool _isDirty;
    private bool _dragInside;
    private IDragData _activeCefDragData; // Cache for the current drag session
    private readonly object _textureLock = new();

    private bool _isInitializing;
    public bool IsInitialized { get; private set; }
    
    private IAudioHandler _audioHandler; // Strong reference to prevent GC during unmanaged callbacks
    
    public string Url { get; set; } = "about:blank";
    public Color BackgroundColor { get; set; } = new Color(30, 30, 30);

    public event Action<string> OnAddressChanged;
    public event Action<string> OnTitleChanged;
    public event Action<bool> OnLoadingStateChanged;
    
    // Download and Dialog events
    public event Action<DownloadItem, IBeforeDownloadCallback> OnDownloadStarted;
    public event Action<DownloadItem, IDownloadItemCallback> OnDownloadUpdated;
    public event Action<DialogType, string, string, IJsDialogCallback, bool> OnShowDialog;
    public event Action<CefFileDialogMode, string, string, IReadOnlyCollection<string>, IFileDialogCallback> OnFileDialog;
    
    public event Action OnFocusedEvent;
    public event Action OnUnfocusedEvent;

    public bool CanGoBack => _browser?.CanGoBack ?? false;
    public bool CanGoForward => _browser?.CanGoForward ?? false;
    
    [Obsolete("For Designer/Serialization use only")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public BrowserControl() : this(Vector2.Zero, new Vector2(800, 600)) { }
    
    private TaskScheduler _uiScheduler;

    public BrowserControl(Vector2 position, Vector2 size) : base(position, size) {
        ConsumesInput = true;
        _uiScheduler = TaskScheduler.FromCurrentSynchronizationContext();
    }
    
    /// <summary>
    /// Call this after the GraphicsDevice is available to create the browser instance.
    /// This is a synchronous wrapper for backward compatibility.
    /// </summary>
    public void InitializeBrowser() {
        InitializeBrowserAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Initializes the browser asynchronously to prevent blocking the UI thread.
    /// </summary>
    public async System.Threading.Tasks.Task InitializeBrowserAsync() {
        if (IsInitialized || _browser != null || _isInitializing) return;
        _isInitializing = true;

        try {
            // CEF is initialized in Program.cs (Main Shell), so we don't need to check here
            // This prevents double-initialization or thread conflicts during startup.
            
            int width = Math.Max(1, (int)Size.X);
            int height = Math.Max(1, (int)Size.Y);
            
            _browserTexture = new Texture2D(G.GraphicsDevice, width, height, false, SurfaceFormat.Color);
            _pixelBuffer = new byte[width * height * 4];
            
            var browserSettings = new BrowserSettings {
                WindowlessFrameRate = 60 // Match game framerate
            };
            
            // Capture process context ON THE UI THREAD before offloading to background
            var ownerProcess = GetOwnerProcess();
            DebugLogger.Log($"[BrowserControl] Initializing for process: {ownerProcess?.AppId ?? "NULL"}");

            // Initialize the browser on a background thread to avoid UI freeze
            _browser = await System.Threading.Tasks.Task.Run(() => {
                DebugLogger.Log($"[BrowserControl] Creating ChromiumWebBrowser on background thread {Environment.CurrentManagedThreadId}");
                var b = new ChromiumWebBrowser("", browserSettings); // Start with empty URL to avoid early navigation race
                b.Size = new System.Drawing.Size(width, height);
                
                // Set custom request handler to bridge with our NetworkManager
                b.RequestHandler = new NetworkRequestHandler(ownerProcess);
                
                // Set audio handler to capture PCM data
                _audioHandler = new BrowserAudioHandler(ownerProcess);
                b.AudioHandler = _audioHandler;
                
                // Set download and dialog handlers
                b.DownloadHandler = new DownloadHandler(this);
                b.JsDialogHandler = new JsDialogHandler(this);
                b.DialogHandler = new FileDialogHandler(this);
                b.DragHandler = new DragHandler(this);
                
                // Track initialization state
                b.BrowserInitialized += (s, e) => {
                    var host = b.GetBrowserHost();
                    DebugLogger.Log($"[BrowserControl] CEF Browser Initialized. Host: {(host != null ? "READY" : "NULL")}. Unmuting audio.");
                    host?.SetAudioMuted(false);
                };
                
                return b;
            });
            
            // Handle paint events
            _browser.Paint += OnBrowserPaint;
            
            // Handle loading events
            _browser.LoadingStateChanged += (sender, args) => {
                OnLoadingStateChanged?.Invoke(args.IsLoading);
            };

            _browser.AddressChanged += (sender, args) => {
                Url = args.Address;
                OnAddressChanged?.Invoke(args.Address);
            };

            _browser.TitleChanged += (sender, args) => {
                OnTitleChanged?.Invoke(args.Title);
            };
            
            OnResize += HandleResize;
            IsInitialized = true;
        } finally {
            _isInitializing = false;
        }
    }

    public void Dispose() {
        if (_browser != null) {
            _browser.Paint -= OnBrowserPaint;
            // Dispose the handlers if they are disposable
            (_audioHandler as IDisposable)?.Dispose();
            _audioHandler = null;
            _browser.Dispose();
            _browser = null;
        }

        if (_browserTexture != null) {
            _browserTexture.Dispose();
            _browserTexture = null;
        }

        OnResize -= HandleResize;
    }

    #region Custom Network Handlers

    private class NetworkRequestHandler : CefSharp.Handler.RequestHandler {
        private readonly Process _ownerProcess;

        public NetworkRequestHandler(Process ownerProcess) {
            _ownerProcess = ownerProcess;
        }

        protected override IResourceRequestHandler GetResourceRequestHandler(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, bool isNavigation, bool isDownload, string requestInitiator, ref bool disableDefaultHandling) {
            return new NetworkResourceRequestHandler(_ownerProcess);
        }
    }

    private class NetworkResourceRequestHandler : CefSharp.Handler.ResourceRequestHandler {
        private readonly Process _ownerProcess;

        public NetworkResourceRequestHandler(Process ownerProcess) {
            _ownerProcess = ownerProcess;
        }

        protected override IResourceHandler GetResourceHandler(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request) {
            return new NetworkResourceHandler(_ownerProcess);
        }
    }

    private class NetworkResourceHandler : IResourceHandler {
        private readonly Process _ownerProcess;
        private NetworkResponse _response;
        private Stream _responseStream;
        private string _mimeType;
        private string _charset;
        private TaskCompletionSource<bool> _completionSource;

        public NetworkResourceHandler(Process ownerProcess) {
            _ownerProcess = ownerProcess;
        }

        // --- Core Resource Lifecycle ---

        public bool Open(IRequest request, out bool handleRequest, ICallback callback) {
            handleRequest = true;
            _completionSource = new TaskCompletionSource<bool>();

            // Copy data synchronously - critical for thread safety
            var url = request.Url;
            var methodStr = request.Method;
            var headers = new Dictionary<string, string>();
            if (request.Headers != null) {
                foreach (string key in request.Headers.AllKeys) {
                    headers[key] = request.Headers[key];
                }
            }

            byte[] postData = null;
            if (request.PostData != null) {
                using var ms = new MemoryStream();
                foreach (var element in request.PostData.Elements) {
                    if (element.Type == PostDataElementType.Bytes) {
                        ms.Write(element.Bytes, 0, element.Bytes.Length);
                    }
                }
                postData = ms.ToArray();
            }

            // Execute network request via our OS layer
            Task.Run(async () => {
                try {
                    // Permission Check: Must be registered for network
                    if (_ownerProcess == null || !NetworkManager.Instance.IsEnabled || !NetworkManager.Instance.GetStats(_ownerProcess.ProcessId)?.AppId.Equals(_ownerProcess.AppId) == true) {
                        // Double check registration because GetStats only returns something if registered
                        if (_ownerProcess == null || NetworkManager.Instance.GetStats(_ownerProcess.ProcessId) == null) {
                            _response = CreateErrorResponse(url, "403 Forbidden: Process not registered for Network access.");
                        }
                    }

                    if (_response == null) {
                        var method = new System.Net.Http.HttpMethod(methodStr);
                        // Use streaming API to avoid buffering large responses (like downloads)
                        _response = await Shell.Network.SendRequestStreamAsync(_ownerProcess, url, method, postData, headers);
                    }
                    
                    if (_response == null || _response.StatusCode >= 400) {
                        string reason = _response?.ErrorMessage ?? (_response?.StatusCode.ToString() ?? "Connection failed");
                        _response = CreateErrorResponse(url, reason);
                    }
                    
                    if (_response.Stream != null) {
                        _responseStream = _response.Stream;
                    } else if (_response.BodyBytes != null) {
                        _responseStream = new MemoryStream(_response.BodyBytes);
                    }

                    if (_response.Headers != null && _response.Headers.TryGetValue("Content-Type", out var contentType)) {
                        var parts = contentType.Split(';');
                        _mimeType = parts[0].Trim();
                        
                        // Extract charset if present
                        _charset = null;
                        foreach (var part in parts.Skip(1)) {
                            var subPart = part.Trim();
                            if (subPart.StartsWith("charset=", StringComparison.OrdinalIgnoreCase)) {
                                _charset = subPart.Substring(8).Trim('"', ' ');
                                break;
                            }
                        }
                    } else {
                        _mimeType = "text/html";
                        _charset = "utf-8";
                    }

                    _completionSource.SetResult(true);
                    callback.Continue();
                } catch (Exception ex) {
                    DebugLogger.Log($"[NetworkResourceHandler] Exception for {url}: {ex.Message}");
                    _completionSource.SetResult(false);
                    callback.Cancel();
                }
            });

            return true;
        }

        private NetworkResponse CreateErrorResponse(string url, string reason) {
            DebugLogger.Log($"[NetworkResourceHandler] Handling graceful error for {url}: {reason}");
            
            string errorHtml;
            try {
                string vfsPath = "C:\\Windows\\Web\\ErrorPage\\HorizonError.html";
                errorHtml = VirtualFileSystem.Instance.ReadAllText(vfsPath);
                
                if (!string.IsNullOrEmpty(errorHtml)) {
                    errorHtml = errorHtml.Replace("{reason}", reason);
                } else {
                    errorHtml = $"<html><body style=\"background:#111; color:#eee; font-family:sans-serif; text-align:center; padding-top:20vh;\"><h1>Access Denied</h1><p>{reason}</p></body></html>";
                }
            } catch {
                errorHtml = $"<html><body style=\"background:#111; color:#eee; font-family:sans-serif; text-align:center; padding-top:20vh;\"><h1>Access Denied</h1><p>{reason}</p></body></html>";
            }

            return new NetworkResponse {
                StatusCode = 200,
                BodyBytes = System.Text.Encoding.UTF8.GetBytes(errorHtml),
                Headers = new Dictionary<string, string> { { "Content-Type", "text/html; charset=utf-8" } }
            };
        }

        public void GetResponseHeaders(IResponse response, out long responseLength, out string redirectUrl) {
            _completionSource.Task.Wait(); 

            responseLength = -1; // Streaming
            try {
                if (_responseStream?.CanSeek == true) responseLength = _responseStream.Length;
            } catch { }

            redirectUrl = null;

            response.StatusCode = _response?.StatusCode ?? 200;
            response.StatusText = _response?.StatusCode == 200 ? "OK" : "Error";
            response.MimeType = string.IsNullOrEmpty(_mimeType) ? "text/html" : _mimeType;
            response.Charset = string.IsNullOrEmpty(_charset) ? "utf-8" : _charset;

            var headers = new System.Collections.Specialized.NameValueCollection();
            if (_response?.Headers != null) {
                foreach (var header in _response.Headers) {
                    var key = header.Key.ToLower();
                    if (key == "content-encoding" || key == "transfer-encoding" || key == "content-length") continue;
                    headers.Add(header.Key, header.Value);
                }
            }
            response.Headers = headers;
        }

        public bool Read(Stream dataOut, out int bytesRead, IResourceReadCallback callback) {
            bytesRead = 0;
            if (_responseStream == null) return false;

            try {
                // Buffer to read from our response stream
                var buffer = new byte[8192];
                bytesRead = _responseStream.Read(buffer, 0, (int)Math.Min(buffer.Length, dataOut.Length));

                if (bytesRead > 0) {
                    dataOut.Write(buffer, 0, bytesRead);
                    return true;
                } else {
                    // End of stream
                    _responseStream.Dispose();
                    _responseStream = null;
                }
            } catch (Exception ex) {
                DebugLogger.Log($"[NetworkResourceHandler] Read Error: {ex.Message}");
                _responseStream?.Dispose();
                _responseStream = null;
            }

            return false;
        }

        public bool Skip(long nToSkip, out long nSkipped, IResourceSkipCallback callback) {
            nSkipped = 0;
            if (_responseStream == null || !_responseStream.CanSeek) return false;
            try {
                long oldPos = _responseStream.Position;
                _responseStream.Seek(nToSkip, SeekOrigin.Current);
                nSkipped = _responseStream.Position - oldPos;
                return true;
            } catch {
                return false;
            }
        }

        public void Cancel() {
            _responseStream?.Dispose();
            _responseStream = null;
        }

        public void Dispose() {
            _responseStream?.Dispose();
            _responseStream = null;
        }

        [Obsolete] public bool ProcessRequest(IRequest request, ICallback callback) => false;
        [Obsolete] public bool ReadResponse(Stream dataOut, out int bytesRead, ICallback callback) { bytesRead = 0; return false; }
    }

    #endregion
    
    #region Audio Handler

    public class BrowserAudioHandler : IAudioHandler {
        private readonly Process _ownerProcess;
        private AudioManager.BufferedSampleProvider _audioProvider;
        private string _mediaHandle;
        private bool _isMuted;
        
        // Buffer reuse optimization - prevent GC allocations every packet
        private float[] _interleavedBuffer;
        private int _lastBufferSize = 0;

        public BrowserAudioHandler(Process ownerProcess) {
            _ownerProcess = ownerProcess;
            DebugLogger.Log($"[BrowserAudioHandler] Constructor called for process: {ownerProcess?.AppId ?? "NULL"}");
        }

        public bool GetAudioParameters(IWebBrowser chromiumWebBrowser, IBrowser browser, ref AudioParameters parameters) {
            try {
                DebugLogger.Log($"[BrowserAudioHandler] GetAudioParameters requested: {parameters.SampleRate}Hz, Channels: {parameters.ChannelLayout}");
                
                // PLANAR AUDIO FIX IMPLEMENTED
                // OnAudioStreamPacket now properly handles CefSharp's planar audio format
                // by dereferencing the IntPtr** and interleaving channels manually
                DebugLogger.Log($"[BrowserAudioHandler] Enabling audio with planar-to-interleaved conversion");
                return true; // Enable audio with proper planar handling
            } catch (Exception ex) {
                DebugLogger.Log($"[BrowserAudioHandler] ERROR in GetAudioParameters: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                return false; // Disable audio on error
            }
        }

        public void OnAudioStreamStarted(IWebBrowser chromiumWebBrowser, IBrowser browser, AudioParameters parameters, int channels) {
            try {
                DebugLogger.Log($"[BrowserAudioHandler] OnAudioStreamStarted BEGIN. Process: {_ownerProcess?.AppId}");
                
                bool isRegistered = AudioManager.Instance.IsRegistered(_ownerProcess);
                DebugLogger.Log($"[BrowserAudioHandler] IsRegistered check complete: {isRegistered}");
                
                _isMuted = !isRegistered;
                if (_isMuted) {
                    DebugLogger.Log($"[BrowserAudioHandler] Process {_ownerProcess?.AppId} is not registered as audio player. Muting browser audio.");
                    return;
                }

                DebugLogger.Log($"[BrowserAudioHandler] Creating wave format: {parameters.SampleRate}Hz, {channels}ch");
                var format = NAudio.Wave.WaveFormat.CreateIeeeFloatWaveFormat(parameters.SampleRate, channels);
                
                DebugLogger.Log($"[BrowserAudioHandler] Creating BufferedSampleProvider");
                _audioProvider = new AudioManager.BufferedSampleProvider(format);
                
                DebugLogger.Log($"[BrowserAudioHandler] Registering live media with Shell.Media");
                _mediaHandle = Shell.Media.RegisterLiveMedia(_ownerProcess, _audioProvider);
                
                DebugLogger.Log($"[BrowserAudioHandler] Stream started successfully. Handle: {_mediaHandle}");
            } catch (Exception ex) {
                DebugLogger.Log($"[BrowserAudioHandler] CRITICAL ERROR in OnAudioStreamStarted: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                _isMuted = true; // Mute on error to prevent further issues
            }
        }

        private int _packetCount = 0;
        public void OnAudioStreamPacket(IWebBrowser chromiumWebBrowser, IBrowser browser, IntPtr data, int noOfFrames, long pts) {
            if (_isMuted || _audioProvider == null) return;
            
            // Critical validation: ensure pointer is valid
            if (data == IntPtr.Zero) {
                DebugLogger.Log($"[BrowserAudioHandler] Null data pointer received, skipping packet");
                return;
            }

            if (noOfFrames <= 0) {
                DebugLogger.Log($"[BrowserAudioHandler] Invalid frame count: {noOfFrames}");
                return;
            }

            try {
                int channels = _audioProvider.WaveFormat.Channels;

                if (_packetCount++ % 200 == 0) {
                    DebugLogger.Log($"[BrowserAudioHandler] Packet #{_packetCount}: {noOfFrames} frames, {channels}ch. DataPtr: 0x{data.ToInt64():X}");
                }

                // Safety check: Don't allow insane packet sizes
                if (noOfFrames > 48000) {
                    DebugLogger.Log($"[BrowserAudioHandler] ERROR: Suspicious frame count: {noOfFrames}");
                    return;
                }

                // CRITICAL FIX: CefSharp provides PLANAR audio (Single** = array of channel pointers)
                // We need to:
                // 1. Treat 'data' as IntPtr[] (array of pointers, one per channel)
                // 2. Marshal each channel separately
                // 3. Interleave them manually
                
                unsafe {
                    // data is IntPtr*, pointing to an array of channel pointers
                    IntPtr* channelPtrs = (IntPtr*)data.ToPointer();
                    
                    // Allocate or reuse interleaved output buffer
                    int totalSamples = noOfFrames * channels;
                    if (_interleavedBuffer == null || _lastBufferSize < totalSamples) {
                        // Only allocate when growing - prevents GC pressure from 50-100 allocations/sec
                        _interleavedBuffer = new float[totalSamples];
                        _lastBufferSize = totalSamples;
                    }
                    
                    // Process each channel
                    for (int ch = 0; ch < channels; ch++) {
                        IntPtr channelPtr = channelPtrs[ch];
                        if (channelPtr == IntPtr.Zero) {
                            DebugLogger.Log($"[BrowserAudioHandler] Null pointer for channel {ch}");
                            return;
                        }
                        
                        // Get direct pointer to channel data
                        float* channelData = (float*)channelPtr.ToPointer();
                        
                        // Copy and interleave: sample 0 from all channels, then sample 1 from all channels, etc.
                        for (int frame = 0; frame < noOfFrames; frame++) {
                            _interleavedBuffer[frame * channels + ch] = channelData[frame];
                        }
                    }
                    
                    // Send interleaved data to audio provider
                    _audioProvider.AddSamples(_interleavedBuffer, totalSamples);
                }
            } catch (Exception ex) {
                // Catch ANY exception to prevent crashes
                DebugLogger.Log($"[BrowserAudioHandler] FATAL ERROR in OnAudioStreamPacket: {ex.GetType().Name}");
                DebugLogger.Log($"[BrowserAudioHandler] Frames: {noOfFrames}, Channels: {_audioProvider?.WaveFormat.Channels ?? -1}, DataPtr: 0x{data.ToInt64():X}");
                DebugLogger.Log($"[BrowserAudioHandler] Exception: {ex.Message}\n{ex.StackTrace}");
                
                // If we get ANY critical error, mute the audio stream to prevent cascading failures
                if (ex is AccessViolationException || ex.GetType().Name.Contains("ExecutionEngine")) {
                    _isMuted = true;
                    DebugLogger.Log($"[BrowserAudioHandler] Critical error detected - muting audio stream to prevent further crashes");
                }
                // Don't re-throw - this would crash the entire application
            }
        }

        public void OnAudioStreamStopped(IWebBrowser chromiumWebBrowser, IBrowser browser) {
            try {
                DebugLogger.Log("[BrowserAudioHandler] OnAudioStreamStopped called.");
                if (_mediaHandle != null) {
                    Shell.Media.UnloadMedia(_mediaHandle);
                    _mediaHandle = null;
                }
                _audioProvider = null;
                DebugLogger.Log("[BrowserAudioHandler] Stream stopped successfully.");
            } catch (Exception ex) {
                DebugLogger.Log($"[BrowserAudioHandler] ERROR in OnAudioStreamStopped: {ex.GetType().Name}: {ex.Message}");
            }
        }

        public void OnAudioStreamError(IWebBrowser chromiumWebBrowser, IBrowser browser, string errorMessage) {
            try {
                DebugLogger.Log($"[BrowserAudioHandler] OnAudioStreamError called: {errorMessage}");
                _isMuted = true; // Mute on error
            } catch (Exception ex) {
                DebugLogger.Log($"[BrowserAudioHandler] ERROR in OnAudioStreamError handler: {ex.GetType().Name}: {ex.Message}");
            }
        }

        public void Dispose() {
            if (_mediaHandle != null) Shell.Media.UnloadMedia(_mediaHandle);
        }
    }

    #endregion
    
    #region Download and Dialog Handlers

    private class DownloadHandler : IDownloadHandler {
        private readonly BrowserControl _parent;

        public DownloadHandler(BrowserControl parent) {
            _parent = parent;
        }

        public bool CanDownload(IWebBrowser chromiumWebBrowser, IBrowser browser, string url, string requestMethod) {
            return true;
        }

        public bool OnBeforeDownload(IWebBrowser chromiumWebBrowser, IBrowser browser, DownloadItem downloadItem, IBeforeDownloadCallback callback) {
            DebugLogger.Log($"[DownloadHandler] OnBeforeDownload: {downloadItem.SuggestedFileName} (Total: {downloadItem.TotalBytes} bytes)");
            if (_parent.OnDownloadStarted != null) {
                _parent.OnDownloadStarted?.Invoke(downloadItem, callback);
                return true; // SIGNAL: We are handling this download (CefSharp should wait/use the callback)
            } else {
                // Default behavior if no one is subscribed: cancel to avoid memory leaks
                if (!callback.IsDisposed) callback.Dispose();
            }
            return false;
        }

        public void OnDownloadUpdated(IWebBrowser chromiumWebBrowser, IBrowser browser, DownloadItem downloadItem, IDownloadItemCallback callback) {
            if (downloadItem.IsInProgress && downloadItem.PercentComplete % 10 == 0) {
                DebugLogger.Log($"[DownloadHandler] OnDownloadUpdated: {downloadItem.SuggestedFileName} - {downloadItem.PercentComplete}% ({downloadItem.ReceivedBytes}/{downloadItem.TotalBytes})");
            }
            if (downloadItem.IsComplete) DebugLogger.Log($"[DownloadHandler] Download Complete: {downloadItem.SuggestedFileName}");
            if (downloadItem.IsCancelled) DebugLogger.Log($"[DownloadHandler] Download Cancelled: {downloadItem.SuggestedFileName}");
            
            _parent.OnDownloadUpdated?.Invoke(downloadItem, callback);
        }
    }

    private class JsDialogHandler : IJsDialogHandler {
        private readonly BrowserControl _parent;

        public JsDialogHandler(BrowserControl parent) {
            _parent = parent;
        }

        public bool OnJSDialog(IWebBrowser chromiumWebBrowser, IBrowser browser, string originUrl, CefJsDialogType dialogType, string messageText, string defaultPromptText, IJsDialogCallback callback, ref bool suppressMessage) {
            if (_parent.OnShowDialog != null) {
                _parent.OnShowDialog?.Invoke((DialogType)dialogType, messageText, defaultPromptText, callback, false);
                return true;
            }
            return false;
        }

        public bool OnBeforeUnloadDialog(IWebBrowser chromiumWebBrowser, IBrowser browser, string messageText, bool isReload, IJsDialogCallback callback) {
            if (_parent.OnShowDialog != null) {
                _parent.OnShowDialog?.Invoke(DialogType.Confirm, messageText, null, callback, true);
                return true;
            }
            return false;
        }

        public void OnResetDialogState(IWebBrowser chromiumWebBrowser, IBrowser browser) { }
        public void OnDialogClosed(IWebBrowser chromiumWebBrowser, IBrowser browser) { }
    }

    private class DragHandler : IDragHandler {
        private readonly BrowserControl _parent;

        public DragHandler(BrowserControl parent) {
            _parent = parent;
        }

        public bool OnDragEnter(IWebBrowser chromiumWebBrowser, IBrowser browser, IDragData dragData, DragOperationsMask mask) {
            // Log for diagnostic purposes
            DebugLogger.Log($"[BrowserControl] DragHandler.OnDragEnter: IsLink={dragData.IsLink}, IsFile={dragData.IsFile}, FileCount={dragData.FileNames?.Count ?? 0}");
            
            // CRITICAL: Return false to allow CefSharp to handle the drag operation
            // Returning true would CANCEL the drag and prevent files from being dropped into web pages
            // Note: CefSharp offscreen doesn't support dragging OUT to desktop without complex workarounds
            return false;
        }

        public void OnDraggableRegionsChanged(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IList<DraggableRegion> regions) {
        }
    }

    // Note: BrowserDraggableItem class removed - dragging FROM browser to desktop
    // is not supported in CefSharp offscreen mode without complex workarounds.
    // See implementation_plan.md for details.

    private class FileDialogHandler : IDialogHandler {
        private readonly BrowserControl _parent;

        public FileDialogHandler(BrowserControl parent) {
            _parent = parent;
        }

        public bool OnFileDialog(IWebBrowser chromiumWebBrowser, IBrowser browser, CefFileDialogMode mode, string title, string defaultFilePath, IReadOnlyCollection<string> acceptFilters, IReadOnlyCollection<string> acceptExtensions, IReadOnlyCollection<string> acceptDescriptions, IFileDialogCallback callback) {
            if (_parent.OnFileDialog != null) {
                _parent.OnFileDialog?.Invoke(mode, title, defaultFilePath, acceptFilters, callback);
                return true;
            }
            return false;
        }
    }

    public enum DialogType {
        Alert = 0,
        Confirm = 1,
        Prompt = 2
    }

    #endregion
    
    private void HandleResize() {
        if (_browser == null) return;
        
        int width = Math.Max(1, (int)Size.X);
        int height = Math.Max(1, (int)Size.Y);
        
        _browser.Size = new System.Drawing.Size(width, height);
        
        var host = _browser.GetBrowserHost();
        host?.NotifyMoveOrResizeStarted();
        host?.WasResized();
        host?.Invalidate(PaintElementType.View);
        
        lock (_textureLock) {
            // Recreate texture with new size
            var gd = _browserTexture?.GraphicsDevice;
            if (gd != null) {
                _browserTexture?.Dispose();
                _browserTexture = new Texture2D(gd, width, height, false, SurfaceFormat.Color);
                _pixelBuffer = new byte[width * height * 4];
                _isDirty = false; // Reset dirty flag as we have a new texture
            }
        }
    }
    
    private void OnBrowserPaint(object sender, OnPaintEventArgs e) {
        lock (_textureLock) {
            var info = e.BufferHandle;
            var width = e.Width;
            var height = e.Height;
            
            if (_pixelBuffer.Length != width * height * 4) {
                _pixelBuffer = new byte[width * height * 4];
            }
            
            // Copy from CefSharp buffer to our pixel buffer
            // CefSharp uses BGRA format, we need to convert to RGBA
            System.Runtime.InteropServices.Marshal.Copy(info, _pixelBuffer, 0, _pixelBuffer.Length);
            
            // Convert BGRA to RGBA
            for (int i = 0; i < _pixelBuffer.Length; i += 4) {
                byte b = _pixelBuffer[i];
                byte r = _pixelBuffer[i + 2];
                _pixelBuffer[i] = r;
                _pixelBuffer[i + 2] = b;
            }
            
            _isDirty = true;
        }
    }
    
    public override void Update(GameTime gameTime) {
        base.Update(gameTime);
        
        // Update texture on main thread
        if (_isDirty && _browserTexture != null && _pixelBuffer != null) {
            lock (_textureLock) {
                // Safety check: Ensure buffer size matches texture exactly to avoid ArgumentException
                if (_pixelBuffer.Length == _browserTexture.Width * _browserTexture.Height * 4) {
                    _browserTexture.SetData(_pixelBuffer);
                }
                _isDirty = false;
            }
        }
    }
    
    protected override void OnFocused() {
        _browser?.GetBrowserHost()?.SetFocus(true);
        OnFocusedEvent?.Invoke();
    }
    
    protected override void OnUnfocused() {
        _browser?.GetBrowserHost()?.SetFocus(false);
        OnUnfocusedEvent?.Invoke();
    }
    
    protected override void UpdateInput() {
        base.UpdateInput();
        
        if (_browser == null || !IsVisible) return;

        // Hook into Shell Drag/Drop lifecycle so this control is recognized as a drop target
        // Moving this to UpdateInput ensures higher responsiveness during active drags
        if (Shell.Drag.IsActive) {
            if (IsMouseOver) {
                Shell.Drag.CheckDropTarget(this, InputManager.MousePosition.ToVector2());
            } else if (_dragInside) {
                OnDragLeave();
            }
        }

        // Handle drop on mouse release
        if (Shell.Drag.IsActive && IsMouseOver && InputManager.IsMouseButtonJustReleased(MouseButton.Left)) {
            Shell.Drag.TryDropOn(this, InputManager.MousePosition.ToVector2());
        }
        
        var host = _browser.GetBrowserHost();
        if (host == null) return;
        
        // Forward mouse events
        if (IsMouseOver) {
            var localPos = InputManager.MousePosition.ToVector2() - AbsolutePosition;
            var mouseEvent = new MouseEvent((int)localPos.X, (int)localPos.Y, GetCefModifiers());
            
            // Mouse move (CRITICAL: Skip if drag is active over the browser to avoid conflicts)
            if (!_dragInside) {
                host.SendMouseMoveEvent(mouseEvent, false);
            }
            
            // Mouse clicks
            if (InputManager.IsMouseButtonJustPressed(MouseButton.Left)) {
                host.SendMouseClickEvent(mouseEvent, CefSharp.MouseButtonType.Left, false, 1);
            }
            if (InputManager.IsMouseButtonJustReleased(MouseButton.Left)) {
                host.SendMouseClickEvent(mouseEvent, CefSharp.MouseButtonType.Left, true, 1);
            }
            
            // Mouse wheel
            var scroll = InputManager.ScrollDelta;
            if (scroll != 0) {
                host.SendMouseWheelEvent(mouseEvent, 0, scroll * 2);
            }
        }

        // Forward keyboard events if focused
        if (IsFocused) {
            var modifiers = GetCefModifiers();

            // 1. Raw Key Events (Down/Up)
            foreach (Keys key in Enum.GetValues(typeof(Keys))) {
                if (key == Keys.None) continue;

                if (InputManager.IsKeyJustPressed(key)) {
                    host.SendKeyEvent(new KeyEvent {
                        Type = KeyEventType.KeyDown,
                        WindowsKeyCode = (int)key,
                        Modifiers = modifiers
                    });
                }
                if (InputManager.IsKeyJustReleased(key)) {
                    host.SendKeyEvent(new KeyEvent {
                        Type = KeyEventType.KeyUp,
                        WindowsKeyCode = (int)key,
                        Modifiers = modifiers
                    });
                }
            }

            // 2. Character Input (Typing)
            foreach (char c in InputManager.GetTypedChars()) {
                host.SendKeyEvent(new KeyEvent {
                    Type = KeyEventType.Char,
                    WindowsKeyCode = (int)c,
                    Modifiers = modifiers
                });
            }

            // CRITICAL: Consume input AFTER processing everything, 
            // otherwise GetTypedChars will return empty because it checks IsKeyboardConsumed!
            InputManager.IsKeyboardConsumed = true;
        }
    }

    private CefEventFlags GetCefModifiers() {
        CefEventFlags flags = CefEventFlags.None;
        if (InputManager.IsKeyDown(Keys.LeftShift) || InputManager.IsKeyDown(Keys.RightShift)) flags |= CefEventFlags.ShiftDown;
        if (InputManager.IsKeyDown(Keys.LeftControl) || InputManager.IsKeyDown(Keys.RightControl)) flags |= CefEventFlags.ControlDown;
        if (InputManager.IsKeyDown(Keys.LeftAlt) || InputManager.IsKeyDown(Keys.RightAlt)) flags |= CefEventFlags.AltDown;
        
        if (InputManager.IsMouseButtonDown(MouseButton.Left)) flags |= CefEventFlags.LeftMouseButton;
        if (InputManager.IsMouseButtonDown(MouseButton.Right)) flags |= CefEventFlags.RightMouseButton;
        if (InputManager.IsMouseButtonDown(MouseButton.Middle)) flags |= CefEventFlags.MiddleMouseButton;
        
        return flags;
    }
    
    protected override void DrawSelf(SpriteBatch spriteBatch, ShapeBatch shapeBatch) {
        if (_browserTexture != null) {
            spriteBatch.Draw(_browserTexture, AbsolutePosition, Color.White * AbsoluteOpacity);
        } else {
            // Draw placeholder if browser not initialized
            shapeBatch.FillRectangle(AbsolutePosition, Size, BackgroundColor * AbsoluteOpacity);
        }
    }
    
    /// <summary>
    /// Navigate to a URL
    /// </summary>
    public void Navigate(string url) {
        Url = url;
        _browser?.Load(url);
    }
    
    /// <summary>
    /// Reload the current page
    /// </summary>
    public void Reload() {
        _browser?.Reload();
    }
    
    /// <summary>
    /// Go back in history
    /// </summary>
    public void GoBack() {
        if (_browser?.CanGoBack == true) {
            _browser.Back();
        }
    }
    
    /// <summary>
    /// Go forward in history
    /// </summary>
    public void GoForward() {
        if (_browser?.CanGoForward == true) {
            _browser.Forward();
        }
    }
    
    /// <summary>
    /// Execute JavaScript in the browser
    /// </summary>
    public void ExecuteJavaScript(string script) {
        _browser?.ExecuteScriptAsync(script);
    }

    /// <summary>
    /// Evaluate a JavaScript expression and return the result
    /// </summary>
    public async Task<JavascriptResponse> EvaluateScriptAsync(string script) {
        if (_browser == null) return new JavascriptResponse { Success = false, Message = "Browser not initialized" };
        return await _browser.EvaluateScriptAsync(script);
    }

    // --- IDropTarget Implementation ---

    public bool CanAcceptDrop(object dragData) {
    // Accept files, strings (paths), or IDraggable (which DesktopIcon now implements)
    return dragData is string || dragData is IEnumerable<string> || dragData is IDragData || dragData is IDraggable;
}

    public DragDropEffect OnDragOver(object dragData, Vector2 position) {
        if (_browser == null) {
            DebugLogger.Log("[BrowserControl] OnDragOver: Browser is null, ignoring drag.");
            return DragDropEffect.None;
        }

        var localPos = position - AbsolutePosition;
        var mouseEvent = new MouseEvent((int)localPos.X, (int)localPos.Y, GetCefModifiers());

        var host = _browser.GetBrowserHost();
        if (host == null) {
            DebugLogger.Log("[BrowserControl] OnDragOver: BrowserHost is null, ignoring drag.");
            return DragDropEffect.None;
        }

        if (!_dragInside) {
            // Initialize new drag session
            _activeCefDragData = CreateCefDragData(dragData);
            if (_activeCefDragData == null) {
                DebugLogger.Log("[BrowserControl] OnDragOver: Failed to create CefDragData.");
                return DragDropEffect.None;
            }
            DebugLogger.Log($"Browser: DragTargetDragEnter (data: {dragData?.GetType().Name}, localPos: {localPos}, mods: {mouseEvent.Modifiers})");
            host.DragTargetDragEnter(_activeCefDragData, mouseEvent, DragOperationsMask.Copy | DragOperationsMask.Move | DragOperationsMask.Link);
            _dragInside = true;
        }

        host.DragTargetDragOver(mouseEvent, DragOperationsMask.Copy | DragOperationsMask.Move | DragOperationsMask.Link);
        return DragDropEffect.Copy; // Assume copy for now
    }

    private IDragData CreateCefDragData(object dragData) {
        if (dragData is IDragData existing) return existing;

        var cefDragData = CefSharp.DragData.Create();
        List<string> paths = new();

        if (dragData is string path) paths.Add(path);
        else if (dragData is IEnumerable<string> ps) paths.AddRange(ps);
        else if (dragData is IDraggable draggable) {
            var data = draggable.GetDragData();
            if (data is string s) paths.Add(s);
            else if (data is IEnumerable<string> l) paths.AddRange(l);
            else if (dragData is DesktopIcon di) paths.Add(di.VirtualPath); // Fallback if GetDragData failed
        }

        foreach (var p in paths) {
            if (string.IsNullOrEmpty(p)) continue;
            string hostPath = VirtualFileSystem.Instance.ToHostPath(p);
            if (!string.IsNullOrEmpty(hostPath)) {
                DebugLogger.Log($"Browser: Adding file to drag: {p} -> {hostPath}");
                cefDragData.AddFile(hostPath, Path.GetFileName(p));
            } else {
                DebugLogger.Log($"Browser: Warning - Could not map virtual path to host: {p}. Attempting direct use if it looks like a host path.");
                // If it looks like a host path already or VFS couldn't map it but it's valid, try adding it directly.
                // Some internal drag sources might already provide host paths.
                if (File.Exists(p)) {
                    DebugLogger.Log($"Browser: Using direct host path for drag: {p}");
                    cefDragData.AddFile(p, Path.GetFileName(p));
                }
            }
        }
        
        DebugLogger.Log($"Browser: Created CEF drag data with {cefDragData.FileNames?.Count ?? 0} files.");
        return cefDragData;
    }

    public void OnDragLeave() {
        DebugLogger.Log("Browser: OnDragLeave");
        _dragInside = false;
        var host = _browser?.GetBrowserHost();
        if (host != null) {
            host.DragTargetDragLeave();
            // Notify source that drag ended outside/cancelled
            host.DragSourceEndedAt(0, 0, DragOperationsMask.None);
            host.DragSourceSystemDragEnded();
        }
        _activeCefDragData = null;
    }

    public bool OnDrop(object dragData, Vector2 position) {
        _dragInside = false;
        if (_browser == null) {
            DebugLogger.Log("Browser: OnDrop failed - _browser is null");
            return false;
        }

        var host = _browser.GetBrowserHost();
        if (host == null) {
            DebugLogger.Log("Browser: OnDrop failed - BrowserHost is null");
            return false;
        }

        var localPos = position - AbsolutePosition;
        // CRITICAL: Force LeftMouseButton flag during the Drop event. 
        // In the frame it's released, GetCefModifiers might return None, which confuses Chromium.
        var modifiers = GetCefModifiers() | CefEventFlags.LeftMouseButton;
        var mouseEvent = new MouseEvent((int)localPos.X, (int)localPos.Y, modifiers);
        DebugLogger.Log($"Browser: OnDrop (data: {dragData?.GetType().Name}, localPos: {localPos}, forcedMods: {modifiers})");

        // Complete the drop sequence: final DragOver + Drop
        host.DragTargetDragOver(mouseEvent, DragOperationsMask.Copy | DragOperationsMask.Move | DragOperationsMask.Link);
        host.DragTargetDragDrop(mouseEvent);
        
        // IMPORTANT: Do NOT call DragSourceEndedAt or DragSourceSystemDragEnded here!
        // Those are only for when the BROWSER is the drag source.
        // We are dropping INTO the browser, so it's the drop TARGET, not the source.
        
        _activeCefDragData = null;
        return true;
    }

    public Rectangle GetDropBounds() => Bounds;
}
