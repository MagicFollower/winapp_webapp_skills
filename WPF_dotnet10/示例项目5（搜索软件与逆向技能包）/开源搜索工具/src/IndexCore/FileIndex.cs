using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Enumeration;
using System.Runtime.InteropServices;
using System.Text;

namespace IndexCore;

/// <summary>
/// 文件名内存索引：目录遍历建表，UTF-8 arena 存名称，快照落盘。
///
/// M2.0 起支持运行期增量：新条目只追加，删除用墓碑，目录/文件通过父子链维护；
/// 查询仍扫描连续数组，1M 规模的下限不变。快照写盘时只写活条目并重映射父下标，
/// 因此 M1 写出的 index.bin 仍可直接加载，M2 写出的快照也可被 M1 读取。
/// </summary>
public sealed class FileIndex
{
    private const string SnapshotMagic = "IDX1";
    private const int SnapshotVersion = 1;
    private const int MaxSnapshotEntries = 20_000_000;
    private const int MaxSnapshotBytes = 1_500_000_000;

    private FileEntry[] _entries;
    private NameArena _names;
    private NameArena _lower;
    private readonly string[] _roots;

    private readonly object _gate = new();
    private bool[] _dead;
    private int[] _firstChild;
    private int[] _nextSibling;
    private int[] _lastChild;
    private int _count;
    private int _fileCount;
    private int _dirCount;
    private int _deadCount;

    private readonly List<string> _excludedPaths;
    private readonly HashSet<string> _excludedNames;
    private readonly Dictionary<int, string> _pathCache = new();
    private readonly Dictionary<string, int> _rootIndex = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _dirCache = new(StringComparer.OrdinalIgnoreCase);
    private int[] _pathScratch = new int[64];

    private FileIndex(FileEntry[] entries, int count, NameArena names, NameArena lower, string[] roots,
        IEnumerable<string>? excludedPaths, IEnumerable<string>? excludedNames)
    {
        _entries = entries;
        _count = count;
        _names = names;
        _lower = lower;
        _roots = roots;
        _excludedPaths = NormalizeExcludes(excludedPaths);
        _excludedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (excludedNames != null)
        {
            foreach (string name in excludedNames)
                if (!string.IsNullOrWhiteSpace(name)) _excludedNames.Add(name);
        }

        _dead = new bool[entries.Length];
        _firstChild = new int[entries.Length];
        _nextSibling = new int[entries.Length];
        _lastChild = new int[entries.Length];
        Array.Fill(_firstChild, -1);
        Array.Fill(_nextSibling, -1);
        Array.Fill(_lastChild, -1);

        BuildChildLinks();
        Recount();

        for (int i = 0; i < _count; i++)
        {
            if (_dead[i] || _entries[i].ParentIndex >= 0) continue;
            string rootPath = GetFullPathLocked(i);
            if (rootPath.Length > 0) _rootIndex[rootPath] = i;
        }
    }

    public int Count => _fileCount + _dirCount;
    public int FileCount => _fileCount;
    public int DirectoryCount => _dirCount;
    public IReadOnlyList<string> Roots => _roots;
    public string[] RootPaths => _roots;
    public int TombstoneCount => _deadCount;

    public string GetDiagnostics()
    {
        lock (_gate)
        {
            return "entries=" + _entries.Length + " count=" + _count
                + " live=" + (_fileCount + _dirCount) + " files=" + _fileCount + " dirs=" + _dirCount
                + " dead=" + _deadCount
                + " names=" + _names.Length + "/" + _names.Capacity
                + " lower=" + _lower.Length + "/" + _lower.Capacity
                + " pathCache=" + _pathCache.Count + " dirCache=" + _dirCache.Count;
        }
    }

