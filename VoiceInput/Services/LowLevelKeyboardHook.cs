using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace VoiceInput.Services;

public sealed class LowLevelKeyboardHook : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;

    private const int VkLControl = 0xA2;
    private const int VkRControl = 0xA3;
    private const int VkLMenu = 0xA4;
    private const int VkRMenu = 0xA5;
    private const int VkLShift = 0xA0;
    private const int VkRShift = 0xA1;
    private const int VkLWin = 0x5B;
    private const int VkRWin = 0x5C;

    private static readonly HashSet<uint> FnVirtualKeys = new()
    {
        0xFF,
        0xE8,
        0xE9
    };

    private readonly LowLevelKeyboardProc _hookProc;
    private IntPtr _hookHandle = IntPtr.Zero;
    private uint[] _fallbackVirtualKeys = { 0xA3 };
    private ModifierKeys _requiredModifiers = ModifierKeys.Control;
    private bool _recordingKeyPressed;

    public LowLevelKeyboardHook(string fallbackHotkey)
    {
        _hookProc = HookCallback;
        SetFallbackHotkey(fallbackHotkey);
    }

    public event EventHandler? RecordingStarted;

    public event EventHandler? RecordingStopped;

    public void Start()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            return;
        }

        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule;
        var moduleName = module?.ModuleName;
        var moduleHandle = string.IsNullOrWhiteSpace(moduleName)
            ? IntPtr.Zero
            : GetModuleHandle(moduleName);

        _hookHandle = SetWindowsHookEx(WhKeyboardLl, _hookProc, moduleHandle, 0);
        if (_hookHandle == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "安装低级键盘钩子失败。");
        }

        AppLogger.Info("低级键盘钩子已安装。");
    }

    public void Stop()
    {
        if (_hookHandle == IntPtr.Zero)
        {
            return;
        }

        _recordingKeyPressed = false;

        if (!UnhookWindowsHookEx(_hookHandle))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "卸载低级键盘钩子失败。");
        }

        _hookHandle = IntPtr.Zero;
        AppLogger.Info("低级键盘钩子已卸载。");
    }

    public void SetFallbackHotkey(string fallbackHotkey)
    {
        var text = string.IsNullOrWhiteSpace(fallbackHotkey)
            ? Key.RightCtrl.ToString()
            : fallbackHotkey.Trim();

        var tokens = text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var nonModifierKeys = new List<Key>();
        var modifierKeys = new List<Key>();
        var requiredModifiers = ModifierKeys.None;

        foreach (var token in tokens)
        {
            if (TryParseModifierToken(token, out var modifier))
            {
                requiredModifiers |= modifier;
                continue;
            }

            if (!Enum.TryParse<Key>(token, true, out var key))
            {
                continue;
            }

            if (TryMapKeyToModifier(key, out var keyModifier))
            {
                modifierKeys.Add(key);
                requiredModifiers |= keyModifier;
            }
            else
            {
                nonModifierKeys.Add(key);
            }
        }

        var triggerKeys = new List<Key>();
        if (nonModifierKeys.Count > 0)
        {
            triggerKeys.Add(nonModifierKeys[0]);
        }
        else if (modifierKeys.Count > 0)
        {
            triggerKeys.AddRange(modifierKeys);
        }
        else
        {
            triggerKeys.Add(Key.RightCtrl);
            requiredModifiers = ModifierKeys.Control;
        }

        var virtualKeys = new List<uint>();
        foreach (var triggerKey in triggerKeys)
        {
            var virtualKey = KeyInterop.VirtualKeyFromKey(triggerKey);
            if (virtualKey > 0)
            {
                var vk = (uint)virtualKey;
                if (!virtualKeys.Contains(vk))
                {
                    virtualKeys.Add(vk);
                }
            }
        }

        if (virtualKeys.Count == 0)
        {
            virtualKeys.Add(0xA3);
            requiredModifiers = ModifierKeys.Control;
        }

        _fallbackVirtualKeys = virtualKeys.ToArray();
        _requiredModifiers = requiredModifiers;
    }

    public void Dispose()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
            AppLogger.Info("低级键盘钩子 Dispose 卸载。");
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (nCode < 0)
            {
                return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
            }

            var hookStruct = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
            var vkCode = hookStruct.VkCode;
            if (!IsTriggerKey(vkCode))
            {
                return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
            }

            var message = unchecked((int)(long)wParam);
            var isKeyDown = message == WmKeyDown || message == WmSysKeyDown;
            var isKeyUp = message == WmKeyUp || message == WmSysKeyUp;

            if (isKeyDown && !_recordingKeyPressed)
            {
                if (!FnVirtualKeys.Contains(vkCode) && !AreRequiredModifiersPressed())
                {
                    return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
                }

                _recordingKeyPressed = true;
                RecordingStarted?.Invoke(this, EventArgs.Empty);
                return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
            }

            if (isKeyUp && _recordingKeyPressed)
            {
                _recordingKeyPressed = false;
                RecordingStopped?.Invoke(this, EventArgs.Empty);
                return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
            }

            return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }
        catch (Exception ex)
        {
            AppLogger.Error("键盘钩子回调异常，已忽略以防止进程退出。", ex);
            return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }
    }

    private bool IsTriggerKey(uint vkCode)
    {
        if (FnVirtualKeys.Contains(vkCode))
        {
            return true;
        }

        var fallbackKeys = _fallbackVirtualKeys;
        for (var i = 0; i < fallbackKeys.Length; i++)
        {
            if (fallbackKeys[i] == vkCode)
            {
                return true;
            }
        }

        return false;
    }

    private bool AreRequiredModifiersPressed()
    {
        var required = _requiredModifiers;

        if ((required & ModifierKeys.Control) != 0 &&
            !IsEitherPressed(VkLControl, VkRControl))
        {
            return false;
        }

        if ((required & ModifierKeys.Alt) != 0 &&
            !IsEitherPressed(VkLMenu, VkRMenu))
        {
            return false;
        }

        if ((required & ModifierKeys.Shift) != 0 &&
            !IsEitherPressed(VkLShift, VkRShift))
        {
            return false;
        }

        if ((required & ModifierKeys.Windows) != 0 &&
            !IsEitherPressed(VkLWin, VkRWin))
        {
            return false;
        }

        return true;
    }

    private static bool TryParseModifierToken(string token, out ModifierKeys modifier)
    {
        switch (token)
        {
            case "Ctrl":
            case "Control":
                modifier = ModifierKeys.Control;
                return true;
            case "Alt":
                modifier = ModifierKeys.Alt;
                return true;
            case "Shift":
                modifier = ModifierKeys.Shift;
                return true;
            case "Win":
            case "Windows":
            case "Meta":
                modifier = ModifierKeys.Windows;
                return true;
            default:
                modifier = ModifierKeys.None;
                return false;
        }
    }

    private static bool TryMapKeyToModifier(Key key, out ModifierKeys modifier)
    {
        switch (key)
        {
            case Key.LeftCtrl:
            case Key.RightCtrl:
                modifier = ModifierKeys.Control;
                return true;
            case Key.LeftAlt:
            case Key.RightAlt:
                modifier = ModifierKeys.Alt;
                return true;
            case Key.LeftShift:
            case Key.RightShift:
                modifier = ModifierKeys.Shift;
                return true;
            case Key.LWin:
            case Key.RWin:
                modifier = ModifierKeys.Windows;
                return true;
            default:
                modifier = ModifierKeys.None;
                return false;
        }
    }

    private static bool IsEitherPressed(int leftVirtualKey, int rightVirtualKey)
    {
        return IsPressed(leftVirtualKey) || IsPressed(rightVirtualKey);
    }

    private static bool IsPressed(int virtualKey)
    {
        return (GetKeyState(virtualKey) & 0x8000) != 0;
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHookStruct
    {
        public uint VkCode;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr DwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(
        int idHook,
        LowLevelKeyboardProc lpfn,
        IntPtr hMod,
        uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);
}
