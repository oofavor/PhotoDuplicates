using System.Drawing;
using System.Drawing.Imaging;

namespace PhotoDuplicates.Core
{
    public static class ImageLoader
    {
        public static readonly string[] SupportedExtensions =
        {
            ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff"
        };

        public static bool IsImageFile(string path)
        {
            string extension = Path.GetExtension(path);
            foreach (string supported in SupportedExtensions)
            {
                if (string.Equals(extension, supported, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        // копируем в память, чтобы файл не оставался заблокированным (иначе его не удалить)
        public static Bitmap Load(string path)
        {
            using (Image original = Image.FromFile(path))
            {
                var copy = new Bitmap(original);
                RotateByExif(original, copy);
                return copy;
            }
        }

        public static Bitmap CreateThumbnail(Image image, int size, Color background)
        {
            var thumbnail = new Bitmap(size, size);
            using (Graphics g = Graphics.FromImage(thumbnail))
            {
                g.Clear(background);
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

                double scale = Math.Min((double)size / image.Width, (double)size / image.Height);
                int width = Math.Max(1, (int)(image.Width * scale));
                int height = Math.Max(1, (int)(image.Height * scale));
                int x = (size - width) / 2;
                int y = (size - height) / 2;

                g.DrawImage(image, x, y, width, height);
            }
            return thumbnail;
        }

        // фото с телефона часто хранятся на боку, а нужный поворот записан в EXIF
        private static void RotateByExif(Image original, Image target)
        {
            const int OrientationPropertyId = 0x0112;

            if (!original.PropertyIdList.Contains(OrientationPropertyId))
            {
                return;
            }

            PropertyItem? property = original.GetPropertyItem(OrientationPropertyId);
            if (property == null || property.Value == null || property.Value.Length == 0)
            {
                return;
            }

            switch (property.Value[0])
            {
                case 2: target.RotateFlip(RotateFlipType.RotateNoneFlipX); break;
                case 3: target.RotateFlip(RotateFlipType.Rotate180FlipNone); break;
                case 4: target.RotateFlip(RotateFlipType.Rotate180FlipX); break;
                case 5: target.RotateFlip(RotateFlipType.Rotate90FlipX); break;
                case 6: target.RotateFlip(RotateFlipType.Rotate90FlipNone); break;
                case 7: target.RotateFlip(RotateFlipType.Rotate270FlipX); break;
                case 8: target.RotateFlip(RotateFlipType.Rotate270FlipNone); break;
            }
        }
    }
}
