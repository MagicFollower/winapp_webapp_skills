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

public enum FileChangeKind
{
    Created = 0,
    Changed,
    Deleted,
    Renamed
}

/// <summary>watcher/对账层交给 FileIndex 的最小变更单元。Renamed 用 Path 表示新路径、OldPath 表示旧路径。</summary>
public sealed class FileChange
{
    public FileChangeKind Kind { get; init; }
    public string Path { get; init; } = "";
    public string? OldPath { get; init; }

    public static FileChange Created(string path) => new() { Kind = FileChangeKind.Created, Path = path };
    public static FileChange Changed(string path) => new() { Kind = FileChangeKind.Changed, Path = path };
    public static FileChange Deleted(string path) => new() { Kind = FileChangeKind.Deleted, Path = path };
    public static FileChange Renamed(string oldPath, string newPath) => new()
    {
        Kind = FileChangeKind.Renamed,
        Path = newPath,
        OldPath = oldPath
    };
}

/// <summary>目录直接子项的快照；对账时用来和索引里的子项比较。用 struct 是为了在扫描大目录时不产生百万级临时对象。</summary>
public readonly struct DirectoryChildInfo
{
    public string Name { get; init; }
    public string FullPath { get; init; }
    public bool IsDirectory { get; init; }
    public long Length { get; init; }
    public long ModifiedUtcTicks { get; init; }
    public FileAttributes Attributes { get; init; }
}

public sealed class ReconcileResult
{
    public int DirectoriesChecked { get; init; }
    public int DirectoriesChanged { get; init; }
    public int Added { get; init; }
    public int Removed { get; init; }
    public int Updated { get; init; }
    public long ElapsedMs { get; init; }
    public IReadOnlyList<string> ChangedPaths { get; init; } = Array.Empty<string>();

    public bool HasChanges => Added + Removed + Updated > 0;
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
