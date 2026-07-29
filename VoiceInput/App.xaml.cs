using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Hardcodet.Wpf.TaskbarNotification;
using VoiceInput.Services;
using VoiceInput.UI;

namespace VoiceInput;

public partial class App
{
    private static readonly (string Code, string Label)[] Languages =
    {
        ("en-US", "English"),
        ("zh-CN", "简体中文"),
        ("zh-TW", "繁體中文"),
        ("ja-JP", "日本語"),
        ("ko-KR", "한국어")
    };

    private readonly SemaphoreSlim _pipelineLock = new(1, 1);
    private readonly Dictionary<string, MenuItem> _languageItems = new(StringComparer.OrdinalIgnoreCase);

    private MainWindow? _mainWindow;
    private OverlayWindow? _overlayWindow;
    private SettingsWindow? _settingsWindow;
    private TaskbarIcon? _trayIcon;
    private MenuItem? _llmToggleItem;

    private ConfigService? _configService;
    private LowLevelKeyboardHook? _keyboardHook;
    private AudioRecorder? _audioRecorder;
    private SpeechTranscriptionService? _speechService;
    private CloudflareTranscriptionService? _cloudflareService;
    private SegmentedTranscriptionCoordinator? _segmentCoordinator;
    private LlmRefinementService? _llmService;
    private TextInjector? _textInjector;

    /// <summary>
    /// 本轮录音使用的供应商。按下热键时锁定，避免录音期间改配置导致停止流程走错分支。
    /// </summary>
    private bool _currentRoundUsesCloudflare;

    private bool _isRecording;
    private volatile bool _suppressHotkeyTrigger;

