﻿using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SEApiComposeImages
{
    static class ImageTools
    {
        static public void SaveImageAsPng(BitmapFrame frame, string path)
        {
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(frame);
            using (var stream = File.Create(path))
                encoder.Save(stream);
        }

        static public BitmapFrame CombineImages(IEnumerable<BitmapFrame> images, ImageGridSettings settings)
        {
            var totalWidth = (settings.CellPixelWidth + settings.Gap) * settings.Columns - settings.Gap;
            var totalHeight = (settings.CellPixelHeight + settings.Gap) * settings.Rows - settings.Gap;

            var drawingVisual = new DrawingVisual();
            using (DrawingContext drawingContext = drawingVisual.RenderOpen())
            {
                if (settings.BackgroundColor != null)
                    drawingContext.DrawRectangle(
                        new SolidColorBrush(settings.BackgroundColor.Value), null,
                        new Rect(0, 0, totalWidth, totalHeight));
                int i = 0;
                foreach (var image in images)
                {
                    int x = i % settings.Columns, y = i / settings.Columns;
                    drawingContext.DrawRoundedRectangle(
                        new ImageBrush(image), null,
                        new Rect((settings.CellPixelWidth + settings.Gap) * x,
                                 (settings.CellPixelHeight + settings.Gap) * y,
                                 settings.CellPixelWidth,
                                 settings.CellPixelHeight),
                        settings.CornerRadiusX,
                        settings.CornerRadiusY);
                    i++;
                }
            }

            // Converts the Visual (DrawingVisual) into a BitmapSource
            var bmp = new RenderTargetBitmap(totalWidth, totalHeight, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(drawingVisual);
            var frame = BitmapFrame.Create(bmp);
            frame.Freeze();
            return frame;
        }

        static public bool IsAutomaticImage(BitmapFrame frame)
        {
            var pixels = GetPixels(frame);

            if (IsQuarterSymmetric(pixels, frame.PixelWidth, frame.PixelHeight))
                return true;

            return false;
        }

        static bool IsQuarterSymmetric(Color[,] pixels, int width, int height)
        {
            if (width != height)
                return false;
            int countDiffering = 0;
            for (int y = 0; y < height / 2; y++)
                for (int x = 0; x < width / 2; x++)
                {
                    var hsv1 = HSVColor.FromRgbColor(pixels[x, y]);
                    var hsv2 = HSVColor.FromRgbColor(pixels[y, width - 1 - x]);
                    var hsv3 = HSVColor.FromRgbColor(pixels[width - 1 - x, height - 1 - y]);
                    var hsv4 = HSVColor.FromRgbColor(pixels[height - 1 - y, x]);
                    if (!HSVColor.AreClose(hsv1, hsv2) || !HSVColor.AreClose(hsv1, hsv3) || !HSVColor.AreClose(hsv1, hsv4))
                        countDiffering++;
                }
            var ratio = (double)countDiffering / width / height;
            return ratio < 0.025;
        }


        static Color[,] GetPixels(BitmapSource source)
        {
            if (source.Format != PixelFormats.Bgra32)
                source = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

            int width = source.PixelWidth;
            int height = source.PixelHeight;

            var pixelBytes = new byte[height * width * 4];
            source.CopyPixels(pixelBytes, width * 4, 0);

            Color[,] pixels = new Color[width, height];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    pixels[x, y] = Color.FromArgb(
                        b: pixelBytes[(y * width + x) * 4 + 0],
                        g: pixelBytes[(y * width + x) * 4 + 1],
                        r: pixelBytes[(y * width + x) * 4 + 2],
                        a: pixelBytes[(y * width + x) * 4 + 3]);
            return pixels;
        }
    }
}
