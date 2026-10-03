using System.Text.Json;

namespace PhotoDuplicates.Core
{
    public class AppSettings
    {
        public string Folder { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

        public bool IncludeSubfolders { get; set; } = true;

        public int Threshold { get; set; } = DuplicateFinder.DefaultThreshold;

        public SearchMode Mode { get; set; } = SearchMode.ExactAndSimilar;

        public bool UseCache { get; set; } = true;

        public int ThumbnailSize { get; set; } = 128;

        public bool DarkTheme { get; set; } = false;

        public string MoveFolder { get; set; } = "";

        public AppSettings Clone()
        {
            return (AppSettings)MemberwiseClone();
        }

        // чинит значения после ручной правки или порчи файла
        public void FixInvalidValues()
        {
            Threshold = Math.Clamp(Threshold, 0, DuplicateFinder.MaxThreshold);
            ThumbnailSize = Math.Clamp(ThumbnailSize, 64, 256);
            if (Folder == null)
            {
                Folder = "";
            }
            if (MoveFolder == null)
            {
                MoveFolder = "";
            }
        }

        public static string DefaultFilePath
        {
            get
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(appData, "PhotoDuplicates", "settings.json");
            }
        }

        public static AppSettings Load(string filePath)
        {
            AppSettings? settings = null;
            try
            {
                if (File.Exists(filePath))
                {
                    settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(filePath));
                }
            }
            catch (Exception)
            {
                settings = null;
            }

            if (settings == null)
            {
                settings = new AppSettings();
            }
            settings.FixInvalidValues();
            return settings;
        }

        public void Save(string filePath)
        {
            string? folder = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(filePath, JsonSerializer.Serialize(this, options));
        }
    }
}
