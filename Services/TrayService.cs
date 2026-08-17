using System.IO;
using System.Windows.Forms;
using Application = System.Windows.Application;
using MenuItem = System.Windows.Forms.ToolStripMenuItem;

namespace Naibao.Services;

/// <summary>系统托盘（菜单栏）图标与右键菜单。</summary>
public sealed class TrayService : IDisposable
{
    private readonly NotifyIcon _notify;
    private readonly Stream? _iconStream;

    public event Action? ShowPetRequested;
    public event Action? HidePetRequested;
    public event Action? SettingsRequested;
    public event Action? ExitRequested;

    public TrayService()
    {
        _notify = new NotifyIcon
        {
            Visible = true,
            Text = "naibao 桌面宠物"
        };

        var info = Application.GetResourceStream(new Uri("pack://application:,,,/assets/naibao.ico"));
        if (info?.Stream != null)
        {
            _iconStream = info.Stream;
            try
            {
                _notify.Icon = new System.Drawing.Icon(_iconStream);
            }
            catch
            {
                // 图标加载失败时托盘仍可用，只是没有图标。
            }
        }

        var menu = new ContextMenuStrip();
        menu.Items.Add(new MenuItem("显示宠物", null, (_, _) => ShowPetRequested?.Invoke()));
        menu.Items.Add(new MenuItem("隐藏宠物", null, (_, _) => HidePetRequested?.Invoke()));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new MenuItem("设置", null, (_, _) => SettingsRequested?.Invoke()));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new MenuItem("退出", null, (_, _) => ExitRequested?.Invoke()));
        _notify.ContextMenuStrip = menu;
        _notify.DoubleClick += (_, _) => ShowPetRequested?.Invoke();
    }

    public void Dispose()
    {
        _notify.Visible = false;
        _notify.Dispose();
        _iconStream?.Dispose();
    }
}
