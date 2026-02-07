using System;
using System.Runtime.InteropServices;
using System.Diagnostics;
using TheGame.Core;
using TheGame.Core.Input;
using Microsoft.Xna.Framework.Input;

public class WindowsKeyHook : IDisposable {
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int VK_SNAPSHOT = 0x2C;
    private const int VK_LWIN = 0x5B;
    private const int VK_RWIN = 0x5C;
    private const int VK_CONTROL = 0x11;

    private LowLevelKeyboardProc _proc;
    private IntPtr _hookID = IntPtr.Zero;
    private bool _isActive = true;

    public WindowsKeyHook() {
        _proc = HookCallback;
        _hookID = SetHook(_proc);
    }

    // Call this from your Game.OnActivated / OnDeactivated
    public void SetActive(bool active) => _isActive = active;

    private IntPtr SetHook(LowLevelKeyboardProc proc) {
        using (Process curProcess = Process.GetCurrentProcess())
        using (ProcessModule curModule = curProcess.MainModule) {
            return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
        }
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam) {
        if (nCode >= 0 && _isActive) {
            KBDLLHOOKSTRUCT kbStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            uint vkCode = kbStruct.vkCode;

            // Check if Alt is held (Bit 5 of flags)
            bool altDown = (kbStruct.flags & 0x20) != 0;

            // Check if Ctrl is held (Highest bit of return value means 'down')
            bool ctrlDown = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;

            bool isKeyDown = wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN;
            bool isKeyUp = wParam == (IntPtr)0x0101 || wParam == (IntPtr)0x0105; // WM_KEYUP, WM_SYSKEYUP

            // Block Print Screen Key
            if (vkCode == VK_SNAPSHOT) {
                if (isKeyDown) {
                    InputManager.SetKeyOverride(Keys.PrintScreen, true);
                }
                if (isKeyUp) {
                    InputManager.SetKeyOverride(Keys.PrintScreen, false);
                }
                
                return (IntPtr)1; 
            }

            // Block Windows Keys (Left: 0x5B, Right: 0x5C)
            if (vkCode == 0x5B || vkCode == 0x5C) {
                var key = vkCode == 0x5B ? Keys.LeftWindows : Keys.RightWindows;
                if (isKeyDown) {
                    InputManager.SetKeyOverride(key, true);
                }
                if (isKeyUp) {
                    InputManager.SetKeyOverride(key, false);
                }
                return (IntPtr)1;
            }

            // Block Alt+Tab (Tab: 0x09)
            if (vkCode == 0x09 && altDown) return (IntPtr)1;

            // Block Alt+Esc (Esc: 0x1B)
            if (vkCode == 0x1B && altDown) return (IntPtr)1;

            // Block Ctrl+Esc
            if (vkCode == 0x1B && ctrlDown) return (IntPtr)1;
        }

        return CallNextHookEx(_hookID, nCode, wParam, lParam);
    }

    public void Dispose() {
        UnhookWindowsHookEx(_hookID);
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);
}
