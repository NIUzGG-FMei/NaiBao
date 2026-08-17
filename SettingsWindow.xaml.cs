using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Naibao.Models;
using Naibao.Services;

namespace Naibao;

public partial class SettingsWindow : Window
{
    private readonly ObservableCollection<WebLinkItem> _links = new();

    public SettingsWindow()
    {
        InitializeComponent();
        LoadConfig();
    }

    private void LoadConfig()
    {
        var cfg = App.CurrentConfig;

        var displayRb = cfg.DisplayMode == "tray" ? RbTray : RbTopmost;
        displayRb.IsChecked = true;

        CbAutoStart.IsChecked = cfg.AutoStart;

        CmbStartupMode.SelectedIndex = cfg.StartupMode == "tray" ? 1 : 0;

        _links.Clear();
        foreach (var l in cfg.WebLinks)
        {
            _links.Add(new WebLinkItem { Name = l.Name, Url = l.Url });
        }
        LvWebLinks.ItemsSource = _links;

        var chimeRb = cfg.ChimeMode switch
        {
            "message_only" => RbMessageOnly,
            "full_mute" => RbFullMute,
            _ => RbSoundMessage
        };
        chimeRb.IsChecked = true;

        TbSoundPath.Text = cfg.SoundPath;
        SlVolume.Value = Math.Clamp(cfg.Volume * 100, 0, 100);
        UpdateVolumeLabel();

        TbAbout.Text = $"naibao v1.0.0\n配置目录：{ConfigService.ConfigDir}";
    }

    // ---------------- 网页快捷方式 ----------------

    private void LvWebLinks_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LvWebLinks.SelectedItem is not WebLinkItem selected)
        {
            return;
        }

        TbName.Text = selected.Name;
        TbUrl.Text = selected.Url;
    }

    private void AddLinkButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadLinkInput(out var name, out var url))
        {
            return;
        }

        _links.Add(new WebLinkItem { Name = name, Url = url });
        TbName.Clear();
        TbUrl.Clear();
        LvWebLinks.SelectedItem = null;
        TbName.Focus();
    }

    private void SaveLinkButton_Click(object sender, RoutedEventArgs e)
    {
        if (LvWebLinks.SelectedItem is not WebLinkItem selected)
        {
            MessageBox.Show("请先在列表中选择要修改的项。", "naibao",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!TryReadLinkInput(out var name, out var url))
        {
            return;
        }

        selected.Name = name;
        selected.Url = url;
    }

    private void DeleteLinkButton_Click(object sender, RoutedEventArgs e)
    {
        if (LvWebLinks.SelectedItem is not WebLinkItem selected)
        {
            MessageBox.Show("请先在列表中选择要删除的项。", "naibao",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _links.Remove(selected);
        TbName.Clear();
        TbUrl.Clear();
    }

    private void UpLinkButton_Click(object sender, RoutedEventArgs e)
    {
        int i = LvWebLinks.SelectedIndex;
        if (i <= 0) return;
        _links.Move(i, i - 1);
        LvWebLinks.SelectedIndex = i - 1;
    }

    private void DownLinkButton_Click(object sender, RoutedEventArgs e)
    {
        int i = LvWebLinks.SelectedIndex;
        if (i < 0 || i >= _links.Count - 1) return;
        _links.Move(i, i + 1);
        LvWebLinks.SelectedIndex = i + 1;
    }

    private void TestLinkButton_Click(object sender, RoutedEventArgs e)
    {
        var url = TbUrl.Text.Trim();
        if (!WebLinkService.IsValidUrl(url))
        {
            MessageBox.Show("网址无效，请输入以 http:// 或 https:// 开头的完整网址。", "naibao",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        WebLinkService.Open(url);
    }

    private bool TryReadLinkInput(out string name, out string url)
    {
        name = TbName.Text.Trim();
        url = TbUrl.Text.Trim();

        if (name.Length == 0)
        {
            MessageBox.Show("请输入网页名称。", "naibao", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (!WebLinkService.IsValidUrl(url))
        {
            MessageBox.Show("网址无效，请输入以 http:// 或 https:// 开头的完整网址。", "naibao",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        return true;
    }

    // ---------------- 音效 ----------------

    private void BrowseSoundButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "选择报时音效",
            Filter = "音频文件 (*.mp3;*.wav)|*.mp3;*.wav|所有文件 (*.*)|*.*"
        };
        if (dlg.ShowDialog(this) == true)
        {
            TbSoundPath.Text = dlg.FileName;
        }
    }

    private void TestSoundButton_Click(object sender, RoutedEventArgs e)
    {
        var path = TbSoundPath.Text.Trim();
        if (path.Length == 0)
        {
            MessageBox.Show("请先选择音效文件。", "naibao", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        App.CurrentApp.PreviewSound(path, SlVolume.Value / 100.0);
    }

    private void SlVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateVolumeLabel();
    }

    private void UpdateVolumeLabel()
    {
        if (LblVolume != null)
        {
            LblVolume.Text = $"{SlVolume.Value:0}%";
        }
    }

    // ---------------- 保存 / 取消 ----------------

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        for (int i = 0; i < _links.Count; i++)
        {
            var link = _links[i];
            if (string.IsNullOrWhiteSpace(link.Name))
            {
                MessageBox.Show($"第 {i + 1} 项网页名称为空。", "naibao",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!WebLinkService.IsValidUrl(link.Url))
            {
                MessageBox.Show($"第 {i + 1} 项“{link.Name}”的网址无效，请输入以 http:// 或 https:// 开头的完整网址。",
                    "naibao", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        var cfg = App.CurrentConfig;
        cfg.DisplayMode = RbTray.IsChecked == true ? "tray" : "topmost";
        cfg.StartupMode = (CmbStartupMode.SelectedItem as ComboBoxItem)?.Tag as string ?? "topmost";
        cfg.AutoStart = CbAutoStart.IsChecked == true;
        cfg.WebLinks = _links.Select(l => new WebLinkItem { Name = l.Name, Url = l.Url }).ToList();
        cfg.ChimeMode = RbMessageOnly.IsChecked == true
            ? "message_only"
            : RbFullMute.IsChecked == true ? "full_mute" : "sound_message";
        cfg.SoundPath = TbSoundPath.Text.Trim();
        cfg.Volume = SlVolume.Value / 100.0;

        ConfigService.Save(cfg);
        AutoStartService.SetEnabled(cfg.AutoStart);
        App.CurrentApp.ApplySettingsChanged();

        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    // ---------------- 关于 ----------------

    private void OpenConfigDirButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(ConfigService.ConfigDir);
            Process.Start(new ProcessStartInfo(ConfigService.ConfigDir) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"打开配置目录失败：{ex.Message}", "naibao",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
