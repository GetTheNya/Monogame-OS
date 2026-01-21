using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using TheGame.Core.OS;
using System.Reflection;

namespace TheGame.Core.OS;

public enum MediaStatus {
    Starting,
    Playing,
    Pausing,
    Paused,
    Stopping,
    Stopped
}

public class AudioManager {
    private static AudioManager _instance;
    public static AudioManager Instance => _instance ??= new AudioManager();

    private WaveOutEvent _lowLatencyOutput;
    private WaveOutEvent _highLatencyOutput;
    private MixingSampleProvider _lowLatencyMixer;
    private MixingSampleProvider _highLatencyMixer;
    private PeakSampleProvider _lowLatencyPeak;
    private PeakSampleProvider _highLatencyPeak;
    private float _masterVolume = 1.0f;

    private readonly Dictionary<string, MediaHandle> _handles = new();
    private readonly Dictionary<Process, ProcessContext> _processContexts = new();
    private readonly Dictionary<string, CachedAudio> _audioCache = new();
    private readonly object _lock = new();
    
    private Process _systemProcess;
    public Process SystemProcess => _systemProcess;

    public event Action OnProcessRegistered;
    public event Action OnProcessUnregistered;

    public IEnumerable<Process> RegisteredProcesses {
        get {
            lock (_lock) {
                return _processContexts.Where(kvp => kvp.Value.IsRegistered || kvp.Key == _systemProcess).Select(kvp => kvp.Key).ToList();
            }
        }
    }

    private AudioManager() {
        InitializeEngine();
    }

    private void InitializeEngine() {
        try {
            // Create a dummy system process for global sounds
            _systemProcess = new Process { AppId = "SYSTEM" };

            // Low latency (60ms) for one-shots and UI
            _lowLatencyOutput = new WaveOutEvent { DesiredLatency = 60 };
            _lowLatencyMixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2));
            _lowLatencyMixer.ReadFully = true;
            _lowLatencyPeak = new PeakSampleProvider(_lowLatencyMixer);
            _lowLatencyOutput.Init(_lowLatencyPeak);
            _lowLatencyOutput.Play();

