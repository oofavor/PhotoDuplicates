namespace PhotoDuplicates.Core
{
    public enum GroupKind
    {
        ExactCopies,

        Similar,
    }

    public class DuplicateGroup
    {
        public int Number { get; set; }

        public GroupKind Kind { get; set; }

        public List<PhotoInfo> Files { get; } = new List<PhotoInfo>(); // первым всегда идёт лучший файл

        public PhotoInfo BestFile => Files[0];

        public string KindTitle => Kind == GroupKind.ExactCopies ? "точные копии" : "похожие";

        public long BytesToFree
        {
            get
            {
                long total = 0;
                for (int i = 1; i < Files.Count; i++)
                {
                    total = total + Files[i].FileSize;
                }
                return total;
            }
        }

        // -1, если у одного из файлов нет dHash
        public int DistanceToBest(PhotoInfo photo)
        {
            if (photo.Sha256 == BestFile.Sha256)
            {
                return 0;
            }
            if (!photo.HasDHash || !BestFile.HasDHash)
            {
                return -1;
            }
            return PerceptualHash.Distance(photo.DHash, BestFile.DHash);
        }

        public override string ToString()
        {
            return $"Группа {Number}: {KindTitle}, файлов: {Files.Count}";
        }
    }
}
