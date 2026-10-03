namespace PhotoDuplicates.Core
{
    public class PhotoInfo
    {
        public string FilePath { get; set; } = "";

        public long FileSize { get; set; }

        public DateTime LastWriteTimeUtc { get; set; }

        public string Sha256 { get; set; } = "";

        public bool HasDHash { get; set; }

        public ulong DHash { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public string FileName => Path.GetFileName(FilePath);

        public string ResolutionText => HasDHash ? $"{Width}×{Height}" : "?";

        public override string ToString()
        {
            return FilePath;
        }
    }
}
