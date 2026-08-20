using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Naibao.Models;
using Naibao.Services;

namespace Naibao;

public partial class App : Application
{
    private const string MutexName = "naibao_single_instance_mutex";
    private const string ShowEventName = "naibao_show_event";

    private Mutex? _mutex;
    private EventWaitHandle? _showEvent;
    private bool _isPrimaryInstance;

    private TrayService? _tray;
    private PetWindow? _petWindow;
    private SettingsWindow? _settingsWindow;
    private HourlyChimeService? _chime;
    private SoundService? _sound;
    private PetAnimationController? _animator;
    private bool _petVisibleBeforeScreenshot;

    public static AppConfig CurrentConfig { get; private set; } = new();

    public static App CurrentApp => (App)Current;

    /// <summary>宠物动作状态机（大笑 / 功夫 / 睡懒觉 / 起床）。</summary>
    public PetAnimationController Animator => _animator!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mutex = new Mutex(true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            // 已有实例在运行：通知它显示宠物，然后本实例退出。
            SignalExistingInstance();
            Shutdown();
            return;
        }

        _isPrimaryInstance = true;
        CurrentConfig = ConfigService.Load();

        bool autostart = e.Args.Any(a => string.Equals(a, "--autostart", StringComparison.OrdinalIgnoreCase));

        _sound = new SoundService();
        _petWindow = new PetWindow();
        _petWindow.RestorePosition();

        _animator = new PetAnimationController(_petWindow);
        _animator.Start();

        _tray = new TrayService();
        _tray.ShowPetRequested += ShowPet;
        _tray.HidePetRequested += HidePet;
        _tray.SettingsRequested += ShowSettings;
        _tray.ExitRequested += ExitApp;

        _chime = new HourlyChimeService();
        _chime.Chime += OnHourlyChime;
        _chime.Start();

        StartShowSignalListener();

        // 开机自启动时按“启动时显示方式”，平时按当前显示模式。
        string mode = autostart ? CurrentConfig.StartupMode : CurrentConfig.DisplayMode;
        if (mode == "topmost")
        {
            ShowPet();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // 只有主实例才写配置，避免第二个实例用默认配置覆盖用户配置。
        if (_isPrimaryInstance)
        {
            try { _petWindow?.SavePosition(); } catch { /* 忽略退出时保存异常 */ }
            try { ConfigService.Save(CurrentConfig); } catch { /* 忽略退出时保存异常 */ }
        }

        _chime?.Stop();
        _sound?.Dispose();
        _animator?.Dispose();
        _tray?.Dispose();

        try { _mutex?.ReleaseMutex(); } catch { /* 未持有或已释放 */ }

        base.OnExit(e);
    }

    // ---------------- 宠物显示控制 ----------------

    public void ShowPet()
    {
        if (_petWindow == null) return;
        _petWindow.Topmost = true;
        _petWindow.Show();
    }

    public void HidePet()
    {
        _petWindow?.Hide();
    }

    public void ApplySettingsChanged()
    {
        if (CurrentConfig.DisplayMode == "tray")
        {
            HidePet();
        }
        else
        {
            ShowPet();
        }

        _animator?.ApplyConfig();
    }

    // ---------------- 设置 / 退出 ----------------

    public void ShowSettings()
    {
        if (_settingsWindow != null && _settingsWindow.IsVisible)
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow();
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    public void ExitApp() => Shutdown();

    // ---------------- 截图 ----------------

    public void StartScreenshot()
    {
        if (_petWindow == null) return;
        _petVisibleBeforeScreenshot = _petWindow.IsVisible;
        _ = ScreenshotService.InvokeAsync(HidePet, RestoreAfterScreenshot);
    }

    private void RestoreAfterScreenshot()
    {
        if (_petVisibleBeforeScreenshot)
        {
            ShowPet();
        }
    }

    // ---------------- 整点报时 ----------------

    private void OnHourlyChime(DateTime beijingNow)
    {
        var cfg = CurrentConfig;
        if (cfg.ChimeMode == "full_mute")
        {
            return; // 完全静音：无消息、无声音。
        }

        if (cfg.ChimeMode == "sound_message")
        {
            _sound?.Play(cfg.SoundPath, cfg.Volume); // 未设置音效文件时自动降级为仅消息。
        }

        bool wasHidden = _petWindow == null || !_petWindow.IsVisible;
        if (wasHidden)
        {
            ShowPet(); // 托盘模式下也临时弹出报时消息。
        }

        _petWindow?.ShowChime($"叮咚～ 现在是北京时间 {beijingNow:HH:mm}");

        if (wasHidden)
        {
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(7) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                if (CurrentConfig.DisplayMode == "tray" && _petWindow != null && _petWindow.IsVisible)
                {
                    _petWindow.Hide();
                }
            };
            timer.Start();
        }
    }

    public void PreviewSound(string? path, double volume)
    {
        _sound?.Play(path, volume);
    }

    // ---------------- 单实例 ----------------

    private void StartShowSignalListener()
    {
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        var thread = new Thread(() =>
        {
            while (_showEvent != null)
            {
                try
                {
                    if (_showEvent.WaitOne())
                    {
                        Dispatcher.BeginInvoke(ShowPet);
                    }
                }
                catch
                {
                    return;
                }
            }
        })
        {
            IsBackground = true
        };
        thread.Start();
    }

    private static void SignalExistingInstance()
    {
        for (int i = 0; i < 10; i++)
        {
            try
            {
                using var evt = EventWaitHandle.OpenExisting(ShowEventName);
                evt.Set();
                return;
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                Thread.Sleep(100); // 主实例可能还没创建事件，稍等重试。
            }
        }
    }
}
