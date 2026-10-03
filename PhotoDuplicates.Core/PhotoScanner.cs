using System.Diagnostics;
using System.Drawing;

namespace PhotoDuplicates.Core
{
    public class PhotoScanner
    {
        private readonly HashCache? cache;

        public event EventHandler<ScanProgressEventArgs>? ProgressChanged;

        public PhotoScanner(HashCache? cache)
        {
            this.cache = cache;
        }

        public static List<string> FindImageFiles(string folder, bool includeSubfolders)
        {
            var files = new List<string>();
            if (!Directory.Exists(folder))
            {
                return files;
            }

            var options = new EnumerationOptions();
            options.RecurseSubdirectories = includeSubfolders;
            options.IgnoreInaccessible = true;

            foreach (string file in Directory.EnumerateFiles(folder, "*", options))
            {
                if (ImageLoader.IsImageFile(file))
                {
                    files.Add(file);
                }
            }

            files.Sort(StringComparer.OrdinalIgnoreCase);
            return files;
        }

        // cancel нужен для кнопки "Отмена"
        public ScanResult Scan(string folder, bool includeSubfolders, SearchMode mode, CancellationToken cancel)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new ScanResult();
            result.Folder = folder;

            if (!Directory.Exists(folder))
            {
                result.Errors.Add("Папка не найдена: " + folder);
                return result;
            }

            List<string> files = FindImageFiles(folder, includeSubfolders);
            bool needDHash = mode == SearchMode.ExactAndSimilar;

            for (int i = 0; i < files.Count; i++)
            {
                if (cancel.IsCancellationRequested)
                {
                    result.WasCancelled = true;
                    break;
                }

                string file = files[i];
                PhotoInfo? photo = ProcessFile(file, needDHash, result);
                if (photo != null)
                {
                    result.Photos.Add(photo);
                }

                ProgressChanged?.Invoke(this, new ScanProgressEventArgs(i + 1, files.Count, file));
            }

            result.Elapsed = stopwatch.Elapsed;
            return result;
        }

        private PhotoInfo? ProcessFile(string file, bool needDHash, ScanResult result)
        {
            try
            {
                var fileInfo = new FileInfo(file);

                // сначала пробуем кеш
                if (cache != null)
                {
                    PhotoInfo? cached = cache.Find(file, fileInfo.Length, fileInfo.LastWriteTimeUtc);

                    // если в прошлый раз искали только точные копии, dHash в кеше может не быть
                    if (cached != null && (cached.HasDHash || !needDHash))
                    {
                        result.FromCacheCount++;
                        return cached;
                    }
                }

                // считаем хеши
                var photo = new PhotoInfo();
                photo.FilePath = file;
                photo.FileSize = fileInfo.Length;
                photo.LastWriteTimeUtc = fileInfo.LastWriteTimeUtc;
                photo.Sha256 = FileHasher.ComputeSha256(file);

                if (needDHash)
                {
                    try
                    {
                        using (Bitmap image = ImageLoader.Load(file))
                        {
                            photo.DHash = PerceptualHash.ComputeDHash(image);
                            photo.Width = image.Width;
                            photo.Height = image.Height;
                            photo.HasDHash = true;
                        }
                    }
                    catch (Exception)
                    {
                        // файл не картинка или битый, но точные копии всё равно найдутся по SHA-256
                        result.Errors.Add("Не удалось распознать изображение: " + file);
                    }
                }

                // битые картинки не кешируем, иначе ошибка о них пропадёт из следующего отчёта
                if (cache != null && (photo.HasDHash || !needDHash))
                {
                    cache.Add(photo);
                }

                return photo;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Не удалось прочитать файл: {file} ({ex.Message})");
                return null;
            }
        }
    }
}
