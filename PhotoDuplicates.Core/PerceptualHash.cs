using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Numerics;

namespace PhotoDuplicates.Core
{
    // dHash: сжимаем картинку до 9x8, переводим в серый и в каждой строке сравниваем
    // соседние пиксели (левый ярче правого = бит 1), выходит 8x8 = 64 бита
    // у похожих картинок (уменьшенных, пересжатых) биты почти совпадают
    public static class PerceptualHash
    {
        private const int HashWidth = 9;  // 9 пикселей в строке дают 8 сравнений
        private const int HashHeight = 8;

        public static ulong ComputeDHash(Image image)
        {
            using (Bitmap small = Shrink(image, HashWidth, HashHeight))
            {
                ulong hash = 0;
                int bitNumber = 0;

                for (int y = 0; y < HashHeight; y++)
                {
                    for (int x = 0; x < HashWidth - 1; x++)
                    {
                        double left = GetBrightness(small.GetPixel(x, y));
                        double right = GetBrightness(small.GetPixel(x + 1, y));

                        if (left > right)
                        {
                            hash = hash | (1UL << bitNumber); // ставим бит в 1
                        }
                        bitNumber++;
                    }
                }

                return hash;
            }
        }

        // расстояние Хэмминга: сколько битов отличается (0..64)
        public static int Distance(ulong hash1, ulong hash2)
        {
            // XOR даёт 1 там, где биты разные, PopCount считает единицы
            ulong differentBits = hash1 ^ hash2;
            return BitOperations.PopCount(differentBits);
        }

        public static string ToHex(ulong hash)
        {
            return hash.ToString("X16");
        }

        // стандартная формула перевода цвета в яркость
        private static double GetBrightness(Color color)
        {
            return 0.299 * color.R + 0.587 * color.G + 0.114 * color.B;
        }

        private static Bitmap Shrink(Image image, int width, int height)
        {
            var result = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(result))
            using (var attributes = new ImageAttributes())
            {
                g.Clear(Color.White); // прозрачное в PNG считаем белым
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                // без этого по краям уменьшенной картинки появляется тёмная рамка
                attributes.SetWrapMode(WrapMode.TileFlipXY);

                var target = new Rectangle(0, 0, width, height);
                g.DrawImage(image, target, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
            }
            return result;
        }
    }
}
