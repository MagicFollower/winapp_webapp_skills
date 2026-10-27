using System.Diagnostics;
using IndexCore;

string root = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "IndexApp1M");
string snapshot = args.Length > 1 ? args[1] : Path.Combine(Path.GetTempPath(), "IndexApp1M.idx");
bool rebuild = args.Contains("--rebuild") || !File.Exists(snapshot);
bool reconcile = args.Contains("--reconcile");
int iterations = 10;

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.WriteLine("root=" + root);
Console.WriteLine("snapshot=" + snapshot);
Console.WriteLine("iterations=" + iterations);

FileIndex index;
var process = Process.GetCurrentProcess();

if (rebuild)
{
    var watch = Stopwatch.StartNew();
    var lastReport = TimeSpan.Zero;
    var progress = new Progress<IndexProgress>(p =>
    {
        if (p.Elapsed - lastReport > TimeSpan.FromSeconds(10))
        {
            lastReport = p.Elapsed;
            Console.WriteLine("build " + p.Files.ToString("N0") + " files / " + p.Directories.ToString("N0") + " dirs");
        }
    });

    index = FileIndex.Build(new IndexOptions
    {
        Roots = { root },
        ExcludedDirectoryNames = { "node_modules", ".git", "bin", "obj" }
    }, progress, CancellationToken.None);

    watch.Stop();
    Console.WriteLine("BUILD_MS=" + watch.ElapsedMilliseconds);
    var saveWatch = Stopwatch.StartNew();
    index.Save(snapshot);
    saveWatch.Stop();
    Console.WriteLine("SAVE_MS=" + saveWatch.ElapsedMilliseconds);
}
else
{
    var watch = Stopwatch.StartNew();
    index = FileIndex.Load(snapshot) ?? throw new InvalidOperationException("snapshot load failed");
    watch.Stop();
    Console.WriteLine("LOAD_MS=" + watch.ElapsedMilliseconds);
}

Console.WriteLine("ENTRIES=" + index.Count);
Console.WriteLine("FILES=" + index.FileCount);
Console.WriteLine("DIRS=" + index.DirectoryCount);
Console.WriteLine("SNAPSHOT_BYTES=" + (File.Exists(snapshot) ? new FileInfo(snapshot).Length : 0));
Console.WriteLine("MANAGED_BYTES=" + GC.GetTotalMemory(true));
Console.WriteLine("PRIVATE_BYTES=" + process.PrivateMemorySize64);
Console.WriteLine("WORKING_SET=" + process.WorkingSet64);
Console.WriteLine("DIAG=" + index.GetDiagnostics());
Console.WriteLine("ROOTS=" + string.Join(";", index.Roots));

if (reconcile)
{
    var reconcileWatch = Stopwatch.StartNew();
    long beforeReconcile = GC.GetTotalMemory(true);
    var reconcileResult = index.Reconcile(null, CancellationToken.None);
    long afterReconcile = GC.GetTotalMemory(true);
    reconcileWatch.Stop();
    process.Refresh();
    Console.WriteLine("RECONCILE_MS=" + reconcileWatch.ElapsedMilliseconds);
    Console.WriteLine("RECONCILE_MANAGED_BEFORE_MB=" + (beforeReconcile / 1024 / 1024));
    Console.WriteLine("RECONCILE_MANAGED_AFTER_MB=" + (afterReconcile / 1024 / 1024));
    Console.WriteLine("RECONCILE_CHECKED=" + reconcileResult.DirectoriesChecked);
    Console.WriteLine("RECONCILE_CHANGED=" + reconcileResult.DirectoriesChanged);
    Console.WriteLine("RECONCILE_ADDED=" + reconcileResult.Added);
    Console.WriteLine("RECONCILE_REMOVED=" + reconcileResult.Removed);
    Console.WriteLine("RECONCILE_UPDATED=" + reconcileResult.Updated);
    Console.WriteLine("RECONCILE_TOMBSTONES=" + index.TombstoneCount);
    Console.WriteLine("RECONCILE_PRIVATE_MB=" + (process.PrivateMemorySize64 / 1024 / 1024));

    index.Compact();
    GC.Collect(2, GCCollectionMode.Optimized, false, true);
    process.Refresh();
    Console.WriteLine("COMPACT_DIAG=" + index.GetDiagnostics());
    Console.WriteLine("COMPACT_PRIVATE_MB=" + (process.PrivateMemorySize64 / 1024 / 1024));
}

string[] queries = { "report", "photo", "notes", "zzzz", "" };
foreach (string query in queries)
{
    var times = new List<double>();
    SearchResult? last = null;

    for (int i = 0; i < iterations; i++)
    {
        var watch = Stopwatch.StartNew();
        last = index.Search(new SearchQuery
        {
            Text = query,
            Sort = SortField.Modified,
            Descending = true,
            MaxResults = 200_000
        });
        watch.Stop();
        times.Add(watch.Elapsed.TotalMilliseconds);
    }

    times.Sort();
    double p50 = times[times.Count / 2];
    double p95 = times[Math.Min(times.Count - 1, (int)Math.Ceiling(times.Count * 0.95) - 1)];
    Console.WriteLine("QUERY=" + (query.Length == 0 ? "<empty>" : query)
        + " total=" + last!.Total
        + " hits=" + last.Hits.Length
        + " p50_ms=" + p50.ToString("F1")
        + " p95_ms=" + p95.ToString("F1")
        + " max_ms=" + times[^1].ToString("F1"));
}

var rowWatch = Stopwatch.StartNew();
var sample = index.Search(new SearchQuery { Text = "report", MaxResults = 200 });
for (int i = 0; i < Math.Min(200, sample.Hits.Length); i++)
{
    var row = index.GetRow(sample.Hits[i]);
    _ = row.Path;
}
rowWatch.Stop();
Console.WriteLine("GETROWS_200_MS=" + rowWatch.ElapsedMilliseconds);
Console.WriteLine("DONE");
