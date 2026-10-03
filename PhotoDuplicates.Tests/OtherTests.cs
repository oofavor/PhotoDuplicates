using PhotoDuplicates.Core;
using Xunit.Abstractions;

namespace PhotoDuplicates.Tests
{
    public class OtherTests
    {
        private readonly ITestOutputHelper output;

        public OtherTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact(DisplayName = "Все параметры командной строки читаются правильно")]
        public void CommandLine_ParsesOptions()
        {
            string[] args = { @"C:\Фото", "-t", "6", "--exact-only", "--no-recursive", "--no-cache", "-o", "report.txt" };
            output.WriteLine("Командная строка: " + string.Join(" ", args));

            CommandLineOptions options = CommandLineOptions.Parse(args);
            output.WriteLine($"Папка: {options.Folder}, порог: {options.Threshold}, режим: {options.Mode}");
            output.WriteLine($"Подпапки: {options.IncludeSubfolders}, кеш: {options.UseCache}, отчёт: {options.ReportFile}");

            Assert.False(options.HasErrors);
            Assert.Equal(@"C:\Фото", options.Folder);
            Assert.Equal(6, options.Threshold);
            Assert.Equal(SearchMode.ExactOnly, options.Mode);
            Assert.False(options.IncludeSubfolders);
            Assert.False(options.UseCache);
            Assert.Equal("report.txt", options.ReportFile);
            output.WriteLine("Сработало: все параметры прочитаны, ошибок нет");
        }

        [Theory(DisplayName = "Неправильный параметр даёт понятную ошибку")]
        [InlineData("-t", "abc")]
        [InlineData("-t", "100")]
        [InlineData("--unknown", "x")]
        public void CommandLine_WrongValues_GiveErrors(string name, string value)
        {
            CommandLineOptions options = CommandLineOptions.Parse(new[] { name, value });
            output.WriteLine($"Командная строка: {name} {value}");
            foreach (string error in options.Errors)
            {
                output.WriteLine("Программа ответила: " + error);
            }

            Assert.True(options.HasErrors);
            output.WriteLine("Сработало: ошибка найдена, программа не упала");
        }

        [Fact(DisplayName = "Параметр без значения даёт ошибку")]
        public void CommandLine_MissingValue_GivesError()
        {
            CommandLineOptions options = CommandLineOptions.Parse(new[] { "--report" });
            output.WriteLine("Командная строка: --report (а имя файла не указано)");
            output.WriteLine("Программа ответила: " + string.Join("; ", options.Errors));

            Assert.True(options.HasErrors);
            output.WriteLine("Сработало: ошибка найдена");
        }

        [Fact(DisplayName = "Командная строка меняет только то, что в ней указано")]
        public void ApplyTo_ChangesOnlyGivenSettings()
        {
            var settings = new AppSettings { Threshold = 15, Folder = "old" };
            output.WriteLine("Было: папка old, порог 15. В командной строке только папка new");

            CommandLineOptions.Parse(new[] { "new" }).ApplyTo(settings);
            output.WriteLine($"Стало: папка {settings.Folder}, порог {settings.Threshold}");

            Assert.Equal(Path.GetFullPath("new"), settings.Folder); // относительный путь превращается в полный
            Assert.Equal(15, settings.Threshold);
            output.WriteLine("Сработало: папка поменялась, порог остался прежним");
        }

        [Fact(DisplayName = "Настройки сохраняются и загружаются обратно")]
        public void Settings_SaveAndLoad()
        {
            using (var images = new TestImages())
            {
                string file = Path.Combine(images.Folder, "settings.json");
                var settings = new AppSettings { Threshold = 7, Mode = SearchMode.ExactOnly, DarkTheme = true };
                settings.Save(file);
                output.WriteLine("Сохранили: порог 7, только точные копии, тёмная тема");
                output.WriteLine("Содержимое файла:");
                output.WriteLine(File.ReadAllText(file));

                AppSettings loaded = AppSettings.Load(file);

                Assert.Equal(7, loaded.Threshold);
                Assert.Equal(SearchMode.ExactOnly, loaded.Mode);
                Assert.True(loaded.DarkTheme);
                output.WriteLine("Сработало: после загрузки значения те же");
            }
        }

        [Fact(DisplayName = "Испорченный файл настроек не ломает программу")]
        public void Settings_BrokenFile_GivesDefaults()
        {
            using (var images = new TestImages())
            {
                string file = Path.Combine(images.Folder, "settings.json");
                File.WriteAllText(file, "{ испорчено");
                output.WriteLine("В файле настроек мусор: { испорчено");

                AppSettings loaded = AppSettings.Load(file);
                output.WriteLine("Загруженный порог: " + loaded.Threshold);

                Assert.Equal(DuplicateFinder.DefaultThreshold, loaded.Threshold);
                output.WriteLine("Сработало: вместо ошибки взяты настройки по умолчанию");
            }
        }

        [Fact(DisplayName = "При перемещении занятое имя получает номер (1)")]
        public void MoveToFolder_AddsNumberWhenNameIsTaken()
        {
            using (var images = new TestImages())
            {
                string target = Path.Combine(images.Folder, "target");
                string first = images.SaveJpeg("photo.jpg", 1, 50, 50);
                string second = images.SaveJpeg(Path.Combine("sub", "photo.jpg"), 2, 50, 50);
                output.WriteLine("Перемещаем в одну папку два разных файла с именем photo.jpg");

                string moved1 = FileOperations.MoveToFolder(first, target);
                string moved2 = FileOperations.MoveToFolder(second, target);
                output.WriteLine("Первый стал: " + Path.GetFileName(moved1));
                output.WriteLine("Второй стал: " + Path.GetFileName(moved2));

                Assert.Equal("photo.jpg", Path.GetFileName(moved1));
                Assert.Equal("photo (1).jpg", Path.GetFileName(moved2));
                Assert.False(File.Exists(first));
                Assert.True(File.Exists(moved2));
                output.WriteLine("Сработало: файлы не затёрли друг друга");
            }
        }

        [Fact(DisplayName = "Размер файла показывается в удобном виде")]
        public void FormatSize_IsReadable()
        {
            output.WriteLine("500 байт -> " + FileOperations.FormatSize(500));
            output.WriteLine("2048 байт -> " + FileOperations.FormatSize(2048));

            Assert.Equal("500 Б", FileOperations.FormatSize(500));
            Assert.Equal("2" + System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator + "0 КБ",
                FileOperations.FormatSize(2048));
            output.WriteLine("Сработало");
        }
    }
}
