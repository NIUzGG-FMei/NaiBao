using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Threading;

namespace Naibao.Services;

public enum PetState
{
    Default,
    Laughing,
    KungFu,
    Sleeping,
    Waking
}

/// <summary>
/// 宠物动作状态机：
/// 默认 → 左键大笑 / 随机功夫；默认 → 长时间无操作睡觉；睡觉 → 鼠标动了起床；
/// 功夫只在默认状态下触发。
/// </summary>
public sealed class PetAnimationController : IDisposable
{
    private readonly PetWindow _pet;
    private readonly GifPlayer _player = new();
    private readonly DispatcherTimer _kungFuTimer = new();
    private readonly DispatcherTimer _idleTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly Random _random = new();

    private Action? _onPlayerCompleted;

    public PetState State { get; private set; } = PetState.Default;

    public event Action<PetState>? StateChanged;

    public PetAnimationController(PetWindow pet)
    {
        _pet = pet;
        _player.FrameReady += frame => _pet.SetImageSource(frame);
        _player.Completed += OnPlayerCompleted;
        _kungFuTimer.Tick += OnKungFuTimerTick;
        _idleTimer.Tick += OnIdleTimerTick;
    }

    public void Start()
    {
        _idleTimer.Start();
        ApplyConfig();
    }

    /// <summary>设置保存后重新应用随机间隔与开关。</summary>
    public void ApplyConfig()
    {
        ScheduleKungFu();
    }

    /// <summary>左键点击：默认状态下播放“大笑”。</summary>
    public void TriggerLaugh()
    {
        if (State != PetState.Default)
        {
            return;
        }

        PlayGif(PetState.Laughing, App.CurrentConfig.LaughGifPath,
            freezeOnEnd: false, useLastFrameAsReference: false,
            onCompleted: () => SetState(PetState.Default));
    }

    private void ScheduleKungFu()
    {
        var cfg = App.CurrentConfig;
        if (!cfg.KungFuEnabled)
        {
            _kungFuTimer.Stop();
            return;
        }

        double min = Math.Min(cfg.KungFuMinMinutes, cfg.KungFuMaxMinutes);
        double max = Math.Max(cfg.KungFuMinMinutes, cfg.KungFuMaxMinutes);
        double minutes = min + _random.NextDouble() * (max - min);
        _kungFuTimer.Interval = TimeSpan.FromMinutes(minutes);
        _kungFuTimer.Start();
    }

    private void OnKungFuTimerTick(object? sender, EventArgs e)
    {
        _kungFuTimer.Stop();

        // 只在默认站姿时做功夫动作；大笑/睡觉/起床期间不触发。
        if (State == PetState.Default)
        {
            PlayGif(PetState.KungFu, App.CurrentConfig.KungFuGifPath,
                freezeOnEnd: false, useLastFrameAsReference: false,
                onCompleted: () => SetState(PetState.Default));
        }

        // 无论是否触发，都重新随机下一次间隔。
        ScheduleKungFu();
    }

    private void OnIdleTimerTick(object? sender, EventArgs e)
    {
        double idleSeconds = GetIdleSeconds();
        double threshold = App.CurrentConfig.IdleSleepSeconds;

        if (State == PetState.Default && idleSeconds >= threshold)
        {
            StartSleeping();
        }
        else if (State == PetState.Sleeping && idleSeconds < threshold)
        {
            StartWaking();
        }
    }

    private void StartSleeping()
    {
        PlayGif(PetState.Sleeping, App.CurrentConfig.SleepGifPath,
            freezeOnEnd: true, useLastFrameAsReference: false,
            onCompleted: null); // 睡姿保持最后一帧，状态仍为 Sleeping。
    }

    private void StartWaking()
    {
        PlayGif(PetState.Waking, App.CurrentConfig.WakeGifPath,
            freezeOnEnd: false, useLastFrameAsReference: true,
            onCompleted: () =>
            {
                _pet.ShowDefaultImage(); // 起床末帧 = 默认形象，最终切回静态默认图。
                SetState(PetState.Default);
            });
    }

    private void PlayGif(PetState state, string? path, bool freezeOnEnd,
        bool useLastFrameAsReference, Action? onCompleted)
    {
        _player.Stop();
        _onPlayerCompleted = onCompleted;
        SetState(state);

        bool started = _player.Play(path, _pet.DisplayCanvasSize, freezeOnEnd, useLastFrameAsReference);
        if (!started)
        {
            // GIF 缺失或读取失败：回到默认形象，保证功能可用。
            _onPlayerCompleted = null;
            _pet.ShowDefaultImage();
            SetState(PetState.Default);
        }
    }

    private void OnPlayerCompleted()
    {
        var action = _onPlayerCompleted;
        _onPlayerCompleted = null;
        action?.Invoke();
    }

    private void SetState(PetState state)
    {
        if (State == state)
        {
            return;
        }

        State = state;
        StateChanged?.Invoke(state);
    }

    // ---------------- 系统输入空闲检测 ----------------

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LastInputInfo plii);

    private static double GetIdleSeconds()
    {
        var info = new LastInputInfo { cbSize = (uint)Marshal.SizeOf<LastInputInfo>() };
        if (!GetLastInputInfo(ref info))
        {
            return 0;
        }

        uint now = (uint)Environment.TickCount;
        return (now - info.dwTime) / 1000.0;
    }

    public void Dispose()
    {
        _kungFuTimer.Stop();
        _idleTimer.Stop();
        _player.Stop();
        _player.Dispose();
    }
}
