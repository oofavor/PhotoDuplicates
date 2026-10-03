using System.Text.Json;

namespace PhotoDuplicates.Core
{
    // кеш хешей в JSON, запись считается верной, только если у файла
    // не изменились размер и дата изменения
    public class HashCache
    {
        // ключ это путь к файлу, регистр букв не важен (как в Windows)
        private readonly Dictionary<string, PhotoInfo> entries =
            new Dictionary<string, PhotoInfo>(StringComparer.OrdinalIgnoreCase);

        public string FilePath { get; }

        public int Count => entries.Count;

        public HashCache()
            : this(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PhotoDuplicates", "hash-cache.json"))
        {
        }

        public HashCache(string filePath)
        {
            FilePath = filePath;
        }

        public PhotoInfo? Find(string filePath, long fileSize, DateTime lastWriteTimeUtc)
        {
            if (!entries.TryGetValue(filePath, out PhotoInfo? cached))
            {
                return null;
            }

            if (cached.FileSize != fileSize || cached.LastWriteTimeUtc != lastWriteTimeUtc)
            {
                return null; // файл менялся, хеши устарели
            }

            return cached;
        }

        public void Add(PhotoInfo photo)
        {
            entries[photo.FilePath] = photo;
        }

        public void Remove(string filePath)
        {
            entries.Remove(filePath);
        }

        public void Clear()
        {
            entries.Clear();
        }

        public void Load()
        {
            entries.Clear();

            try
            {
                if (!File.Exists(FilePath))
                {
                    return;
                }

                string json = File.ReadAllText(FilePath);
                List<PhotoInfo>? list = JsonSerializer.Deserialize<List<PhotoInfo>>(json);
                if (list == null)
                {
                    return;
                }

                foreach (PhotoInfo photo in list)
                {
                    entries[photo.FilePath] = photo;
                }
            }
            catch (Exception)
            {
                // битый кеш не страшен, хеши просто посчитаются заново
                entries.Clear();
            }
        }

        // записи об удалённых файлах при сохранении выбрасываем
        public void Save()
        {
            var list = new List<PhotoInfo>();
            foreach (PhotoInfo photo in entries.Values)
            {
                if (File.Exists(photo.FilePath))
                {
                    list.Add(photo);
                }
            }

            string? folder = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }

            string json = JsonSerializer.Serialize(list);
            File.WriteAllText(FilePath, json);
        }
    }
}
