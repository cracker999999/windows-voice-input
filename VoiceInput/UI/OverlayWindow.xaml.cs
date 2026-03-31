using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace VoiceInput.UI;

public partial class OverlayWindow
{
    private static readonly double[] BarWeights =
    {
        0.5,
        0.8,
        1.0,
        0.75,
        0.55
    };

    private readonly Rectangle[] _bars;
    private readonly DispatcherTimer _waveformTimer;
    private readonly Random _random = new();

    private double _targetRms;
    private double _smoothedEnvelope;
    private TaskCompletionSource<bool>? _hideCompletionSource;

    public OverlayWindow()
    {
        InitializeComponent();

        _bars = new[]
        {
            Bar1,
            Bar2,
            Bar3,
            Bar4,
            Bar5
        };

        _waveformTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(1000d / 60d)
        };
        _waveformTimer.Tick += OnWaveformTick;

        Loaded += (_, _) => PositionAtBottomCenter(CapsuleBorder.Width);
    }

    public void ShowOverlay()
    {
        if (!IsVisible)
        {
            Show();
        }

        ResetAnimationState();
        PositionAtBottomCenter(CapsuleBorder.Width);
        StartEntryAnimation();
        _waveformTimer.Start();
    }

    public async Task HideOverlayAsync()
    {
        if (!IsVisible)
        {
            return;
        }

        if (_hideCompletionSource is not null)
        {
            await _hideCompletionSource.Task.ConfigureAwait(true);
            return;
        }

        _hideCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var storyboard = ((Storyboard)Resources["ExitStoryboard"]).Clone();
        storyboard.Completed += (_, _) =>
        {
            _waveformTimer.Stop();
            Hide();
            _hideCompletionSource?.TrySetResult(true);
            _hideCompletionSource = null;
            ResetBars();
        };

        storyboard.Begin(this, true);
        await _hideCompletionSource.Task.ConfigureAwait(true);
    }

    public void UpdateText(string text)
    {
        TranscriptText.Text = text ?? string.Empty;
        AnimateCapsuleWidth(CalculateTargetWidth(TranscriptText.Text));
    }

    public void ShowRefining()
    {
        UpdateText("Refining...");
    }

    public void UpdateRms(float rms)
    {
        _targetRms = Math.Clamp(rms, 0f, 1f);
    }

    private void ResetAnimationState()
    {
        Opacity = 1;
        RootScaleTransform.ScaleX = 0.6;
        RootScaleTransform.ScaleY = 0.6;
    }

    private void StartEntryAnimation()
    {
        var storyboard = ((Storyboard)Resources["EntryStoryboard"]).Clone();
        storyboard.Begin(this, true);
    }

    private void PositionAtBottomCenter(double width)
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Left + (workArea.Width - width) / 2;
        Top = workArea.Bottom - Height - 32;
    }

    private void AnimateCapsuleWidth(double targetWidth)
    {
        var currentWidth = double.IsNaN(CapsuleBorder.Width) ? targetWidth : CapsuleBorder.Width;
        var widthAnimation = new DoubleAnimation
        {
            From = currentWidth,
            To = targetWidth,
            Duration = TimeSpan.FromSeconds(0.25),
            EasingFunction = new QuadraticEase
            {
                EasingMode = EasingMode.EaseOut
            }
        };
        CapsuleBorder.BeginAnimation(WidthProperty, widthAnimation, HandoffBehavior.SnapshotAndReplace);

        var currentWindowWidth = double.IsNaN(Width) ? targetWidth : Width;
        var windowWidthAnimation = new DoubleAnimation
        {
            From = currentWindowWidth,
            To = targetWidth,
            Duration = TimeSpan.FromSeconds(0.25),
            EasingFunction = new QuadraticEase
            {
                EasingMode = EasingMode.EaseOut
            }
        };
        BeginAnimation(WidthProperty, windowWidthAnimation, HandoffBehavior.SnapshotAndReplace);

        var workArea = SystemParameters.WorkArea;
        var targetLeft = workArea.Left + (workArea.Width - targetWidth) / 2;
        var currentLeft = double.IsNaN(Left) ? targetLeft : Left;
        if (double.IsNaN(Left))
        {
            Left = currentLeft;
        }

        var leftAnimation = new DoubleAnimation
        {
            From = currentLeft,
            To = targetLeft,
            Duration = TimeSpan.FromSeconds(0.25),
            EasingFunction = new QuadraticEase
            {
                EasingMode = EasingMode.EaseOut
            }
        };

        BeginAnimation(LeftProperty, leftAnimation, HandoffBehavior.SnapshotAndReplace);
    }

    private double CalculateTargetWidth(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 280;
        }

        var formattedText = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(TranscriptText.FontFamily, TranscriptText.FontStyle, TranscriptText.FontWeight, TranscriptText.FontStretch),
            TranscriptText.FontSize,
            Brushes.Transparent,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        var textWidth = Math.Clamp(
            formattedText.WidthIncludingTrailingWhitespace + 6,
            TranscriptText.MinWidth,
            TranscriptText.MaxWidth);

        return Math.Clamp(textWidth + 98, 240, 660);
    }

    private void OnWaveformTick(object? sender, EventArgs e)
    {
        var scaledTarget = Math.Clamp(_targetRms * 6d, 0d, 1d);

        if (scaledTarget >= _smoothedEnvelope)
        {
            _smoothedEnvelope = scaledTarget * 0.4 + _smoothedEnvelope * 0.6;
        }
        else
        {
            _smoothedEnvelope = scaledTarget * 0.15 + _smoothedEnvelope * 0.85;
        }

        for (var index = 0; index < _bars.Length; index++)
        {
            var jitter = 1d + (_random.NextDouble() * 0.08d - 0.04d);
            var weighted = _smoothedEnvelope * BarWeights[index] * jitter;
            var height = Math.Clamp(4d + weighted * 28d, 4d, 32d);
            _bars[index].Height = height;
        }
    }

    private void ResetBars()
    {
        _targetRms = 0;
        _smoothedEnvelope = 0;

        foreach (var bar in _bars)
        {
            bar.Height = 4;
        }
    }
}

