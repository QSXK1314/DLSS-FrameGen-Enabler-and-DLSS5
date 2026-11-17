using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace DLSSFrameGenEnabler_WinUI3.Services
{
    /// <summary>
    /// 与正式版 HomePage.SetGameIcon 保持一致：直接使用
    /// Icon.ExtractAssociatedIcon 的原生尺寸输出 PNG，不再强制放大到 96px。
    /// 以前 PrivateExtractIcons 取 96px 时，若 exe 只有 32px 图标会被二次插值放大，
    /// 浏览器再缩到 32px 显示就会发糊。
    /// </summary>
    public static class IconExtractor
    {
        public static string GetDataUrl(string exePath, int maxSize = 0)
        {
            if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
            {
                return "";
            }

            try
            {
                using Icon? icon = Icon.ExtractAssociatedIcon(exePath);
                if (icon == null)
                {
                    return "";
                }

                using Bitmap source = icon.ToBitmap();
                // 正式版不做任何缩放。这里仅当调用方明确要求更小尺寸且源图更大时，
                // 才做一次高质量缩小，绝不会把低分辨率图标放大。
                if (maxSize > 0 && source.Width > maxSize && source.Height > maxSize)
                {
                    using Bitmap output = Resize(source, maxSize);
                    return Png(output);
                }

                return Png(source);
            }
            catch
            {
                return "";
            }
        }

        private static Bitmap Resize(Bitmap source, int size)
        {
            Bitmap output = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(output))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.Clear(Color.Transparent);
                g.DrawImage(source, 0, 0, size, size);
            }
            return output;
        }

        private static string Png(Bitmap bitmap)
        {
            using MemoryStream ms = new MemoryStream();
            bitmap.Save(ms, ImageFormat.Png);
            return "data:image/png;base64," + Convert.ToBase64String(ms.ToArray());
        }
    }
}
