using System.IO;
using System.Windows.Media;

namespace Naibao.Services;

/// <summary>使用 WPF MediaPlayer 播放音效（支持 mp3 / wav）。</summary>
public sealed class SoundService : IDisposable
{
    private MediaPlayer? _player;

    /// <summary>播放音效。文件不存在或格式不支持时返回 false，由调用方降级为仅消息。</summary>
    public bool Play(string? path, double volume)
    {
        Stop();

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return false;
        }

        try
        {
            _player = new MediaPlayer();
            _player.Open(new Uri(path, UriKind.Absolute));
            _player.Volume = Math.Clamp(volume, 0.0, 1.0);
            _player.Play();
            return true;
        }
        catch
        {
            Stop();
            return false;
        }
    }

    public void Stop()
    {
        try
        {
            _player?.Close();
        }
        catch
        {
            // 忽略关闭异常。
        }

        _player = null;
    }

    public void Dispose() => Stop();
}
