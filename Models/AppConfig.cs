using System.ComponentModel;
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
}