    public static string DefaultSnapshotPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "IndexApp", "index.bin");

    // ── 全量构建 ───────────────────────────────────────────────────

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
                            entry.ToFullPath(),
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
                        if (IsExcludedPath(child.FullPath!, excludedPaths)) continue;
                        if (child.IsDirectory && excludedNames.Contains(child.Name)) continue;

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

        var array = entries.ToArray();
        names.Trim();
        lower.Trim();
        return new FileIndex(array, array.Length, names, lower, roots.ToArray(), excludedPaths, excludedNames);
    }

    // ── 查询 ───────────────────────────────────────────────────────

    public SearchResult Search(SearchQuery query, CancellationToken ct = default)
    {
        query ??= new SearchQuery();
        var watch = Stopwatch.StartNew();

        lock (_gate)
        {
            int scopeIndex = -1;
            if (!string.IsNullOrWhiteSpace(query.ScopePath))
            {
                scopeIndex = FindDirectoryIndexLocked(query.ScopePath!);
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

            int entryCount = _count;
            int liveCount = _fileCount + _dirCount;
            if (entryCount == 0 || liveCount == 0)
                return new SearchResult { Hits = Array.Empty<int>(), Total = 0, ElapsedMs = watch.ElapsedMilliseconds };

            int partitions = Math.Min(Math.Min(Environment.ProcessorCount, 16), Math.Max(1, liveCount / 50_000));
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
                    if (_dead[i]) continue;

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
    }

    public FileRow GetRow(int entryIndex)
    {
        lock (_gate)
        {
            if (entryIndex < 0 || entryIndex >= _count || _dead[entryIndex])
                return new FileRow();

            ref readonly FileEntry entry = ref _entries[entryIndex];
            string name = _names.GetString(entry.NameOffset, entry.NameLength);
            string path = GetFullPathLocked(entryIndex);
            string folder = entry.ParentIndex >= 0 ? GetFullPathLocked(entry.ParentIndex) : path;

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
    }

    public string GetFullPath(int entryIndex)
    {
        lock (_gate) return GetFullPathLocked(entryIndex);
    }

    public int FindDirectoryIndex(string path)
    {
        lock (_gate) return FindDirectoryIndexLocked(path);
    }

    /// <summary>
    /// 路径是否命中索引排除项；watcher 在入队前先问这一句，避免 node_modules 之类刷爆队列。
    /// 排除表构造后只读，这里故意不加锁：watcher 事件处理器不能在对账持锁时被阻塞。
    /// </summary>
    public bool IsPathExcluded(string path, bool isDirectory = true)
    {
        return IsExcluded(path, isDirectory);
    }

    // ── 增量 ───────────────────────────────────────────────────────

    public int ApplyChanges(IReadOnlyList<FileChange>? changes)
    {
        if (changes == null || changes.Count == 0) return 0;

        lock (_gate)
        {
            int applied = 0;
            foreach (var change in changes)
            {
                if (change == null || string.IsNullOrWhiteSpace(change.Path)) continue;
                try
                {
                    switch (change.Kind)
                    {
                        case FileChangeKind.Created:
                        case FileChangeKind.Changed:
                            applied += UpsertPathLocked(change.Path);
                            break;
                        case FileChangeKind.Deleted:
                            applied += RemovePathLocked(change.Path);
                            break;
                        case FileChangeKind.Renamed:
                            applied += RenamePathLocked(change.OldPath ?? "", change.Path);
                            break;
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                           or DirectoryNotFoundException or PathTooLongException)
                {
                    // 单个文件被锁/刚消失不应让整批变更失败；下一批或 F5 会再对齐。
                }
            }

            return applied;
        }
    }

    public int UpsertPath(string path)
    {
        lock (_gate) return UpsertPathLocked(path);
    }

    public int RemovePath(string path)
    {
        lock (_gate) return RemovePathLocked(path);
    }

    public int RenamePath(string oldPath, string newPath)
    {
        lock (_gate) return RenamePathLocked(oldPath, newPath);
    }

    public bool RefreshDirectoryMetadata(string path)
    {
        lock (_gate)
        {
            int index = FindDirectoryIndexLocked(path);
            if (index < 0) return false;
            try
            {
                var info = new DirectoryInfo(path);
                if (!info.Exists) return false;
                ref readonly FileEntry entry = ref _entries[index];
                _entries[index] = new FileEntry(entry.ParentIndex,
                    entry.NameOffset, entry.NameLength, entry.LowerOffset, entry.LowerLength,
                    0, info.LastWriteTimeUtc.Ticks, info.Attributes, FileCategory.Folder);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// 压实索引：丢掉墓碑条目，并把名称 arena 里已失效的旧名字搬走。
    /// 增量只追加不回收，长期跑下来旧名字会把 arena 撑大；快照写盘前和对账结束后要收一次。
    /// </summary>
    public void Compact()
    {
        lock (_gate)
        {
            if (_deadCount == 0 && _entries.Length == _count && _names.Capacity <= _names.Length + 1024 * 1024
                && _lower.Capacity <= _lower.Length + 1024 * 1024)
            {
                _pathCache.Clear();
                _dirCache.Clear();
                _names.Trim();
                _lower.Trim();
                return;
            }

            int live = _fileCount + _dirCount;
            if (live == 0)
            {
                _entries = Array.Empty<FileEntry>();
                _count = 0;
                _dead = Array.Empty<bool>();
                _firstChild = Array.Empty<int>();
                _nextSibling = Array.Empty<int>();
                _lastChild = Array.Empty<int>();
                _rootIndex.Clear();
                _pathCache.Clear();
                _dirCache.Clear();
                _names.Trim();
                _lower.Trim();
                return;
            }

            var remap = new int[_count];
            Array.Fill(remap, -1);
            int write = 0;
            for (int i = 0; i < _count; i++)
                if (!_dead[i]) remap[i] = write++;

            var compactEntries = new FileEntry[live];
            var newNames = new NameArena(Math.Max(16, _names.Length));
            var newLower = new NameArena(Math.Max(16, _lower.Length));

            write = 0;
            for (int i = 0; i < _count; i++)
            {
                if (_dead[i]) continue;
                ref readonly FileEntry old = ref _entries[i];
                int nameOffset = newNames.Add(_names.Get(old.NameOffset, old.NameLength), out int nameLength);
                int lowerOffset = newLower.Add(_lower.Get(old.LowerOffset, old.LowerLength), out int lowerLength);
                int parent = old.ParentIndex >= 0 && old.ParentIndex < _count ? remap[old.ParentIndex] : -1;
                compactEntries[write++] = new FileEntry(parent, nameOffset, nameLength,
                    lowerOffset, lowerLength, old.Size, old.ModifiedUtcTicks, old.Attributes, old.Category);
            }

            newNames.Trim();
            newLower.Trim();
            _entries = compactEntries;
            _count = compactEntries.Length;
            _names = newNames;
            _lower = newLower;
            _dead = new bool[_count];
            _firstChild = new int[_count];
            _nextSibling = new int[_count];
            _lastChild = new int[_count];
            Array.Fill(_firstChild, -1);
            Array.Fill(_nextSibling, -1);
            Array.Fill(_lastChild, -1);
            _rootIndex.Clear();
            _pathCache.Clear();
            _dirCache.Clear();

            BuildChildLinks();
            Recount();

            for (int i = 0; i < _count; i++)
            {
                if (_entries[i].ParentIndex >= 0) continue;
                string rootPath = GetFullPathLocked(i);
                if (rootPath.Length > 0) _rootIndex[rootPath] = i;
            }
        }
    }

    /// <summary>
    /// 对账/批量增量之后回收多申请的数组与 arena 容量，并清掉临时路径缓存。
    /// 只在后台空闲点调用：Array.Resize 会短暂多占一份，不能放在查询热路径上。
    /// </summary>
    public void TrimCapacity()
    {
        Compact();
    }

    /// <summary>
    /// 启动对账：只 stat 每个已索引目录的 mtime，变化目录才枚举直接子项并做 Diff。
    /// 覆盖应用关闭期间的新增/删除/重命名；文件内容变化不保证（父目录 mtime 不变）。
    ///
    /// 分块执行并在块间让出锁：全盘 11 万目录一次 stat 要 5 秒，不能把冷启动和搜索全卡住。
    /// </summary>
    public ReconcileResult Reconcile(IProgress<IndexProgress>? progress, CancellationToken ct)
    {
        var watch = Stopwatch.StartNew();
        int[] directories;
        lock (_gate)
        {
            var list = new List<int>(Math.Max(16, _dirCount));
            for (int i = 0; i < _count; i++)
                if (!_dead[i] && _entries[i].IsDirectory) list.Add(i);
            directories = list.ToArray();
        }

        int checkedDirs = 0, changedDirs = 0, added = 0, removed = 0, updated = 0;
        var changedPaths = new List<string>();
        const int chunkSize = 512;

        for (int start = 0; start < directories.Length; start += chunkSize)
        {
            ct.ThrowIfCancellationRequested();
            int end = Math.Min(start + chunkSize, directories.Length);

            for (int k = start; k < end; k++)
            {
                ct.ThrowIfCancellationRequested();
                int index = directories[k];
                string path;
                long storedTicks;
                lock (_gate)
                {
                    if (index < 0 || index >= _count || _dead[index] || !_entries[index].IsDirectory) continue;
                    path = GetPathNoCacheLocked(index);
                    storedTicks = _entries[index].ModifiedUtcTicks;
                }

                if (path.Length == 0) continue;

                long actualTicks;
                if (!TryGetLastWriteTicks(path, out actualTicks)) continue;

                checkedDirs++;
                if (actualTicks == storedTicks) continue;

                var children = EnumerateChildren(path, ct);
                if (children == null) continue;

                lock (_gate)
                {
                    if (index < 0 || index >= _count || _dead[index]) continue;
                    if (_entries[index].ModifiedUtcTicks == actualTicks) continue;

                    changedDirs++;
                    if (changedPaths.Count < 64) changedPaths.Add(path);
                    SyncDirectoryLocked(index, path, children, ct, ref added, ref removed, ref updated);
                }
            }

            if (checkedDirs > 0 && checkedDirs % 2000 == 0)
            {
                progress?.Report(new IndexProgress
                {
                    Phase = "对账",
                    Files = _fileCount,
                    Directories = _dirCount,
                    CurrentDirectory = string.Empty,
                    Elapsed = watch.Elapsed
                });
            }

            Thread.Sleep(0);
        }

        return new ReconcileResult
        {
            DirectoriesChecked = checkedDirs,
            DirectoriesChanged = changedDirs,
            Added = added,
            Removed = removed,
            Updated = updated,
            ElapsedMs = watch.ElapsedMilliseconds,
            ChangedPaths = changedPaths
        };
    }

    private int UpsertPathLocked(string path)
    {
        string full;
        try { full = NormalizePath(path); }
        catch { return 0; }

        FileAttributes attributes;
        try { attributes = File.GetAttributes(full); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FileNotFoundException or DirectoryNotFoundException)
        {
            return 0;
        }

        bool isDirectory = (attributes & FileAttributes.Directory) != 0;
        if (IsExcluded(full, isDirectory)) return 0;

        long size = 0;
        long ticks;
        try
        {
            if (isDirectory)
            {
                ticks = new DirectoryInfo(full).LastWriteTimeUtc.Ticks;
            }
            else
            {
                var info = new FileInfo(full);
                size = info.Length;
                ticks = info.LastWriteTimeUtc.Ticks;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FileNotFoundException or DirectoryNotFoundException)
        {
            return 0;
        }

        int existing = FindEntryByPathLocked(full);
        if (existing >= 0)
        {
            bool wasDirectory = _entries[existing].IsDirectory;
            if (wasDirectory != isDirectory)
            {
                RemoveSubtreeLocked(existing);
                return AddPathLocked(full, isDirectory, size, ticks, attributes) >= 0 ? 1 : 0;
            }

            return UpdateEntryMetadataLocked(existing, size, ticks, attributes, isDirectory) ? 1 : 0;
        }

        return AddPathLocked(full, isDirectory, size, ticks, attributes) >= 0 ? 1 : 0;
    }

    private int AddPathLocked(string fullPath, bool isDirectory, long size, long ticks, FileAttributes attributes)
    {
        string full;
        try { full = NormalizePath(fullPath); }
        catch { return -1; }

        if (IsExcluded(full, isDirectory)) return -1;

        string? parentPath = Path.GetDirectoryName(full);
        string name = Path.GetFileName(full);
        if (string.IsNullOrEmpty(name)) return -1;

        int parentIndex = -1;
        if (!string.IsNullOrEmpty(parentPath))
        {
            parentIndex = FindDirectoryIndexLocked(parentPath);
            if (parentIndex < 0) parentIndex = EnsureDirectoryLocked(parentPath);
            if (parentIndex < 0) return -1;
        }

        return AppendEntry(parentIndex, name, size, ticks, attributes,
            isDirectory ? FileCategory.Folder : CategoryOf(name));
    }

    private int EnsureDirectoryLocked(string path)
    {
        string full;
        try { full = NormalizePath(path); }
        catch { return -1; }

        int existing = FindDirectoryIndexLocked(full);
        if (existing >= 0) return existing;

        if (!Directory.Exists(full)) return -1;
        if (IsExcluded(full, true)) return -1;

        string? parentPath = Path.GetDirectoryName(full);
        int parentIndex = -1;
        if (!string.IsNullOrEmpty(parentPath) &&
            !string.Equals(parentPath, full, StringComparison.OrdinalIgnoreCase))
        {
            parentIndex = EnsureDirectoryLocked(parentPath);
            if (parentIndex < 0) return -1;
        }

        FileAttributes attributes;
        long ticks;
        try
        {
            attributes = File.GetAttributes(full);
            if ((attributes & FileAttributes.Directory) == 0) return -1;
            ticks = new DirectoryInfo(full).LastWriteTimeUtc.Ticks;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FileNotFoundException or DirectoryNotFoundException)
        {
            return -1;
        }

        string name = Path.GetFileName(full);
        if (string.IsNullOrEmpty(name)) name = full;
        return AppendEntry(parentIndex, name, 0, ticks, attributes, FileCategory.Folder);
    }

    private int RenamePathLocked(string oldPath, string newPath)
    {
        string oldFull, newFull;
        try
        {
            oldFull = NormalizePath(oldPath);
            newFull = NormalizePath(newPath);
        }
        catch { return 0; }

        int index = FindEntryByPathLocked(oldFull);
        if (index < 0)
        {
            // 旧路径不在索引里时绝不能删新路径：watcher 经常只报 Renamed 的另一端，
            // 新路径可能是一棵已经索引好的目录，删掉再 Upsert 会把整棵子树丢掉。
            return UpsertPathLocked(newFull) > 0 ? 1 : 0;
        }

        int existingNew = FindEntryByPathLocked(newFull);
        if (existingNew >= 0 && existingNew != index)
            RemoveSubtreeLocked(existingNew);

        string? parentPath = Path.GetDirectoryName(newFull);
        string name = Path.GetFileName(newFull);
        if (string.IsNullOrEmpty(name)) return 0;

        int newParent = -1;
        if (!string.IsNullOrEmpty(parentPath))
        {
            newParent = FindDirectoryIndexLocked(parentPath);
            if (newParent < 0) newParent = EnsureDirectoryLocked(parentPath);
            if (newParent < 0) return 0;
        }

        bool isDirectory = _entries[index].IsDirectory;
        if (IsExcluded(newFull, isDirectory))
        {
            RemoveSubtreeLocked(index);
            return 1;
        }

        long size = _entries[index].Size;
        long ticks = _entries[index].ModifiedUtcTicks;
        FileAttributes attributes = _entries[index].Attributes;
        try
        {
            attributes = File.GetAttributes(newFull);
            bool actualDirectory = (attributes & FileAttributes.Directory) != 0;
            if (actualDirectory)
            {
                ticks = new DirectoryInfo(newFull).LastWriteTimeUtc.Ticks;
            }
            else
            {
                var info = new FileInfo(newFull);
                size = info.Length;
                ticks = info.LastWriteTimeUtc.Ticks;
            }

            if (actualDirectory != isDirectory)
            {
                RemoveSubtreeLocked(index);
                return AddPathLocked(newFull, actualDirectory, size, ticks, attributes) >= 0 ? 1 : 0;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FileNotFoundException or DirectoryNotFoundException)
        {
            // 重命名事件到达时目标刚被删掉：保留旧条目，下一批删除/重建会收敛。
            return 0;
        }

        int oldParent = _entries[index].ParentIndex;
        int nameOffset = _names.Add(name, out int nameLength);
        int lowerOffset = _lower.Add(name.ToLowerInvariant(), out int lowerLength);
        _entries[index] = new FileEntry(newParent, nameOffset, nameLength, lowerOffset, lowerLength,
            size, ticks, attributes, isDirectory ? FileCategory.Folder : CategoryOf(name));

        if (oldParent != newParent)
        {
            if (oldParent >= 0) UnlinkChildLocked(oldParent, index);
            if (newParent >= 0) LinkChild(newParent, index);
        }

        _dirCache.Clear();
        InvalidatePaths();
        return 1;
    }

    private int RemovePathLocked(string path)
    {
        int index = FindEntryByPathLocked(path);
        return index < 0 ? 0 : RemoveSubtreeLocked(index);
    }

    private int RemoveSubtreeLocked(int index)
    {
        if (index < 0 || index >= _count || _dead[index]) return 0;

        int parent = _entries[index].ParentIndex;
        if (parent >= 0 && !_dead[parent]) UnlinkChildLocked(parent, index);

        // 单文件删除是最常见的路径：不走子树收集，也不动目录索引/路径缓存。
        if (!_entries[index].IsDirectory)
        {
            _dead[index] = true;
            _deadCount++;
            _fileCount--;
            return 1;
        }

        // 目录删掉前先把子树节点收集起来：路径要在所有节点还活着时算，
        // 否则父节点一旦标死，GetFullPathLocked 会提前返回空串。
        var nodes = new List<int>(16);
        var stack = new Stack<int>();
        stack.Push(index);
        while (stack.Count > 0)
        {
            int current = stack.Pop();
            if (current < 0 || current >= _count || _dead[current]) continue;
            nodes.Add(current);
            for (int child = _firstChild[current]; child >= 0; child = _nextSibling[child])
                stack.Push(child);
        }

        int removed = 0;
        foreach (int node in nodes)
        {
            if (_dead[node]) continue;
            _dead[node] = true;
            _deadCount++;
            removed++;
            if (_entries[node].IsDirectory) _dirCount--;
            else _fileCount--;
        }

        return removed;
    }

    private bool UpdateEntryMetadataLocked(int index, long size, long ticks, FileAttributes attributes, bool isDirectory)
    {
        ref readonly FileEntry old = ref _entries[index];
        bool changed = old.Size != size || old.ModifiedUtcTicks != ticks || old.Attributes != attributes;
        string name = _names.GetString(old.NameOffset, old.NameLength);
        var category = isDirectory ? FileCategory.Folder : CategoryOf(name);
        if (!changed && old.Category == category) return false;

        _entries[index] = new FileEntry(old.ParentIndex, old.NameOffset, old.NameLength,
            old.LowerOffset, old.LowerLength, size, ticks, attributes, category);
        return true;
    }

    private bool UpdateDirectoryAttributesLocked(int index, FileAttributes attributes)
    {
        ref readonly FileEntry old = ref _entries[index];
        if (old.Attributes == attributes) return false;

        _entries[index] = new FileEntry(old.ParentIndex, old.NameOffset, old.NameLength,
            old.LowerOffset, old.LowerLength, 0, old.ModifiedUtcTicks, attributes, FileCategory.Folder);
        return true;
    }

    private void SyncDirectoryLocked(int dirIndex, string dirPath, List<DirectoryChildInfo> children,
        CancellationToken ct, ref int added, ref int removed, ref int updated)
    {
        var incoming = new Dictionary<string, DirectoryChildInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var child in children)
            if (!incoming.ContainsKey(child.Name)) incoming[child.Name] = child;

        var existing = new List<int>();
        for (int child = _firstChild[dirIndex]; child >= 0; child = _nextSibling[child])
            if (!_dead[child]) existing.Add(child);

        foreach (int child in existing)
        {
            ct.ThrowIfCancellationRequested();
            if (_dead[child]) continue;

            string name = GetNameLocked(child);
            if (!incoming.TryGetValue(name, out var info))
            {
                removed += RemoveSubtreeLocked(child);
                continue;
            }

            bool wasDirectory = _entries[child].IsDirectory;
            if (wasDirectory != info.IsDirectory)
            {
                removed += RemoveSubtreeLocked(child);
                continue;
            }

            if (info.IsDirectory)
            {
                // 目录 mtime 与子树交给外层逐个目录处理；这里若把 mtime 也更新掉，
                // 外层会误以为该目录没变化，目录内文件的大小/时间就永远不刷新。
                if (UpdateDirectoryAttributesLocked(child, info.Attributes)) updated++;
                incoming.Remove(name);
                continue;
            }

            if (UpdateEntryMetadataLocked(child, info.Length, info.ModifiedUtcTicks, info.Attributes, false))
                updated++;
            incoming.Remove(name);
        }

        foreach (var info in incoming.Values)
        {
            ct.ThrowIfCancellationRequested();
            int child = AddChildLocked(dirIndex, info);
            added++;
            if (info.IsDirectory) ScanSubtreeLocked(child, ct, ref added, ref removed, ref updated);
        }

        try
        {
            var info = new DirectoryInfo(dirPath);
            if (info.Exists)
            {
                ref readonly FileEntry entry = ref _entries[dirIndex];
                _entries[dirIndex] = new FileEntry(entry.ParentIndex,
                    entry.NameOffset, entry.NameLength, entry.LowerOffset, entry.LowerLength,
                    0, info.LastWriteTimeUtc.Ticks, info.Attributes, FileCategory.Folder);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
        }
    }

    private void ScanSubtreeLocked(int dirIndex, CancellationToken ct, ref int added, ref int removed, ref int updated)
    {
        var stack = new Stack<int>();
        stack.Push(dirIndex);

        while (stack.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            int current = stack.Pop();
            if (_dead[current]) continue;

            string path = GetPathNoCacheLocked(current);
            if (path.Length == 0) continue;

            var children = EnumerateChildren(path, ct);
            if (children == null) continue;

            // 直接子项按父下标建一次名字表；绝不能在每个子项上走 FindEntryByPathLocked，
            // 那会在新增目录时反复重建整张目录索引（1M 规模下是 O(N²)）。
            var existingChildren = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int child = _firstChild[current]; child >= 0; child = _nextSibling[child])
                if (!_dead[child]) existingChildren[GetNameLocked(child)] = child;

            foreach (var info in children)
            {
                ct.ThrowIfCancellationRequested();
                int child;
                if (existingChildren.TryGetValue(info.Name, out int existing))
                {
                    if (_entries[existing].IsDirectory != info.IsDirectory)
                    {
                        removed += RemoveSubtreeLocked(existing);
                        child = AddChildLocked(current, info);
                        added++;
                    }
                    else
                    {
                        if (UpdateEntryMetadataLocked(existing, info.Length, info.ModifiedUtcTicks, info.Attributes, info.IsDirectory))
                            updated++;
                        child = existing;
                    }
                }
                else
                {
                    child = AddChildLocked(current, info);
                    added++;
                }

                existingChildren[info.Name] = child;
                if (info.IsDirectory) stack.Push(child);
            }
        }
    }

    private int AddChildLocked(int parentIndex, DirectoryChildInfo info)
    {
        return AppendEntry(parentIndex, info.Name, info.Length, info.ModifiedUtcTicks, info.Attributes,
            info.IsDirectory ? FileCategory.Folder : CategoryOf(info.Name));
    }

    private int AppendEntry(int parentIndex, string name, long size, long ticks,
        FileAttributes attributes, FileCategory category)
    {
        EnsureCapacity(_count + 1);

        int nameOffset = _names.Add(name, out int nameLength);
        int lowerOffset = _lower.Add(name.ToLowerInvariant(), out int lowerLength);

        int index = _count++;
        _entries[index] = new FileEntry(parentIndex, nameOffset, nameLength, lowerOffset, lowerLength,
            size, ticks, attributes, category);
        _dead[index] = false;
        _firstChild[index] = -1;
        _nextSibling[index] = -1;
        _lastChild[index] = -1;

        if (parentIndex >= 0 && parentIndex < _count)
            LinkChild(parentIndex, index);

        if (category == FileCategory.Folder)
        {
            _dirCount++;
        }
        else
        {
            _fileCount++;
        }

        return index;
    }

    private void EnsureCapacity(int required)
    {
        if (required <= _entries.Length) return;

        int oldLength = _entries.Length;
        // 25% 增长而不是翻倍：全盘快照 76 万条时，多一条就把数组和 arena 一起顶到 150 万，
        // 实测能把常驻内存从 ~210MB 抬到 ~410MB。
        int size = oldLength == 0 ? 16 : oldLength + Math.Max(4096, oldLength / 4);
        while (size < required)
        {
            int grow = Math.Max(4096, size / 4);
            if (size > int.MaxValue - grow) { size = required; break; }
            size += grow;
        }

        Array.Resize(ref _entries, size);
        Array.Resize(ref _dead, size);
        Array.Resize(ref _firstChild, size);
        Array.Resize(ref _nextSibling, size);
        Array.Resize(ref _lastChild, size);

        for (int i = oldLength; i < size; i++)
        {
            _firstChild[i] = -1;
            _nextSibling[i] = -1;
            _lastChild[i] = -1;
        }
    }

    private void BuildChildLinks()
    {
        for (int i = 0; i < _count; i++)
        {
            int parent = _entries[i].ParentIndex;
            if (parent >= 0 && parent < _count) LinkChild(parent, i);
        }
    }

    private void LinkChild(int parent, int child)
    {
        if (parent < 0 || parent >= _count || child < 0 || child >= _count) return;
        if (_firstChild[parent] < 0) _firstChild[parent] = child;
        else _nextSibling[_lastChild[parent]] = child;
        _lastChild[parent] = child;
    }

    private void UnlinkChildLocked(int parent, int child)
    {
        if (parent < 0 || parent >= _count) return;

        int previous = -1;
        int current = _firstChild[parent];
        while (current >= 0)
        {
            if (current == child)
            {
                if (previous < 0) _firstChild[parent] = _nextSibling[current];
                else _nextSibling[previous] = _nextSibling[current];
                if (_lastChild[parent] == current) _lastChild[parent] = previous;
                _nextSibling[current] = -1;
                return;
            }

            previous = current;
            current = _nextSibling[current];
        }
    }

    private void Recount()
    {
        int files = 0, directories = 0, dead = 0;
        for (int i = 0; i < _count; i++)
        {
            if (_dead[i])
            {
                dead++;
                continue;
            }

            if (_entries[i].IsDirectory) directories++;
            else files++;
        }

        _fileCount = files;
        _dirCount = directories;
        _deadCount = dead;
    }

    private void InvalidatePaths()
    {
        _pathCache.Clear();
    }

    private string GetNameLocked(int index)
    {
        ref readonly FileEntry entry = ref _entries[index];
        return _names.GetString(entry.NameOffset, entry.NameLength);
    }

    private string GetFullPathLocked(int entryIndex)
    {
        return BuildPathLocked(entryIndex, cache: true);
    }

    private string GetPathNoCacheLocked(int entryIndex)
    {
        return BuildPathLocked(entryIndex, cache: false);
    }

    private string BuildPathLocked(int entryIndex, bool cache)
    {
        if (entryIndex < 0 || entryIndex >= _count || _dead[entryIndex]) return string.Empty;
        if (cache && _pathCache.TryGetValue(entryIndex, out var cached)) return cached;

        int depth = 0;
        int current = entryIndex;
        int guard = 0;
        string prefix = string.Empty;

        while (current >= 0 && guard++ < 512)
        {
            if (current >= _count || _dead[current]) return string.Empty;
            if (cache && _pathCache.TryGetValue(current, out var found))
            {
                prefix = found;
                break;
            }

            if (depth == _pathScratch.Length) Array.Resize(ref _pathScratch, _pathScratch.Length * 2);
            _pathScratch[depth++] = current;
            current = _entries[current].ParentIndex;
        }

        if (current >= 0 && guard >= 512) return string.Empty;

        for (int i = depth - 1; i >= 0; i--)
        {
            int index = _pathScratch[i];
            ref readonly FileEntry entry = ref _entries[index];
            string name = _names.GetString(entry.NameOffset, entry.NameLength);
            if (prefix.Length == 0) prefix = name;
            else if (prefix.EndsWith("\\", StringComparison.Ordinal)) prefix += name;
            else prefix += "\\" + name;
            if (cache) _pathCache[index] = prefix;
        }

        return prefix;
    }

    private int FindDirectoryIndexLocked(string path)
    {
        string normalized;
        try { normalized = NormalizePath(path); }
        catch { return -1; }

        if (_dirCache.TryGetValue(normalized, out int cached))
        {
            if (cached >= 0 && cached < _count && !_dead[cached]) return cached;
            _dirCache.Remove(normalized);
        }

        if (_rootIndex.TryGetValue(normalized, out int exactRoot) &&
            exactRoot >= 0 && exactRoot < _count && !_dead[exactRoot])
        {
            CacheDirectory(normalized, exactRoot);
            return exactRoot;
        }

        int current = -1;
        string rest = string.Empty;
        foreach (string root in _roots)
        {
            string prefix = root.EndsWith("\\", StringComparison.Ordinal) ? root : root + "\\";
            if (!normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            if (!_rootIndex.TryGetValue(root, out int rootIndex) || rootIndex < 0 || rootIndex >= _count || _dead[rootIndex])
                continue;
            current = rootIndex;
            rest = normalized.Substring(prefix.Length);
            break;
        }

        if (current < 0) return -1;

        foreach (string segment in rest.Split('\\', StringSplitOptions.RemoveEmptyEntries))
        {
            int child = FindChildByNameLocked(current, segment);
            if (child < 0) return -1;
            current = child;
        }

        if (_dead[current]) return -1;
        CacheDirectory(normalized, current);
        return current;
    }

    private void CacheDirectory(string path, int index)
    {
        if (_dirCache.Count >= 5000) _dirCache.Clear();
        _dirCache[path] = index;
    }

    private int FindChildByNameLocked(int parentIndex, string name)
    {
        if (parentIndex < 0 || parentIndex >= _count) return -1;
        for (int child = _firstChild[parentIndex]; child >= 0; child = _nextSibling[child])
        {
            if (_dead[child]) continue;
            if (string.Equals(GetNameLocked(child), name, StringComparison.OrdinalIgnoreCase))
                return child;
        }
        return -1;
    }

    private int FindEntryByPathLocked(string path)
    {
        string normalized;
        try { normalized = NormalizePath(path); }
        catch { return -1; }

        string name = Path.GetFileName(normalized);
        if (string.IsNullOrEmpty(name)) return FindDirectoryIndexLocked(normalized);

        string? parentPath = Path.GetDirectoryName(normalized);
        if (string.IsNullOrEmpty(parentPath)) return -1;

        int parent = FindDirectoryIndexLocked(parentPath);
        if (parent < 0) return -1;

        for (int child = _firstChild[parent]; child >= 0; child = _nextSibling[child])
        {
            if (_dead[child]) continue;
            if (string.Equals(GetNameLocked(child), name, StringComparison.OrdinalIgnoreCase))
                return child;
        }

        return -1;
    }

    private List<DirectoryChildInfo>? EnumerateChildren(string directory, CancellationToken ct)
    {
        try
        {
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = false,
                IgnoreInaccessible = true,
                AttributesToSkip = 0,
                ReturnSpecialDirectories = false,
                BufferSize = 64 * 1024
            };

            var result = new List<DirectoryChildInfo>(64);
            var enumerable = new FileSystemEnumerable<DirectoryChildInfo>(
                directory,
                (ref FileSystemEntry entry) => new DirectoryChildInfo
                {
                    Name = entry.FileName.ToString(),
                    FullPath = entry.ToFullPath(),
                    IsDirectory = entry.IsDirectory,
                    Length = entry.IsDirectory ? 0 : entry.Length,
                    ModifiedUtcTicks = entry.LastWriteTimeUtc.UtcDateTime.Ticks,
                    Attributes = entry.Attributes
                },
                options);

            int count = 0;
            foreach (var child in enumerable)
            {
                if ((count++ & 1023) == 0) ct.ThrowIfCancellationRequested();
                if (IsExcluded(child.FullPath, child.IsDirectory)) continue;
                result.Add(child);
            }

            return result;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            return null;
        }
    }

    private bool IsExcluded(string path, bool isDirectory)
    {
        string normalized;
        try { normalized = NormalizePath(path); }
        catch { return false; }

        foreach (string exclude in _excludedPaths)
        {
            if (normalized.Equals(exclude, StringComparison.OrdinalIgnoreCase)) return true;
            string prefix = exclude.EndsWith("\\", StringComparison.Ordinal) ? exclude : exclude + "\\";
            if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
        }

        if (_excludedNames.Count == 0) return false;

        string root = Path.GetPathRoot(normalized) ?? string.Empty;
        string rest = normalized.Length > root.Length ? normalized.Substring(root.Length) : string.Empty;
        if (rest.Length == 0) return false;

        string[] segments = rest.Split('\\');
        int limit = isDirectory ? segments.Length : segments.Length - 1;
        for (int i = 0; i < limit; i++)
            if (_excludedNames.Contains(segments[i])) return true;

        return false;
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

    // ── 快照 ───────────────────────────────────────────────────────

    public void Save(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("快照路径为空。", nameof(path));

        string directory = Path.GetDirectoryName(path) ?? ".";
        Directory.CreateDirectory(directory);
        string temp = path + ".tmp";

        Compact();
        lock (_gate)
        {
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20, FileOptions.WriteThrough))
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(Encoding.ASCII.GetBytes(SnapshotMagic));
                writer.Write(SnapshotVersion);
                writer.Write(_count);
                writer.Write(_names.Length);
                writer.Write(_lower.Length);
                writer.Write(_roots.Length);
                foreach (string root in _roots) writer.Write(root);

                for (int i = 0; i < _count; i++)
                {
                    ref readonly FileEntry entry = ref _entries[i];
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
        }

        if (File.Exists(path)) File.Copy(path, path + ".bak", true);
        File.Move(temp, path, true);
    }

    public static FileIndex? Load(string path, IndexOptions? options = null)
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

            return new FileIndex(entries, entryCount, nameArena, lowerArena, roots,
                options?.ExcludedPaths, options?.ExcludedDirectoryNames);
        }
        catch (Exception)
        {
            return null;
        }
    }

    // ── 静态工具 ───────────────────────────────────────────────────

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

    private static List<string> NormalizeExcludes(IEnumerable<string>? excludes)
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

    [StructLayout(LayoutKind.Sequential)]
    private struct Win32FileAttributeData
    {
        public uint dwFileAttributes;
        public uint ftCreationTimeLow;
        public uint ftCreationTimeHigh;
        public uint ftLastAccessTimeLow;
        public uint ftLastAccessTimeHigh;
        public uint ftLastWriteTimeLow;
        public uint ftLastWriteTimeHigh;
        public uint nFileSizeHigh;
        public uint nFileSizeLow;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileAttributesExW(string lpFileName, int fInfoLevelId, out Win32FileAttributeData lpFileInformation);

    private const int GetFileExInfoStandard = 0;
    private const uint FileAttributeDirectory = 0x10;
    private const ulong FileTimeToDateTimeTicks = 504911232000000000UL;

    /// <summary>
    /// 对账要 stat 11 万个目录；用 DirectoryInfo/GetLastWriteTimeUtc 会把这些短命对象推到 200MB 私有内存，
    /// 直接一次 GetFileAttributesExW 拿 FILETIME，少一层托管包装。
    /// </summary>
    private static bool TryGetLastWriteTicks(string path, out long ticks)
    {
        ticks = 0;
        try
        {
            if (!GetFileAttributesExW(path, GetFileExInfoStandard, out var info)) return false;
            if ((info.dwFileAttributes & FileAttributeDirectory) == 0) return false;
            ulong fileTime = ((ulong)info.ftLastWriteTimeHigh << 32) | info.ftLastWriteTimeLow;
            ticks = (long)(fileTime + FileTimeToDateTimeTicks);
            return true;
        }
        catch
        {
            return false;
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
