using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace Naibao.Models;

/// <summary>网页快捷方式预设项。</summary>
public sealed class WebLinkItem : INotifyPropertyChanged
{
    private string _name = "";
    private string _url = "";

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public string Url
    {
        get => _url;
        set { _url = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>应用配置（保存在 %APPDATA%\naibao\config.json）。</summary>
public sealed class AppConfig
{
    /// <summary>topmost = 悬浮在最上层；tray = 隐藏到菜单栏（托盘）。</summary>
    public string DisplayMode { get; set; } = "topmost";

    /// <summary>开机自启动时的显示方式（topmost / tray）。</summary>
    public string StartupMode { get; set; } = "topmost";

    public bool AutoStart { get; set; }

    /// <summary>宠物窗口上次位置，null 表示使用默认右下角。</summary>
    public double? PetX { get; set; }

    public double? PetY { get; set; }

    /// <summary>宠物显示尺寸（像素）。</summary>
    public double PetSize { get; set; } = 160;

    public List<WebLinkItem> WebLinks { get; set; } = new();

    /// <summary>sound_message = 声音+消息；message_only = 仅消息；full_mute = 完全静音。</summary>
    public string ChimeMode { get; set; } = "sound_message";

    public string SoundPath { get; set; } = "";

    /// <summary>音量 0.0 - 1.0。</summary>
    public double Volume { get; set; } = 0.8;

    // ---------------- 动作与动画 ----------------

    /// <summary>大笑动作 GIF（左键点击触发）。</summary>
    public string LaughGifPath { get; set; } = DefaultGifPath("11.GIF");

    /// <summary>功夫动作 GIF（默认状态下随机触发）。</summary>
    public string KungFuGifPath { get; set; } = DefaultGifPath("22.GIF");

    /// <summary>睡懒觉动作 GIF（鼠标长时间无操作触发）。</summary>
    public string SleepGifPath { get; set; } = DefaultGifPath("33.GIF");

    /// <summary>伸懒腰起床动作 GIF（睡眠后再次移动鼠标触发）。</summary>
    public string WakeGifPath { get; set; } = DefaultGifPath("44.GIF");

    /// <summary>是否开启“功夫”随机触发。</summary>
    public bool KungFuEnabled { get; set; } = true;

    /// <summary>“功夫”随机触发最小间隔（分钟）。</summary>
    public double KungFuMinMinutes { get; set; } = 2;

    /// <summary>“功夫”随机触发最大间隔（分钟）。</summary>
    public double KungFuMaxMinutes { get; set; } = 5;

    /// <summary>鼠标无操作多少秒后进入“睡懒觉”。</summary>
    public double IdleSleepSeconds { get; set; } = 300;

    /// <summary>默认动作 GIF 路径：安装/解压目录下的 gifs 文件夹。</summary>
    private static string DefaultGifPath(string fileName)
        => Path.Combine(AppContext.BaseDirectory, "gifs", fileName);
}
