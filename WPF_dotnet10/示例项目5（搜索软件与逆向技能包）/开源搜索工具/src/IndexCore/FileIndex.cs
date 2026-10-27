using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Enumeration;
using System.Text;

namespace IndexCore;

/// <summary>
/// 文件名内存索引：目录遍历建表，UTF-8 arena 存名称，快照落盘。
/// M1 不使用卷句柄（本机非管理员无法验证 USN/MFT）；接口保持“全量构建 + 查询 + 快照”，
/// M2 可把 Build 的扫描器换成 $MFT/USN，而不改查询与 UI。
/// </summary>
public sealed class FileIndex
{
    private const string SnapshotMagic = "IDX1";
    private const int SnapshotVersion = 1;
    private const int MaxSnapshotEntries = 20_000_000;
    private const int MaxSnapshotBytes = 1_500_000_000;

    private readonly FileEntry[] _entries;
    private readonly NameArena _names;
    private readonly NameArena _lower;
    private readonly string[] _roots;
    private readonly Dictionary<int, string> _pathCache = new();
    private readonly object _pathLock = new();
    private Dictionary<string, int>? _dirIndex;

    private FileIndex(FileEntry[] entries, NameArena names, NameArena lower, string[] roots)
    {
        _entries = entries;
        _names = names;
        _lower = lower;
        _roots = roots;

        int dirs = 0;
        foreach (var entry in entries)
            if (entry.IsDirectory) dirs++;
        DirectoryCount = dirs;
        FileCount = entries.Length - dirs;
    }

    public int Count => _entries.Length;
    public int FileCount { get; }
    public int DirectoryCount { get; }
    public IReadOnlyList<string> Roots => _roots;
    public string[] RootPaths => _roots;

