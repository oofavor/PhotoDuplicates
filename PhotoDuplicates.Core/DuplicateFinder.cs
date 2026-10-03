namespace PhotoDuplicates.Core
{
    // собирает файлы в группы, дубликаты это одинаковый SHA-256 или dHash,
    // отличающийся не больше чем на Threshold бит, группы собираются по цепочке:
    // если A похож на B, а B на C, все трое попадут в одну группу
    public class DuplicateFinder
    {
        public const int DefaultThreshold = 10;

        public const int MaxThreshold = 32;

        public int Threshold { get; set; } = DefaultThreshold;

        public SearchMode Mode { get; set; } = SearchMode.ExactAndSimilar;

        public bool AreDuplicates(PhotoInfo a, PhotoInfo b)
        {
            if (a.Sha256 == b.Sha256)
            {
                return true;
            }

            if (Mode == SearchMode.ExactOnly)
            {
                return false;
            }

            if (!a.HasDHash || !b.HasDHash)
            {
                return false;
            }

            return PerceptualHash.Distance(a.DHash, b.DHash) <= Threshold;
        }

        public List<DuplicateGroup> FindGroups(List<PhotoInfo> photos)
        {
            var groups = new List<DuplicateGroup>();
            bool[] used = new bool[photos.Count]; // файл уже попал в какую-то группу

            for (int start = 0; start < photos.Count; start++)
            {
                if (used[start])
                {
                    continue;
                }

                // поиск в ширину: в очереди файлы группы, для которых ещё не искали похожих
                var members = new List<PhotoInfo>();
                var queue = new Queue<int>();
                queue.Enqueue(start);
                used[start] = true;

                while (queue.Count > 0)
                {
                    int current = queue.Dequeue();
                    members.Add(photos[current]);

                    for (int other = 0; other < photos.Count; other++)
                    {
                        if (!used[other] && AreDuplicates(photos[current], photos[other]))
                        {
                            used[other] = true;
                            queue.Enqueue(other);
                        }
                    }
                }

                if (members.Count >= 2)
                {
                    groups.Add(CreateGroup(members));
                }
            }

            SortGroups(groups);
            return groups;
        }

        private static DuplicateGroup CreateGroup(List<PhotoInfo> members)
        {
            var group = new DuplicateGroup();

            // у всех одинаковый SHA-256, значит это точные копии
            group.Kind = GroupKind.ExactCopies;
            foreach (PhotoInfo photo in members)
            {
                if (photo.Sha256 != members[0].Sha256)
                {
                    group.Kind = GroupKind.Similar;
                }
            }

            // лучший файл первым, остальные по пути
            PhotoInfo best = members[0];
            foreach (PhotoInfo photo in members)
            {
                if (IsBetter(photo, best))
                {
                    best = photo;
                }
            }

            group.Files.Add(best);
            members.Remove(best);
            members.Sort((a, b) => string.Compare(a.FilePath, b.FilePath, StringComparison.OrdinalIgnoreCase));
            group.Files.AddRange(members);

            return group;
        }

        // лучше = больше пикселей, потом больше размер файла, потом короче путь
        public static bool IsBetter(PhotoInfo a, PhotoInfo b)
        {
            long pixelsA = (long)a.Width * a.Height;
            long pixelsB = (long)b.Width * b.Height;
            if (pixelsA != pixelsB)
            {
                return pixelsA > pixelsB;
            }

            if (a.FileSize != b.FileSize)
            {
                return a.FileSize > b.FileSize;
            }

            return a.FilePath.Length < b.FilePath.Length;
        }

        private static void SortGroups(List<DuplicateGroup> groups)
        {
            groups.Sort((a, b) =>
            {
                if (a.Kind != b.Kind)
                {
                    return a.Kind.CompareTo(b.Kind);
                }
                return b.Files.Count.CompareTo(a.Files.Count);
            });

            for (int i = 0; i < groups.Count; i++)
            {
                groups[i].Number = i + 1;
            }
        }
    }
}
