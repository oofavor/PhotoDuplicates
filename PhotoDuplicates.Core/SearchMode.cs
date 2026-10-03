namespace PhotoDuplicates.Core
{
    public enum SearchMode
    {
        ExactAndSimilar,

        ExactOnly, // только SHA-256, картинки не открываются, поэтому быстрее
    }
}
