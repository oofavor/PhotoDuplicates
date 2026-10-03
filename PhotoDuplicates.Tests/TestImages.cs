using System.Drawing;
using System.Drawing.Imaging;

namespace PhotoDuplicates.Tests
{
    public class TestImages : IDisposable
    {
        public string Folder { get; }

        public TestImages()
        {
            Folder = Path.Combine(Path.GetTempPath(), "PhotoDuplicatesTests_" + Guid.NewGuid());
            Directory.CreateDirectory(Folder);
        }

        public static Bitmap Draw(int seed, int width, int height)
        {
            var random = new Random(seed);
            var bitmap = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.FromArgb(random.Next(256), random.Next(256), random.Next(256)));
                for (int i = 0; i < 12; i++)
                {
                    using (var brush = new SolidBrush(Color.FromArgb(random.Next(256), random.Next(256), random.Next(256))))
                    {
                        int x = random.Next(width);
                        int y = random.Next(height);
                        int w = random.Next(width / 8, width / 2);
                        int h = random.Next(height / 8, height / 2);
                        if (i % 2 == 0)
                        {
                            g.FillRectangle(brush, x - w / 2, y - h / 2, w, h);
                        }
                        else
                        {
                            g.FillEllipse(brush, x - w / 2, y - h / 2, w, h);
                        }
                    }
                }
            }
            return bitmap;
        }

        public string SaveJpeg(string fileName, int seed, int width, int height)
        {
            string path = Path.Combine(Folder, fileName);
            string? folder = Path.GetDirectoryName(path);
            if (folder != null)
            {
                Directory.CreateDirectory(folder);
            }

            using (Bitmap bitmap = Draw(seed, width, height))
            {
                bitmap.Save(path, ImageFormat.Jpeg);
            }
            return path;
        }

        public string Copy(string sourcePath, string newFileName)
        {
            string path = Path.Combine(Folder, newFileName);
            string? folder = Path.GetDirectoryName(path);
            if (folder != null)
            {
                Directory.CreateDirectory(folder);
            }
            File.Copy(sourcePath, path);
            return path;
        }

        public string SaveBrokenFile(string fileName)
        {
            string path = Path.Combine(Folder, fileName);
            File.WriteAllText(path, "это не картинка");
            return path;
        }

        public void Dispose()
        {
            Directory.Delete(Folder, true);
        }
    }
}
