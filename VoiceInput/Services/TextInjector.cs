using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

namespace VoiceInput.Services;

public sealed class TextInjector
{
    private const int ClipboardRetryCount = 3;
    private const uint InputKeyboard = 1;
    private const uint KeyeventfKeyup = 0x0002;
    private static readonly TimeSpan ClipboardRetryDelay = TimeSpan.FromMilliseconds(50);

    private readonly Dispatcher _dispatcher;

    public TextInjector(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public async Task InjectAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        IDataObject? backupClipboard = null;
        await _dispatcher.InvokeAsync(() => backupClipboard = Clipboard.GetDataObject());

        await ExecuteClipboardActionAsync(() => Clipboard.SetText(text), cancellationToken).ConfigureAwait(false);

        SendCtrlV();
        await Task.Delay(100, cancellationToken).ConfigureAwait(false);

        if (backupClipboard is not null)
        {
            await ExecuteClipboardActionAsync(() => Clipboard.SetDataObject(backupClipboard, true), cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            await ExecuteClipboardActionAsync(Clipboard.Clear, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ExecuteClipboardActionAsync(Action action, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= ClipboardRetryCount; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await _dispatcher.InvokeAsync(action);
                return;
            }
            catch (ExternalException) when (attempt < ClipboardRetryCount)
            {
                await Task.Delay(ClipboardRetryDelay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static void SendCtrlV()
    {
        var inputs = new[]
        {
            CreateKeyboardInput(0x11, false),
            CreateKeyboardInput(0x56, false),
            CreateKeyboardInput(0x56, true),
            CreateKeyboardInput(0x11, true)
        };

        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        if (sent != (uint)inputs.Length)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "模拟 Ctrl+V 失败。");
        }
    }

    private static Input CreateKeyboardInput(ushort vk, bool keyUp)
    {
        return new Input
        {
            Type = InputKeyboard,
            Data = new InputUnion
            {
                Keyboard = new KeybdInput
                {
                    Vk = vk,
                    Scan = 0,
                    Flags = keyUp ? KeyeventfKeyup : 0u,
                    Time = 0,
                    ExtraInfo = IntPtr.Zero
                }
            }
        };
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MouseInput Mouse;

        [FieldOffset(0)]
        public KeybdInput Keyboard;

        [FieldOffset(0)]
        public HardwareInput Hardware;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeybdInput
    {
        public ushort Vk;
        public ushort Scan;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HardwareInput
    {
        public uint Msg;
        public ushort ParamL;
        public ushort ParamH;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, Input[] pInputs, int cbSize);
}
