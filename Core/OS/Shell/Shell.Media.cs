using System;

namespace TheGame.Core.OS;

public static partial class Shell {
    /// <summary>
    /// Advanced Media API for apps to play and control audio/video files.
    /// </summary>
    public static class Media {
        /// <summary>
        /// Registers the calling process as a media player. Required before calling LoadMedia.
        /// </summary>
        public static void RegisterAsPlayer(TheGame.Core.OS.Process process) {
            AudioManager.Instance.RegisterAsPlayer(process);
        }

        /// <summary>
        /// Loads a media file for the calling process.
        /// </summary>
        public static string LoadMedia(TheGame.Core.OS.Process process, string virtualPath, bool useFading = true, bool autoUnload = false) {
            return AudioManager.Instance.LoadMedia(process, virtualPath, useFading, autoUnload);
        }

        /// <summary>
        /// Preloads a media file into memory for instant playback.
        /// </summary>
        public static void Preload(string virtualPath) => AudioManager.Instance.Preload(virtualPath);

        /// <summary>
        /// Checks if a media handle is currently loaded.
        /// </summary>
        public static bool IsLoaded(string id) => AudioManager.Instance.IsLoaded(id);

        /// <summary>
        /// Plays a media file as a one-shot (auto-unloads when finished).
        /// </summary>
        public static void PlayOneShot(string virtualPath, float volume = 1.0f) {
            string id = LoadMedia(null, virtualPath, false, true);
            if (id != null) {
                SetVolume(id, volume);
                Play(id);
            }
        }

        /// <summary>
        /// Unloads a media file and frees resources.
        /// </summary>
        public static void UnloadMedia(string id) => AudioManager.Instance.UnloadMedia(id);

        /// <summary>
        /// Starts playback of the specified media.
        /// </summary>
        public static void Play(string id) => AudioManager.Instance.Play(id);

        /// <summary>
        /// Pauses playback.
        /// </summary>
        public static void Pause(string id) => AudioManager.Instance.Pause(id);

        /// <summary>
        /// Stops playback and rewinds to the beginning.
        /// </summary>
        public static void Stop(string id) => AudioManager.Instance.Stop(id);

        /// <summary>
        /// Seeks to a specific position in seconds.
        /// </summary>
        public static void Seek(string id, double seconds) => AudioManager.Instance.Seek(id, seconds);

        /// <summary>
        /// Gets the total duration of the media in seconds.
        /// </summary>
        public static double GetDuration(string id) => AudioManager.Instance.GetDuration(id);

        /// <summary>
        /// Gets the current playback position in seconds.
        /// </summary>
        public static double GetPosition(string id) => AudioManager.Instance.GetPosition(id);

        /// <summary>
        /// Gets the current status (Playing, Paused, Stopped).
        /// </summary>
        public static MediaStatus GetStatus(string id) => AudioManager.Instance.GetStatus(id);


        /// <summary>
        /// Registers a callback to be called when playback of the specified media finishes.
        /// </summary>
        public static void RegisterPlaybackFinished(string id, Action callback) {
            AudioManager.Instance.RegisterFinishedCallback(id, callback);
        }

        /// <summary>
        /// Gets the current master volume (0.0 to 1.0).
        /// </summary>
        public static float GetMasterVolume() => AudioManager.Instance.MasterVolume;

        /// <summary>
        /// Sets the global master volume (0.0 to 1.0).
        /// </summary>
        public static void SetMasterVolume(float volume) => AudioManager.Instance.MasterVolume = volume;

        /// <summary>
        /// Gets the volume for the current process (0.0 to 1.0).
        /// </summary>
        public static float GetProcessVolume(TheGame.Core.OS.Process process) {
            if (process == null) return 0f;
            return AudioManager.Instance.GetProcessVolume(process);
        }

        /// <summary>
        /// Sets the volume for the current process (0.0 to 1.0).
        /// </summary>
        public static void SetProcessVolume(TheGame.Core.OS.Process process, float volume) {
            if (process != null) AudioManager.Instance.SetProcessVolume(process, volume);
        }

        /// <summary>
        /// Gets the current volume of a media handle (0.0 to 1.0).
        /// </summary>
        public static float GetVolume(string id) => AudioManager.Instance.GetVolume(id);

        /// <summary>
        /// Sets the volume of a media handle (0.0 to 1.0).
        /// </summary>
        public static void SetVolume(string id, float volume) => AudioManager.Instance.SetVolume(id, volume);

        /// <summary> Gets current master audio level (0.0 to 1.0). </summary>
        public static float GetMasterLevel() => AudioManager.Instance.GetMasterLevel();

        /// <summary> Gets master peak hold level. </summary>
        public static float GetMasterPeak() => AudioManager.Instance.GetMasterPeak();

        /// <summary> Gets current system sounds level. </summary>
        public static float GetSystemLevel() => AudioManager.Instance.GetSystemLevel();

        /// <summary> Gets system sounds peak hold level. </summary>
        public static float GetSystemPeak() => AudioManager.Instance.GetSystemPeak();

        /// <summary> Gets current audio level for a process. </summary>
        public static float GetProcessLevel(TheGame.Core.OS.Process process) => AudioManager.Instance.GetProcessLevel(process);

        /// <summary> Gets peak hold level for a process. </summary>
        public static float GetProcessPeak(TheGame.Core.OS.Process process) => AudioManager.Instance.GetProcessPeak(process);
    }
}