    protected override void OnStartup(StartupEventArgs e)
    {
        AppLogger.Info("应用启动。");
        RegisterGlobalExceptionHandlers();
        base.OnStartup(e);

        _configService = new ConfigService();
        var config = _configService.Load();

        _mainWindow = new MainWindow();
        _mainWindow.Show();
        _mainWindow.Hide();

        _overlayWindow = new OverlayWindow();

        _audioRecorder = new AudioRecorder();
        _audioRecorder.RmsChanged += OnAudioRms;
        _audioRecorder.ErrorOccurred += ShowErrorBalloon;

        _speechService = new SpeechTranscriptionService(_configService);
        _speechService.SetLanguage(config.Language);
        _speechService.InterimTextUpdated += text => Dispatcher.BeginInvoke(() => _overlayWindow?.UpdateText(text));
        _speechService.ErrorOccurred += ShowErrorBalloon;

        _cloudflareService = new CloudflareTranscriptionService(_configService);
        _cloudflareService.ErrorOccurred += ShowErrorBalloon;

        _segmentCoordinator = new SegmentedTranscriptionCoordinator(_audioRecorder, _cloudflareService);
        _segmentCoordinator.PartialTextUpdated += text =>
            Dispatcher.BeginInvoke(() => _overlayWindow?.UpdateText(text));

        _llmService = new LlmRefinementService(_configService);
        _llmService.SetEnabled(config.LlmEnabled);

        _textInjector = new TextInjector(Dispatcher);

        SetupTrayIcon();

        if (!HasSpeechConfig(config))
        {
            Dispatcher.BeginInvoke(OpenSettingsWindow);
        }

        try
        {
            _keyboardHook = new LowLevelKeyboardHook(config.FallbackHotkey);
            _keyboardHook.RecordingStarted += (_, _) => _ = HandleRecordingStartedAsync();
            _keyboardHook.RecordingStopped += (_, _) => _ = HandleRecordingStoppedAsync();
            _keyboardHook.Start();
        }
        catch (Exception ex)
        {
            AppLogger.Error("键盘钩子启动失败。", ex);
            ShowErrorBalloon($"键盘钩子启动失败: {ex.Message}");
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        AppLogger.Info("应用退出。");
        UnregisterGlobalExceptionHandlers();

        _keyboardHook?.Dispose();
        _audioRecorder?.Dispose();
        _speechService?.Dispose();
        _cloudflareService?.Dispose();
        _llmService?.Dispose();
        _trayIcon?.Dispose();

        _settingsWindow?.Close();
        _overlayWindow?.Close();

        _pipelineLock.Dispose();

        base.OnExit(e);
    }

    private void SetupTrayIcon()
    {
        _trayIcon = (TaskbarIcon)FindResource("AppTrayIcon");
        _trayIcon.ContextMenu = BuildTrayMenu();
        RefreshMenuChecks();
        AppLogger.Info("托盘图标已初始化。");
    }

    private ContextMenu BuildTrayMenu()
    {
        var menu = new ContextMenu();

        var languageSubMenu = new MenuItem
        {
            Header = "Language"
        };

        foreach (var language in Languages)
        {
            var item = new MenuItem
            {
                Header = language.Label,
                Tag = language.Code,
                IsCheckable = true
            };
            item.Click += OnLanguageMenuClick;
            _languageItems[language.Code] = item;
            languageSubMenu.Items.Add(item);
        }

        menu.Items.Add(languageSubMenu);

        _llmToggleItem = new MenuItem
        {
            Header = "LLM Refinement",
            IsCheckable = true
        };
        _llmToggleItem.Click += OnLlmToggleClick;
        menu.Items.Add(_llmToggleItem);

        menu.Items.Add(new Separator());

        var settingsItem = new MenuItem
        {
            Header = "Settings"
        };
        settingsItem.Click += (_, _) => OpenSettingsWindow();
        menu.Items.Add(settingsItem);

        var openConfigDirItem = new MenuItem
        {
            Header = "Open Config Folder"
        };
        openConfigDirItem.Click += (_, _) => OpenConfigFolder();
        menu.Items.Add(openConfigDirItem);

        var exitItem = new MenuItem
        {
            Header = "Exit"
        };
        exitItem.Click += (_, _) => Shutdown();
        menu.Items.Add(exitItem);

        return menu;
    }

    private void OnLanguageMenuClick(object sender, RoutedEventArgs e)
    {
        if (_configService is null || _speechService is null)
        {
            return;
        }

        if (sender is not MenuItem menuItem || menuItem.Tag is not string language)
        {
            return;
        }

        _configService.Update(config => config.Language = language);
        _speechService.SetLanguage(language);
        RefreshMenuChecks();
    }

    private void OnLlmToggleClick(object sender, RoutedEventArgs e)
    {
        if (_configService is null || _llmService is null)
        {
            return;
        }

        var enabled = !_configService.Current.LlmEnabled;
        _configService.Update(config => config.LlmEnabled = enabled);
        _llmService.SetEnabled(enabled);
        RefreshMenuChecks();
    }

    private void RefreshMenuChecks()
    {
        if (_configService is null)
        {
            return;
        }

        var currentLanguage = _configService.Current.Language;
        foreach (var languageItem in _languageItems)
        {
            languageItem.Value.IsChecked = string.Equals(
                languageItem.Key,
                currentLanguage,
                StringComparison.OrdinalIgnoreCase);
        }

        if (_llmToggleItem is not null)
        {
            _llmToggleItem.IsChecked = _configService.Current.LlmEnabled;
        }
    }

    private void OpenSettingsWindow()
    {
        if (_settingsWindow is { IsVisible: true })
        {
            _settingsWindow.Activate();
            return;
        }

        if (_configService is null || _llmService is null || _cloudflareService is null)
        {
            return;
        }

        _settingsWindow = new SettingsWindow(
            _configService,
            _llmService,
            _cloudflareService,
            key => _keyboardHook?.SetFallbackHotkey(key),
            isRecordingHotkey => _suppressHotkeyTrigger = isRecordingHotkey)
        {
            Owner = _mainWindow
        };
        _settingsWindow.Closed += (_, _) =>
        {
            _suppressHotkeyTrigger = false;
            _settingsWindow = null;
        };
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private void OnTrayMouseDoubleClick(object sender, RoutedEventArgs e)
    {
        OpenSettingsWindow();
    }
    private void OpenConfigFolder()
    {
        if (_configService is null)
        {
            return;
        }

        Directory.CreateDirectory(_configService.ConfigDirectory);
        Process.Start(new ProcessStartInfo
        {
            FileName = _configService.ConfigDirectory,
            UseShellExecute = true
        });
    }

    private async Task HandleRecordingStartedAsync()
    {
        if (_suppressHotkeyTrigger)
        {
            return;
        }

        await _pipelineLock.WaitAsync().ConfigureAwait(false);

        try
        {
            if (_suppressHotkeyTrigger)
            {
                return;
            }

            if (_isRecording)
            {
                return;
            }

            if (_configService is null ||
                _speechService is null ||
                _audioRecorder is null ||
                _overlayWindow is null ||
                _segmentCoordinator is null)
            {
                return;
            }

            var config = _configService.Load();
            _speechService.SetLanguage(config.Language);
            _keyboardHook?.SetFallbackHotkey(config.FallbackHotkey);

            if (!HasSpeechConfig(config))
            {
                var missingHint = config.UsesCloudflare
                    ? "请先在 Settings 中配置 Cloudflare Account ID 与 API Token。"
                    : "请先在 Settings 中配置 Azure Speech Key 与 Region。";

                await Dispatcher.InvokeAsync(() =>
                {
                    OpenSettingsWindow();
                    ShowErrorBalloon(missingHint);
                });
                return;
            }

            _currentRoundUsesCloudflare = config.UsesCloudflare;
            _isRecording = true;

            await Dispatcher.InvokeAsync(() =>
            {
                _overlayWindow.UpdateText(string.Empty);
                _overlayWindow.UpdateRms(0);
                _overlayWindow.ShowOverlay();
            });

            if (_currentRoundUsesCloudflare)
            {
                // whisper 无法流式，改为缓冲 + VAD 分段：说话停顿处切段并行上传，
                // 松键时只剩最后一段待返回，等待时间不再随录音时长线性增长。
                _segmentCoordinator.Start(config.Language);
                _audioRecorder.StartBuffering();
            }
            else
            {
                var stream = _audioRecorder.StartStreaming();
                await _speechService.StartAsync(stream).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _isRecording = false;
            AppLogger.Error("开始录音流程异常。", ex);
            ShowErrorBalloon($"开始录音失败: {ex.Message}");
            await HideOverlayOnUiAsync().ConfigureAwait(false);
        }
        finally
        {
            _pipelineLock.Release();
        }
    }

    private async Task HandleRecordingStoppedAsync()
    {
        if (_suppressHotkeyTrigger && !_isRecording)
        {
            return;
        }

        await _pipelineLock.WaitAsync().ConfigureAwait(false);

        try
        {
            if (_suppressHotkeyTrigger && !_isRecording)
            {
                return;
            }

            if (!_isRecording)
            {
                return;
            }

            if (_audioRecorder is null ||
                _speechService is null ||
                _cloudflareService is null ||
                _overlayWindow is null ||
                _textInjector is null)
            {
                return;
            }

            _isRecording = false;
            _audioRecorder.Stop();

            string transcript;
            if (_currentRoundUsesCloudflare)
            {
                if (_segmentCoordinator is null)
                {
                    return;
                }

                // 已完成的分段此时已显示在胶囊上，只有末段还在飞时才提示，避免覆盖已有文本。
                await Dispatcher.InvokeAsync(_overlayWindow.ShowTranscribingIfEmpty);

                transcript = await _segmentCoordinator.CompleteAsync().ConfigureAwait(false);
            }
            else
            {
                transcript = await _speechService.StopAsync().ConfigureAwait(false);
            }

            if (string.IsNullOrWhiteSpace(transcript))
            {
                await HideOverlayOnUiAsync().ConfigureAwait(false);
                return;
            }

            await Dispatcher.InvokeAsync(() => _overlayWindow.UpdateText(transcript));

            var finalText = transcript;
            if (_llmService is { IsEnabled: true })
            {
                await Dispatcher.InvokeAsync(() => _overlayWindow.ShowRefining());
                finalText = await _llmService.RefineAsync(transcript).ConfigureAwait(false);
                await Dispatcher.InvokeAsync(() => _overlayWindow.UpdateText(finalText));
            }

            try
            {
                await _textInjector.InjectAsync(finalText).ConfigureAwait(false);
            }
            catch (ExternalException ex)
            {
                // 剪贴板被其他进程长期占用时注入会失败。转写结果已经拿到，
                // 写进日志让用户还能找回，而不是笼统报「录音流程失败」。
                AppLogger.Error($"文本注入失败，转写结果: {finalText}", ex);
                ShowErrorBalloon("剪贴板被占用，注入失败。文本已记入 voiceinput.log。");
            }

            await HideOverlayOnUiAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _isRecording = false;
            AppLogger.Error("停止录音流程异常。", ex);
            ShowErrorBalloon($"录音流程失败: {ex.Message}");
            await HideOverlayOnUiAsync().ConfigureAwait(false);
        }
        finally
        {
            _pipelineLock.Release();
        }
    }

    /// <summary>
    /// 音频帧 RMS 回调，运行在 NAudio 录音线程。
    /// 波形更新需切回 UI 线程；分段判断留在录音线程，避免 UI 繁忙时错过切段时机。
    /// </summary>
    private void OnAudioRms(float rms)
    {
        Dispatcher.BeginInvoke(() => _overlayWindow?.UpdateRms(rms));

        if (_currentRoundUsesCloudflare)
        {
            _segmentCoordinator?.OnAudioFrame(rms, AudioRecorder.FrameDuration);
        }
    }

    private Task HideOverlayOnUiAsync()
    {
        if (_overlayWindow is null)
        {
            return Task.CompletedTask;
        }

        return Dispatcher.InvokeAsync(() => _overlayWindow.HideOverlayAsync()).Task.Unwrap();
    }

    private void ShowErrorBalloon(string message)
    {
        if (_trayIcon is null)
        {
            return;
        }

        Dispatcher.Invoke(() =>
        {
            _trayIcon.ShowBalloonTip("VoiceInput", message, BalloonIcon.Error);
        });
    }

    private static bool HasSpeechConfig(Models.AppConfig config)
    {
        return config.UsesCloudflare
            ? !string.IsNullOrWhiteSpace(config.CloudflareAccountId) &&
              !string.IsNullOrWhiteSpace(config.CloudflareApiToken)
            : !string.IsNullOrWhiteSpace(config.AzureSpeechKey) &&
              !string.IsNullOrWhiteSpace(config.AzureSpeechRegion);
    }

    private void RegisterGlobalExceptionHandlers()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void UnregisterGlobalExceptionHandlers()
    {
        DispatcherUnhandledException -= OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException -= OnCurrentDomainUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        AppLogger.Error("UI 线程未处理异常。", e.Exception);
        e.Handled = true;
        ShowErrorBalloon("程序内部异常，已记录日志。请查看 voiceinput.log。");
    }

    private void OnCurrentDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception;
        AppLogger.Error("应用域未处理异常。", ex);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        AppLogger.Error("任务未观察异常。", e.Exception);
        e.SetObserved();
    }
}


