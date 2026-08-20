using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Naibao.Services;

/// <summary>
/// 自实现的 GIF 播放器：
/// 1) 逐帧解码并按 GIF disposal 规则合成完整帧；
/// 2) 用首帧（或末帧）内容包围盒对齐默认宠物形象；
/// 3) 播放时逐帧惰性渲染（避免打开时长时间卡顿）；
/// 4) 对齐/渲染失败时自动降级为直接播放原始帧，保证动作一定能动。
/// </summary>
public sealed class GifPlayer : IDisposable
{
    private sealed class LoadedAnimation
    {
        public List<BitmapSource> RawFrames { get; } = new();
        public List<int> DelaysMs { get; } = new();
        public List<BitmapSource?> DisplayFrames { get; } = new();
        public double Scale = 1;
        public double Dx;
        public double Dy;
        public int OutSize;
        public bool UseTransform;
    }

    private readonly DispatcherTimer _timer = new();
    private LoadedAnimation? _animation;
    private int _index;
    private bool _freezeOnEnd;

    public bool IsPlaying { get; private set; }

    /// <summary>最近一次加载失败/降级的错误信息（供调试与提示）。</summary>
    public string? LastError { get; private set; }

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
        LastError = null;

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            LastError = "文件不存在或路径为空。";
            return false;
        }

        LoadedAnimation? animation;
        try
        {
            animation = LoadAnimation(path, outputCanvasSize, useLastFrameAsReference);
        }
        catch (Exception ex)
        {
            // 完整加载失败（例如解码异常）：记录错误，并尝试最简播放。
            LastError = ex.ToString();
            try
            {
                animation = LoadRawAnimation(path);
            }
            catch
            {
                return false;
            }
        }

        if (animation.RawFrames.Count == 0)
        {
            LastError ??= "GIF 中没有可播放的帧。";
            return false;
        }

        _animation = animation;
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
        _animation = null;
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        _timer.Stop();
        if (_animation == null)
        {
            return;
        }

        _index++;
        if (_index >= _animation.RawFrames.Count)
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
        if (_animation == null || _index >= _animation.RawFrames.Count)
        {
            return;
        }

        FrameReady?.Invoke(GetDisplayFrame(_index));
        _timer.Interval = TimeSpan.FromMilliseconds(
            Math.Clamp(_animation.DelaysMs[_index], 10, 500));
        _timer.Start();
    }

    /// <summary>惰性渲染：只渲染当前要显示的一帧，并缓存结果。</summary>
    private BitmapSource GetDisplayFrame(int index)
    {
        var cached = _animation!.DisplayFrames[index];
        if (cached != null)
        {
            return cached;
        }

        BitmapSource source = _animation.RawFrames[index];
        if (_animation.UseTransform)
        {
            try
            {
                cached = RenderScaledFrame(source, _animation.Scale, _animation.Dx,
                    _animation.Dy, _animation.OutSize);
            }
            catch (Exception ex)
            {
                LastError ??= ex.ToString();
                cached = source; // 渲染失败：直接用原始帧，保证动画继续。
            }
        }
        else
        {
            cached = source;
        }

        _animation.DisplayFrames[index] = cached;
        return cached;
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTimerTick;
        _animation = null;
    }

    // ---------------- 加载 ----------------

    private static LoadedAnimation LoadAnimation(
        string path, int outputCanvasSize, bool useLastFrameAsReference)
    {
        var (rawFrames, delays) = LoadRawFrames(path);
        var animation = new LoadedAnimation
        {
            OutSize = Math.Max(64, outputCanvasSize)
        };
        animation.RawFrames.AddRange(rawFrames);
        animation.DelaysMs.AddRange(delays);
        animation.DisplayFrames.AddRange(new BitmapSource?[rawFrames.Count]);

        int referenceIndex = useLastFrameAsReference ? rawFrames.Count - 1 : 0;
        ComputeTransform(animation, referenceIndex);
        return animation;
    }

    private static LoadedAnimation LoadRawAnimation(string path)
    {
        var (rawFrames, delays) = LoadRawFrames(path);
        var animation = new LoadedAnimation
        {
            UseTransform = false,
            OutSize = rawFrames.Count > 0 ? rawFrames[0].PixelWidth : 160
        };
        animation.RawFrames.AddRange(rawFrames);
        animation.DelaysMs.AddRange(delays);
        animation.DisplayFrames.AddRange(new BitmapSource?[rawFrames.Count]);
        return animation;
    }

    private static (List<BitmapSource> frames, List<int> delays) LoadRawFrames(string path)
    {
        var decoder = new GifBitmapDecoder(new Uri(path),
            BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);

        if (decoder.Frames.Count == 0)
        {
            return (new List<BitmapSource>(), new List<int>());
        }

        return ComposeRawFrames(decoder);
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

    /// <summary>用参考帧内容包围盒对齐默认 PNG，计算统一缩放与平移。</summary>
    private static void ComputeTransform(LoadedAnimation animation, int referenceIndex)
    {
        try
        {
            // 每帧只计算一次内容包围盒，避免重复像素扫描造成首帧前卡顿。
            var boundsList = new List<Int32Rect?>(animation.RawFrames.Count);
            foreach (var frame in animation.RawFrames)
            {
                boundsList.Add(ImageMetrics.GetContentBounds(frame));
            }

            var referenceBounds = boundsList[referenceIndex];
            var defaultBox = ImageMetrics.DefaultPetBounds;
            if (referenceBounds == null || defaultBox == Int32Rect.Empty
                || defaultBox.Width <= 0 || defaultBox.Height <= 0)
            {
                return; // 保持 UseTransform = false，直接播放原始帧。
            }

            var refBox = referenceBounds.Value;
            int outSize = animation.OutSize;
            double outScale = outSize / 1024.0;

            // 以参考帧（默认站姿）的宽度对齐默认 PNG，保证动作首帧/末帧与静态形象重合。
            double scale = (defaultBox.Width * outScale) / refBox.Width;
            double dx = defaultBox.X * outScale - refBox.X * scale;
            double dy = defaultBox.Y * outScale - refBox.Y * scale;

            // 检查所有帧按此变换后是否越界；越界则缩小并把参考帧中心对齐默认中心。
            double unionLeft = double.MaxValue, unionTop = double.MaxValue;
            double unionRight = double.MinValue, unionBottom = double.MinValue;
            foreach (var bounds in boundsList)
            {
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

            animation.Scale = scale;
            animation.Dx = dx;
            animation.Dy = dy;
            animation.UseTransform = true;
        }
        catch
        {
            // 对齐计算失败：降级为直接播放原始帧。
            animation.UseTransform = false;
        }
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
