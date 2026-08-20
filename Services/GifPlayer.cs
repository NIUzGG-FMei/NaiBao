using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Naibao.Services;

/// <summary>
/// 自实现的 GIF 播放器：逐帧解码 → 按 GIF disposal 规则合成完整帧 →
/// 以首帧（或末帧）内容包围盒对齐默认宠物形象，消除动作衔接跳变。
/// </summary>
public sealed class GifPlayer : IDisposable
{
    private readonly DispatcherTimer _timer = new();
    private List<BitmapSource>? _frames;
    private List<int>? _delaysMs;
    private int _index;
    private bool _freezeOnEnd;

    public bool IsPlaying { get; private set; }

    public event Action<BitmapSource>? FrameReady;
    public event Action? Completed;

    public GifPlayer()
    {
        _timer.Tick += OnTimerTick;
    }

    /// <summary>
    /// 播放一次 GIF。
    /// <paramref name="useLastFrameAsReference"/>：true 时用末帧包围盒对齐默认形象
    /// （用于“起床”GIF，其末帧才是默认站姿）；false 用首帧对齐。
    /// </summary>
    public bool Play(string? path, int outputCanvasSize, bool freezeOnEnd, bool useLastFrameAsReference)
    {
        Stop();

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return false;
        }

        List<BitmapSource> frames;
        List<int> delays;
        try
        {
            (frames, delays) = LoadAndNormalize(path, outputCanvasSize, useLastFrameAsReference);
        }
        catch
        {
            return false;
        }

        if (frames.Count == 0)
        {
            return false;
        }

