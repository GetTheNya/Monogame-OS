using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using TheGame.Core.UI;

namespace TheGame.Core.OS;

public static partial class Shell {
    /// <summary>
    /// Process API for apps to interact with their own process.
    /// </summary>
    public static class Process {
        /// <summary>
        /// [DEPRECATED] Gets the calling app's process (detected from the active window).
        /// This is focus-dependent and fragile. Use explicit process context instead.
        /// </summary>
        [Obsolete("Use explicit Process context instead of relying on focus.")]
        public static TheGame.Core.OS.Process Current => Window.ActiveWindow?.OwnerProcess;
        
        /// <summary>
        /// Gets the ProcessManager for advanced process control.
        /// </summary>
        public static ProcessManager Manager => ProcessManager.Instance;
        
        /// <summary>
        /// Creates a new window of the specified type owned by the specified process.
        /// </summary>
        public static T CreateWindow<T>(TheGame.Core.OS.Process owner) where T : WindowBase, new() {
            if (owner == null) {
                DebugLogger.Log("Shell.Process.CreateWindow: No process context provided");
                return null;
            }
            return owner.CreateWindow<T>();
        }
        
        /// <summary>
        /// Shows a modal dialog that blocks input to the current window.
        /// </summary>
        public static void ShowModal(TheGame.Core.OS.Process owner, WindowBase dialog, WindowBase parent = null, Rectangle? startBounds = null) {
            if (owner == null) {
                UI.OpenWindow(dialog, startBounds);
                return;
            }
            // Use MainWindow as default parent if none provided
            parent ??= owner.MainWindow;
            owner.ShowModal(dialog, parent, startBounds);
        }
        
        /// <summary>
        /// Closes all windows and enters background mode.
        /// The process continues receiving OnUpdate calls.
        /// </summary>
        public static void GoToBackground(TheGame.Core.OS.Process owner) {
            owner?.GoToBackground();
        }
        
        /// <summary>
        /// Terminates the specified process.
        /// </summary>
        public static void Exit(TheGame.Core.OS.Process owner) {
            if (owner != null) {
                AudioManager.Instance.CleanupProcess(owner);
                owner.Terminate();
            }
        }
        
        /// <summary>
        /// Gets all running processes.
        /// </summary>
        public static IEnumerable<OS.Process> GetAll() => ProcessManager.Instance.GetAllProcesses();

        /// <summary>
        /// Sends a signal (e.g., Ctrl+C) to a process.
        /// </summary>
        public static void SendSignal(TheGame.Core.OS.Process process, string signal) {
            if (process == null) return;
            if (signal == "SIGINT" || signal == "CTRL+C") {
                // Trigger the internal signal handler on the process
                process.TriggerSignalCancel();
            }
        }
    }
}
