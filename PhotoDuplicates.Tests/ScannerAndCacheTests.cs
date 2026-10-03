using System.Drawing;
using PhotoDuplicates.Core;
using Xunit.Abstractions;

namespace PhotoDuplicates.Tests
{
    public class ScannerAndCacheTests
    {
        private readonly ITestOutputHelper output;

        public ScannerAndCacheTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        // папка: оригинал, его копия в подпапке, уменьшенная версия, другое фото и битый файл
        private static void CreateTestFolder(TestImages images)
        {
            string original = images.SaveJpeg("original.jpg", 1, 1200, 900);
            images.Copy(original, Path.Combine("backup", "original_copy.jpg"));

            using (Bitmap big = TestImages.Draw(1, 1200, 900))
            using (var small = new Bitmap(big, 300, 225))
            {
                small.Save(Path.Combine(images.Folder, "small.png"), System.Drawing.Imaging.ImageFormat.Png);
            }

            images.SaveJpeg("other.jpg", 2, 1200, 900);
            images.SaveBrokenFile("broken.jpg");
        }

        private static AppSettings Settings(string folder)
        {
            var settings = new AppSettings();
            settings.Folder = folder;
            settings.IncludeSubfolders = true;
            settings.Threshold = DuplicateFinder.DefaultThreshold;
            return settings;
        }

        private void PrintResult(SearchResult result)
        {
            output.WriteLine("Прочитано файлов: " + result.Scan.Photos.Count + ", из кеша: " + result.Scan.FromCacheCount);
            foreach (string error in result.Scan.Errors)
            {
                output.WriteLine("  проблема: " + Path.GetFileName(error));
            }
            output.WriteLine("Найдено групп: " + result.Groups.Count);
            foreach (DuplicateGroup group in result.Groups)
            {
                var names = new List<string>();
                foreach (PhotoInfo photo in group.Files)
                {
                    names.Add(photo.FileName);
                }
                output.WriteLine($"  группа {group.Number} ({group.KindTitle}): {string.Join(", ", names)}");
            }
        }

        [Fact(DisplayName = "Поиск в папке находит копию и уменьшенную версию")]
        public void Search_FindsCopiesAndSimilarPhotos()
        {
            using (var images = new TestImages())
            {
                CreateTestFolder(images);
                output.WriteLine("В папке: original.jpg, backup/original_copy.jpg, small.png (уменьшенный оригинал), other.jpg, broken.jpg");
                var search = new DuplicateSearch(new HashCache(Path.Combine(images.Folder, "cache.json")));

                SearchResult result = search.Run(Settings(images.Folder), false, CancellationToken.None);
                PrintResult(result);

                Assert.Equal(5, result.Scan.Photos.Count);
                Assert.Single(result.Scan.Errors); // broken.jpg не распознан как картинка
                Assert.Single(result.Groups);      // original + копия + уменьшенная версия
                Assert.Equal(3, result.Groups[0].Files.Count);
                Assert.Equal(GroupKind.Similar, result.Groups[0].Kind);
                output.WriteLine("Сработало: оригинал, копия и уменьшенная версия в одной группе, битый файл в списке проблем");
            }
        }

        [Fact(DisplayName = "Режим только точных копий находит лишь одинаковые файлы")]
        public void Search_ExactOnly_FindsOnlyCopies()
        {
            using (var images = new TestImages())
            {
                CreateTestFolder(images);
                var search = new DuplicateSearch(new HashCache(Path.Combine(images.Folder, "cache.json")));
                AppSettings settings = Settings(images.Folder);
                settings.Mode = SearchMode.ExactOnly;

                SearchResult result = search.Run(settings, false, CancellationToken.None);
                PrintResult(result);

                Assert.Single(result.Groups);
                Assert.Equal(2, result.Groups[0].Files.Count);
                Assert.Equal(GroupKind.ExactCopies, result.Groups[0].Kind);
                output.WriteLine("Сработало: нашлись только оригинал и его копия, уменьшенная версия не попала");
            }
        }

        [Fact(DisplayName = "Без вложенных папок копия в backup не видна")]
        public void Search_WithoutSubfolders_DoesNotSeeBackup()
        {
            using (var images = new TestImages())
            {
                CreateTestFolder(images);
                var search = new DuplicateSearch(new HashCache(Path.Combine(images.Folder, "cache.json")));
                AppSettings settings = Settings(images.Folder);
                settings.IncludeSubfolders = false;

                SearchResult result = search.Run(settings, false, CancellationToken.None);
                PrintResult(result);

                Assert.Equal(4, result.Scan.Photos.Count);
                output.WriteLine("Сработало: прочитано 4 файла, папка backup пропущена");
            }
        }