            // High latency (150ms) for music and app media
            _highLatencyOutput = new WaveOutEvent { DesiredLatency = 150 };
            _highLatencyMixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2));
            _highLatencyMixer.ReadFully = true;
            _highLatencyPeak = new PeakSampleProvider(_highLatencyMixer);
            _highLatencyOutput.Init(_highLatencyPeak);
            _highLatencyOutput.Play();
            
            // Load values from registry
            _masterVolume = Registry.GetValue($"{Shell.Registry.Audio}\\MasterVolume", 1.0f);
        } catch (Exception ex) {
            DebugLogger.Log($"Failed to initialize NAudio engine: {ex.Message}");
        }
    }

    public float MasterVolume {
        get => _masterVolume;
        set {
            _masterVolume = Math.Clamp(value, 0f, 1f);
            Registry.SetValue($"{Shell.Registry.Audio}\\MasterVolume", _masterVolume);
            lock (_lock) {
                foreach (var context in _processContexts.Values) {
                    context.UpdateEffectiveVolume(_masterVolume);
                }
            }
        }
    }

    public void RegisterAsPlayer(Process owner) {
        if (owner == null) return;
        lock (_lock) {
            var context = GetOrCreateProcessContext(owner);
            bool wasRegistered = context.IsRegistered;
            context.IsRegistered = true;
            if (!wasRegistered) OnProcessRegistered?.Invoke();
        }
    }

    public bool IsRegistered(Process owner) {
        if (owner == null || owner == _systemProcess) return true;
        lock (_lock) {
            return _processContexts.TryGetValue(owner, out var context) && context.IsRegistered;
        }
    }

    public string LoadMedia(Process owner, string virtualPath, bool useFading = true, bool autoUnload = false) {
        if (owner == null) owner = _systemProcess;
        
        if (owner != _systemProcess && !IsRegistered(owner)) {
            DebugLogger.Log($"Process {owner.AppId} attempted to load media without registering first. Call Shell.Media.RegisterAsPlayer() first.");
            return null;
        }

        string hostPath = VirtualFileSystem.Instance.ToHostPath(virtualPath);
        if (!File.Exists(hostPath)) {
            DebugLogger.Log($"Media file not found: {virtualPath}");
            return null;
        }

        lock (_lock) {
            var processContext = GetOrCreateProcessContext(owner);
            if (processContext == null) return null;
            if (processContext.HandleCount >= 32) {
                DebugLogger.Log($"Process {owner.AppId} exceeded media handle limit (32).");
                return null;
            }

            try {
                // Check cache first
                CachedAudio cached = null;
                if (_audioCache.TryGetValue(virtualPath, out cached)) {
                    var handle = new MediaHandle(owner, virtualPath, cached, useFading, autoUnload);
                    _handles[handle.Id] = handle;
                    processContext.AddHandle(handle);
                    return handle.Id;
                }

                // If not cached, load and check if it should be cached
                var handleFromDisk = new MediaHandle(owner, virtualPath, hostPath, useFading, autoUnload);
                
                // Auto-cache small files (< 2MB of raw samples)
                if (new FileInfo(hostPath).Length < 1024 * 1024 * 2) {
                    PreloadInternal(virtualPath, hostPath);
                    if (_audioCache.TryGetValue(virtualPath, out cached)) {
                        handleFromDisk.Dispose(); 
                        handleFromDisk = new MediaHandle(owner, virtualPath, cached, useFading, autoUnload);
                    }
                }

                _handles[handleFromDisk.Id] = handleFromDisk;
                processContext.AddHandle(handleFromDisk);
                return handleFromDisk.Id;
            } catch (Exception ex) {
                DebugLogger.Log($"Error loading media {virtualPath}: {ex.Message}");
                return null;
            }
        }
    }

    public void Preload(string virtualPath) {
        string hostPath = VirtualFileSystem.Instance.ToHostPath(virtualPath);
        if (!File.Exists(hostPath)) return;
        lock (_lock) {
            PreloadInternal(virtualPath, hostPath);
        }
    }

    private void PreloadInternal(string virtualPath, string hostPath) {
        if (_audioCache.ContainsKey(virtualPath)) return;

        try {
            using var reader = new AudioFileReader(hostPath);
            ISampleProvider source = reader;

            if (reader.WaveFormat.Channels == 1) {
                source = new MonoToStereoSampleProvider(source);
            }

            if (source.WaveFormat.SampleRate != 44100) {
                source = new WdlResamplingSampleProvider(source, 44100);
            }
            
            var samples = new List<float>();
            float[] buffer = new float[4096];
            int read;
            while ((read = source.Read(buffer, 0, buffer.Length)) > 0) {
                samples.AddRange(buffer.Take(read));
            }

            _audioCache[virtualPath] = new CachedAudio(samples.ToArray(), source.WaveFormat);
            DebugLogger.Log($"Cached audio: {virtualPath} ({samples.Count * 4 / 1024} KB)");
        } catch (Exception ex) {
            DebugLogger.Log($"Failed to cache {virtualPath}: {ex.Message}");
        }
    }

    public void UnloadMedia(string id) {
        lock (_lock) {
            if (_handles.Remove(id, out var handle)) {
                if (_processContexts.TryGetValue(handle.Owner, out var context)) {
                    context.RemoveHandle(handle);
                }
                handle.Dispose();
            }
        }
    }

    public void Play(string id) {
        if (_handles.TryGetValue(id, out var handle)) {
            if (handle.Status == MediaStatus.Playing) return;
            
            var context = GetOrCreateProcessContext(handle.Owner);
            context.PlayHandle(handle);
        }
    }

    public void Pause(string id) {
        if (_handles.TryGetValue(id, out var handle)) {
            handle.Pause();
        }
    }

    public void Stop(string id) {
        if (_handles.TryGetValue(id, out var handle)) {
            handle.Stop();
        }
    }

    public void Seek(string id, double seconds) {
        if (_handles.TryGetValue(id, out var handle)) {
            handle.Seek(seconds);
        }
    }

    public double GetPosition(string id) {
        return _handles.TryGetValue(id, out var handle) ? handle.Position : 0;
    }

    public double GetDuration(string id) {
        return _handles.TryGetValue(id, out var handle) ? handle.Duration : 0;
    }

    public MediaStatus GetStatus(string id) {
        return _handles.TryGetValue(id, out var handle) ? handle.Status : MediaStatus.Stopped;
    }

    public bool IsLoaded(string id) {
        return _handles.ContainsKey(id);
    }

    public float GetVolume(string id) {
        return _handles.TryGetValue(id, out var handle) ? handle.Volume : 0;
    }

    public float GetProcessVolume(Process process) {
        if (process == null) return 0;
        lock (_lock) {
            return _processContexts.TryGetValue(process, out var context) ? context.Volume : 1.0f;
        }
    }

    public float GetProcessLevel(Process process) {
        if (process == null) return 0;
        lock (_lock) {
            return _processContexts.TryGetValue(process, out var context) ? context.Level : 0f;
        }
    }

    public float GetProcessPeak(Process process) {
        if (process == null) return 0;
        lock (_lock) {
            return _processContexts.TryGetValue(process, out var context) ? context.PeakHold : 0f;
        }
    }

    public float GetMasterLevel() {
        return Math.Max(_lowLatencyPeak?.Level ?? 0, _highLatencyPeak?.Level ?? 0);
    }

    public float GetMasterPeak() {
        return Math.Max(_lowLatencyPeak?.PeakHold ?? 0, _highLatencyPeak?.PeakHold ?? 0);
    }

    public float GetSystemLevel() {
        if (_systemProcess == null) return 0;
        return GetProcessLevel(_systemProcess);
    }

    public float GetSystemPeak() {
        if (_systemProcess == null) return 0;
        return GetProcessPeak(_systemProcess);
    }

    public void SetVolume(string id, float volume) {
        if (_handles.TryGetValue(id, out var handle)) {
            handle.Volume = volume;
        }
    }

    public void SetProcessVolume(Process process, float volume) {
        lock (_lock) {
            if (_processContexts.TryGetValue(process, out var context)) {
                context.Volume = volume;
                context.UpdateEffectiveVolume(_masterVolume);

                // Save to registry
                if (process.AppId == "SYSTEM") {
                    Registry.SetValue($"{Shell.Registry.Audio}\\SystemVolume", volume);
                } else {
                    Registry.SetValue($"{Shell.Registry.AppSettings(process.AppId)}\\Volume", volume);
                }
            }
        }
    }

    public void RegisterFinishedCallback(string id, Action callback) {
        if (_handles.TryGetValue(id, out var handle)) {
            handle.OnFinishedCallback = callback; 
        }
    }

    private ProcessContext GetOrCreateProcessContext(Process process) {
        if (process == null) return null;
        lock (_lock) {
            if (!_processContexts.TryGetValue(process, out var context)) {
                context = new ProcessContext(process);

                // Load from registry
                if (process.AppId == "SYSTEM") {
                    context.Volume = Registry.GetValue($"{Shell.Registry.Audio}\\SystemVolume", 1.0f);
                } else {
                    context.Volume = Registry.GetValue($"{Shell.Registry.AppSettings(process.AppId)}\\Volume", 1.0f);
                }

                _processContexts[process] = context;
                context.UpdateEffectiveVolume(_masterVolume);
            }
            return context;
        }
    }

    public void Update(float elapsedSeconds) {
        var toUnload = new List<string>();
        lock (_lock) {
            _lowLatencyPeak?.Update(elapsedSeconds);
            _highLatencyPeak?.Update(elapsedSeconds);

            foreach (var context in _processContexts.Values) {
                context.Update(elapsedSeconds);
            }

            foreach (var handle in _handles.Values.ToList()) {
                handle.Update(elapsedSeconds);

                if (handle.Status == MediaStatus.Playing && handle.Position >= handle.Duration - 0.01) {
                    handle.OnFinished();
                    if (handle.AutoUnload) {
                        toUnload.Add(handle.Id);
                    }
                } else if (handle.Status == MediaStatus.Stopping) {
                    if (!handle.IsFading) {
                        handle.OnFinished(); // Fully stop after fade
                        if (handle.AutoUnload) {
                            toUnload.Add(handle.Id);
                        }
                    }
                } else if (handle.Status == MediaStatus.Pausing) {
                    if (!handle.IsFading) {
                        handle.InternalPause(); // Fully pause after fade
                    }
                }
            }
        }
        foreach (var id in toUnload) UnloadMedia(id);
    }

    public void CleanupProcess(Process process) {
        lock (_lock) {
            if (_processContexts.Remove(process, out var context)) {
                _lowLatencyMixer.RemoveMixerInput(context.PeakProvider);
                _highLatencyMixer.RemoveMixerInput(context.PeakProvider);
                context.Dispose();
                
                var processHandles = _handles.Where(kvp => kvp.Value.Owner == process).Select(kvp => kvp.Key).ToList();
                foreach (var id in processHandles) {
                    _handles.Remove(id);
                }

                OnProcessUnregistered?.Invoke();
            }
        }
    }

    public void Shutdown() {
        _lowLatencyOutput?.Stop();
        _highLatencyOutput?.Stop();
        
        lock (_lock) {
            foreach (var handle in _handles.Values) handle.Dispose();
            _handles.Clear();
            foreach (var context in _processContexts.Values) context.Dispose();
            _processContexts.Clear();
        }

        _lowLatencyOutput?.Dispose();
        _highLatencyOutput?.Dispose();
    }

    private class ProcessContext : IDisposable {
        public Process Owner { get; }
        public MixingSampleProvider Mixer { get; }
        public PeakSampleProvider PeakProvider => _peakProvider;
        private PeakSampleProvider _peakProvider;
        private VolumeSampleProvider _masterVolumeProvider;
        public float Level => _peakProvider?.Level ?? 0f;
        public float PeakHold => _peakProvider?.PeakHold ?? 0f;

        public float Volume { get; set; } = 1.0f;
        public bool IsRegistered { get; set; }
        public int HandleCount => _handles.Count;

        private readonly List<MediaHandle> _handles = new();

        public ProcessContext(Process owner) {
            Owner = owner;
            Mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2));
            Mixer.ReadFully = true;
            _peakProvider = new PeakSampleProvider(Mixer);
            _masterVolumeProvider = new VolumeSampleProvider(_peakProvider);
            _masterVolumeProvider.Volume = AudioManager.Instance.MasterVolume;

            if (owner.AppId == "SYSTEM") {
                AudioManager.Instance._lowLatencyMixer.AddMixerInput(_masterVolumeProvider);
            } else {
                AudioManager.Instance._highLatencyMixer.AddMixerInput(_masterVolumeProvider);
            }
        }

        public void AddHandle(MediaHandle handle) {
            _handles.Add(handle);
            handle.UpdateProcessVolume(Volume);
        }
        public void RemoveHandle(MediaHandle handle) {
            _handles.Remove(handle);
            Mixer.RemoveMixerInput(handle.PeakProvider);
        }

        public void PlayHandle(MediaHandle handle) {
            if (!Mixer.MixerInputs.Contains(handle.PeakProvider)) {
                Mixer.AddMixerInput(handle.PeakProvider);
            }
            handle.Play();
        }

        public void Update(float elapsedSeconds) {
            _peakProvider?.Update(elapsedSeconds);
        }

        public void UpdateEffectiveVolume(float masterVolume) {
            _masterVolumeProvider.Volume = masterVolume;
            foreach (var handle in _handles) {
                handle.UpdateProcessVolume(Volume);
            }
        }

        public void Dispose() {
            foreach (var handle in _handles) {
                Mixer.RemoveMixerInput(handle.PeakProvider);
                handle.Dispose();
            }
            _handles.Clear();
        }
    }

    private class MediaHandle : IDisposable {
        public string Id { get; } = Guid.NewGuid().ToString();
        public Process Owner { get; }
        public string VirtualPath { get; }
        public MediaStatus Status { get; private set; } = MediaStatus.Stopped;
        
        public bool AutoUnload { get; }
        public Action OnFinishedCallback { get; set; }
        
        private ISampleProvider _source;
        private AudioFileReader _reader; 
        private CachedSampleProvider _cachedSource; 

        private SmoothedVolumeSampleProvider _volumeProvider;
        private FadeInOutSampleProvider _fadeProvider;
        private PausableSampleProvider _pausableProvider;
        private WdlResamplingSampleProvider _resampler;
        private LockingSampleProvider _finalProvider;
        private PeakSampleProvider _peakProvider;
        
        private bool _useFading;
        
        public ISampleProvider FinalProvider => _finalProvider;
        public PeakSampleProvider PeakProvider => _peakProvider;
        public bool IsFading => _fadeProvider?.IsFading ?? false;

        private float _userVolume = 1.0f;
        private float _processEffectiveVolume = 1.0f;

        public double Duration { get; private set; }
        public double Position {
            get {
                if (_reader != null) return _reader.CurrentTime.TotalSeconds;
                if (_cachedSource != null) return _cachedSource.Position.TotalSeconds;
                return 0;
            }
        }

        public float Volume {
            get => _userVolume;
            set {
                _finalProvider.Lock(() => {
                    _userVolume = Math.Clamp(value, 0f, 1f);
                    UpdateFinalVolume();
                });
            }
        }

        public MediaHandle(Process owner, string virtualPath, string hostPath, bool useFading, bool autoUnload) {
            Owner = owner;
            VirtualPath = virtualPath;
            AutoUnload = autoUnload;
            _useFading = useFading;
            _reader = new AudioFileReader(hostPath);
            _source = _reader; 
            Duration = _reader.TotalTime.TotalSeconds;
            InitializeChain(useFading);
        }

        public MediaHandle(Process owner, string virtualPath, CachedAudio cached, bool useFading, bool autoUnload) {
            Owner = owner;
            VirtualPath = virtualPath;
            AutoUnload = autoUnload;
            _useFading = useFading;
            _cachedSource = new CachedSampleProvider(cached);
            _source = _cachedSource;
            Duration = _cachedSource.DurationCorrect.TotalSeconds;
            InitializeChain(useFading);
        }

        private void InitializeChain(bool useFading) {
            ISampleProvider current = _source;

            if (current.WaveFormat.Channels == 1) {
                current = new MonoToStereoSampleProvider(current);
            }

            _volumeProvider = new SmoothedVolumeSampleProvider(current);
            current = _volumeProvider;
            
            _fadeProvider = new FadeInOutSampleProvider(current);
            current = _fadeProvider;

            _pausableProvider = new PausableSampleProvider(current);
            current = _pausableProvider;

            if (current.WaveFormat.SampleRate != 44100 || current.WaveFormat.Channels != 2) {
                _resampler = new WdlResamplingSampleProvider(current, 44100);
                current = _resampler;
            }

            _finalProvider = new LockingSampleProvider(current);
            _peakProvider = new PeakSampleProvider(_finalProvider);
        }

        public void Update(float elapsedSeconds) {
            _peakProvider?.Update(elapsedSeconds);
        }

        public void Play() {
            _finalProvider.Lock(() => {
                if (Status == MediaStatus.Playing) return;
                
                _pausableProvider.Paused = false;
                if (_useFading) _fadeProvider?.BeginFadeIn(200);
                else _fadeProvider?.Reset(1.0f);
                Status = MediaStatus.Playing;
                UpdateFinalVolume();
            });
        }

        public void Pause() {
            _finalProvider.Lock(() => {
                if (Status != MediaStatus.Playing && Status != MediaStatus.Pausing) return;

                if (_useFading && Status == MediaStatus.Playing) {
                    _fadeProvider?.BeginFadeOut(200);
                    Status = MediaStatus.Pausing;
                    UpdateFinalVolume();
                } else {
                    InternalPause();
                }
            });
        }

        public void InternalPause() {
            _finalProvider.Lock(() => {
                Status = MediaStatus.Paused;
                _pausableProvider.Paused = true;
                _fadeProvider?.Reset(0.0f);
                UpdateFinalVolume();
            });
        }

        public void Stop() {
            _finalProvider.Lock(() => {
                if (_useFading && (Status == MediaStatus.Playing || Status == MediaStatus.Stopping || Status == MediaStatus.Pausing)) {
                    _fadeProvider?.BeginFadeOut(200);
                    Status = MediaStatus.Stopping;
                    UpdateFinalVolume();
                } else {
                    OnFinished();
                }
            });
        }

        public void Seek(double seconds) {
            _finalProvider.Lock(() => {
                if (_reader != null) {
                    _reader.CurrentTime = TimeSpan.FromSeconds(Math.Clamp(seconds, 0, Duration));
                } else if (_cachedSource != null) {
                    _cachedSource.Position = TimeSpan.FromSeconds(Math.Clamp(seconds, 0, Duration));
                }
            });
        }

        public void UpdateProcessVolume(float effectiveVolume) {
            _finalProvider.Lock(() => {
                _processEffectiveVolume = effectiveVolume;
                UpdateFinalVolume();
            });
        }

        private void UpdateFinalVolume() {
            if (Status == MediaStatus.Playing || Status == MediaStatus.Stopping || Status == MediaStatus.Pausing) {
                _volumeProvider.Volume = _userVolume * _processEffectiveVolume;
            } else {
                _volumeProvider.Volume = 0;
            }
        }

        public void OnFinished() {
            _finalProvider.Lock(() => {
                if (Status == MediaStatus.Stopped) return;
                
                Status = MediaStatus.Stopped;
                _pausableProvider.Paused = true;
                _volumeProvider.Volume = 0;
                _fadeProvider?.Reset(0f);
                if (_reader != null) _reader.CurrentTime = TimeSpan.Zero;
                else if (_cachedSource != null) _cachedSource.Position = TimeSpan.Zero;
                
                var callback = OnFinishedCallback;
                OnFinishedCallback = null; 
                callback?.Invoke();
            });
        }

        public void Dispose() {
            _finalProvider.Lock(() => {
                _reader?.Dispose();
                _reader = null;
            });
        }
    }

    private class LockingSampleProvider : ISampleProvider {
        private readonly ISampleProvider _source;
        private readonly object _lock = new();

        public WaveFormat WaveFormat => _source.WaveFormat;

        public LockingSampleProvider(ISampleProvider source) {
            _source = source;
        }

        public void Lock(Action action) {
            lock (_lock) action();
        }

        public int Read(float[] buffer, int offset, int count) {
            lock (_lock) return _source.Read(buffer, offset, count);
        }
    }

    private class CachedAudio {
        public float[] AudioData { get; }
        public WaveFormat WaveFormat { get; }
        public CachedAudio(float[] data, WaveFormat format) {
            AudioData = data;
            WaveFormat = format;
        }
    }

    private class CachedSampleProvider : ISampleProvider {
        private readonly CachedAudio _cachedAudio;
        private long _position;

        public WaveFormat WaveFormat => _cachedAudio.WaveFormat;
        public TimeSpan DurationCorrect => TimeSpan.FromSeconds((double)_cachedAudio.AudioData.Length / WaveFormat.Channels / WaveFormat.SampleRate);
        
        public TimeSpan Position {
            get => TimeSpan.FromSeconds((double)_position / WaveFormat.Channels / WaveFormat.SampleRate);
            set => _position = (long)(value.TotalSeconds * WaveFormat.SampleRate * WaveFormat.Channels);
        }

        public CachedSampleProvider(CachedAudio cachedAudio) {
            _cachedAudio = cachedAudio;
        }

        public int Read(float[] buffer, int offset, int count) {
            int available = _cachedAudio.AudioData.Length - (int)_position;
            int samplesToCopy = Math.Min(available, count);
            if (samplesToCopy <= 0) return 0;
            Array.Copy(_cachedAudio.AudioData, (int)_position, buffer, offset, samplesToCopy);
            _position += samplesToCopy;
            return samplesToCopy;
        }
    }

    private class PausableSampleProvider : ISampleProvider {
        private readonly ISampleProvider _source;
        public bool Paused { get; set; } = true;

        public WaveFormat WaveFormat => _source.WaveFormat;

        public PausableSampleProvider(ISampleProvider source) {
            _source = source;
        }

        public int Read(float[] buffer, int offset, int count) {
            if (Paused) {
                Array.Clear(buffer, offset, count);
                return count;
            }
            return _source.Read(buffer, offset, count);
        }
    }

    private class SmoothedVolumeSampleProvider : ISampleProvider {
        private readonly ISampleProvider _source;
        private float _targetVolume = 0.0f;
        private float _currentVolume = -1.0f; 

        public WaveFormat WaveFormat => _source.WaveFormat;

        public float Volume {
            get => _targetVolume;
            set => _targetVolume = value;
        }

        public SmoothedVolumeSampleProvider(ISampleProvider source) {
            _source = source;
        }

        public int Read(float[] buffer, int offset, int count) {
            int read = _source.Read(buffer, offset, count);
            if (read == 0) return 0;

            if (_currentVolume < 0) {
                _currentVolume = _targetVolume;
            }

            if (Math.Abs(_currentVolume - _targetVolume) < 0.0001f) {
                _currentVolume = _targetVolume;
                if (_currentVolume == 1.0f) return read;
                for (int i = 0; i < read; i++) {
                    buffer[offset + i] *= _currentVolume;
                }
            } else {
                // Smoothing over approx 50ms (at 44.1kHz stereo, that's ~4410 samples)
                // We'll use a per-sample increment for perfectly smooth ramping
                float step = (_targetVolume - _currentVolume) / (WaveFormat.SampleRate * 0.05f);
                int channels = WaveFormat.Channels;

                for (int i = 0; i < read; i += channels) {
                    for (int ch = 0; ch < channels; ch++) {
                        buffer[offset + i + ch] *= _currentVolume;
                    }
                    _currentVolume += step;
                    
                    // Clamp if we overshot
                    if ((step > 0 && _currentVolume > _targetVolume) || (step < 0 && _currentVolume < _targetVolume)) {
                        _currentVolume = _targetVolume;
                        // Fast path for rest of buffer if we reached target
                        for (int j = i + channels; j < read; j++) {
                            buffer[offset + j] *= _currentVolume;
                        }
                        break;
                    }
                }
            }

            return read;
        }
    }

    private class FadeInOutSampleProvider : ISampleProvider {
        private readonly ISampleProvider _source;
        private float _multiplier = 0.0f;
        private float _targetMultiplier = 0.0f;
        private float _increment = 0.0f;

        public WaveFormat WaveFormat => _source.WaveFormat;

        public FadeInOutSampleProvider(ISampleProvider source) {
            _source = source;
        }

        public void Reset(float volume) {
            _multiplier = volume;
            _targetMultiplier = volume;
            _increment = 0;
        }

        public void BeginFadeIn(int milliseconds) {
            int samples = (WaveFormat.SampleRate * milliseconds) / 1000;
            _targetMultiplier = 1.0f;
            _increment = (1.0f - _multiplier) / (samples > 0 ? samples : 1);
        }

        public void BeginFadeOut(int milliseconds) {
            int samples = (WaveFormat.SampleRate * milliseconds) / 1000;
            _targetMultiplier = 0.0f;
            _increment = (0.0f - _multiplier) / (samples > 0 ? samples : 1);
        }

        public bool IsFading => _increment != 0;

        public int Read(float[] buffer, int offset, int count) {
            int read = _source.Read(buffer, offset, count);
            if (read == 0) return 0;

            if (_increment == 0) {
                if (_multiplier == 1.0f) return read;
                if (_multiplier == 0.0f) {
                    Array.Clear(buffer, offset, read);
                    return read;
                }
                for (int i = 0; i < read; i++) buffer[offset + i] *= _multiplier;
                return read;
            }

            int channels = WaveFormat.Channels;
            for (int i = 0; i < read; i += channels) {
                for (int ch = 0; ch < channels; ch++) {
                    buffer[offset + i + ch] *= _multiplier;
                }
                _multiplier += _increment;

                if ((_increment > 0 && _multiplier >= _targetMultiplier) || 
                    (_increment < 0 && _multiplier <= _targetMultiplier)) {
                    _multiplier = _targetMultiplier;
                    _increment = 0;
                    // Fast path for rest of buffer
                    for (int j = i + channels; j < read; j++) buffer[offset + j] *= _multiplier;
                    break;
                }
            }

            return read;
        }
    }

    private class PeakSampleProvider : ISampleProvider {
        private readonly ISampleProvider _source;
        private float _currentPeak = 0;
        private float _holdPeak = 0;
        private float _holdTime = 0;
        private readonly object _lock = new object();

        public WaveFormat WaveFormat => _source.WaveFormat;
        public float Level => _currentPeak;
        public float PeakHold => _holdPeak;

        public PeakSampleProvider(ISampleProvider source) {
            _source = source;
        }

        public int Read(float[] buffer, int offset, int count) {
            int read = _source.Read(buffer, offset, count);
            if (read <= 0) return read;

            float max = 0;
            for (int i = 0; i < read; i++) {
                float abs = Math.Abs(buffer[offset + i]);
                if (abs > max) max = abs;
            }

            lock (_lock) {
                if (max > _currentPeak) {
                    _currentPeak = max;
                }

                if (max >= _holdPeak) {
                    _holdPeak = max;
                    _holdTime = 1.0f; // 1 second hold
                }
            }

            return read;
        }

        public void Update(float elapsedSeconds) {
            lock (_lock) {
                // Decay current level
                // Use exponential decay for smoother, more natural fall (approx -12dB/s)
                float decay = (float)Math.Pow(0.1, elapsedSeconds * 1.5f);
                _currentPeak *= decay;
                
                // Also ensures it hits zero eventually
                _currentPeak -= elapsedSeconds * 0.1f;
                if (_currentPeak < 0) _currentPeak = 0;

                // Update Hold
                if (_holdTime > 0) {
                    _holdTime -= elapsedSeconds;
                } else {
                    _holdPeak *= (float)Math.Pow(0.1, elapsedSeconds * 0.8f);
                    _holdPeak -= elapsedSeconds * 0.05f;
                    if (_holdPeak < 0) _holdPeak = 0;
                }
            }
        }
    }
}
