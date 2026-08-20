using System.IO;

namespace Naibao.Services;

/// <summary>
/// 动作 GIF 路径解析：
/// 1) 用户配置的路径存在 → 优先使用；
/// 2) 否则回退到程序目录下 gifs 文件夹中的内置动作；
/// 3) 都不存在 → 返回内置路径（播放器会给出“文件不存在”提示）。
/// </summary>
public static class GifPathResolver
{
    public static string Resolve(string? configuredPath, string fileName)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
        {
            return configuredPath;
        }

        string bundled = Path.Combine(AppContext.BaseDirectory, "gifs", fileName);
        if (File.Exists(bundled))
        {
            return bundled;
        }

        return configuredPath ?? bundled;
    }
}