        [Fact(DisplayName = "Второй поиск берёт хеши из кеша")]
        public void SecondSearch_UsesCache()
        {
            using (var images = new TestImages())
            {
                CreateTestFolder(images);
                string cacheFile = Path.Combine(images.Folder, "cache.json");

                SearchResult first = new DuplicateSearch(new HashCache(cacheFile)).Run(Settings(images.Folder), false, CancellationToken.None);
                output.WriteLine("Первый поиск, из кеша: " + first.Scan.FromCacheCount);
                SearchResult second = new DuplicateSearch(new HashCache(cacheFile)).Run(Settings(images.Folder), false, CancellationToken.None);
                output.WriteLine("Второй поиск, из кеша: " + second.Scan.FromCacheCount);

                // 4 нормальные картинки берутся из кеша, испорченный файл в кеш не попадает
                Assert.Equal(4, second.Scan.FromCacheCount);
                Assert.Single(second.Groups);
                output.WriteLine("Сработало: во второй раз хеши не пересчитывались, результат тот же");
            }
        }

        [Fact(DisplayName = "Кеш не верит файлу, у которого изменился размер или дата")]
        public void Cache_IgnoresChangedFile()
        {
            using (var images = new TestImages())
            {
                var cache = new HashCache(Path.Combine(images.Folder, "cache.json"));
                var photo = new PhotoInfo { FilePath = @"C:\a.jpg", FileSize = 100, LastWriteTimeUtc = new DateTime(2025, 1, 1) };
                cache.Add(photo);
                output.WriteLine("В кеше: C:\\a.jpg, 100 байт, дата 01.01.2025");

                output.WriteLine("Ищем C:\\A.JPG с теми же данными: " + (cache.Find(@"C:\A.JPG", 100, new DateTime(2025, 1, 1)) != null ? "нашли" : "не нашли"));
                output.WriteLine("Ищем с размером 200: " + (cache.Find(@"C:\a.jpg", 200, new DateTime(2025, 1, 1)) != null ? "нашли" : "не нашли"));
                output.WriteLine("Ищем с другой датой: " + (cache.Find(@"C:\a.jpg", 100, new DateTime(2025, 6, 1)) != null ? "нашли" : "не нашли"));

                Assert.NotNull(cache.Find(@"C:\A.JPG", 100, new DateTime(2025, 1, 1))); // регистр букв не важен
                Assert.Null(cache.Find(@"C:\a.jpg", 200, new DateTime(2025, 1, 1)));    // изменился размер
                Assert.Null(cache.Find(@"C:\a.jpg", 100, new DateTime(2025, 6, 1)));    // изменилось время
                output.WriteLine("Сработало: изменённый файл считается новым, хеши будут посчитаны заново");
            }
        }

        [Fact(DisplayName = "Кеш сохраняется в файл и загружается обратно")]
        public void Cache_SaveAndLoad()
        {
            using (var images = new TestImages())
            {
                string realFile = images.SaveJpeg("a.jpg", 1, 100, 100); // при сохранении остаются только существующие файлы
                string cacheFile = Path.Combine(images.Folder, "cache.json");

                var cache = new HashCache(cacheFile);
                cache.Add(new PhotoInfo { FilePath = realFile, Sha256 = "ABC", DHash = 42, HasDHash = true });
                cache.Add(new PhotoInfo { FilePath = @"Z:\deleted.jpg", Sha256 = "DEF" });
                cache.Save();
                output.WriteLine("Записали в кеш 2 файла: существующий и уже удалённый");

                var loaded = new HashCache(cacheFile);
                loaded.Load();
                output.WriteLine("После загрузки в кеше файлов: " + loaded.Count);

                Assert.Equal(1, loaded.Count);
                output.WriteLine("Сработало: запись об удалённом файле выброшена при сохранении");
            }
        }

        [Fact(DisplayName = "Отмена сразу останавливает поиск")]
        public void Cancel_StopsSearch()
        {
            using (var images = new TestImages())
            {
                CreateTestFolder(images);
                var cancelSource = new CancellationTokenSource();
                cancelSource.Cancel(); // отменяем сразу
                output.WriteLine("Нажали отмену ещё до начала поиска");

                SearchResult result = new DuplicateSearch(new HashCache(Path.Combine(images.Folder, "cache.json")))
                    .Run(Settings(images.Folder), false, cancelSource.Token);
                output.WriteLine("Поиск прерван: " + (result.Scan.WasCancelled ? "да" : "нет") + ", прочитано файлов: " + result.Scan.Photos.Count);

                Assert.True(result.Scan.WasCancelled);
                Assert.Empty(result.Scan.Photos);
                output.WriteLine("Сработало: ни один файл не обработан");
            }
        }
    }
}
