using System.Diagnostics;
using System.Windows;

namespace Naibao.Services;

public static class WebLinkService
{
    public static bool IsValidUrl(string? url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    /// <summary>用系统默认浏览器打开网址。</summary>
    public static void Open(string? url)
    {
        if (!IsValidUrl(url))
        {
            MessageBox.Show("网址无效，请输入以 http:// 或 https:// 开头的网址。", "naibao",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(url!) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"打开网址失败：{ex.Message}", "naibao",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
