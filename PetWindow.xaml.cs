using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Naibao.Services;

namespace Naibao;

public partial class PetWindow : Window
{
    private const double DragThreshold = 6.0;

    private static readonly IntPtr HwndTopmost = new(-1);
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;

    private Point _dragStart;
    private bool _dragMoved;
    private bool _suppressLeftClick;
    private bool _suppressRightClick;
    private IntPtr _hwnd;

    private readonly DispatcherTimer _chimeCloseTimer = new() { Interval = TimeSpan.FromSeconds(6) };
    private readonly DispatcherTimer _topmostTimer = new() { Interval = TimeSpan.FromSeconds(1) };

    public PetWindow()
    {
        InitializeComponent();

        double size = App.CurrentConfig.PetSize;
        PetImage.Width = size;
        PetImage.Height = size;
        ShowDefaultImage();

        _chimeCloseTimer.Tick += (_, _) =>
        {
            _chimeCloseTimer.Stop();
            ChimePopup.IsOpen = false;
        };

        // 定期重断言 HWND_TOPMOST：修复被其他置顶窗口覆盖后无法自动回到最上层的问题。
        _topmostTimer.Tick += (_, _) =>
        {
            if (IsVisible && _hwnd != IntPtr.Zero)
            {
                SetWindowPos(_hwnd, HwndTopmost, 0, 0, 0, 0,
                    SwpNoMove | SwpNoSize | SwpNoActivate);
            }
        };

        SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            ReassertTopmost();
        };

