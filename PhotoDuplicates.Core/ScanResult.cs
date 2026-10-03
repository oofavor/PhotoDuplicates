namespace PhotoDuplicates.Core
{
    public class ScanResult
    {
        public string Folder { get; set; } = "";

        public List<PhotoInfo> Photos { get; } = new List<PhotoInfo>();

        public List<string> Errors { get; } = new List<string>();

        public int FromCacheCount { get; set; }

        public bool WasCancelled { get; set; }

        public TimeSpan Elapsed { get; set; }
    }
}
