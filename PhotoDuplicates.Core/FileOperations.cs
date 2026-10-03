namespace PhotoDuplicates.Core
{
    public static class FileOperations
    {
        public static string MoveToFolder(string filePath, string targetFolder)
        {
            Directory.CreateDirectory(targetFolder);

            string targetPath = GetFreeFileName(targetFolder, Path.GetFileName(filePath));
            File.Move(filePath, targetPath);
            return targetPath;
        }

        public static string GetFreeFileName(string folder, string fileName)
        {
            string path = Path.Combine(folder, fileName);
            string name = Path.GetFileNameWithoutExtension(fileName);
            string extension = Path.GetExtension(fileName);

            int number = 1;
            while (File.Exists(path))
            {
                path = Path.Combine(folder, $"{name} ({number}){extension}");
                number++;
            }
            return path;
        }

        public static string FormatSize(long bytes)
        {
            if (bytes < 1024)
            {
                return bytes + " Б";
            }
            if (bytes < 1024 * 1024)
            {
                return (bytes / 1024.0).ToString("0.0") + " КБ";
            }
            if (bytes < 1024L * 1024 * 1024)
            {
                return (bytes / 1024.0 / 1024.0).ToString("0.0") + " МБ";
            }
            return (bytes / 1024.0 / 1024.0 / 1024.0).ToString("0.00") + " ГБ";
        }
    }
}
