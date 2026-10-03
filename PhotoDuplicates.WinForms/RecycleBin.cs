using Microsoft.VisualBasic.FileIO;

namespace PhotoDuplicates.WinForms
{
    // в .NET нет метода "удалить в корзину", а в Microsoft.VisualBasic есть, и из C# им можно пользоваться
    internal static class RecycleBin
    {
        public static void DeleteFile(string filePath)
        {
            FileSystem.DeleteFile(filePath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
        }
    }
}
