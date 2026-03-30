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

    private static readonly HashSet<uint> FnVirtualKeys = new()
    {
        0xFF,
        0xE8,
        0xE9
    };

    private readonly LowLevelKeyboardProc _hookProc;
    private IntPtr _hookHandle = IntPtr.Zero;
    private uint _fallbackVirtualKey = 0xA3;
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
        if (!Enum.TryParse<Key>(fallbackHotkey, true, out var key))
        {
            key = Key.RightCtrl;
        }

        var virtualKey = KeyInterop.VirtualKeyFromKey(key);
        _fallbackVirtualKey = virtualKey > 0 ? (uint)virtualKey : 0xA3;
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
                _recordingKeyPressed = true;
                RecordingStarted?.Invoke(this, EventArgs.Empty);
                return (IntPtr)1;
            }

            if (isKeyUp && _recordingKeyPressed)
            {
                _recordingKeyPressed = false;
                RecordingStopped?.Invoke(this, EventArgs.Empty);
                return (IntPtr)1;
            }

            return (IntPtr)1;
        }
        catch (Exception ex)
        {
            AppLogger.Error("键盘钩子回调异常，已忽略以防止进程退出。", ex);
            return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }
    }

    private bool IsTriggerKey(uint vkCode)
    {
        return vkCode == _fallbackVirtualKey || FnVirtualKeys.Contains(vkCode);
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

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);
}
