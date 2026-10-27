using System.Collections.Concurrent;
using System.IO;

namespace IndexCore;

public sealed class IndexChangedEventArgs : EventArgs
{
    public IndexChangedEventArgs(int eventCount, int appliedCount)
    {
        EventCount = eventCount;
        AppliedCount = appliedCount;
    }

    public int EventCount { get; }
    public int AppliedCount { get; }
}

/// <summary>
/// 普通权限下的实时路径：FileSystemWatcher（ReadDirectoryChangesW）→ 500ms 合并 → FileIndex.ApplyChanges。
/// 不读文件内容、不发网络请求；缓冲区溢出时抛 Faulted，让 UI 明确提示重建，而不是假装还在实时。
/// </summary>
public sealed class IndexWatcher : IDisposable
{
    private const int MaxBatch = 20_000;

    private readonly FileIndex _index;
    private readonly IReadOnlyList<string> _roots;
    private readonly int _debounceMs;
    private readonly ConcurrentQueue<FileChange> _queue = new();
    private readonly List<FileSystemWatcher> _watchers = new();

    private Timer? _timer;
    private int _started;
    private int _draining;
    private int _overflowed;
    private long _appliedChangeCount;
    private int _disposed;

    public IndexWatcher(FileIndex index, IReadOnlyList<string> roots, int debounceMs = 500)
    {
        _index = index ?? throw new ArgumentNullException(nameof(index));
        _roots = roots ?? throw new ArgumentNullException(nameof(roots));
        _debounceMs = Math.Max(100, debounceMs);
    }

    public event EventHandler<IndexChangedEventArgs>? Changed;
    public event Action<Exception>? Faulted;

    public bool IsRunning => Volatile.Read(ref _started) == 1;
    public bool Overflowed => Volatile.Read(ref _overflowed) == 1;
    public int WatcherCount => _watchers.Count;
    public long AppliedChangeCount => Interlocked.Read(ref _appliedChangeCount);

    public void Start()
    {
        if (Interlocked.Exchange(ref _started, 1) == 1) return;

        foreach (string root in _roots)
        {
            if (string.IsNullOrWhiteSpace(root)) continue;
            try
            {
                var watcher = new FileSystemWatcher(root)
                {
                    IncludeSubdirectories = true,
                    InternalBufferSize = 64 * 1024,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName
                                   | NotifyFilters.Size | NotifyFilters.LastWrite
                                   | NotifyFilters.Attributes | NotifyFilters.CreationTime,
                    Filter = "*"
                };

                watcher.Created += OnCreated;
                watcher.Changed += OnChanged;
                watcher.Deleted += OnDeleted;
                watcher.Renamed += OnRenamed;
                watcher.Error += OnError;
                watcher.EnableRaisingEvents = true;
                _watchers.Add(watcher);
            }
            catch (Exception ex)
            {
                RaiseFault(ex);
            }
        }

        _timer = new Timer(_ => Drain(), null, _debounceMs, _debounceMs);
        if (_watchers.Count == 0)
            RaiseFault(new IOException("没有可用的目录监视器；实时刷新不可用，请按 F5 重建。"));
    }

    public void Stop()
    {
        if (Interlocked.Exchange(ref _started, 0) == 0)
        {
            DisposeWatchers();
            return;
        }

        _timer?.Dispose();
        _timer = null;
        DisposeWatchers();
        Drain();
    }

    public void ClearOverflow()
    {
        Interlocked.Exchange(ref _overflowed, 0);
    }

    private void DisposeWatchers()
    {
        foreach (var watcher in _watchers)
        {
            try
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
            catch
            {
            }
        }
        _watchers.Clear();
    }

    private void OnCreated(object sender, FileSystemEventArgs e) => Enqueue(FileChange.Created(e.FullPath));
    private void OnChanged(object sender, FileSystemEventArgs e) => Enqueue(FileChange.Changed(e.FullPath));
    private void OnDeleted(object sender, FileSystemEventArgs e) => Enqueue(FileChange.Deleted(e.FullPath));

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        // 移入排除目录：索引里的旧条目必须删掉，不能因为新路径被忽略就整条丢弃。
        if (ShouldIgnore(e.FullPath))
        {
            if (!ShouldIgnore(e.OldFullPath)) _queue.Enqueue(FileChange.Deleted(e.OldFullPath));
            return;
        }

        if (ShouldIgnore(e.OldFullPath))
        {
            _queue.Enqueue(FileChange.Created(e.FullPath));
            return;
        }

        Enqueue(FileChange.Renamed(e.OldFullPath, e.FullPath));
    }

    private void Enqueue(FileChange change)
    {
        if (Volatile.Read(ref _disposed) == 1) return;
        if (ShouldIgnore(change.Path)) return;
        _queue.Enqueue(change);
    }

    private bool ShouldIgnore(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return true;
        bool isDirectory = false;
        try { isDirectory = Directory.Exists(path); }
        catch
        {
        }

        try { return _index.IsPathExcluded(path, isDirectory); }
        catch { return false; }
    }

    private void OnError(object sender, ErrorEventArgs e)
    {
        Exception ex = e.GetException();
        if (ex is InternalBufferOverflowException)
        {
            if (Interlocked.Exchange(ref _overflowed, 1) == 0)
                RaiseFault(new IOException("目录监视缓冲区溢出：索引可能已落后，请按 F5 重建索引。", ex));
            return;
        }

        RaiseFault(ex);
    }

    private void Drain()
    {
        if (Interlocked.Exchange(ref _draining, 1) == 1) return;
        try
        {
            var batch = new List<FileChange>(Math.Min(MaxBatch, Math.Max(16, _queue.Count)));
            while (batch.Count < MaxBatch && _queue.TryDequeue(out var change))
                batch.Add(change);

            if (batch.Count == 0) return;

            int applied = _index.ApplyChanges(batch);

            // watcher 只改了文件/目录内容，父目录 mtime 也要跟上，否则下次启动对账会重复枚举。
            var parents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var change in batch)
            {
                AddParent(parents, change.Path);
                AddParent(parents, change.OldPath);
            }

            foreach (string parent in parents)
            {
                try { _index.RefreshDirectoryMetadata(parent); }
                catch
                {
                }
            }

            if (applied > 0)
            {
                Interlocked.Add(ref _appliedChangeCount, applied);
                Changed?.Invoke(this, new IndexChangedEventArgs(batch.Count, applied));
            }
        }
        catch (Exception ex)
        {
            RaiseFault(ex);
        }
        finally
        {
            Volatile.Write(ref _draining, 0);
        }
    }

    private static void AddParent(HashSet<string> parents, string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            string? parent = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(parent)) parents.Add(parent);
        }
        catch
        {
        }
    }

    private void RaiseFault(Exception ex)
    {
        try { Faulted?.Invoke(ex); }
        catch
        {
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
        Stop();
        GC.SuppressFinalize(this);
    }
}
