using System.Diagnostics;
using System.IO;
using System.Windows;

namespace Naibao.Services;

public static class ScreenshotService
{
    private static readonly string SnippingToolPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.System), "SnippingTool.exe");

    /// <summary>
    /// 隐藏宠物 → 启动 Windows 截图工具 → 工具退出后恢复宠物。
    /// SnippingTool.exe 不存在时回退到 Win10 新截图协议 ms-screenclip:。
    /// </summary>
    public static async Task InvokeAsync(Action hidePet, Action restorePet)
    {
        var sw = Stopwatch.StartNew();
        hidePet();

        // 等待宠物窗口完全隐藏，避免被截进画面。
        await Task.Delay(350);

        try
        {
            if (File.Exists(SnippingToolPath))
            {
                var process = Process.Start(new ProcessStartInfo(SnippingToolPath) { UseShellExecute = false });
                if (process != null)
                {
                    await Task.Run(() =>
                    {
                        try { process.WaitForExit(); } catch { /* 忽略进程等待异常 */ }
                    });

                    // 若截图工具秒退（例如系统把调用转发给了新版应用），保证宠物至少隐藏 3 秒。
                    var remaining = 3000 - (int)sw.ElapsedMilliseconds;
                    if (remaining > 0)
                    {
                        await Task.Delay(remaining);
                    }

                    return;
                }
            }

            // 回退：Win10 1809+ 的新版截图与草图。
            Process.Start(new ProcessStartInfo("ms-screenclip:") { UseShellExecute = true });
            await Task.Delay(8000);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"无法启动系统截图工具：\n{ex.Message}", "naibao",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            restorePet();
        }
    }
}
