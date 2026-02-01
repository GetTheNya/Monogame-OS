namespace TheGame.Core.OS;

public static partial class Shell {
    public static class Audio {
        /// <summary>
        /// Plays a sound file using the SYSTEM process context.
        /// Automatically unloads the media when finished.
        /// </summary>
        public static void PlaySound(string virtualPath, float volume = 1.0f) {
            Media.PlayOneShot(virtualPath, volume);
        }
    }
}
