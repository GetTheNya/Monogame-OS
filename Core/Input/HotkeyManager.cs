using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using TheGame.Core.UI;
using TheGame.Core.OS;

namespace TheGame.Core.Input;

public class HotkeyEntry {
    public Hotkey Hotkey;
    public Action Action;
    public Process Owner; // If null, it's a global hotkey
    public bool CallInBackground;
    public bool RewriteSystemHotkey;
}

public static class HotkeyManager {
    private static readonly List<HotkeyEntry> _globalHotkeys = new();
    private static readonly List<HotkeyEntry> _localHotkeys = new();
    private static KeyboardState _previousState;
    private static bool _suppressModifierOnlyHotkeys;

    public static void RegisterGlobal(Hotkey hotkey, Action action) {
        _globalHotkeys.Add(new HotkeyEntry { Hotkey = hotkey, Action = action });
    }

    public static void UnregisterGlobal(Hotkey hotkey) {
        _globalHotkeys.RemoveAll(e => e.Hotkey == hotkey);
    }

    public static void RegisterLocal(Process process, Hotkey hotkey, Action action, bool callInBackground = false, bool rewriteSystemHotkey = false) {
        if (process == null) return;
        _localHotkeys.Add(new HotkeyEntry { 
            Owner = process, 
            Hotkey = hotkey, 
            Action = action, 
            CallInBackground = callInBackground, 
            RewriteSystemHotkey = rewriteSystemHotkey 
        });
    }

    public static void UnregisterLocal(Process process) {
        if (process == null) return;
        _localHotkeys.RemoveAll(e => e.Owner == process);
    }

    public static void Update(GameTime gameTime, KeyboardState currentState) {
        var pressedKeys = currentState.GetPressedKeys();
        
        // Suppress modifier-only hotkeys if a non-modifier key is pressed
        bool anyNonModifierDown = pressedKeys.Any(k => !IsModifierKey(k) && k != Keys.None);
        if (anyNonModifierDown) _suppressModifierOnlyHotkeys = true;

        // Reset per-frame flags
        InputManager.IsKeyboardConsumed = false;

        // Priority 1: Local Overwrites (rewriteSystemHotkey = true)
        ProcessHotkeyEntries(_localHotkeys.Where(e => e.RewriteSystemHotkey).ToList(), currentState, true);

        // Priority 2: Global Hotkeys (only if keyboard not consumed)
        if (!InputManager.IsKeyboardConsumed) {
            ProcessHotkeyEntries(_globalHotkeys, currentState, false);
        }

        // Priority 3: Regular Local Hotkeys (only if keyboard not consumed)
        if (!InputManager.IsKeyboardConsumed) {
            ProcessHotkeyEntries(_localHotkeys.Where(e => !e.RewriteSystemHotkey).ToList(), currentState, true);
        }

        // Reset suppression only AFTER hotkeys are processed for the frame
        bool anyModifierDown = pressedKeys.Any(k => IsModifierKey(k));
        if (!anyModifierDown) {
            _suppressModifierOnlyHotkeys = false;
        }

        _previousState = currentState;
    }

    private static bool IsModifierKey(Keys key) {
        return key == Keys.LeftControl || key == Keys.RightControl ||
               key == Keys.LeftAlt || key == Keys.RightAlt ||
               key == Keys.LeftShift || key == Keys.RightShift ||
               key == Keys.LeftWindows || key == Keys.RightWindows;
    }

    private static void ProcessHotkeyEntries(List<HotkeyEntry> entries, KeyboardState currentState, bool isLocal) {
        foreach (var entry in entries) {
            // Check if local hotkey is eligible to fire
            if (isLocal && entry.Owner != null) {
                bool isFocused = Window.ActiveWindow?.OwnerProcess == entry.Owner;
                if (!isFocused && !entry.CallInBackground) continue;
            }

            bool isModifierOnly = entry.Hotkey.Key == Keys.None;

            if (!isModifierOnly) {
                // Key + Modifiers (e.g. Win+V) triggers on DOWN
                bool keyJustPressed = currentState.IsKeyDown(entry.Hotkey.Key) && !_previousState.IsKeyDown(entry.Hotkey.Key);
                
                if (keyJustPressed && entry.Hotkey.IsPressed(currentState)) {
                    _suppressModifierOnlyHotkeys = true; 
                    entry.Action?.Invoke();
                    InputManager.IsKeyboardConsumed = true;
                    // We only trigger one hotkey per key press
                    break; 
                }
            } else {
                // Modifier-only (e.g. Win) triggers on RELEASE
                if (!entry.Hotkey.IsPressed(currentState) && entry.Hotkey.IsPressed(_previousState)) {
                    if (!_suppressModifierOnlyHotkeys) {
                        entry.Action?.Invoke();
                        InputManager.IsKeyboardConsumed = true;
                        break;
                    }
                }
            }
            
            if (InputManager.IsKeyboardConsumed) break;
        }
    }
}
