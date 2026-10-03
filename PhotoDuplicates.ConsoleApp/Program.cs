using System.Text;
using PhotoDuplicates.Core;

namespace PhotoDuplicates.ConsoleApp
{
    // консольный режим: только печатает отчёт, файлы не трогает
    internal class Program
    {
        private static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8; // иначе кириллица в консоли ломается

            CommandLineOptions options = CommandLineOptions.Parse(args);

            if (options.HasErrors)
            {
                Console.WriteLine("Ошибки в параметрах:");
                foreach (string error in options.Errors)
                {
                    Console.WriteLine("  - " + error);
                }
                Console.WriteLine("Справка: PhotoDuplicates.ConsoleApp.exe --help");
                return 1;
            }

            if (options.ShowHelp)
            {
                Console.WriteLine(HelpText.CommandLine);
                return 0;
            }

            // настройки из файла, параметры командной строки поверх них
            AppSettings settings = AppSettings.Load(AppSettings.DefaultFilePath);
            options.ApplyTo(settings);
            settings.FixInvalidValues();

            if (!Directory.Exists(settings.Folder))
            {
                Console.WriteLine("Папка не найдена: " + settings.Folder);
                return 2;
            }

            Console.WriteLine("Поиск дубликатов в папке: " + settings.Folder);

            var search = new DuplicateSearch(new HashCache());
            search.ProgressChanged += Search_ProgressChanged;

            SearchResult result = search.Run(settings, options.ClearCache, CancellationToken.None);

            Console.WriteLine();
            Console.WriteLine();

            string report = result.BuildReport();
            Console.WriteLine(report);

            if (options.ReportFile != null)
            {
                File.WriteAllText(options.ReportFile, report, Encoding.UTF8);
                Console.WriteLine("Отчёт сохранён в файл: " + Path.GetFullPath(options.ReportFile));
            }

            return 0;
        }

        private static void Search_ProgressChanged(object? sender, ScanProgressEventArgs e)
        {
            // если вывод перенаправлен (например, в Jupyter), счётчик не печатаем, иначе будет каша из \r
            if (Console.IsOutputRedirected)
            {
                return;
            }

            // \r возвращает курсор в начало строки, счётчик обновляется на месте
            Console.Write($"\rОбработано файлов: {e.Done} из {e.Total}");
        }
    }
}
