namespace PhotoDuplicates.Core
{
    public class ScanProgressEventArgs : EventArgs
    {
        public int Done { get; }

        public int Total { get; }

        public string FilePath { get; }

        public ScanProgressEventArgs(int done, int total, string filePath)
        {
            Done = done;
            Total = total;
            FilePath = filePath;
        }
    }
}
