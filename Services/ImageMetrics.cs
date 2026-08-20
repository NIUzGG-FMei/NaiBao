using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Naibao.Services;

/// <summary>位图度量工具：计算非透明内容包围盒、读取默认宠物形象。</summary>
public static class ImageMetrics
{
    private static BitmapSource? _defaultPet;
    private static Int32Rect? _defaultPetBounds;

    /// <summary>默认宠物 PNG（pack URI 资源）。</summary>
    public static BitmapSource DefaultPet
    {
        get
        {
            if (_defaultPet == null)
            {
                var info = Application.GetResourceStream(new Uri("pack://application:,,,/assets/naibao.png"));
                var decoder = new PngBitmapDecoder(info!.Stream,
                    BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                var frame = decoder.Frames[0];
                frame.Freeze();
                _defaultPet = frame;
            }

            return _defaultPet;
        }
    }

    /// <summary>默认宠物 PNG 的非透明内容包围盒（1024×1024 坐标）。</summary>
    public static Int32Rect DefaultPetBounds => _defaultPetBounds ??= GetContentBounds(DefaultPet) ?? Int32Rect.Empty;

    /// <summary>默认宠物内容顶部在整张图中的比例（用于把泡泡定位到头顶）。</summary>
    public static double DefaultPetContentTopRatio =>
        DefaultPetBounds == Int32Rect.Empty ? 0.0 : DefaultPetBounds.Y / 1024.0;

    /// <summary>计算 alpha 大于阈值的像素包围盒。</summary>
    public static Int32Rect? GetContentBounds(BitmapSource source)
    {
        if (source == null)
        {
            return null;
        }

        BitmapSource bgra = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

        int width = bgra.PixelWidth;
        int height = bgra.PixelHeight;
        int stride = width * 4;
        var pixels = new byte[height * stride];
        bgra.CopyPixels(pixels, stride, 0);

        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
        for (int y = 0; y < height; y++)
        {
            int row = y * stride;
            for (int x = 0; x < width; x++)
            {
                if (pixels[row + x * 4 + 3] > 10)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }

        if (minX == int.MaxValue)
        {
            return null;
        }

        return new Int32Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }
}
