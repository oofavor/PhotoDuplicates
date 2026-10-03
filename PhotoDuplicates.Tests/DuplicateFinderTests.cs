using PhotoDuplicates.Core;
using Xunit.Abstractions;

namespace PhotoDuplicates.Tests
{
    public class DuplicateFinderTests
    {
        private readonly ITestOutputHelper output;

        public DuplicateFinderTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        // фото без настоящего файла, для проверки группировки этого достаточно
        private static PhotoInfo Photo(string path, string sha, ulong dHash, int width = 100, int height = 100, long size = 1000)
        {
            return new PhotoInfo
            {
                FilePath = path,
                Sha256 = sha,
                DHash = dHash,
                HasDHash = true,
                Width = width,
                Height = height,
                FileSize = size,
            };
        }

        // печатает группы в вывод теста
        private void PrintGroups(List<DuplicateGroup> groups)
        {
            output.WriteLine("Найдено групп: " + groups.Count);
            foreach (DuplicateGroup group in groups)
            {
                var names = new List<string>();
                foreach (PhotoInfo photo in group.Files)
                {
                    names.Add(photo.FilePath);
                }
                output.WriteLine($"  группа {group.Number} ({group.KindTitle}): {string.Join(", ", names)}");
            }
        }

        [Fact(DisplayName = "Точные копии попадают в одну группу")]
        public void ExactCopies_FormOneGroup()
        {
            var photos = new List<PhotoInfo>
            {
                Photo("a.jpg", "AAA", 0b1111),
                Photo("b.jpg", "AAA", 0b1111),
                Photo("c.jpg", "CCC", ulong.MaxValue), // совсем другое фото
            };
            output.WriteLine("Файлы: a.jpg и b.jpg одинаковые, c.jpg совсем другой");

            List<DuplicateGroup> groups = new DuplicateFinder().FindGroups(photos);
            PrintGroups(groups);

            Assert.Single(groups);
            Assert.Equal(GroupKind.ExactCopies, groups[0].Kind);
            Assert.Equal(2, groups[0].Files.Count);
            output.WriteLine("Сработало: одна группа из a.jpg и b.jpg, c.jpg в неё не попал");
        }

        [Fact(DisplayName = "Порог решает, считать ли фото похожими")]
        public void SimilarPhotos_UseThreshold()
        {
            var photos = new List<PhotoInfo>
            {
                Photo("a.jpg", "A", 0),
                Photo("b.jpg", "B", 0b111), // отличается на 3 бита
            };
            output.WriteLine("Фото отличаются на 3 бита");

            var strict = new DuplicateFinder { Threshold = 2 };
            var normal = new DuplicateFinder { Threshold = 3 };
            output.WriteLine("С порогом 2 групп: " + strict.FindGroups(photos).Count);
            output.WriteLine("С порогом 3 групп: " + normal.FindGroups(photos).Count);

            Assert.Empty(strict.FindGroups(photos));
            Assert.Single(normal.FindGroups(photos));
            Assert.Equal(GroupKind.Similar, normal.FindGroups(photos)[0].Kind);
            output.WriteLine("Сработало: при пороге 2 фото разные, при пороге 3 уже похожие");
        }

        [Fact(DisplayName = "В режиме только точных копий похожие фото не группируются")]
        public void ExactOnlyMode_IgnoresSimilarPhotos()
        {
            var photos = new List<PhotoInfo>
            {
                Photo("a.jpg", "A", 0),
                Photo("b.jpg", "B", 0), // такой же dHash, но другой файл
            };
            output.WriteLine("У файлов одинаковый dHash, но разный SHA-256");

            var finder = new DuplicateFinder { Mode = SearchMode.ExactOnly };
            List<DuplicateGroup> groups = finder.FindGroups(photos);
            PrintGroups(groups);

            Assert.Empty(groups);
            output.WriteLine("Сработало: в этом режиме dHash не учитывается");
        }

