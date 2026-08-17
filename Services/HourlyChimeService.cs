using System.Windows.Threading;

namespace Naibao.Services;

/// <summary>
/// 北京时间整点报时调度。每 250ms 检查一次，整点后 30 秒内触发一次，
/// 系统睡眠唤醒后自动继续工作，已过整点不补报。
/// </summary>
public sealed class HourlyChimeService
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private int _lastFiredHour = -1;

    private static readonly TimeZoneInfo Beijing = CreateBeijingTimeZone();

    public event Action<DateTime>? Chime;

    public void Start()
    {
        _timer.Tick += OnTick;
        _timer.Start();
    }

    public void Stop()
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Beijing);
        if (now.Minute == 0 && now.Second <= 30 && now.Hour != _lastFiredHour)
        {
            _lastFiredHour = now.Hour;
            Chime?.Invoke(now);
        }
    }

    private static TimeZoneInfo CreateBeijingTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");
        }
        catch
        {
            // 极少数系统缺少该时区 ID 时，回退为固定 UTC+8。
            return TimeZoneInfo.CreateCustomTimeZone("UTC+8 (Beijing)", TimeSpan.FromHours(8),
                "UTC+8 (Beijing)", "UTC+8 (Beijing)");
        }
    }
}
