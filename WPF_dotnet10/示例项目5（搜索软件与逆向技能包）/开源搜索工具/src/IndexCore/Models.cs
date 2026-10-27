using System.IO;

namespace IndexCore;

public enum CategoryFilter
{
    All = 0,
    Image,
    Document,
    Video,
    Audio,
    Mobile,
    Folder
}

[Flags]
public enum FileCategory : byte
{
    Other = 0,
    Image = 1,
    Document = 2,
    Video = 4,
    Audio = 8,
    Mobile = 16,
    Folder = 32
}

public enum SortField
{
    Name = 0,
    Size,
    Modified
}

public sealed class IndexOptions
{
    public List<string> Roots { get; set; } = new();
    public List<string> ExcludedPaths { get; set; } = new();
    public List<string> ExcludedDirectoryNames { get; set; } = new();
    public int MaxEntries { get; set; } = 4_000_000;
}

public sealed class IndexProgress
{
    public string Phase { get; init; } = "";
    public int Files { get; init; }
    public int Directories { get; init; }
    public string CurrentDirectory { get; init; } = "";
    public TimeSpan Elapsed { get; init; }
}

public readonly struct FileEntry
{
    public readonly int ParentIndex;
    public readonly int NameOffset;
    public readonly int NameLength;
    public readonly int LowerOffset;
    public readonly int LowerLength;
    public readonly long Size;
    public readonly long ModifiedUtcTicks;
    public readonly FileAttributes Attributes;
    public readonly FileCategory Category;

    public FileEntry(
        int parentIndex,
        int nameOffset,
        int nameLength,
        int lowerOffset,
        int lowerLength,
        long size,
        long modifiedUtcTicks,
        FileAttributes attributes,
        FileCategory category)
    {
        ParentIndex = parentIndex;
        NameOffset = nameOffset;
        NameLength = nameLength;
        LowerOffset = lowerOffset;
        LowerLength = lowerLength;
        Size = size;
        ModifiedUtcTicks = modifiedUtcTicks;
        Attributes = attributes;
        Category = category;
    }

    public bool IsDirectory => (Attributes & FileAttributes.Directory) != 0;
}

public sealed class SearchQuery
{
    public string Text { get; set; } = "";
    public CategoryFilter Category { get; set; } = CategoryFilter.All;
    public string? ScopePath { get; set; }
    public bool CaseSensitive { get; set; }
    public bool WholeWord { get; set; }
    public bool Wildcard { get; set; } = true;
    public SortField Sort { get; set; } = SortField.Modified;
    public bool Descending { get; set; } = true;
    public int MaxResults { get; set; } = 200_000;
}

public sealed class SearchResult
{
    public int[] Hits { get; init; } = Array.Empty<int>();
    public int Total { get; init; }
    public long ElapsedMs { get; init; }
    public bool ScopeMissing { get; init; }
}

public sealed class FileRow
{
    public int EntryIndex { get; init; }
    public string Name { get; init; } = "";
    public string Folder { get; init; } = "";
    public string Path { get; init; } = "";
    public long Size { get; init; }
    public DateTime ModifiedLocal { get; init; }
    public bool IsDirectory { get; init; }
    public FileCategory Category { get; init; }

    public string SizeText
    {
        get
        {
            if (IsDirectory) return "—";
            double value = Size;
            if (value < 1024) return value.ToString("N0") + " B";
            value /= 1024;
            if (value < 1024) return value.ToString("N0") + " KB";
            value /= 1024;
            if (value < 1024) return value.ToString("N1") + " MB";
            value /= 1024;
            return value.ToString("N2") + " GB";
        }
    }

    public string ModifiedText => ModifiedLocal.ToString("yyyy-MM-dd HH:mm");

    public override string ToString() => Name;

    public string KindText
    {
        get
        {
            if (IsDirectory) return "文件夹";
            string ext = System.IO.Path.GetExtension(Name);
            return string.IsNullOrEmpty(ext) ? "文件" : ext.TrimStart('.').ToUpperInvariant() + " 文件";
        }
    }
}
