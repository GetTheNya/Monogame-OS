using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework.Audio;
using TheGame.Core.OS;

namespace TheGame.Core.OS;

public class AudioManager {
    private static AudioManager _instance;
    public static AudioManager Instance => _instance ??= new AudioManager();

    private Dictionary<string, SoundEffect> _soundCache = new();
    private List<SoundEffectInstance> _activeInstances = new();

    private AudioManager() { }

    public void PlaySound(string virtualPath, float volume = 1.0f, float pitch = 0f, float pan = 0f) {
        try {
            var effect = GetSoundEffect(virtualPath);
            if (effect != null) {
                var instance = effect.CreateInstance();
                instance.Volume = volume;
                instance.Pitch = pitch;
                instance.Pan = pan;
                instance.Play();
                _activeInstances.Add(instance);
            }
        } catch (Exception ex) {
            DebugLogger.Log($"Error playing sound {virtualPath}: {ex.Message}");
        }
    }

    private SoundEffect GetSoundEffect(string virtualPath) {
        if (_soundCache.TryGetValue(virtualPath, out var cached)) return cached;

        string hostPath = VirtualFileSystem.Instance.ToHostPath(virtualPath);
        if (File.Exists(hostPath)) {
            try {
                using (var stream = File.OpenRead(hostPath)) {
                    var effect = SoundEffect.FromStream(stream);
                    _soundCache[virtualPath] = effect;
                    return effect;
                }
            } catch (Exception ex) {
                DebugLogger.Log($"Failed to load sound from {hostPath}: {ex.Message}");
            }
        }

        return null;
    }

    public void Update() {
        // Clean up finished instances
        for (int i = _activeInstances.Count - 1; i >= 0; i--) {
            if (_activeInstances[i].State == SoundState.Stopped) {
                _activeInstances[i].Dispose();
                _activeInstances.RemoveAt(i);
            }
        }
    }

    public void Shutdown() {
        foreach (var instance in _activeInstances) instance.Dispose();
        _activeInstances.Clear();
        foreach (var effect in _soundCache.Values) effect.Dispose();
        _soundCache.Clear();
    }
}
