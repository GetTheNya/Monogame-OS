using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace TheGame.Core.Input;

public static class InputManager {
    private static MouseState _currentMouse;
    private static MouseState _previousMouse;

    private static KeyboardState _currentKeyboard;
    private static KeyboardState _previousKeyboard;
    private static readonly HashSet<Keys> _overriddenKeys = new();

    // UI Consumption flags
    public static bool IsMouseConsumed { get; set; }
    public static bool IsKeyboardConsumed { get; set; }
    public static bool IsScrollConsumed { get; set; }
    
    // Character Input Buffer
    private static List<char> _charBuffer = new();
    private static List<char> _typedChars = new();
    public static event Action<char> OnCharEntered;

    public static void AddChar(char c) {
        _charBuffer.Add(c);
        OnCharEntered?.Invoke(c);
    }

    public static IEnumerable<char> GetTypedChars() {
        if (IsKeyboardConsumed) return Enumerable.Empty<char>();
        return _typedChars;
    }

    public static Point MousePosition => _currentMouse.Position;
    public static int MouseX => _currentMouse.X;
    public static int MouseY => _currentMouse.Y;
    public static int ScrollWheel => _currentMouse.ScrollWheelValue;
    public static int ScrollDelta => _currentMouse.ScrollWheelValue - _previousMouse.ScrollWheelValue;

    private static double _lastLeftClickTime;
    private static double _lastRightClickTime;
    private const double DoubleClickThreshold = 0.3; // 300ms
    private static bool _isDoubleClickFrame; // Only true for one frame
    private static bool _isRightDoubleClickFrame;

    private static Dictionary<Keys, float> _keyRepeatTimers = new();
    private static float _initialRepeatDelay = 0.5f;
    private static float _repeatRate = 0.05f;

    public static void Update(GameTime gameTime) {
        _previousMouse = _currentMouse;
        _currentMouse = Mouse.GetState();

        _previousKeyboard = _currentKeyboard;
        _currentKeyboard = GetMergedKeyboardState();
        
        _typedChars.Clear();
        _typedChars.AddRange(_charBuffer);
        _charBuffer.Clear();
        IsMouseConsumed = false;
        IsKeyboardConsumed = false;
        IsScrollConsumed = false;

        // Check Hotkeys
        HotkeyManager.Update(gameTime, _currentKeyboard);

        _isDoubleClickFrame = false;
        _isRightDoubleClickFrame = false;

        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        
        // Update key repeat timers
        var currentKeys = _currentKeyboard.GetPressedKeys();
        foreach (var key in currentKeys) {
            if (!_keyRepeatTimers.ContainsKey(key)) {
                _keyRepeatTimers[key] = -_initialRepeatDelay;
            } else {
                _keyRepeatTimers[key] += dt;
            }
        }

        // Remove keys that are no longer pressed
        List<Keys> toRemove = new();
        foreach (var key in _keyRepeatTimers.Keys) {
            if (_currentKeyboard.IsKeyUp(key)) toRemove.Add(key);
        }
        foreach (var key in toRemove) _keyRepeatTimers.Remove(key);

        // Detect Left Click
        if (GetButtonState(_currentMouse, MouseButton.Left) == ButtonState.Pressed &&
            GetButtonState(_previousMouse, MouseButton.Left) == ButtonState.Released) {
                
            double now = gameTime.TotalGameTime.TotalSeconds;
            if (now - _lastLeftClickTime < DoubleClickThreshold) {
                _isDoubleClickFrame = true;
                _lastLeftClickTime = 0; // Reset so triple click isn't two double clicks
            } else {
                 _isDoubleClickFrame = false; 
                 _lastLeftClickTime = now;
            }
        }
        
        // Detect Right Click
        if (GetButtonState(_currentMouse, MouseButton.Right) == ButtonState.Pressed &&
            GetButtonState(_previousMouse, MouseButton.Right) == ButtonState.Released) {
                
            double now = gameTime.TotalGameTime.TotalSeconds;
            if (now - _lastRightClickTime < DoubleClickThreshold) {
                _isRightDoubleClickFrame = true;
                _lastRightClickTime = 0;
            } else {
                 _isRightDoubleClickFrame = false; 
                 _lastRightClickTime = now;
            }
        }
    }

