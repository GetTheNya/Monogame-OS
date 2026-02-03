using System;
using System.Resources;

namespace TheGame.Core.OS;

public class MediaAPI : BaseAPI {
    public MediaAPI(Process process) : base(process) {
    }

    /// <summary>
    /// Registers the calling process as a media player. Required before calling LoadMedia.
    /// </summary>
    public void RegisterAsPlayer() {
        Shell.Media.RegisterAsPlayer(OwningProcess);
    }

    /// <summary>
    /// Unregisters the calling process as a media player.
    /// </summary>
    public void UnregisterAsPlayer() {
        Shell.Media.UnregisterAsPlayer(OwningProcess);
    }

    /// <summary>
    /// Loads a media file for the calling process.
    /// </summary>
    public string LoadMedia(string virtualPath, bool useFading = true, bool autoUnload = false) {
        return Shell.Media.LoadMedia(OwningProcess, virtualPath, useFading, autoUnload);
    }

    /// <summary>
    /// Preloads a media file into memory for instant playback.
    /// </summary>
    public void Preload(string virtualPath) => Shell.Media.Preload(virtualPath);

    /// <summary>
    /// Checks if a media handle is currently loaded.
    /// </summary>
    public bool IsLoaded(string id) => Shell.Media.IsLoaded(id);

    /// <summary>
    /// Plays a media file as a one-shot (auto-unloads when finished).
    /// </summary>
    public void PlayOneShot(string virtualPath, float volume = 1.0f) {
        Shell.Media.PlayOneShot(virtualPath, volume);
    }

    /// <summary>
    /// Unloads a media file and frees resources.
    /// </summary>
    public void UnloadMedia(string id) => Shell.Media.UnloadMedia(id);

    /// <summary>
    /// Starts playback of the specified media.
    /// </summary>
    public void Play(string id) => Shell.Media.Play(id);

    /// <summary>
    /// Pauses playback.
    /// </summary>
    public void Pause(string id) => Shell.Media.Pause(id);

    /// <summary>
    /// Stops playback and rewinds to the beginning.
    /// </summary>
    public void Stop(string id) => Shell.Media.Stop(id);

    /// <summary>
    /// Seeks to a specific position in seconds.
    /// </summary>
    public void Seek(string id, double seconds) => Shell.Media.Seek(id, seconds);

    /// <summary>
    /// Gets the total duration of the media in seconds.
    /// </summary>
    public double GetDuration(string id) => Shell.Media.GetDuration(id);

    /// <summary>
    /// Gets the current playback position in seconds.
    /// </summary>
    public double GetPosition(string id) => Shell.Media.GetPosition(id);

    /// <summary>
    /// Gets the current status (Playing, Paused, Stopped).
    /// </summary>
    public MediaStatus GetStatus(string id) => Shell.Media.GetStatus(id);


    /// <summary>
    /// Registers a callback to be called when playback of the specified media finishes.
    /// </summary>
    public void RegisterPlaybackFinished(string id, Action callback) {
        Shell.Media.RegisterPlaybackFinished(id, callback);
    }

    /// <summary>
    /// Gets the volume for the current application (0.0 to 1.0).
    /// </summary>
    public float GetApplicationVolume() {
        return Shell.Media.GetProcessVolume(OwningProcess);
    }

    /// <summary>
    /// Sets the volume for the current application (0.0 to 1.0).
    /// </summary>
    public void SetApplicationVolume(float volume) {
        Shell.Media.SetProcessVolume(OwningProcess, volume);
    }

    /// <summary>
    /// Gets the current volume of a media handle (0.0 to 1.0).
    /// </summary>
    public float GetMediaVolume(string id) => Shell.Media.GetVolume(id);

    /// <summary>
    /// Sets the volume of a media handle (0.0 to 1.0).
    /// </summary>
    public void SetMediaVolume(string id, float volume) => Shell.Media.SetVolume(id, volume);

    /// <summary>
    /// Gets the current audio level for the application (0.0 to 1.0).
    /// </summary>
    public float GetApplicationLevel() => Shell.Media.GetProcessLevel(OwningProcess);

    /// <summary>
    /// Gets the peak audio level for the application (0.0 to 1.0).
    /// </summary>
    public float GetApplicationPeak() => Shell.Media.GetProcessPeak(OwningProcess);
}
