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
    Paused,
    Stopped
}

public class AudioManager {
    private static AudioManager _instance;
    public static AudioManager Instance => _instance ??= new AudioManager();

    private WaveOutEvent _lowLatencyOutput;
    private WaveOutEvent _highLatencyOutput;
    private MixingSampleProvider _lowLatencyMixer;
    private MixingSampleProvider _highLatencyMixer;
    private float _masterVolume = 1.0f;

    private readonly Dictionary<string, MediaHandle> _handles = new();
    private readonly Dictionary<Process, ProcessContext> _processContexts = new();
    private readonly Dictionary<string, CachedAudio> _audioCache = new();
    private readonly object _lock = new();
    
    private Process _systemProcess;

    private AudioManager() {
        InitializeEngine();
    }

    private void InitializeEngine() {
        try {
            // Low latency (60ms) for one-shots and UI
            _lowLatencyOutput = new WaveOutEvent { DesiredLatency = 60 };
            _lowLatencyMixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2));
            _lowLatencyMixer.ReadFully = true;
            _lowLatencyOutput.Init(_lowLatencyMixer);
            _lowLatencyOutput.Play();

            // High latency (150ms) for music and app media
            // 300ms was too high and caused UI stuttering during lock acquisition
            _highLatencyOutput = new WaveOutEvent { DesiredLatency = 150 };
            _highLatencyMixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2));
            _highLatencyMixer.ReadFully = true;
            _highLatencyOutput.Init(_highLatencyMixer);
            _highLatencyOutput.Play();
            
            // Create a dummy system process for global sounds
            _systemProcess = new Process { AppId = "SYSTEM" };
        } catch (Exception ex) {
            DebugLogger.Log($"Failed to initialize NAudio engine: {ex.Message}");
        }
    }

    public float MasterVolume {
        get => _masterVolume;
        set {
            _masterVolume = Math.Clamp(value, 0f, 1f);
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
            context.IsRegistered = true;
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
            if (handle.Status != MediaStatus.Playing) return;
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
            }
        }
    }

    public void RegisterFinishedCallback(string id, Action callback) {
        if (_handles.TryGetValue(id, out var handle)) {
            handle.OnFinishedCallback += callback;
        }
    }

    private ProcessContext GetOrCreateProcessContext(Process process) {
        lock (_lock) {
            if (!_processContexts.TryGetValue(process, out var context)) {
                context = new ProcessContext(process);
                _processContexts[process] = context;
                context.UpdateEffectiveVolume(_masterVolume);
            }
            return context;
        }
    }

    public void Update() {
        var toUnload = new List<string>();
        foreach (var handle in _handles.Values) {
            if (handle.Status == MediaStatus.Playing && handle.Position >= handle.Duration - 0.01) {
                handle.OnFinished();
                if (handle.AutoUnload) {
                    toUnload.Add(handle.Id);
                }
            }
        }
        foreach (var id in toUnload) UnloadMedia(id);
    }

    public void CleanupProcess(Process process) {
        lock (_lock) {
            if (_processContexts.Remove(process, out var context)) {
                _lowLatencyMixer.RemoveMixerInput(context.Mixer);
                _highLatencyMixer.RemoveMixerInput(context.Mixer);
                context.Dispose();
                
                var processHandles = _handles.Where(kvp => kvp.Value.Owner == process).Select(kvp => kvp.Key).ToList();
                foreach (var id in processHandles) {
                    _handles.Remove(id);
                }
            }
        }
    }

    public void Shutdown() {
        _lowLatencyOutput?.Stop();
        _highLatencyOutput?.Stop();
        foreach (var handle in _handles.Values) handle.Dispose();
        _handles.Clear();
        foreach (var context in _processContexts.Values) context.Dispose();
        _processContexts.Clear();
        _lowLatencyOutput?.Dispose();
        _highLatencyOutput?.Dispose();
    }

    private class ProcessContext : IDisposable {
        public Process Owner { get; }
        public MixingSampleProvider Mixer { get; }
        public float Volume { get; set; } = 1.0f;
        public bool IsRegistered { get; set; }
        public int HandleCount => _handles.Count;

        private readonly List<MediaHandle> _handles = new();

        public ProcessContext(Process owner) {
            Owner = owner;
            Mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2));
            Mixer.ReadFully = true;

            if (owner.AppId == "SYSTEM") {
                AudioManager.Instance._lowLatencyMixer.AddMixerInput(Mixer);
            } else {
                AudioManager.Instance._highLatencyMixer.AddMixerInput(Mixer);
            }
        }

        public void AddHandle(MediaHandle handle) => _handles.Add(handle);
        public void RemoveHandle(MediaHandle handle) {
            _handles.Remove(handle);
            Mixer.RemoveMixerInput(handle.FinalProvider);
        }

        public void PlayHandle(MediaHandle handle) {
            if (!Mixer.MixerInputs.Contains(handle.FinalProvider)) {
                Mixer.AddMixerInput(handle.FinalProvider);
            }
            handle.Play();
        }

        public void UpdateEffectiveVolume(float masterVolume) {
            foreach (var handle in _handles) {
                handle.UpdateProcessVolume(Volume * masterVolume);
            }
        }

        public void Dispose() {
            foreach (var handle in _handles) handle.Dispose();
            _handles.Clear();
        }
    }

    private class MediaHandle : IDisposable {
        public string Id { get; } = Guid.NewGuid().ToString();
        public Process Owner { get; }
        public string VirtualPath { get; }
        public MediaStatus Status { get; private set; } = MediaStatus.Stopped;
        
        public bool AutoUnload { get; }
        public event Action OnFinishedCallback;
        
        private ISampleProvider _source;
        private AudioFileReader _reader; 
        private CachedSampleProvider _cachedSource; 

        private SmoothedVolumeSampleProvider _volumeProvider;
        private FadeInOutSampleProvider _fadeProvider;
        private PausableSampleProvider _pausableProvider;
        private WdlResamplingSampleProvider _resampler;
        private LockingSampleProvider _finalProvider;
        
        public ISampleProvider FinalProvider => _finalProvider;

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
            _reader = new AudioFileReader(hostPath);
            _source = _reader; 
            Duration = _reader.TotalTime.TotalSeconds;
            InitializeChain(useFading);
        }

        public MediaHandle(Process owner, string virtualPath, CachedAudio cached, bool useFading, bool autoUnload) {
            Owner = owner;
            VirtualPath = virtualPath;
            AutoUnload = autoUnload;
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
        }

        public void Play() {
            if (Status == MediaStatus.Playing) return;
            _finalProvider.Lock(() => {
                _pausableProvider.Paused = false;
                _fadeProvider?.BeginFadeIn(100);
                Status = MediaStatus.Playing;
                UpdateFinalVolume();
            });
        }

        public void Pause() {
            if (Status != MediaStatus.Playing) return;
            _finalProvider.Lock(() => {
                Status = MediaStatus.Paused;
                _pausableProvider.Paused = true;
                UpdateFinalVolume();
            });
        }

        public void Stop() {
            _finalProvider.Lock(() => {
                _pausableProvider.Paused = true;
                _fadeProvider?.BeginFadeOut(100);
                Status = MediaStatus.Stopped;
                UpdateFinalVolume();
                if (_reader != null) _reader.CurrentTime = TimeSpan.Zero;
                else if (_cachedSource != null) _cachedSource.Position = TimeSpan.Zero;
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
            float master = AudioManager.Instance.MasterVolume;
            if (Status == MediaStatus.Playing) {
                _volumeProvider.Volume = _userVolume * _processEffectiveVolume * master;
            } else {
                _volumeProvider.Volume = 0;
            }
        }

        public void OnFinished() {
            _finalProvider.Lock(() => {
                Status = MediaStatus.Stopped;
                _volumeProvider.Volume = 0;
                if (_reader != null) _reader.CurrentTime = TimeSpan.Zero;
                else if (_cachedSource != null) _cachedSource.Position = TimeSpan.Zero;
                OnFinishedCallback?.Invoke();
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
        private float _targetVolume = 1.0f;
        private float _currentVolume = 1.0f;
        private const float VolumeStep = 0.05f; // Duration of smoothing in seconds roughly

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
}
