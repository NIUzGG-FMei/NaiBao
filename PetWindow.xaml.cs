using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Naibao.Services;

namespace Naibao;

public partial class PetWindow : Window
{
    private const double DragThreshold = 6.0;

    private Point _dragStart;
    private bool _dragMoved;
    private bool _suppressClick;

    private readonly DispatcherTimer _chimeCloseTimer = new() { Interval = TimeSpan.FromSeconds(6) };

    public PetWindow()
    {
        InitializeComponent();

        double size = App.CurrentConfig.PetSize;
        PetImage.Width = size;
        PetImage.Height = size;

        _chimeCloseTimer.Tick += (_, _) =>
        {
            _chimeCloseTimer.Stop();
            ChimePopup.IsOpen = false;
        };
    }

    // ---------------- 位置 ----------------

    public void RestorePosition()
    {
        var cfg = App.CurrentConfig;
        double left;
        double top;

        if (cfg.PetX.HasValue && cfg.PetY.HasValue && IsPositionVisible(cfg.PetX.Value, cfg.PetY.Value))
        {
            left = cfg.PetX.Value;
            top = cfg.PetY.Value;
        }
        else
        {
            // 默认：屏幕工作区右下角。
            left = SystemParameters.WorkArea.Right - cfg.PetSize - 24;
            top = SystemParameters.WorkArea.Bottom - cfg.PetSize - 24;
        }

        Left = left;
        Top = top;
    }

    public void SavePosition()
    {
        var cfg = App.CurrentConfig;
        cfg.PetX = Left;
        cfg.PetY = Top;
        ConfigService.Save(cfg);
    }

    private static bool IsPositionVisible(double x, double y)
    {
        const double margin = 40;
        return x >= SystemParameters.VirtualScreenLeft - 120 + margin
               && y >= SystemParameters.VirtualScreenTop - 120 + margin
               && x <= SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - margin
               && y <= SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - margin;
    }

    // ---------------- 拖拽与单击 ----------------

    private void PetImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(this);
        _dragMoved = false;
        _suppressClick = PetMenu.IsOpen || ChimePopup.IsOpen;
    }

    private void PetImage_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragMoved)
        {
            return;
        }

        var pos = e.GetPosition(this);
        if (Math.Abs(pos.X - _dragStart.X) >= DragThreshold
            || Math.Abs(pos.Y - _dragStart.Y) >= DragThreshold)
        {
            _dragMoved = true;
            PetMenu.IsOpen = false;
            ChimePopup.IsOpen = false;
            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
                // 鼠标在 DragMove 期间被释放时会抛出该异常，直接忽略。
            }

            SavePosition();
        }
    }

    private void PetImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragMoved || _suppressClick)
        {
            return;
        }

        PetMenu.IsOpen = true; // 单击宠物弹出菜单。
    }

    // ---------------- 菜单 ----------------

    private void PetMenu_Opened(object sender, RoutedEventArgs e)
    {
        // 菜单每次打开时动态生成 n 个网页跳转选项：有几个预设就显示几个。
        RefreshWebLinkMenuItems();
    }

    private void RefreshWebLinkMenuItems()
    {
        WebJumpMenuItem.Items.Clear();
        var links = App.CurrentConfig.WebLinks;

        if (links.Count == 0)
        {
            WebJumpMenuItem.Items.Add(new MenuItem
            {
                Header = "暂无预设，请到设置中添加",
                IsEnabled = false
            });
            return;
        }

        foreach (var link in links)
        {
            var item = new MenuItem
            {
                Header = link.Name,
                ToolTip = link.Url,
                Tag = link.Url
            };
            item.Click += WebLinkItem_Click;
            WebJumpMenuItem.Items.Add(item);
        }
    }

    private void WebLinkItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: string url })
        {
            PetMenu.IsOpen = false;
            WebLinkService.Open(url);
        }
    }

    private void ScreenshotItem_Click(object sender, RoutedEventArgs e)
    {
        PetMenu.IsOpen = false;
        App.CurrentApp.StartScreenshot();
    }

    private void SettingsItem_Click(object sender, RoutedEventArgs e)
    {
        PetMenu.IsOpen = false;
        App.CurrentApp.ShowSettings();
    }

    private void HideItem_Click(object sender, RoutedEventArgs e)
    {
        PetMenu.IsOpen = false;
        App.CurrentApp.HidePet();
    }

    private void ExitItem_Click(object sender, RoutedEventArgs e)
    {
        PetMenu.IsOpen = false;
        App.CurrentApp.ExitApp();
    }

    // ---------------- 整点报时气泡 ----------------

    public void ShowChime(string text)
    {
        if (PetMenu.IsOpen)
        {
            PetMenu.IsOpen = false;
        }

        ChimeText.Text = text;
        ChimePopup.IsOpen = true;
        _chimeCloseTimer.Stop();
        _chimeCloseTimer.Start();
    }

    private CustomPopupPlacement[] PlaceChimePopup(Size popupSize, Size targetSize, Point offset)
    {
        var wa = SystemParameters.WorkArea;

        double x = Math.Max(wa.Left + 8,
            Math.Min(offset.X + targetSize.Width / 2 - popupSize.Width / 2, wa.Right - popupSize.Width - 8));

        double above = offset.Y - popupSize.Height - 10;
        double below = offset.Y + targetSize.Height + 10;

        double y = above >= wa.Top + 8
            ? above
            : Math.Min(below, wa.Bottom - popupSize.Height - 8);

        return new[] { new CustomPopupPlacement(new Point(x, y), PopupPrimaryAxis.Vertical) };
    }
}
