using System;
using Microsoft.Xna.Framework.Input;
using TheGame.Core.Input;

namespace TheGame.Core.OS;

public static partial class Shell {
    public static class Hotkeys {
        public static void RegisterGlobal(Keys key, HotkeyModifiers mods, Action callback) {
            HotkeyManager.RegisterGlobal(new Hotkey(key, mods), callback);
        }

        public static void UnregisterGlobal(Keys key, HotkeyModifiers mods) {
            HotkeyManager.UnregisterGlobal(new Hotkey(key, mods));
        }

        public static void RegisterLocal(TheGame.Core.OS.Process process, Keys key, HotkeyModifiers mods, Action callback, bool callInBackground = false, bool rewriteSystemHotkey = false) {
            HotkeyManager.RegisterLocal(process, new Hotkey(key, mods), callback, callInBackground, rewriteSystemHotkey);
        }

        public static void RegisterLocal(TheGame.Core.OS.Process process, string shortcut, Action callback, bool callInBackground = false, bool rewriteSystemHotkey = false) {
            HotkeyManager.RegisterLocal(process, Hotkey.Parse(shortcut), callback, callInBackground, rewriteSystemHotkey);
        }

        public static void UnregisterLocal(TheGame.Core.OS.Process process) {
            HotkeyManager.UnregisterLocal(process);
        }
    }
}
