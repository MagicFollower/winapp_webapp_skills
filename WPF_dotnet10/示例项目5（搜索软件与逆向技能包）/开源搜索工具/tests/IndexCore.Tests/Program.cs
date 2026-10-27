using System;
using System.IO;
using System.Linq;
using System.Text;
using IndexCore;

internal static class Program
{
    private static int _passed;
    private static int _failed;

    private static int Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        string root = Path.Combine(Path.GetTempPath(), "IndexCoreTests-" + Guid.NewGuid().ToString("N"));

        try
        {
            BuildTree(root);
            Run(root);
        }
        catch (Exception ex)
        {
            Console.WriteLine("EXCEPTION " + ex);
            _failed++;
        }
        finally
        {
            try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { }
        }

        Console.WriteLine();
        Console.WriteLine("PASS=" + _passed + " FAIL=" + _failed);
        return _failed == 0 ? 0 : 1;
    }

    private static void Run(string root)
    {
        var options = new IndexOptions
        {
            Roots = { root },
            ExcludedDirectoryNames = { "node_modules" },
            MaxEntries = 1_000_000
        };

        var progressCount = 0;
        var progress = new Progress<IndexProgress>(_ => progressCount++);
        var index = FileIndex.Build(options, progress, default);

        Assert(index.Count >= 10, "build entry count >= 10 (actual " + index.Count + ")");
        Assert(index.FileCount >= 8, "build file count >= 8 (actual " + index.FileCount + ")");
        Assert(index.DirectoryCount >= 2, "build directory count >= 2 (actual " + index.DirectoryCount + ")");

        var report = Search(index, "report");
        Assert(report.Total == 3, "search report total=3 (actual " + report.Total + ")");
        Assert(report.ScopeMissing == false, "search report scope not missing");

        var reportDocs = Search(index, "report", category: CategoryFilter.Document);
        Assert(reportDocs.Total == 3, "search report + document total=3 (actual " + reportDocs.Total + ")");

        var images = Search(index, "", category: CategoryFilter.Image);
        Assert(images.Total == 1, "category image total=1 (actual " + images.Total + ")");

        var scoped = Search(index, "report", scope: Path.Combine(root, "sub"));
        Assert(scoped.Total == 1, "scope sub report total=1 (actual " + scoped.Total + ")");

        var missingScope = Search(index, "report", scope: Path.Combine(root, "not-exists"));
        Assert(missingScope.ScopeMissing && missingScope.Total == 0, "missing scope reports ScopeMissing");

        var wildcard = Search(index, "*.pdf");
        Assert(wildcard.Total == 3, "wildcard *.pdf total=3 (actual " + wildcard.Total + ")");

        var upper = Search(index, "REPORT");
        Assert(upper.Total == 3, "case-insensitive REPORT total=3 (actual " + upper.Total + ")");

        var caseSensitive = Search(index, "REPORT", caseSensitive: true);
        Assert(caseSensitive.Total == 0, "case-sensitive REPORT total=0 (actual " + caseSensitive.Total + ")");

        var wholeWordMiss = Search(index, "port", wholeWord: true);
        Assert(wholeWordMiss.Total == 0, "whole-word port total=0 (actual " + wholeWordMiss.Total + ")");

        var wholeWordHit = Search(index, "report-copy", wholeWord: true);
        Assert(wholeWordHit.Total == 2, "whole-word report-copy total=2 (actual " + wholeWordHit.Total + ")");

        var excluded = Search(index, "ignored");
        Assert(excluded.Total == 0, "node_modules excluded total=0 (actual " + excluded.Total + ")");

        var capped = Search(index, "report", maxResults: 1);
        Assert(capped.Total == 3 && capped.Hits.Length == 1, "cap keeps total=3 hits=1");

        var sorted = index.Search(new SearchQuery
        {
            Text = "report",
            Sort = SortField.Name,
            Descending = false,
            MaxResults = 10
        });
        Assert(sorted.Hits.Length == 3, "name sort hits=3");
        if (sorted.Hits.Length == 3)
        {
            var first = index.GetRow(sorted.Hits[0]);
            Assert(first.Name == "report-2026.pdf", "name sort first=report-2026.pdf (actual " + first.Name + ")");
            Assert(File.Exists(first.Path), "row path exists: " + first.Path);
            Assert(first.SizeText.EndsWith("B") || first.SizeText.EndsWith("KB"), "row size text non-empty: " + first.SizeText);
            Assert(!string.IsNullOrWhiteSpace(first.ModifiedText), "row modified text non-empty");
        }

        var all = Search(index, "");
        Assert(all.Total == index.Count, "empty query total=all entries (" + all.Total + "/" + index.Count + ")");

        string snapshot = Path.Combine(root, "index.bin");
        index.Save(snapshot);
        Assert(File.Exists(snapshot), "snapshot written");
        Assert(File.Exists(snapshot + ".bak") == false, "first snapshot has no bak");

        var loaded = FileIndex.Load(snapshot);
        Assert(loaded != null, "snapshot loads");
        if (loaded != null)
        {
            var loadedReport = Search(loaded, "report");
            Assert(loadedReport.Total == 3, "loaded search report total=3 (actual " + loadedReport.Total + ")");
            Assert(loaded.Count == index.Count, "loaded count equals original");
            var row = loaded.GetRow(loadedReport.Hits[0]);
            Assert(File.Exists(row.Path), "loaded row path exists: " + row.Path);
        }

        index.Save(snapshot);
        Assert(File.Exists(snapshot + ".bak"), "second snapshot writes bak");

        byte[] bytes = File.ReadAllBytes(snapshot);
        bytes[bytes.Length / 2] ^= 0xFF;
        string corrupt = Path.Combine(root, "corrupt.bin");
        File.WriteAllBytes(corrupt, bytes);
        Assert(FileIndex.Load(corrupt) == null, "corrupt snapshot rejected");

        Assert(progressCount >= 0, "progress callback did not throw");

        RunIncremental(root, index);
        RunReconcile(root);
        RunWatcher(root);
    }

    private static void RunIncremental(string root, FileIndex index)
    {
        string live = Path.Combine(root, "live");
        Directory.CreateDirectory(live);

        string created = Path.Combine(live, "m2-live.txt");
        File.WriteAllText(created, "v1");
        Assert(index.ApplyChanges(new[] { FileChange.Created(created) }) == 1, "incremental create applies");
        var hit = Search(index, "m2-live");
        Assert(hit.Total == 1 && hit.Hits.Length == 1, "incremental create searchable (actual " + hit.Total + ")");
        if (hit.Hits.Length == 1)
            Assert(index.GetRow(hit.Hits[0]).Path == created, "incremental create row path");

        File.WriteAllText(created, new string('x', 4096));
        Assert(index.ApplyChanges(new[] { FileChange.Changed(created) }) == 1, "incremental change applies");
        hit = Search(index, "m2-live");
        if (hit.Hits.Length == 1)
            Assert(index.GetRow(hit.Hits[0]).Size == 4096, "incremental change refreshes size (actual "
                + index.GetRow(hit.Hits[0]).Size + ")");

        string renamed = Path.Combine(live, "m2-renamed.txt");
        File.Move(created, renamed);
        Assert(index.ApplyChanges(new[] { FileChange.Renamed(created, renamed) }) == 1, "incremental rename applies");
        Assert(Search(index, "m2-live").Total == 0, "old name gone after rename");
        hit = Search(index, "m2-renamed");
        Assert(hit.Total == 1, "new name searchable after rename (actual " + hit.Total + ")");
        if (hit.Hits.Length == 1)
            Assert(index.GetRow(hit.Hits[0]).Path == renamed, "renamed row path updated");

        string sub = Path.Combine(live, "sub2");
        Directory.CreateDirectory(sub);
        string nested = Path.Combine(sub, "nested.txt");
        File.WriteAllText(nested, "nested");
        Assert(index.ApplyChanges(new[]
        {
            FileChange.Created(sub),
            FileChange.Created(nested)
        }) == 2, "incremental directory + child applies");
        Assert(Search(index, "nested").Total == 1, "incremental nested file searchable");

        string subRenamed = Path.Combine(live, "sub2-renamed");
        Directory.Move(sub, subRenamed);
        Assert(index.ApplyChanges(new[] { FileChange.Renamed(sub, subRenamed) }) == 1, "incremental directory rename applies");
        hit = Search(index, "nested");
        Assert(hit.Total == 1, "nested file survives directory rename");
        if (hit.Hits.Length == 1)
            Assert(index.GetRow(hit.Hits[0]).Path == Path.Combine(subRenamed, "nested.txt"),
                "nested path follows directory rename (actual " + index.GetRow(hit.Hits[0]).Path + ")");

        Directory.Delete(subRenamed, true);
        Assert(index.ApplyChanges(new[] { FileChange.Deleted(subRenamed) }) == 2, "incremental directory delete removes subtree");
        Assert(Search(index, "nested").Total == 0, "nested file gone after directory delete");

        string keep = Path.Combine(live, "keep");
        Directory.CreateDirectory(keep);
        string keepChild = Path.Combine(keep, "keep-child.txt");
        File.WriteAllText(keepChild, "keep");
        Assert(index.ApplyChanges(new[]
        {
            FileChange.Created(keep),
            FileChange.Created(keepChild)
        }) == 2, "rename regression directory + child applies");
        Assert(index.RenamePath(Path.Combine(live, "unknown-source"), keep) >= 0,
            "rename with unknown source is safe");
        Assert(Search(index, "keep-child").Total == 1,
            "unknown-source rename must not delete existing target subtree");

        string excluded = Path.Combine(root, "node_modules", "later");
        Directory.CreateDirectory(excluded);
        File.WriteAllText(Path.Combine(excluded, "should-not-index.txt"), "x");
        Assert(index.ApplyChanges(new[] { FileChange.Created(Path.Combine(excluded, "should-not-index.txt")) }) == 0,
            "excluded path rejected by incremental layer");
        Assert(Search(index, "should-not-index").Total == 0, "excluded incremental file not searchable");

        string incrementalSnapshot = Path.Combine(root, "incremental.bin");
        index.Save(incrementalSnapshot);
        var loaded = FileIndex.Load(incrementalSnapshot);
        Assert(loaded != null, "incremental snapshot loads");
        if (loaded != null)
        {
            Assert(loaded.Count == index.Count, "incremental snapshot compacts tombstones ("
                + loaded.Count + "/" + index.Count + ")");
            Assert(Search(loaded, "m2-renamed").Total == 1, "incremental snapshot keeps live entry");
            Assert(Search(loaded, "nested").Total == 0, "incremental snapshot drops deleted subtree");
        }
    }

    private static void RunReconcile(string root)
    {
        string recRoot = Path.Combine(root, "reconcile");
        Directory.CreateDirectory(recRoot);
        Directory.CreateDirectory(Path.Combine(recRoot, "rename-old"));
        Directory.CreateDirectory(Path.Combine(recRoot, "stable"));
        File.WriteAllText(Path.Combine(recRoot, "old-file.txt"), "old");
        File.WriteAllText(Path.Combine(recRoot, "rename-old", "inside.txt"), "inside");
        File.WriteAllText(Path.Combine(recRoot, "stable", "keep.txt"), "keep");

        var options = new IndexOptions { Roots = { recRoot }, MaxEntries = 100_000 };
        var index = FileIndex.Build(options, null, default);

        File.Delete(Path.Combine(recRoot, "old-file.txt"));
        File.WriteAllText(Path.Combine(recRoot, "new-file.txt"), "new");
        Directory.Move(Path.Combine(recRoot, "rename-old"), Path.Combine(recRoot, "rename-new"));
        Directory.CreateDirectory(Path.Combine(recRoot, "new-dir"));
        File.WriteAllText(Path.Combine(recRoot, "new-dir", "inner.txt"), "inner");
        File.WriteAllText(Path.Combine(recRoot, "stable", "keep.txt"), new string('k', 2048));

        // 文件内容变化不改父目录 mtime；显式碰目录，模拟“目录被外层同步工具更新过”的对账场景。
        Directory.SetLastWriteTimeUtc(recRoot, DateTime.UtcNow.AddMinutes(1));
        Directory.SetLastWriteTimeUtc(Path.Combine(recRoot, "stable"), DateTime.UtcNow.AddMinutes(2));

        var result = index.Reconcile(null, default);
        Assert(result.DirectoriesChanged >= 1, "reconcile detects changed directory (actual "
            + result.DirectoriesChanged + ")");
        Assert(result.Added >= 3, "reconcile adds new file/dir/subtree (actual " + result.Added + ")");
        Assert(result.Removed >= 1, "reconcile removes deleted file (actual " + result.Removed + ")");
        Assert(result.Updated >= 1, "reconcile refreshes file metadata (actual " + result.Updated + ")");
        Assert(Search(index, "old-file").Total == 0, "reconcile removed old file");
        Assert(Search(index, "new-file").Total == 1, "reconcile added new file");
        Assert(Search(index, "rename-new").Total == 1, "reconcile adds renamed directory");
        Assert(Search(index, "inside").Total == 1, "reconcile keeps moved subtree content");
        Assert(Search(index, "inner").Total == 1, "reconcile scans new directory subtree");
        Assert(Search(index, "keep").Total == 1, "reconcile keeps stable file");

        // 新建目录的 mtime 可能被系统后台补写一次；第二次对账吸收这种迟到 touch，第三次才要求稳定 no-op。
        _ = index.Reconcile(null, default);
        var third = index.Reconcile(null, default);
        Assert(third.DirectoriesChanged == 0, "third reconcile is a no-op (actual "
            + third.DirectoriesChanged + "; changed=" + string.Join(",", third.ChangedPaths.ToArray())
            + ")" + DescribeMtimeMismatch(index, recRoot));
    }

    private static string DescribeMtimeMismatch(FileIndex index, string root)
    {
        var dirs = new List<string> { root };
        dirs.AddRange(Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories));
        foreach (string dir in dirs)
        {
            int entry = index.FindDirectoryIndex(dir);
            if (entry < 0) return " [missing " + dir + "]";

            var row = index.GetRow(entry);
            DateTime actual = Directory.GetLastWriteTimeUtc(dir);
            DateTime indexed = row.ModifiedLocal.ToUniversalTime();
            if (Math.Abs((indexed - actual).Ticks) > 1)
                return " [mtime " + dir + " index=" + indexed.ToString("o")
                       + " actual=" + actual.ToString("o") + "]";
        }
        return "";
    }

    private static void RunWatcher(string root)
    {
        string watchRoot = Path.Combine(root, "watch");
        Directory.CreateDirectory(watchRoot);
        var options = new IndexOptions { Roots = { watchRoot }, MaxEntries = 100_000 };
        var index = FileIndex.Build(options, null, default);

        using var watcher = new IndexWatcher(index, index.Roots, 150);
        using var signal = new ManualResetEventSlim(false);
        string? fault = null;
        watcher.Faulted += ex => fault = ex.Message;
        watcher.Changed += (_, _) => signal.Set();
        watcher.Start();

        string file = Path.Combine(watchRoot, "watcher-new.txt");
        File.WriteAllText(file, "watcher");
        bool gotCreate = signal.Wait(TimeSpan.FromSeconds(5));
        Assert(gotCreate && Search(index, "watcher-new").Total == 1,
            "watcher sees created file" + (fault == null ? "" : " [fault " + fault + "]"));

        signal.Reset();
        File.Delete(file);
        bool gotDelete = signal.Wait(TimeSpan.FromSeconds(5));
        Assert(gotDelete && Search(index, "watcher-new").Total == 0,
            "watcher sees deleted file" + (fault == null ? "" : " [fault " + fault + "]"));

        watcher.Stop();
        Assert(!watcher.IsRunning, "watcher stops cleanly");
    }

    private static SearchResult Search(FileIndex index, string text, CategoryFilter category = CategoryFilter.All,
        string? scope = null, bool caseSensitive = false, bool wholeWord = false, int maxResults = 200_000)
    {
        return index.Search(new SearchQuery
        {
            Text = text,
            Category = category,
            ScopePath = scope,
            CaseSensitive = caseSensitive,
            WholeWord = wholeWord,
            Wildcard = true,
            MaxResults = maxResults
        });
    }

    private static void BuildTree(string root)
    {
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(root, "sub"));
        Directory.CreateDirectory(Path.Combine(root, "node_modules"));

        File.WriteAllText(Path.Combine(root, "report-2026.pdf"), new string('a', 2048));
        File.WriteAllText(Path.Combine(root, "report-copy.PDF"), new string('b', 4096));
        File.WriteAllText(Path.Combine(root, "notes.txt"), "hello world");
        File.WriteAllText(Path.Combine(root, "readme.md"), "# readme");
        File.WriteAllText(Path.Combine(root, "photo.jpg"), new string('c', 1024));
        File.WriteAllText(Path.Combine(root, "video.mp4"), new string('d', 512));
        File.WriteAllText(Path.Combine(root, "song.mp3"), new string('e', 256));
        File.WriteAllText(Path.Combine(root, "app.apk"), new string('f', 128));
        File.WriteAllText(Path.Combine(root, "中文 文件.txt"), "中文内容");
        File.WriteAllText(Path.Combine(root, "sub", "report-copy.pdf"), new string('g', 1024));
        File.WriteAllText(Path.Combine(root, "sub", "data.json"), "{}");
        File.WriteAllText(Path.Combine(root, "node_modules", "ignored.js"), "ignored");

        File.SetLastWriteTimeUtc(Path.Combine(root, "report-2026.pdf"), new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(Path.Combine(root, "report-copy.PDF"), new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    private static void Assert(bool condition, string name)
    {
        if (condition)
        {
            _passed++;
            Console.WriteLine("PASS " + name);
        }
        else
        {
            _failed++;
            Console.WriteLine("FAIL " + name);
        }
    }
}
