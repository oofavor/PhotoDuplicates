using System.Text;

namespace PhotoDuplicates.Core
{
    public static class ReportBuilder
    {
        public static string Build(ScanResult scan, List<DuplicateGroup> groups, DuplicateFinder finder)
        {
            var text = new StringBuilder();

            int exactCount = 0;
            int similarCount = 0;
            long bytesToFree = 0;
            foreach (DuplicateGroup group in groups)
            {
                if (group.Kind == GroupKind.ExactCopies)
                {
                    exactCount++;
                }
                else
                {
                    similarCount++;
                }
                bytesToFree = bytesToFree + group.BytesToFree;
            }

            string modeText = finder.Mode == SearchMode.ExactOnly
                ? "только точные копии"
                : $"точные копии и похожие (порог {finder.Threshold} бит из 64)";

            text.AppendLine("ОТЧЁТ О ПОИСКЕ ДУБЛИКАТОВ ФОТОГРАФИЙ");
            text.AppendLine($"Папка:              {scan.Folder}");
            text.AppendLine($"Дата:               {DateTime.Now:dd.MM.yyyy HH:mm}");
            text.AppendLine($"Режим:              {modeText}");
            text.AppendLine($"Проверено файлов:   {scan.Photos.Count} (из кеша: {scan.FromCacheCount}, время: {scan.Elapsed.TotalSeconds:0.0} сек.)");
            text.AppendLine($"Найдено групп:      {groups.Count} (точные копии: {exactCount}, похожие: {similarCount})");
            text.AppendLine($"Можно освободить:   {FileOperations.FormatSize(bytesToFree)} (если в каждой группе оставить один лучший файл)");
            if (scan.WasCancelled)
            {
                text.AppendLine("ВНИМАНИЕ: поиск был прерван, проверены не все файлы.");
            }
            text.AppendLine();

            foreach (DuplicateGroup group in groups)
            {
                text.AppendLine($"Группа {group.Number} - {group.KindTitle}, файлов: {group.Files.Count}");

                foreach (PhotoInfo photo in group.Files)
                {
                    string mark = photo == group.BestFile ? "[оставить]" : "[дубликат]";
                    string distance = "";
                    if (photo != group.BestFile && group.Kind == GroupKind.Similar)
                    {
                        if (photo.Sha256 == group.BestFile.Sha256)
                        {
                            distance = ", точная копия";
                        }
                        else
                        {
                            distance = $", отличие {group.DistanceToBest(photo)} бит";
                        }
                    }

                    text.AppendLine($"  {mark} {photo.FilePath}");
                    text.AppendLine($"             {photo.ResolutionText}, {FileOperations.FormatSize(photo.FileSize)}{distance}");
                }
                text.AppendLine();
            }

            if (groups.Count == 0)
            {
                text.AppendLine("Дубликаты не найдены.");
                text.AppendLine();
            }

            if (scan.Errors.Count > 0)
            {
                text.AppendLine($"Проблемы при чтении ({scan.Errors.Count}):");
                foreach (string error in scan.Errors)
                {
                    text.AppendLine("  " + error);
                }
            }

            return text.ToString();
        }
    }
}