        IsVisibleChanged += (_, _) => ReassertTopmost();
        _topmostTimer.Start();
    }

    private void ReassertTopmost()
    {
        if (IsVisible && _hwnd != IntPtr.Zero)
        {
            SetWindowPos(_hwnd, HwndTopmost, 0, 0, 0, 0,
                SwpNoMove | SwpNoSize | SwpNoActivate);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int x, int y, int cx, int cy, uint flags);

    // ---------------- 宠物形象 ----------------

    /// <summary>GIF 归一化渲染画布尺寸（显示尺寸的 2 倍，保证清晰度）。</summary>
    public int DisplayCanvasSize => (int)Math.Max(80, App.CurrentConfig.PetSize * 2);

    public void SetImageSource(BitmapSource source)
    {
        PetImage.Source = source;
    }

    public void ShowDefaultImage()
    {
        PetImage.Source = ImageMetrics.DefaultPet;
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

    // ---------------- 左键（大笑）与拖拽 ----------------

    private void PetImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(this);
        _dragMoved = false;
        _suppressLeftClick = MenuPopup.IsOpen || ChimePopup.IsOpen;
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
            MenuPopup.IsOpen = false;
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
        if (_dragMoved || _suppressLeftClick)
        {
            return;
        }

        // 左键点击：播放“大笑”动作。
        App.CurrentApp.Animator.TriggerLaugh();
    }

    // ---------------- 右键（泡泡菜单） ----------------

    private void PetImage_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        _suppressRightClick = MenuPopup.IsOpen || ChimePopup.IsOpen;
    }

    private void PetImage_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_suppressRightClick)
        {
            return;
        }

        if (MenuPopup.IsOpen)
        {
            MenuPopup.IsOpen = false;
        }
        else
        {
            OpenMenu();
        }
    }

    private void OpenMenu()
    {
        if (ChimePopup.IsOpen)
        {
            ChimePopup.IsOpen = false;
        }

        RefreshWebLinks();
        MenuPopup.IsOpen = true;
    }

    private void RefreshWebLinks()
    {
        WebLinkList.Items.Clear();
        var links = App.CurrentConfig.WebLinks;

        if (links.Count == 0)
        {
            WebLinkList.Items.Add(new TextBlock
            {
                Text = "暂无预设，请到设置中添加",
                Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0x6A, 0x90)),
                FontSize = 12,
                Margin = new Thickness(4, 2, 4, 4)
            });
            return;
        }

        var style = Application.Current.TryFindResource("BubbleLinkButtonStyle") as Style;
        foreach (var link in links)
        {
            var button = new Button
            {
                Content = $"🔗 {link.Name}",
                ToolTip = link.Url,
                Tag = link.Url
            };
            if (style != null)
            {
                button.Style = style;
            }

            button.Click += WebLinkItem_Click;
            WebLinkList.Items.Add(button);
        }
    }

    private void WebJumpButton_Click(object sender, RoutedEventArgs e)
    {
        bool show = WebLinkList.Visibility != Visibility.Visible;
        WebLinkList.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        RepositionMenuPopup();
    }

    private void RepositionMenuPopup()
    {
        if (!MenuPopup.IsOpen)
        {
            return;
        }

        // 展开/收起网页列表后重新定位，保证泡泡不跑出屏幕。
        MenuPopup.IsOpen = false;
        Dispatcher.BeginInvoke(new Action(() => MenuPopup.IsOpen = true), DispatcherPriority.Input);
    }

    private void WebLinkItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string url })
        {
            MenuPopup.IsOpen = false;
            WebLinkService.Open(url);
        }
    }

    private void ScreenshotButton_Click(object sender, RoutedEventArgs e)
    {
        MenuPopup.IsOpen = false;
        App.CurrentApp.StartScreenshot();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        MenuPopup.IsOpen = false;
        App.CurrentApp.ShowSettings();
    }

    private void HideButton_Click(object sender, RoutedEventArgs e)
    {
        MenuPopup.IsOpen = false;
        App.CurrentApp.HidePet();
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        MenuPopup.IsOpen = false;
        App.CurrentApp.ExitApp();
    }

    // ---------------- 整点报时气泡 ----------------

    public void ShowChime(string text)
    {
        if (MenuPopup.IsOpen)
        {
            MenuPopup.IsOpen = false;
        }

        ChimeText.Text = text;
        ChimePopup.IsOpen = true;
        _chimeCloseTimer.Stop();
        _chimeCloseTimer.Start();
    }

    private Point GetTargetScreenOrigin()
    {
        var point = PetImage.PointToScreen(new Point(0, 0));
        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget != null)
        {
            point = source.CompositionTarget.TransformFromDevice.Transform(point);
        }

        return point;
    }

    private CustomPopupPlacement[] PlaceChimePopup(Size popupSize, Size targetSize, Point offset)
    {
        // 自定义放置的返回坐标是“相对 PlacementTarget 左上角”，这里统一转成相对坐标。
        var origin = GetTargetScreenOrigin();
        var wa = SystemParameters.WorkArea;

        double x = (targetSize.Width - popupSize.Width) / 2;
        double aboveY = -popupSize.Height - 10;
        double belowY = targetSize.Height + 10;

        double absAbove = origin.Y + aboveY;
        double y = absAbove >= wa.Top + 8
            ? aboveY
            : Math.Min(belowY, wa.Bottom - popupSize.Height - 8 - origin.Y);

        double absX = origin.X + x;
        double clampedAbsX = Math.Max(wa.Left + 8,
            Math.Min(absX, wa.Right - popupSize.Width - 8));
        x = clampedAbsX - origin.X;

        return new[] { new CustomPopupPlacement(new Point(x, y), PopupPrimaryAxis.Vertical) };
    }

    private CustomPopupPlacement[] PlaceMenuPopup(Size popupSize, Size targetSize, Point offset)
    {
        var origin = GetTargetScreenOrigin();
        var wa = SystemParameters.WorkArea;

        // 泡泡底部贴近宠物“头顶”（默认形象内容的顶部），不遮挡宠物本体。
        double headTop = targetSize.Height * ImageMetrics.DefaultPetContentTopRatio;
        double x = (targetSize.Width - popupSize.Width) / 2;
        double aboveY = headTop - 10 - popupSize.Height;
        double belowY = targetSize.Height + 10;

        double absAbove = origin.Y + aboveY;
        double y = absAbove >= wa.Top + 8
            ? aboveY
            : Math.Min(belowY, wa.Bottom - popupSize.Height - 8 - origin.Y);

        double absX = origin.X + x;
        double clampedAbsX = Math.Max(wa.Left + 8,
            Math.Min(absX, wa.Right - popupSize.Width - 8));
        x = clampedAbsX - origin.X;

        return new[] { new CustomPopupPlacement(new Point(x, y), PopupPrimaryAxis.Vertical) };
    }
}