    public static string DefaultSnapshotPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "IndexApp", "index.bin");

    public static FileIndex Build(IndexOptions options, IProgress<IndexProgress>? progress, CancellationToken ct)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));

        var roots = NormalizeRoots(options.Roots);
        if (roots.Count == 0) throw new InvalidOperationException("没有可索引的根目录。");

        var excludedPaths = NormalizeExcludes(options.ExcludedPaths);
        var excludedNames = new HashSet<string>(
            options.ExcludedDirectoryNames ?? new List<string>(), StringComparer.OrdinalIgnoreCase);

        var entries = new List<FileEntry>(1 << 16);
        var names = new NameArena(1 << 20);
        var lower = new NameArena(1 << 20);
        var stack = new Stack<(string Directory, int ParentIndex)>();
        var watch = Stopwatch.StartNew();
        int files = 0, directories = 0;

        foreach (string root in roots)
        {
            ct.ThrowIfCancellationRequested();
            if (!Directory.Exists(root)) continue;

            var info = new DirectoryInfo(root);
            int rootIndex = AddEntry(entries, names, lower, -1, root, 0,
                info.LastWriteTimeUtc.Ticks, FileAttributes.Directory, FileCategory.Folder);
            directories++;
            stack.Push((root, rootIndex));

            var enumerateOptions = new EnumerationOptions
            {
                RecurseSubdirectories = false,
                IgnoreInaccessible = true,
                AttributesToSkip = 0,
                ReturnSpecialDirectories = false,
                BufferSize = 64 * 1024
            };

            while (stack.Count > 0)
            {
                ct.ThrowIfCancellationRequested();
                var (directory, parentIndex) = stack.Pop();

                IEnumerable<ChildInfo> children;
                try
                {
                    children = new FileSystemEnumerable<ChildInfo>(
                        directory,
                        (ref FileSystemEntry entry) => new ChildInfo(
                            entry.FileName.ToString(),
                            entry.IsDirectory ? entry.ToFullPath() : null,
                            entry.IsDirectory,
                            entry.IsDirectory ? 0 : entry.Length,
                            entry.LastWriteTimeUtc.UtcDateTime.Ticks,
                            entry.Attributes),
                        enumerateOptions);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
                {
                    continue;
                }

                try
                {
                    foreach (var child in children)
                    {
                        ct.ThrowIfCancellationRequested();
                        if (child.IsDirectory && (excludedNames.Contains(child.Name) || IsExcludedPath(child.FullPath!, excludedPaths)))
                            continue;

                        if (entries.Count >= options.MaxEntries)
                            throw new InvalidOperationException("索引条目超过上限 " + options.MaxEntries + "，请缩小索引目录或提高上限。");

                        var category = child.IsDirectory ? FileCategory.Folder : CategoryOf(child.Name);
                        int index = AddEntry(entries, names, lower, parentIndex, child.Name,
                            child.Length, child.ModifiedUtcTicks, child.Attributes, category);

                        if (child.IsDirectory)
                        {
                            directories++;
                            if ((child.Attributes & FileAttributes.ReparsePoint) == 0)
                                stack.Push((child.FullPath!, index));
                        }
                        else
                        {
                            files++;
                        }

                        if ((files + directories) % 2000 == 0)
                        {
                            progress?.Report(new IndexProgress
                            {
                                Phase = "扫描",
                                Files = files,
                                Directories = directories,
                                CurrentDirectory = directory,
                                Elapsed = watch.Elapsed
                            });
                        }
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
                {
                    // 全盘扫描时文件可能在枚举途中被删除/锁住；跳过当前目录剩余项，不让整次索引崩掉。
                }
            }
        }

        progress?.Report(new IndexProgress
        {
            Phase = "写入快照",
            Files = files,
            Directories = directories,
            CurrentDirectory = "",
            Elapsed = watch.Elapsed
        });

        return new FileIndex(entries.ToArray(), names, lower, roots.ToArray());
    }

    public SearchResult Search(SearchQuery query, CancellationToken ct = default)
    {
        query ??= new SearchQuery();
        var watch = Stopwatch.StartNew();

        int scopeIndex = -1;
        if (!string.IsNullOrWhiteSpace(query.ScopePath))
        {
            scopeIndex = FindDirectoryIndex(query.ScopePath!);
            if (scopeIndex < 0)
            {
                return new SearchResult
                {
                    Hits = Array.Empty<int>(),
                    Total = 0,
                    ElapsedMs = watch.ElapsedMilliseconds,
                    ScopeMissing = true
                };
            }
        }

        var terms = ParseTerms(query.Text)
            .Select(term => query.CaseSensitive ? term : term.ToLowerInvariant())
            .Where(term => term.Length > 0)
            .ToArray();

        var termBytes = new byte[terms.Length][];
        for (int i = 0; i < terms.Length; i++)
            termBytes[i] = Encoding.UTF8.GetBytes(terms[i]);

        int entryCount = _entries.Length;
        if (entryCount == 0)
            return new SearchResult { Hits = Array.Empty<int>(), Total = 0, ElapsedMs = watch.ElapsedMilliseconds };

        int partitions = Math.Min(Math.Min(Environment.ProcessorCount, 16), Math.Max(1, entryCount / 50_000));
        var partials = new List<int>[partitions];
        var parallelOptions = new ParallelOptions
        {
            CancellationToken = ct,
            MaxDegreeOfParallelism = partitions
        };

        Parallel.For(0, partitions, parallelOptions, partition =>
        {
            int start = (int)((long)entryCount * partition / partitions);
            int end = (int)((long)entryCount * (partition + 1) / partitions);
            var local = new List<int>(256);

            for (int i = start; i < end; i++)
            {
                if ((i & 4095) == 0) ct.ThrowIfCancellationRequested();

                ref readonly FileEntry entry = ref _entries[i];
                if (!CategoryMatches(entry.Category, query.Category)) continue;
                if (scopeIndex >= 0 && !IsInScope(i, scopeIndex)) continue;
                if (!Matches(i, termBytes, query)) continue;

                local.Add(i);
            }

            partials[partition] = local;
        });

        int total = 0;
        foreach (var part in partials) total += part.Count;

        var all = new List<int>(total);
        foreach (var part in partials) all.AddRange(part);

        int[] hits = all.ToArray();
        Array.Sort(hits, new HitComparer(this, query.Sort, query.Descending));

        int max = Math.Max(1, query.MaxResults);
        if (hits.Length > max)
            Array.Resize(ref hits, max);

        return new SearchResult
        {
            Hits = hits,
            Total = total,
            ElapsedMs = watch.ElapsedMilliseconds
        };
    }

    public FileRow GetRow(int entryIndex)
    {
        if (entryIndex < 0 || entryIndex >= _entries.Length)
            return new FileRow();

        ref readonly FileEntry entry = ref _entries[entryIndex];
        string name = _names.GetString(entry.NameOffset, entry.NameLength);
        string path = GetFullPath(entryIndex);
        string folder = entry.ParentIndex >= 0 ? GetFullPath(entry.ParentIndex) : path;

        return new FileRow
        {
            EntryIndex = entryIndex,
            Name = name,
            Folder = folder,
            Path = path,
            Size = entry.Size,
            ModifiedLocal = new DateTime(entry.ModifiedUtcTicks, DateTimeKind.Utc).ToLocalTime(),
            IsDirectory = entry.IsDirectory,
            Category = entry.Category
        };
    }

    public string GetFullPath(int entryIndex)
    {
        if (entryIndex < 0 || entryIndex >= _entries.Length) return string.Empty;

        lock (_pathLock)
        {
            if (_pathCache.TryGetValue(entryIndex, out var cached)) return cached;

            var chain = new List<int>(8);
            int current = entryIndex;
            int guard = 0;
            while (current >= 0 && guard++ < 512 && !_pathCache.ContainsKey(current))
            {
                chain.Add(current);
                current = _entries[current].ParentIndex;
            }

            string prefix = current >= 0 && _pathCache.TryGetValue(current, out var found) ? found : string.Empty;

            for (int i = chain.Count - 1; i >= 0; i--)
            {
                ref readonly FileEntry entry = ref _entries[chain[i]];
                string name = _names.GetString(entry.NameOffset, entry.NameLength);
                if (prefix.Length == 0) prefix = name;
                else if (prefix.EndsWith("\\", StringComparison.Ordinal)) prefix += name;
                else prefix += "\\" + name;
                _pathCache[chain[i]] = prefix;
            }

            return _pathCache.TryGetValue(entryIndex, out var result) ? result : string.Empty;
        }
    }

    public int FindDirectoryIndex(string path)
    {
        string normalized = NormalizePath(path);
        if (_dirIndex == null)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _entries.Length; i++)
            {
                if (!_entries[i].IsDirectory) continue;
                map[GetFullPath(i)] = i;
            }
            _dirIndex = map;
        }

        return _dirIndex.TryGetValue(normalized, out int index) ? index : -1;
    }

    public void Save(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("快照路径为空。", nameof(path));

        string directory = Path.GetDirectoryName(path) ?? ".";
        Directory.CreateDirectory(directory);
        string temp = path + ".tmp";

        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20, FileOptions.WriteThrough))
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(Encoding.ASCII.GetBytes(SnapshotMagic));
            writer.Write(SnapshotVersion);
            writer.Write(_entries.Length);
            writer.Write(_names.Length);
            writer.Write(_lower.Length);
            writer.Write(_roots.Length);
            foreach (string root in _roots) writer.Write(root);

            foreach (var entry in _entries)
            {
                writer.Write(entry.ParentIndex);
                writer.Write(entry.NameOffset);
                writer.Write(entry.NameLength);
                writer.Write(entry.LowerOffset);
                writer.Write(entry.LowerLength);
                writer.Write(entry.Size);
                writer.Write(entry.ModifiedUtcTicks);
                writer.Write((int)entry.Attributes);
                writer.Write((byte)entry.Category);
            }

            writer.Write(_names.Buffer, 0, _names.Length);
            writer.Write(_lower.Buffer, 0, _lower.Length);
            writer.Write(ComputeChecksum(_entries, _names, _lower, _roots));
            writer.Flush();
            stream.Flush(true);
        }

        if (File.Exists(path)) File.Copy(path, path + ".bak", true);
        File.Move(temp, path, true);
    }

    public static FileIndex? Load(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, FileOptions.SequentialScan);
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

            byte[] magic = reader.ReadBytes(4);
            if (magic.Length != 4 || Encoding.ASCII.GetString(magic) != SnapshotMagic) return null;
            if (reader.ReadInt32() != SnapshotVersion) return null;

            int entryCount = reader.ReadInt32();
            int namesLength = reader.ReadInt32();
            int lowerLength = reader.ReadInt32();
            int rootCount = reader.ReadInt32();
            if (entryCount < 0 || entryCount > MaxSnapshotEntries) return null;
            if (namesLength < 0 || lowerLength < 0 || namesLength > MaxSnapshotBytes || lowerLength > MaxSnapshotBytes) return null;
            if (rootCount < 0 || rootCount > 4096) return null;

            var roots = new string[rootCount];
            for (int i = 0; i < rootCount; i++) roots[i] = reader.ReadString();

            var entries = new FileEntry[entryCount];
            for (int i = 0; i < entryCount; i++)
            {
                entries[i] = new FileEntry(
                    reader.ReadInt32(),
                    reader.ReadInt32(),
                    reader.ReadInt32(),
                    reader.ReadInt32(),
                    reader.ReadInt32(),
                    reader.ReadInt64(),
                    reader.ReadInt64(),
                    (FileAttributes)reader.ReadInt32(),
                    (FileCategory)reader.ReadByte());
            }

            byte[] names = reader.ReadBytes(namesLength);
            byte[] lower = reader.ReadBytes(lowerLength);
            if (names.Length != namesLength || lower.Length != lowerLength) return null;
            ulong checksum = reader.ReadUInt64();

            var nameArena = new NameArena(names);
            var lowerArena = new NameArena(lower);
            if (ComputeChecksum(entries, nameArena, lowerArena, roots) != checksum) return null;

            return new FileIndex(entries, nameArena, lowerArena, roots);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private bool Matches(int entryIndex, byte[][] terms, SearchQuery query)
    {
        if (terms.Length == 0) return true;

        ref readonly FileEntry entry = ref _entries[entryIndex];
        ReadOnlySpan<byte> name = query.CaseSensitive
            ? _names.Get(entry.NameOffset, entry.NameLength)
            : _lower.Get(entry.LowerOffset, entry.LowerLength);

        foreach (byte[] term in terms)
        {
            if (!MatchTerm(name, term, query)) return false;
        }
        return true;
    }

    private static bool MatchTerm(ReadOnlySpan<byte> name, ReadOnlySpan<byte> term, SearchQuery query)
    {
        bool hasWildcard = query.Wildcard
            && (term.IndexOf((byte)'*') >= 0 || term.IndexOf((byte)'?') >= 0);

        if (hasWildcard)
            return WildcardMatch(Encoding.UTF8.GetString(name), Encoding.UTF8.GetString(term));

        int position = name.IndexOf(term);
        if (position < 0) return false;
        if (!query.WholeWord) return true;
        return IsWholeWord(name, position, term.Length);
    }

    private static bool IsWholeWord(ReadOnlySpan<byte> name, int start, int length)
    {
        bool leftOk = start == 0 || !IsWordByte(name[start - 1]);
        int end = start + length;
        bool rightOk = end >= name.Length || !IsWordByte(name[end]);
        return leftOk && rightOk;
    }

    private static bool IsWordByte(byte value)
    {
        return value is >= (byte)'0' and <= (byte)'9'
            or >= (byte)'A' and <= (byte)'Z'
            or >= (byte)'a' and <= (byte)'z'
            or (byte)'_'
            or >= 0x80;
    }

    private static bool WildcardMatch(string text, string pattern)
    {
        int textIndex = 0, patternIndex = 0;
        int starIndex = -1, matchIndex = 0;

        while (textIndex < text.Length)
        {
            if (patternIndex < pattern.Length &&
                (pattern[patternIndex] == '?' || pattern[patternIndex] == text[textIndex]))
            {
                textIndex++;
                patternIndex++;
            }
            else if (patternIndex < pattern.Length && pattern[patternIndex] == '*')
            {
                starIndex = patternIndex++;
                matchIndex = textIndex;
            }
            else if (starIndex >= 0)
            {
                patternIndex = starIndex + 1;
                textIndex = ++matchIndex;
            }
            else
            {
                return false;
            }
        }

        while (patternIndex < pattern.Length && pattern[patternIndex] == '*') patternIndex++;
        return patternIndex == pattern.Length;
    }

    private bool IsInScope(int entryIndex, int scopeIndex)
    {
        int current = entryIndex;
        int guard = 0;
        while (current >= 0 && guard++ < 512)
        {
            if (current == scopeIndex) return true;
            current = _entries[current].ParentIndex;
        }
        return false;
    }

    private static bool CategoryMatches(FileCategory category, CategoryFilter filter)
    {
        if (filter == CategoryFilter.All) return true;
        if (filter == CategoryFilter.Folder) return category == FileCategory.Folder;
        return (category & ToCategory(filter)) != 0;
    }

    private static FileCategory ToCategory(CategoryFilter filter)
    {
        return filter switch
        {
            CategoryFilter.Image => FileCategory.Image,
            CategoryFilter.Document => FileCategory.Document,
            CategoryFilter.Video => FileCategory.Video,
            CategoryFilter.Audio => FileCategory.Audio,
            CategoryFilter.Mobile => FileCategory.Mobile,
            _ => FileCategory.Other
        };
    }

    private static FileCategory CategoryOf(string name)
    {
        string ext = Path.GetExtension(name).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" or ".ico" or ".tif" or ".tiff" or ".svg" or ".heic" or ".avif" => FileCategory.Image,
            ".pdf" or ".doc" or ".docx" or ".xls" or ".xlsx" or ".ppt" or ".pptx" or ".txt" or ".md" or ".rtf" or ".csv" or ".json" or ".xml" or ".html" or ".htm" or ".epub" or ".mobi" or ".log" or ".ini" or ".yaml" or ".yml" or ".toml" or ".cs" or ".java" or ".py" or ".js" or ".ts" or ".rs" or ".go" or ".c" or ".cpp" or ".h" or ".hpp" or ".sql" or ".ps1" or ".bat" or ".cmd" or ".sh" => FileCategory.Document,
            ".mp4" or ".mkv" or ".avi" or ".mov" or ".wmv" or ".flv" or ".webm" or ".m4v" or ".mpg" or ".mpeg" => FileCategory.Video,
            ".mp3" or ".wav" or ".flac" or ".aac" or ".ogg" or ".wma" or ".m4a" or ".ape" => FileCategory.Audio,
            ".apk" or ".ipa" or ".aab" or ".xapk" => FileCategory.Mobile,
            _ => FileCategory.Other
        };
    }

    private static List<string> NormalizeRoots(IEnumerable<string> roots)
    {
        var result = new List<string>();
        if (roots == null) return result;

        foreach (string root in roots)
        {
            if (string.IsNullOrWhiteSpace(root)) continue;
            string full;
            try { full = NormalizePath(root); }
            catch { continue; }
            if (!Directory.Exists(full)) continue;
            if (!result.Contains(full, StringComparer.OrdinalIgnoreCase)) result.Add(full);
        }

        // 去掉被另一个根覆盖的子根，避免重复条目
        var filtered = new List<string>();
        for (int i = 0; i < result.Count; i++)
        {
            bool covered = false;
            for (int j = 0; j < result.Count; j++)
            {
                if (i == j) continue;
                string other = result[j];
                string prefix = other.EndsWith("\\", StringComparison.Ordinal) ? other : other + "\\";
                if (result[i].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    covered = true;
                    break;
                }
            }
            if (!covered) filtered.Add(result[i]);
        }
        return filtered;
    }

    private static List<string> NormalizeExcludes(IEnumerable<string> excludes)
    {
        var result = new List<string>();
        if (excludes == null) return result;
        foreach (string exclude in excludes)
        {
            if (string.IsNullOrWhiteSpace(exclude)) continue;
            try { result.Add(NormalizePath(exclude)); }
            catch { }
        }
        return result;
    }

    private static string NormalizePath(string path)
    {
        string full = Path.GetFullPath(path);
        if (full.Length > 3 && (full.EndsWith("\\", StringComparison.Ordinal) || full.EndsWith("/", StringComparison.Ordinal)))
            full = full.TrimEnd('\\', '/');
        return full;
    }

    private static bool IsExcludedPath(string path, List<string> excludes)
    {
        foreach (string exclude in excludes)
        {
            if (path.Equals(exclude, StringComparison.OrdinalIgnoreCase)) return true;
            string prefix = exclude.EndsWith("\\", StringComparison.Ordinal) ? exclude : exclude + "\\";
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static int AddEntry(List<FileEntry> entries, NameArena names, NameArena lower, int parentIndex,
        string name, long size, long modifiedTicks, FileAttributes attributes, FileCategory category)
    {
        int nameOffset = names.Add(name, out int nameLength);
        int lowerOffset = lower.Add(name.ToLowerInvariant(), out int lowerLength);
        entries.Add(new FileEntry(parentIndex, nameOffset, nameLength, lowerOffset, lowerLength,
            size, modifiedTicks, attributes, category));
        return entries.Count - 1;
    }

    private static List<string> ParseTerms(string text)
    {
        var terms = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return terms;

        var current = new StringBuilder();
        bool quoted = false;
        foreach (char c in text)
        {
            if (c == '"')
            {
                quoted = !quoted;
                continue;
            }
            if (char.IsWhiteSpace(c) && !quoted)
            {
                if (current.Length > 0)
                {
                    terms.Add(current.ToString());
                    current.Clear();
                }
                continue;
            }
            current.Append(c);
        }
        if (current.Length > 0) terms.Add(current.ToString());
        return terms;
    }

    private static ulong ComputeChecksum(FileEntry[] entries, NameArena names, NameArena lower, string[] roots)
    {
        ulong hash = 14695981039346656037UL;

        foreach (var entry in entries)
        {
            AddInt(ref hash, entry.ParentIndex);
            AddInt(ref hash, entry.NameOffset);
            AddInt(ref hash, entry.NameLength);
            AddInt(ref hash, entry.LowerOffset);
            AddInt(ref hash, entry.LowerLength);
            AddLong(ref hash, entry.Size);
            AddLong(ref hash, entry.ModifiedUtcTicks);
            AddInt(ref hash, (int)entry.Attributes);
            AddByte(ref hash, (byte)entry.Category);
        }

        foreach (string root in roots)
            AddBytes(ref hash, Encoding.UTF8.GetBytes(root));

        AddBytes(ref hash, names.Buffer.AsSpan(0, names.Length));
        AddBytes(ref hash, lower.Buffer.AsSpan(0, lower.Length));
        return hash;
    }

    private static void AddInt(ref ulong hash, int value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
        AddBytes(ref hash, buffer);
    }

    private static void AddLong(ref ulong hash, long value)
    {
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(buffer, value);
        AddBytes(ref hash, buffer);
    }

    private static void AddByte(ref ulong hash, byte value)
    {
        hash ^= value;
        hash *= 1099511628211UL;
    }

    private static void AddBytes(ref ulong hash, ReadOnlySpan<byte> bytes)
    {
        foreach (byte value in bytes)
        {
            hash ^= value;
            hash *= 1099511628211UL;
        }
    }

    private sealed class HitComparer : IComparer<int>
    {
        private readonly FileIndex _index;
        private readonly SortField _field;
        private readonly bool _descending;

        public HitComparer(FileIndex index, SortField field, bool descending)
        {
            _index = index;
            _field = field;
            _descending = descending;
        }

        public int Compare(int x, int y)
        {
            int result;
            switch (_field)
            {
                case SortField.Size:
                    result = _index._entries[x].Size.CompareTo(_index._entries[y].Size);
                    break;
                case SortField.Name:
                    result = CompareName(x, y);
                    break;
                default:
                    result = _index._entries[x].ModifiedUtcTicks.CompareTo(_index._entries[y].ModifiedUtcTicks);
                    break;
            }

            if (result == 0) result = CompareName(x, y);
            return _descending ? -result : result;
        }

        private int CompareName(int x, int y)
        {
            ref readonly FileEntry left = ref _index._entries[x];
            ref readonly FileEntry right = ref _index._entries[y];
            return _index._lower.Get(left.LowerOffset, left.LowerLength)
                .SequenceCompareTo(_index._lower.Get(right.LowerOffset, right.LowerLength));
        }
    }

    private readonly struct ChildInfo
    {
        public readonly string Name;
        public readonly string? FullPath;
        public readonly bool IsDirectory;
        public readonly long Length;
        public readonly long ModifiedUtcTicks;
        public readonly FileAttributes Attributes;

        public ChildInfo(string name, string? fullPath, bool isDirectory, long length, long modifiedUtcTicks, FileAttributes attributes)
        {
            Name = name;
            FullPath = fullPath;
            IsDirectory = isDirectory;
            Length = length;
            ModifiedUtcTicks = modifiedUtcTicks;
            Attributes = attributes;
        }
    }
}