        _frames = frames;
        _delaysMs = delays;
        _index = 0;
        _freezeOnEnd = freezeOnEnd;
        IsPlaying = true;
        EmitCurrentFrame();
        return true;
    }

    public void Stop()
    {
        _timer.Stop();
        IsPlaying = false;
        _index = 0;
        _frames = null;
        _delaysMs = null;
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        _timer.Stop();
        if (_frames == null)
        {
            return;
        }

        _index++;
        if (_index >= _frames.Count)
        {
            // 最后一帧已经显示过；freezeOnEnd 时保持最后一帧即可。
            IsPlaying = false;
            Completed?.Invoke();
            return;
        }

        EmitCurrentFrame();
    }

    private void EmitCurrentFrame()
    {
        if (_frames == null || _delaysMs == null || _index >= _frames.Count)
        {
            return;
        }

        FrameReady?.Invoke(_frames[_index]);
        _timer.Interval = TimeSpan.FromMilliseconds(Math.Clamp(_delaysMs[_index], 10, 500));
        _timer.Start();
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTimerTick;
        _frames = null;
    }

    // ---------------- 加载与对齐 ----------------

    private static (List<BitmapSource> frames, List<int> delays) LoadAndNormalize(
        string path, int outputCanvasSize, bool useLastFrameAsReference)
    {
        var decoder = new GifBitmapDecoder(new Uri(path),
            BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);

        if (decoder.Frames.Count == 0)
        {
            return (new List<BitmapSource>(), new List<int>());
        }

        var (rawFrames, delays) = ComposeRawFrames(decoder);
        var frames = NormalizeFrames(rawFrames, useLastFrameAsReference ? rawFrames.Count - 1 : 0, outputCanvasSize);
        return (frames, delays);
    }

    /// <summary>把 GIF 每帧按 disposal 规则合成到完整画布上。</summary>
    private static (List<BitmapSource> frames, List<int> delays) ComposeRawFrames(GifBitmapDecoder decoder)
    {
        int width = decoder.Frames[0].PixelWidth;
        int height = decoder.Frames[0].PixelHeight;
        int stride = width * 4;

        var canvas = new byte[height * stride];
        byte[]? snapshot = null;
        int previousDisposal = 0;

        var frames = new List<BitmapSource>(decoder.Frames.Count);
        var delays = new List<int>(decoder.Frames.Count);

        foreach (var frame in decoder.Frames)
        {
            var bgra = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
            var pixels = new byte[height * stride];
            bgra.CopyPixels(pixels, stride, 0);

            var (delay, disposal) = ReadFrameMeta(frame);

            if (previousDisposal == 2)
            {
                Array.Clear(canvas, 0, canvas.Length); // 恢复为背景（透明）
            }
            else if (previousDisposal == 3 && snapshot != null)
            {
                Array.Copy(snapshot, canvas, canvas.Length); // 恢复为前一帧
            }

            if (disposal == 3)
            {
                snapshot = (byte[])canvas.Clone();
            }

            for (int i = 0; i < pixels.Length; i += 4)
            {
                byte alpha = pixels[i + 3];
                if (alpha == 0)
                {
                    continue; // 透明像素保留上一帧内容（disposal 0/1 的标准行为）
                }

                canvas[i] = pixels[i];
                canvas[i + 1] = pixels[i + 1];
                canvas[i + 2] = pixels[i + 2];
                canvas[i + 3] = alpha;
            }

            var composed = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32,
                null, (byte[])canvas.Clone(), stride);
            composed.Freeze();
            frames.Add(composed);
            delays.Add(delay);
            previousDisposal = disposal;
        }

        return (frames, delays);
    }

    /// <summary>用参考帧内容包围盒对齐默认 PNG，并把全部帧渲染进统一画布。</summary>
    private static List<BitmapSource> NormalizeFrames(
        List<BitmapSource> rawFrames, int referenceIndex, int outputCanvasSize)
    {
        var referenceBounds = ImageMetrics.GetContentBounds(rawFrames[referenceIndex]);
        if (referenceBounds == null)
        {
            return rawFrames;
        }

        var refBox = referenceBounds.Value;
        var defaultBox = ImageMetrics.DefaultPetBounds;
        if (defaultBox == Int32Rect.Empty || defaultBox.Width <= 0 || defaultBox.Height <= 0)
        {
            return rawFrames;
        }

        int outSize = Math.Max(64, outputCanvasSize);
        double outScale = outSize / 1024.0;

        // 以参考帧（默认站姿）的宽高比对齐默认 PNG，保证动作首帧/末帧与静态形象重合。
        double scale = (defaultBox.Width * outScale) / refBox.Width;
        double dx = defaultBox.X * outScale - refBox.X * scale;
        double dy = defaultBox.Y * outScale - refBox.Y * scale;

        // 检查所有帧按此变换后是否越界；越界则缩小并把参考帧中心对齐默认中心。
        double unionLeft = double.MaxValue, unionTop = double.MaxValue;
        double unionRight = double.MinValue, unionBottom = double.MinValue;
        foreach (var frame in rawFrames)
        {
            var bounds = ImageMetrics.GetContentBounds(frame);
            if (bounds == null)
            {
                continue;
            }

            var b = bounds.Value;
            unionLeft = Math.Min(unionLeft, b.X * scale + dx);
            unionTop = Math.Min(unionTop, b.Y * scale + dy);
            unionRight = Math.Max(unionRight, (b.X + b.Width) * scale + dx);
            unionBottom = Math.Max(unionBottom, (b.Y + b.Height) * scale + dy);
        }

        double margin = Math.Max(2, outSize * 0.02);
        if (unionRight - unionLeft > outSize - 2 * margin
            || unionBottom - unionTop > outSize - 2 * margin)
        {
            double fitScale = Math.Min((outSize - 2 * margin) / (unionRight - unionLeft),
                (outSize - 2 * margin) / (unionBottom - unionTop));
            scale = Math.Min(scale, fitScale);

            double defaultCenterX = (defaultBox.X + defaultBox.Width / 2.0) * outScale;
            double defaultCenterY = (defaultBox.Y + defaultBox.Height / 2.0) * outScale;
            double refCenterX = refBox.X + refBox.Width / 2.0;
            double refCenterY = refBox.Y + refBox.Height / 2.0;
            dx = defaultCenterX - refCenterX * scale;
            dy = defaultCenterY - refCenterY * scale;
        }

        var result = new List<BitmapSource>(rawFrames.Count);
        foreach (var frame in rawFrames)
        {
            result.Add(RenderScaledFrame(frame, scale, dx, dy, outSize));
        }

        return result;
    }

    private static BitmapSource RenderScaledFrame(
        BitmapSource frame, double scale, double dx, double dy, int outSize)
    {
        var visual = new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);

        using (var dc = visual.RenderOpen())
        {
            dc.DrawImage(frame, new Rect(dx, dy, frame.PixelWidth * scale, frame.PixelHeight * scale));
        }

        var target = new RenderTargetBitmap(outSize, outSize, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);
        target.Freeze();
        return target;
    }

    private static (int delayMs, int disposal) ReadFrameMeta(BitmapFrame frame)
    {
        try
        {
            if (frame.Metadata is BitmapMetadata metadata
                && metadata.GetQuery("/grctlext") is byte[] bytes
                && bytes.Length >= 4)
            {
                // GCE: bytes[0]=packed, bytes[1]=delay低字节, bytes[2]=delay高字节
                int delayCentis = bytes[1] | (bytes[2] << 8);
                int disposal = (bytes[0] >> 2) & 0x07;
                return (Math.Clamp(delayCentis * 10, 10, 500), disposal);
            }
        }
        catch
        {
            // 元数据不可用时退回 24fps / 不处理 disposal。
        }

        return (41, 0);
    }
}
