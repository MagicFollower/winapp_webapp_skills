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
