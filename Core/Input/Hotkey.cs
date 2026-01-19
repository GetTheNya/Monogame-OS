using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Input;

namespace TheGame.Core.Input;

[Flags]
public enum HotkeyModifiers {
    None = 0,
    Ctrl = 1 << 0,
    Alt = 1 << 1,
    Shift = 1 << 2,
    Win = 1 << 3
}

public struct Hotkey : IEquatable<Hotkey> {
    public Keys Key;
    public HotkeyModifiers Modifiers;

    public Hotkey(Keys key, HotkeyModifiers modifiers = HotkeyModifiers.None) {
        Key = key;
        Modifiers = modifiers;
    }

    public bool IsPressed(KeyboardState state) {
        bool ctrl = state.IsKeyDown(Keys.LeftControl) || state.IsKeyDown(Keys.RightControl);
        bool alt = state.IsKeyDown(Keys.LeftAlt) || state.IsKeyDown(Keys.RightAlt);
        bool shift = state.IsKeyDown(Keys.LeftShift) || state.IsKeyDown(Keys.RightShift);
        bool win = state.IsKeyDown(Keys.LeftWindows) || state.IsKeyDown(Keys.RightWindows);

        // Check if required modifiers are held
        if (((Modifiers & HotkeyModifiers.Ctrl) != 0) != ctrl) return false;
        if (((Modifiers & HotkeyModifiers.Alt) != 0) != alt) return false;
        if (((Modifiers & HotkeyModifiers.Shift) != 0) != shift) return false;
        if (((Modifiers & HotkeyModifiers.Win) != 0) != win) return false;

        // If a specific key is required, check it
        if (Key != Keys.None) {
            return state.IsKeyDown(Key);
        }

        // If no primary key (Key == None), it's a modifier-only hotkey.
        // It's "pressed" if at least one modifier is held (already checked exclusivity above)
        return (ctrl || alt || shift || win);
    }

    public static Hotkey Parse(string shortcut) {
        if (string.IsNullOrEmpty(shortcut)) return new Hotkey(Keys.None);

        string[] parts = shortcut.Split('+');
        HotkeyModifiers mods = HotkeyModifiers.None;
        Keys key = Keys.None;

        for (int i = 0; i < parts.Length; i++) {
            string part = parts[i].Trim().ToUpper();
            if (part == "CTRL") mods |= HotkeyModifiers.Ctrl;
            else if (part == "ALT") mods |= HotkeyModifiers.Alt;
            else if (part == "SHIFT") mods |= HotkeyModifiers.Shift;
            else if (part == "WIN") mods |= HotkeyModifiers.Win;
            else if (Enum.TryParse<Keys>(part, true, out var k)) {
                key = k;
            }
        }

        return new Hotkey(key, mods);
    }

    public override string ToString() {
        List<string> parts = new();
        if ((Modifiers & HotkeyModifiers.Ctrl) != 0) parts.Add("Ctrl");
        if ((Modifiers & HotkeyModifiers.Alt) != 0) parts.Add("Alt");
        if ((Modifiers & HotkeyModifiers.Shift) != 0) parts.Add("Shift");
        if ((Modifiers & HotkeyModifiers.Win) != 0) parts.Add("Win");
        parts.Add(Key.ToString());
        return string.Join("+", parts);
    }

    public override bool Equals(object obj) => obj is Hotkey other && Equals(other);
    public bool Equals(Hotkey other) => Key == other.Key && Modifiers == other.Modifiers;
    public override int GetHashCode() => HashCode.Combine(Key, Modifiers);
    public static bool operator ==(Hotkey left, Hotkey right) => left.Equals(right);
    public static bool operator !=(Hotkey left, Hotkey right) => !left.Equals(right);
}
