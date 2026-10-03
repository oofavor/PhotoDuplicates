namespace PhotoDuplicates.Core
{
    public class SearchResult
    {
        public ScanResult Scan { get; }
        public List<DuplicateGroup> Groups { get; }
        public DuplicateFinder Finder { get; }

        public SearchResult(ScanResult scan, List<DuplicateGroup> groups, DuplicateFinder finder)
        {
            Scan = scan;
            Groups = groups;
            Finder = finder;
        }

        public string BuildReport()
        {
            return ReportBuilder.Build(Scan, Groups, Finder);
        }

        // вызывается после того, как пользователь удалил или переместил файл
        public void RemoveFile(string filePath)
        {
            Scan.Photos.RemoveAll(photo => photo.FilePath == filePath);

            // пересобираем группы из оставшихся файлов
            var remaining = new List<PhotoInfo>();
            foreach (DuplicateGroup group in Groups)
            {
                foreach (PhotoInfo photo in group.Files)
                {
                    if (photo.FilePath != filePath)
                    {
                        remaining.Add(photo);
                    }
                }
            }

            List<DuplicateGroup> newGroups = Finder.FindGroups(remaining);
            Groups.Clear();
            Groups.AddRange(newGroups);
        }
    }

    // весь поиск одним вызовом: кеш -> обход папки -> сохранение кеша -> группы
    public class DuplicateSearch
    {
        public HashCache Cache { get; }

        public event EventHandler<ScanProgressEventArgs>? ProgressChanged;

        public DuplicateSearch(HashCache cache)
        {
            Cache = cache;
        }

        public SearchResult Run(AppSettings settings, bool clearCache, CancellationToken cancel)
        {
            HashCache? cacheToUse = null;
            if (settings.UseCache)
            {
                Cache.Load();
                if (clearCache)
                {
                    Cache.Clear();
                }
                cacheToUse = Cache;
            }

            var scanner = new PhotoScanner(cacheToUse);
            scanner.ProgressChanged += Scanner_ProgressChanged;
            ScanResult scan = scanner.Scan(settings.Folder, settings.IncludeSubfolders, settings.Mode, cancel);

            if (cacheToUse != null)
            {
                try
                {
                    cacheToUse.Save();
                }
                catch (Exception ex)
                {
                    scan.Errors.Add("Не удалось сохранить кеш: " + ex.Message);
                }
            }

            var finder = new DuplicateFinder();
            finder.Threshold = settings.Threshold;
            finder.Mode = settings.Mode;
            List<DuplicateGroup> groups = finder.FindGroups(scan.Photos);

            return new SearchResult(scan, groups, finder);
        }

        private void Scanner_ProgressChanged(object? sender, ScanProgressEventArgs e)
        {
            // пробрасываем событие сканера наружу
            ProgressChanged?.Invoke(this, e);
        }
    }
}
