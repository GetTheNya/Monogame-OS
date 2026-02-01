using Microsoft.Xna.Framework;

namespace TheGame.Core.OS;

public static partial class Shell {
    public static class Taskbar {
        /// <summary>
        /// Sets the progress value for a process's taskbar button.
        /// value: -1.0 to 1.0. -1.0 hides the progress bar.
        /// </summary>
        public static void SetProgress(OS.Process process, float value, Color? color = null) {
            if (process == null) return;
            process.Progress = value;
            if (color.HasValue) process.ProgressColor = color.Value;
        }
    }
}
