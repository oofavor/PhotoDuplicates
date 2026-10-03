using System.Globalization;

namespace PhotoDuplicates.Core
{
    public class CommandLineOptions
    {
        public string? Folder { get; set; }
        public int? Threshold { get; set; }
        public bool? IncludeSubfolders { get; set; }
        public SearchMode? Mode { get; set; }
        public bool? UseCache { get; set; }

        public bool ClearCache { get; set; }

        public string? ReportFile { get; set; }

        public bool ShowHelp { get; set; }

        public List<string> Errors { get; } = new List<string>();

        public bool HasErrors => Errors.Count > 0;

        public void ApplyTo(AppSettings settings)
        {
            if (Folder != null)
            {
                settings.Folder = Path.GetFullPath(Folder); // относительный путь превращаем в полный
            }
            if (Threshold != null)
            {
                settings.Threshold = Threshold.Value;
            }
            if (IncludeSubfolders != null)
            {
                settings.IncludeSubfolders = IncludeSubfolders.Value;
            }
            if (Mode != null)
            {
                settings.Mode = Mode.Value;
            }
            if (UseCache != null)
            {
                settings.UseCache = UseCache.Value;
            }
        }

        public static CommandLineOptions Parse(string[] args)
        {
            var options = new CommandLineOptions();

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];

                switch (arg.ToLowerInvariant())
                {
                    case "-h":
                    case "--help":
                    case "/?":
                        options.ShowHelp = true;
                        break;

                    case "-f":
                    case "--folder":
                        options.Folder = ReadValue(args, ref i, options);
                        break;

                    case "-t":
                    case "--threshold":
                        options.Threshold = ReadThreshold(args, ref i, options);
                        break;

                    case "-r":
                    case "--recursive":
                        options.IncludeSubfolders = true;
                        break;

                    case "--no-recursive":
                        options.IncludeSubfolders = false;
                        break;

                    case "-e":
                    case "--exact-only":
                        options.Mode = SearchMode.ExactOnly;
                        break;

                    case "-s":
                    case "--similar":
                        options.Mode = SearchMode.ExactAndSimilar;
                        break;

                    case "--no-cache":
                        options.UseCache = false;
                        break;

                    case "--clear-cache":
                        options.ClearCache = true;
                        break;

                    case "-o":
                    case "--report":
                        options.ReportFile = ReadValue(args, ref i, options);
                        break;

                    default:
                        if (arg.StartsWith("-"))
                        {
                            options.Errors.Add("Неизвестный параметр: " + arg);
                        }
                        else if (options.Folder == null)
                        {
                            options.Folder = arg; // параметр без имени считаем папкой
                        }
                        else
                        {
                            options.Errors.Add("Лишний параметр: " + arg);
                        }
                        break;
                }
            }

            return options;
        }

        // берёт следующий элемент как значение и сдвигает i (поэтому ref)
        private static string? ReadValue(string[] args, ref int i, CommandLineOptions options)
        {
            if (i + 1 >= args.Length)
            {
                options.Errors.Add($"После {args[i]} нужно указать значение.");
                return null;
            }
            i++;
            return args[i];
        }

        private static int? ReadThreshold(string[] args, ref int i, CommandLineOptions options)
        {
            string? text = ReadValue(args, ref i, options);
            if (text == null)
            {
                return null;
            }

            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                || value < 0 || value > DuplicateFinder.MaxThreshold)
            {
                options.Errors.Add($"Порог должен быть целым числом от 0 до {DuplicateFinder.MaxThreshold}. Получено: {text}");
                return null;
            }
            return value;
        }
    }
}
