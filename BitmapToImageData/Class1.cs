using FFMediaToolkit.Encoding;
using FFMediaToolkit.Graphics;
using System;
using System.Drawing;
using System.Drawing.Imaging;
namespace BitmapToImageData
{
    public static class BMPtoBitmapData
    {
        public static void AddBitmapFrame(MediaOutput video, Bitmap bmp)
        {
            var rect = new Rectangle(Point.Empty, bmp.Size);
            var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            try
            {
                var imgData = ImageData.FromPointer(data.Scan0, ImagePixelFormat.Bgr24, bmp.Size);
                video.Video.AddFrame(imgData);
            }
            finally
            {
                bmp.UnlockBits(data);
            }
        }
    }
}