    public static bool IsKeyRepeated(Keys key) {
        if (IsKeyboardConsumed) return false;
        if (IsKeyJustPressed(key)) return true;
        
        if (_keyRepeatTimers.TryGetValue(key, out float timer)) {
            if (timer >= _repeatRate) {
                _keyRepeatTimers[key] -= _repeatRate;
                return true;
            }
        }
        return false;
    }

    public static bool IsDoubleClick(MouseButton button, bool ignoreConsumed = false) {
        if (!ignoreConsumed && IsMouseConsumed) return false;
        return button switch {
            MouseButton.Left => _isDoubleClickFrame,
            MouseButton.Right => _isRightDoubleClickFrame,
            _ => false
        };
    }

    // --- Mouse ---

    public static bool IsMouseButtonDown(MouseButton button) {
        return GetButtonState(_currentMouse, button) == ButtonState.Pressed;
    }

    public static bool IsMouseButtonJustPressed(MouseButton button) {
        return GetButtonState(_currentMouse, button) == ButtonState.Pressed &&
               GetButtonState(_previousMouse, button) == ButtonState.Released;
    }

    public static bool IsMouseButtonJustReleased(MouseButton button) {
        return GetButtonState(_currentMouse, button) == ButtonState.Released &&
               GetButtonState(_previousMouse, button) == ButtonState.Pressed;
    }

    private static ButtonState GetButtonState(MouseState state, MouseButton button) {
        return button switch {
            MouseButton.Left => state.LeftButton,
            MouseButton.Middle => state.MiddleButton,
            MouseButton.Right => state.RightButton,
            _ => ButtonState.Released
        };
    }

    public static bool IsAnyMouseButtonDown() {
        return _currentMouse.LeftButton == ButtonState.Pressed ||
               _currentMouse.RightButton == ButtonState.Pressed ||
               _currentMouse.MiddleButton == ButtonState.Pressed;
    }

    public static bool IsAnyMouseButtonJustPressed(MouseButton button) {
        return GetButtonState(_currentMouse, button) == ButtonState.Pressed &&
               GetButtonState(_previousMouse, button) == ButtonState.Released;
    }

    public static bool IsAnyMouseButtonJustPressed() {
        return (GetButtonState(_currentMouse, MouseButton.Left) == ButtonState.Pressed && GetButtonState(_previousMouse, MouseButton.Left) == ButtonState.Released) ||
               (GetButtonState(_currentMouse, MouseButton.Right) == ButtonState.Pressed && GetButtonState(_previousMouse, MouseButton.Right) == ButtonState.Released) ||
               (GetButtonState(_currentMouse, MouseButton.Middle) == ButtonState.Pressed && GetButtonState(_previousMouse, MouseButton.Middle) == ButtonState.Released);
    }

    public static bool IsMouseHovering(Rectangle rect, bool ignoreConsumed = false) {
        if (!ignoreConsumed && IsMouseConsumed) return false;
        return rect.Contains(MousePosition);
    }

    // --- Keyboard ---

    public static bool IsKeyDown(Keys key) {
        if (IsKeyboardConsumed) return false;
        return _currentKeyboard.IsKeyDown(key);
    }

    public static bool IsKeyJustPressed(Keys key) {
        if (IsKeyboardConsumed) return false;
        return _currentKeyboard.IsKeyDown(key) && _previousKeyboard.IsKeyUp(key);
    }

    /// <summary>
    /// Manually sets a key state, bypassing the hardware state. 
    /// Used by WindowsKeyHook to report keys that are blocked from the OS.
    /// </summary>
    public static void SetKeyOverride(Keys key, bool isDown) {
        if (isDown) _overriddenKeys.Add(key);
        else _overriddenKeys.Remove(key);
    }

    private static KeyboardState GetMergedKeyboardState() {
        var state = Keyboard.GetState();
        if (_overriddenKeys.Count == 0) return state;

        var pressed = new HashSet<Keys>(state.GetPressedKeys());
        foreach (var key in _overriddenKeys) {
            pressed.Add(key);
        }
        return new KeyboardState(pressed.ToArray());
    }
}

public enum MouseButton {
    Left,
    Middle,
    Right
}
