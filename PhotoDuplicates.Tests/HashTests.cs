using System.Drawing;
using System.Drawing.Imaging;
using PhotoDuplicates.Core;
using Xunit.Abstractions;

namespace PhotoDuplicates.Tests
{
    public class HashTests
    {
        private readonly ITestOutputHelper output;

        // xUnit сам передаёт сюда output, через него тест пишет сообщения
        public HashTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact(DisplayName = "SHA-256 для текста \"abc\" совпадает с эталонным значением")]
        public void Sha256_KnownValue()
        {
            using (var images = new TestImages())
            {
                string path = Path.Combine(images.Folder, "abc.txt");
                File.WriteAllText(path, "abc");

                string hash = FileHasher.ComputeSha256(path);
                output.WriteLine("Посчитали SHA-256 для файла с текстом \"abc\": " + hash);

                // известное значение SHA-256 для строки "abc"
                Assert.Equal("BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD", hash);
                output.WriteLine("Сработало: хеш совпал с эталонным значением");
            }
        }

        [Fact(DisplayName = "Копия файла даёт тот же SHA-256, другой файл даёт другой")]
        public void Sha256_SameForCopy_DifferentForOtherFile()
        {
            using (var images = new TestImages())
            {
                string original = images.SaveJpeg("a.jpg", 1, 400, 300);
                string copy = images.Copy(original, "copy.jpg");
                string other = images.SaveJpeg("b.jpg", 2, 400, 300);

                string originalHash = FileHasher.ComputeSha256(original);
                string copyHash = FileHasher.ComputeSha256(copy);
                string otherHash = FileHasher.ComputeSha256(other);
                output.WriteLine("Оригинал:        " + originalHash);
                output.WriteLine("Копия:           " + copyHash);
                output.WriteLine("Другая картинка: " + otherHash);

                Assert.Equal(originalHash, copyHash);
                Assert.NotEqual(originalHash, otherHash);
                output.WriteLine("Сработало: у копии хеш тот же, у другой картинки другой");
            }
        }

        [Fact(DisplayName = "Расстояние Хэмминга считает число отличающихся битов")]
        public void Distance_CountsDifferentBits()
        {
            output.WriteLine("Одинаковые числа: " + PerceptualHash.Distance(12345, 12345) + " бит отличия");
            output.WriteLine("1011 и 0001: " + PerceptualHash.Distance(0b1011, 0b0001) + " бит отличия");
            output.WriteLine("Все нули и все единицы: " + PerceptualHash.Distance(0, ulong.MaxValue) + " бит отличия");

            Assert.Equal(0, PerceptualHash.Distance(12345, 12345));
            Assert.Equal(2, PerceptualHash.Distance(0b1011, 0b0001));
            Assert.Equal(64, PerceptualHash.Distance(0, ulong.MaxValue));
            output.WriteLine("Сработало: 0, 2 и 64, как и должно быть");
        }

        [Fact(DisplayName = "Одна и та же картинка всегда даёт одинаковый dHash")]
        public void DHash_SameImage_SameHash()
        {
            using (Bitmap a = TestImages.Draw(5, 800, 600))
            using (Bitmap b = TestImages.Draw(5, 800, 600))
            {
                ulong hashA = PerceptualHash.ComputeDHash(a);
                ulong hashB = PerceptualHash.ComputeDHash(b);
                output.WriteLine("dHash первой картинки: " + PerceptualHash.ToHex(hashA));
                output.WriteLine("dHash второй картинки: " + PerceptualHash.ToHex(hashB));

                Assert.Equal(hashA, hashB);
                output.WriteLine("Сработало: хеши одинаковые");
            }
        }

        [Fact(DisplayName = "Картинка, уменьшенная в 4 раза, считается похожей")]
        public void DHash_ResizedImage_IsSimilar()
        {
            using (Bitmap big = TestImages.Draw(7, 1600, 1200))
            using (var small = new Bitmap(big, 400, 300)) // та же картинка, уменьшенная в 4 раза
            {
                int distance = PerceptualHash.Distance(PerceptualHash.ComputeDHash(big), PerceptualHash.ComputeDHash(small));
                output.WriteLine("Оригинал 1600x1200 и копия 400x300 отличаются на " + distance + " бит из 64");

                Assert.True(distance <= 5, $"Расстояние {distance} слишком большое");
                output.WriteLine("Сработало: отличие маленькое, картинки похожи");
            }
        }

        [Fact(DisplayName = "Картинка, пересжатая в JPEG с качеством 10%, считается похожей")]
        public void DHash_RecompressedJpeg_IsSimilar()
        {
            using (var images = new TestImages())
            {
                string path = images.SaveJpeg("a.jpg", 9, 1000, 750);

                // пересохраняем с низким качеством JPEG (10 из 100)
                string recompressed = Path.Combine(images.Folder, "low.jpg");
                using (var image = new Bitmap(path))
                {
                    ImageCodecInfo jpeg = ImageCodecInfo.GetImageEncoders().First(codec => codec.FormatID == ImageFormat.Jpeg.Guid);
                    var parameters = new EncoderParameters(1);
                    parameters.Param[0] = new EncoderParameter(Encoder.Quality, 10L);
                    image.Save(recompressed, jpeg, parameters);
                }
                output.WriteLine("Размер оригинала: " + new FileInfo(path).Length + " байт, после пересжатия: " + new FileInfo(recompressed).Length + " байт");

                using (Bitmap a = ImageLoader.Load(path))
                using (Bitmap b = ImageLoader.Load(recompressed))
                {
                    int distance = PerceptualHash.Distance(PerceptualHash.ComputeDHash(a), PerceptualHash.ComputeDHash(b));
                    output.WriteLine("Отличие по dHash: " + distance + " бит, порог: " + DuplicateFinder.DefaultThreshold);

                    Assert.True(distance <= DuplicateFinder.DefaultThreshold, $"Расстояние {distance} слишком большое");
                    output.WriteLine("Сработало: файлы разные, но картинки признаны похожими");
                }
            }
        }

        [Fact(DisplayName = "Разные картинки далеки друг от друга по dHash")]
        public void DHash_DifferentImages_AreFarApart()
        {
            for (int seed = 0; seed < 10; seed++)
            {
                using (Bitmap a = TestImages.Draw(seed, 640, 480))
                using (Bitmap b = TestImages.Draw(seed + 100, 640, 480))
                {
                    int distance = PerceptualHash.Distance(PerceptualHash.ComputeDHash(a), PerceptualHash.ComputeDHash(b));
                    output.WriteLine($"Пара {seed + 1}: отличие {distance} бит");

                    Assert.True(distance > DuplicateFinder.DefaultThreshold, $"Разные картинки слишком похожи: {distance}");
                }
            }
            output.WriteLine("Сработало: все 10 пар отличаются больше чем на порог " + DuplicateFinder.DefaultThreshold);
        }
    }
}
