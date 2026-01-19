using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using TheGame.Core.UI;

namespace TheGame.Core.Input;

public class HotkeyEntry {
    public Hotkey Hotkey;
    public Action Action;
    public string AppId; // If null, it's a global hotkey
}

public static class HotkeyManager {
    private static readonly List<HotkeyEntry> _globalHotkeys = new();
    private static readonly Dictionary<string, List<HotkeyEntry>> _localHotkeys = new();
    private static KeyboardState _previousState;
    private static bool _suppressModifierOnlyHotkeys;

    public static void RegisterGlobal(Hotkey hotkey, Action action) {
        _globalHotkeys.Add(new HotkeyEntry { Hotkey = hotkey, Action = action });
    }

    public static void RegisterLocal(string appId, Hotkey hotkey, Action action) {
        if (string.IsNullOrEmpty(appId)) return;
        string upperAppId = appId.ToUpper();
        if (!_localHotkeys.ContainsKey(upperAppId)) {
            _localHotkeys[upperAppId] = new List<HotkeyEntry>();
        }
        _localHotkeys[upperAppId].Add(new HotkeyEntry { Hotkey = hotkey, Action = action, AppId = upperAppId });
    }

    public static void UnregisterLocal(string appId) {
        if (string.IsNullOrEmpty(appId)) return;
        _localHotkeys.Remove(appId.ToUpper());
    }

    public static void Update(GameTime gameTime, KeyboardState currentState) {
        var pressedKeys = currentState.GetPressedKeys();
        
        // Suppress modifier-only hotkeys if a non-modifier key is pressed
        bool anyNonModifierDown = pressedKeys.Any(k => !IsModifierKey(k) && k != Keys.None);
        if (anyNonModifierDown) _suppressModifierOnlyHotkeys = true;

        // Check Global Hotkeys
        ProcessHotkeyEntries(_globalHotkeys, currentState);

        // Check Local Hotkeys for Active App
        var activeWindow = Window.ActiveWindow;
        if (activeWindow != null && !string.IsNullOrEmpty(activeWindow.AppId)) {
            string appId = activeWindow.AppId.ToUpper();
            if (_localHotkeys.TryGetValue(appId, out var localEntries)) {
                ProcessHotkeyEntries(localEntries, currentState);
            }
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

    private static void ProcessHotkeyEntries(List<HotkeyEntry> entries, KeyboardState currentState) {
        foreach (var entry in entries) {
            bool isModifierOnly = entry.Hotkey.Key == Keys.None;

            if (!isModifierOnly) {
                // Key + Modifiers (e.g. Win+V) triggers on DOWN
                // Standard behavior: Modifiers must be held, and the primary key must be the one that JUST went down.
                bool keyJustPressed = currentState.IsKeyDown(entry.Hotkey.Key) && !_previousState.IsKeyDown(entry.Hotkey.Key);
                
                if (keyJustPressed && entry.Hotkey.IsPressed(currentState)) {
                    _suppressModifierOnlyHotkeys = true; // Block modifier-only trigger for this hold cycle
                    entry.Action?.Invoke();
                    InputManager.IsKeyboardConsumed = true;
                }
            } else {
                // Modifier-only (e.g. Win) triggers on RELEASE
                // We trigger when the state changes from "Pressed" to "Not Pressed"
                if (!entry.Hotkey.IsPressed(currentState) && entry.Hotkey.IsPressed(_previousState)) {
                    if (!_suppressModifierOnlyHotkeys) {
                        entry.Action?.Invoke();
                        InputManager.IsKeyboardConsumed = true;
                    }
                }
            }
        }
    }
}
