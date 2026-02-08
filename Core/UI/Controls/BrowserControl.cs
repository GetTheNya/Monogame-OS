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

namespace TheGame.Core.UI.Controls;

/// <summary>
/// A UI control that embeds a real Chromium browser using CefSharp offscreen rendering.
/// </summary>
public class BrowserControl : UIElement, IDisposable {
    private ChromiumWebBrowser _browser;
    private Texture2D _browserTexture;
    private byte[] _pixelBuffer;
    private bool _isDirty;
    private readonly object _textureLock = new();

    private bool _isInitializing;
    public bool IsInitialized { get; private set; }
    
    private IAudioHandler _audioHandler; // Strong reference to prevent GC during unmanaged callbacks
    
    public string Url { get; set; } = "about:blank";
    public Color BackgroundColor { get; set; } = new Color(30, 30, 30);

    public event Action<string> OnAddressChanged;
    public event Action<string> OnTitleChanged;
    public event Action<bool> OnLoadingStateChanged;

    public bool CanGoBack => _browser?.CanGoBack ?? false;
    public bool CanGoForward => _browser?.CanGoForward ?? false;
    
    [Obsolete("For Designer/Serialization use only")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public BrowserControl() : this(Vector2.Zero, new Vector2(800, 600)) { }
    
    public BrowserControl(Vector2 position, Vector2 size) : base(position, size) {
        ConsumesInput = true;
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
        private MemoryStream _responseStream;
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
                        _response = await Shell.Network.SendRequestAsync(_ownerProcess, url, method, postData, headers);
                    }
                    
                    if (_response == null || _response.StatusCode >= 400) {
                        string reason = _response?.ErrorMessage ?? (_response?.StatusCode.ToString() ?? "Connection failed");
                        _response = CreateErrorResponse(url, reason);
                    }
                    
                    if (_response.BodyBytes != null) {
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

            responseLength = _responseStream?.Length ?? -1;
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
                var buffer = new byte[dataOut.Length];
                bytesRead = _responseStream.Read(buffer, 0, buffer.Length);

                if (bytesRead > 0) {
                    dataOut.Write(buffer, 0, bytesRead);
                    return true;
                }
            } catch (Exception ex) {
                DebugLogger.Log($"[NetworkResourceHandler] Read Error: {ex.Message}");
            }

            return false;
        }

        public bool Skip(long nToSkip, out long nSkipped, IResourceSkipCallback callback) {
            nSkipped = 0;
            if (_responseStream == null) return false;
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
        }

        public void Dispose() {
            _responseStream?.Dispose();
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
    }
    
    protected override void OnUnfocused() {
        _browser?.GetBrowserHost()?.SetFocus(false);
    }
    
    protected override void UpdateInput() {
        base.UpdateInput();
        
        if (_browser == null || !IsVisible) return;
        
        var host = _browser.GetBrowserHost();
        if (host == null) return;
        
        // Forward mouse events
        if (IsMouseOver) {
            var localPos = InputManager.MousePosition.ToVector2() - AbsolutePosition;
            var mouseEvent = new MouseEvent((int)localPos.X, (int)localPos.Y, GetCefModifiers());
            
            // Mouse move
            host.SendMouseMoveEvent(mouseEvent, false);
            
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
}
