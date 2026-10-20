using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

namespace VoiceInput.Services;

public sealed class TextInjector
{
    // 剪贴板是全局独占资源，输入法、剪贴板管理器等进程都会短暂持锁。
    // 单次操作的递增退避总窗口为 840ms（30+60+90+...+210），比固定 3×50ms 更能熬过这类占用。
    private const int ClipboardRetryCount = 8;
    private const uint InputKeyboard = 1;
    private const uint KeyeventfKeyup = 0x0002;
    private static readonly TimeSpan ClipboardRetryBaseDelay = TimeSpan.FromMilliseconds(30);

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

        // 备份失败不阻断注入：丢掉用户原有剪贴板内容是小损失，丢掉转写结果是大损失。
        IDataObject? backupClipboard = null;
        await TryClipboardActionAsync(
            () => backupClipboard = Clipboard.GetDataObject(),
            cancellationToken).ConfigureAwait(false);

        // 写入是关键步骤：失败还发 Ctrl+V 会把剪贴板里的旧内容粘出去，必须抛出。
        await ExecuteClipboardActionAsync(
            () => Clipboard.SetDataObject(text, false),
            cancellationToken).ConfigureAwait(false);

        SendCtrlV();
        await Task.Delay(100, cancellationToken).ConfigureAwait(false);

        // 恢复失败同样只记日志：文本已经粘贴成功，不该因为收尾失败而报错给用户。
        await TryClipboardActionAsync(
            () =>
            {
                if (backupClipboard is not null)
                {
                    Clipboard.SetDataObject(backupClipboard, false);
                }
                else
                {
                    Clipboard.Clear();
                }
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 带递增退避的剪贴板操作，重试耗尽后抛出最后一次异常。
    /// </summary>
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
                await Task.Delay(ClipboardRetryBaseDelay * attempt, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// 同上但吞掉最终失败，仅记日志。用于备份/恢复这类失败不影响主目标的操作。
    /// </summary>
    private async Task TryClipboardActionAsync(Action action, CancellationToken cancellationToken)
    {
        try
        {
            await ExecuteClipboardActionAsync(action, cancellationToken).ConfigureAwait(false);
        }
        catch (ExternalException ex)
        {
            AppLogger.Error("剪贴板操作失败（已忽略，不影响文本注入）。", ex);
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
