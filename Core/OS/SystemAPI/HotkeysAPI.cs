using System;
using Microsoft.Xna.Framework.Input;
using TheGame.Core.Input;

namespace TheGame.Core.OS;

public class HotkeysAPI : BaseAPI {
    public HotkeysAPI(Process process) : base(process) {
    }

    public void RegisterGlobal(Keys key, HotkeyModifiers mods, Action callback) {
        Shell.Hotkeys.RegisterGlobal(key, mods, callback);
    }

    public void UnregisterGlobal(Keys key, HotkeyModifiers mods) {
        Shell.Hotkeys.UnregisterGlobal(key, mods);
    }

    public void RegisterLocal(Keys key, HotkeyModifiers mods, Action callback, bool callInBackground = false, bool rewriteSystemHotkey = false) {
        Shell.Hotkeys.RegisterLocal(OwningProcess, key, mods, callback, callInBackground, rewriteSystemHotkey);
    }

    public void RegisterLocal(string shortcut, Action callback, bool callInBackground = false, bool rewriteSystemHotkey = false) {
        Shell.Hotkeys.RegisterLocal(OwningProcess, shortcut, callback, callInBackground, rewriteSystemHotkey);
    }

    public void UnregisterLocal() {
        Shell.Hotkeys.UnregisterLocal(OwningProcess);
    }
}