        [Fact(DisplayName = "Похожие по цепочке (A на B, B на C) попадают в одну группу")]
        public void Chain_OfSimilarPhotos_IsOneGroup()
        {
            // a похож на b (2 бита), b похож на c (2 бита), а a и c отличаются на 4 бита
            var photos = new List<PhotoInfo>
            {
                Photo("a.jpg", "A", 0b0000),
                Photo("b.jpg", "B", 0b0011),
                Photo("c.jpg", "C", 0b1111),
            };
            output.WriteLine("a и b отличаются на 2 бита, b и c на 2 бита, a и c на 4 бита, порог 2");

            var finder = new DuplicateFinder { Threshold = 2 };
            List<DuplicateGroup> groups = finder.FindGroups(photos);
            PrintGroups(groups);

            Assert.Single(groups);
            Assert.Equal(3, groups[0].Files.Count);
            output.WriteLine("Сработало: все трое в одной группе, хотя a и c напрямую не похожи");
        }

        [Fact(DisplayName = "Лучшим файлом выбирается фото с наибольшим разрешением")]
        public void BestFile_HasLargestResolution()
        {
            var photos = new List<PhotoInfo>
            {
                Photo("small.jpg", "S", 0, 800, 600),
                Photo("big.jpg", "B", 0, 4000, 3000),
                Photo("medium.jpg", "M", 0, 1600, 1200),
            };
            output.WriteLine("Файлы: small 800x600, big 4000x3000, medium 1600x1200");

            DuplicateGroup group = new DuplicateFinder().FindGroups(photos)[0];
            output.WriteLine("Лучшим выбран: " + group.BestFile.FilePath);

            Assert.Equal("big.jpg", group.BestFile.FilePath);
            Assert.Equal(group.BestFile, group.Files[0]);
            output.WriteLine("Сработало: выбран самый большой и он стоит в группе первым");
        }

        [Fact(DisplayName = "Освобождаемое место равно размеру всех файлов, кроме лучшего")]
        public void BytesToFree_IsSizeOfAllExceptBest()
        {
            var photos = new List<PhotoInfo>
            {
                Photo("a.jpg", "X", 0, size: 5000),
                Photo("b.jpg", "X", 0, size: 5000),
                Photo("c.jpg", "X", 0, size: 5000),
            };
            output.WriteLine("Три одинаковых файла по 5000 байт");

            DuplicateGroup group = new DuplicateFinder().FindGroups(photos)[0];
            output.WriteLine("Можно освободить: " + group.BytesToFree + " байт");

            Assert.Equal(10000, group.BytesToFree);
            output.WriteLine("Сработало: один файл оставляем, два удаляем, это 10000 байт");
        }

        [Fact(DisplayName = "Файлы без dHash могут быть только точными копиями")]
        public void PhotoWithoutDHash_CanBeOnlyExactCopy()
        {
            var broken1 = new PhotoInfo { FilePath = "x.jpg", Sha256 = "X", HasDHash = false };
            var broken2 = new PhotoInfo { FilePath = "y.jpg", Sha256 = "Y", HasDHash = false };
            output.WriteLine("Два битых файла с разным SHA-256 и без dHash");

            List<DuplicateGroup> groups = new DuplicateFinder().FindGroups(new List<PhotoInfo> { broken1, broken2 });
            PrintGroups(groups);

            Assert.Empty(groups);
            output.WriteLine("Сработало: без dHash их нельзя считать похожими");
        }

        [Fact(DisplayName = "После удаления файла группы пересобираются")]
        public void RemoveFile_RebuildsGroups()
        {
            var photos = new List<PhotoInfo>
            {
                Photo("a.jpg", "A", 0, 4000, 3000),
                Photo("b.jpg", "A", 0, 4000, 3000),
                Photo("c.jpg", "C", ulong.MaxValue),
                Photo("d.jpg", "C", ulong.MaxValue),
            };
            var finder = new DuplicateFinder();
            var scan = new ScanResult();
            scan.Photos.AddRange(photos);
            var result = new SearchResult(scan, finder.FindGroups(photos), finder);
            output.WriteLine("До удаления:");
            PrintGroups(result.Groups);

            result.RemoveFile("d.jpg"); // в группе c/d остаётся один файл, группа исчезает
            output.WriteLine("После удаления d.jpg:");
            PrintGroups(result.Groups);

            Assert.Single(result.Groups);
            Assert.Equal(3, result.Scan.Photos.Count);
            output.WriteLine("Сработало: группа c/d исчезла, потому что в ней остался один файл");
        }
    }
}
