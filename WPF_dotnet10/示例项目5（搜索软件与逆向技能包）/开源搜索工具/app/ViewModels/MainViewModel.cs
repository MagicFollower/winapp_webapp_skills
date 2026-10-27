#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using IndexCore;
using IndexApp.Helpers;
using IndexApp.Services;
using StartUI4Controls;

namespace IndexApp.ViewModels
{
    public class ThemeOption
    {
        public ThemeOption(string key, string label) { Key = key; Label = label; }
        public string Key { get; private set; }
        public string Label { get; private set; }
    }

    /// <summary>
    /// 主窗口视图模型：设置（StartUI4 底座）+ 搜索（IndexCore）。
    /// 搜索在后台线程跑；UI 只拿 IList 惰性行，不创建 1M 个行对象。
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        private readonly SettingsService _settings;
        private readonly DispatcherTimer _searchDebounce;
        private readonly Dispatcher _dispatcher;

        private FileIndex? _index;
        private CancellationTokenSource? _searchCts;
        private CancellationTokenSource? _previewCts;
        private CancellationTokenSource? _indexCts;
        private bool _initialized;
        private bool _isIndexing;

        public MainViewModel(SettingsService settings)
        {
            _settings = settings ?? new SettingsService();
            _dispatcher = Dispatcher.CurrentDispatcher;

            _searchDebounce = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(70)
            };
            _searchDebounce.Tick += (_, _) =>
            {
                _searchDebounce.Stop();
                _ = RunSearchAsync();
            };

            ThemeChoices = ThemeService.AvailableKeys()
                .Select(key => new ThemeOption(key, ThemeService.DisplayLabel(key)))
                .ToList();
            SelectThemeCommand = new RelayCommand<string>(SelectTheme);

            AvailableFonts = Typography.BuildFamilyChoices(
                Fonts.SystemFontFamilies.Select(f => f.Source).ToList());

            _themeKey = NormalizeThemeKey(_settings.ThemeKey);
            _baseFontSize = Typography.ClampBase(_settings.BaseFontSize);
            _zoomPercent = Typography.ClampZoom(_settings.ZoomPercent);
            _selectedFontFamily = ResolveFamilyEntry(_settings.FontFamilyName);

            IndexStatusText = "准备索引…";
            ShowNoPreview = true;
            SearchText = "";
            ScopeLabel = "范围: 全部索引";
            Category = CategoryFilter.All;
            Sort = SortField.Modified;
            Descending = true;
        }

        private bool _isSettingsOpen;
        public bool IsSettingsOpen
        {
            get { return _isSettingsOpen; }
            set
            {
                if (SetProperty(ref _isSettingsOpen, value) && !value) _settings.Save();
            }
        }

        private string _statusMessage = "";
        public string StatusMessage
        {
            get { return _statusMessage; }
            private set { SetProperty(ref _statusMessage, value); }
        }

        // ── 搜索状态 ───────────────────────────────────────────────────

        private string _searchText = "";
        public string SearchText
        {
            get { return _searchText; }
            set
            {
                if (!SetProperty(ref _searchText, value ?? "")) return;
                ScheduleSearch();
            }
        }

        private CategoryFilter _category;
        public CategoryFilter Category
        {
            get { return _category; }
            private set { SetProperty(ref _category, value); }
        }

        private SortField _sort;
        public SortField Sort
        {
            get { return _sort; }
            private set { SetProperty(ref _sort, value); }
        }

        private bool _descending;
        public bool Descending
        {
            get { return _descending; }
            private set { SetProperty(ref _descending, value); }
        }

        private bool _caseSensitive;
        public bool CaseSensitive
        {
            get { return _caseSensitive; }
            set
            {
                if (SetProperty(ref _caseSensitive, value)) ScheduleSearch();
            }
        }

        private bool _wholeWord;
        public bool WholeWord
        {
            get { return _wholeWord; }
            set
            {
                if (SetProperty(ref _wholeWord, value)) ScheduleSearch();
            }
        }

        private bool _wildcard = true;
        public bool Wildcard
        {
            get { return _wildcard; }
            set
            {
                if (SetProperty(ref _wildcard, value)) ScheduleSearch();
            }
        }

        private string? _scopePath;
        public string? ScopePath
        {
            get { return _scopePath; }
            private set { SetProperty(ref _scopePath, value); }
        }

        private string _scopeLabel = "范围: 用户目录";
        public string ScopeLabel
        {
            get { return _scopeLabel; }
            private set { SetProperty(ref _scopeLabel, value); }
        }

        private VirtualizingRows? _rows;
        public VirtualizingRows? Rows
        {
            get { return _rows; }
            private set { SetProperty(ref _rows, value); }
        }

        private FileRow? _selectedRow;
        public FileRow? SelectedRow
        {
            get { return _selectedRow; }
            set
            {
                if (!SetProperty(ref _selectedRow, value)) return;
                _ = LoadPreviewAsync(value);
            }
        }

        private string _countText = "";
        public string CountText
        {
            get { return _countText; }
            private set { SetProperty(ref _countText, value); }
        }

        private string _sortText = "排序: 修改时间 ↓";
        public string SortText
        {
            get { return _sortText; }
            private set { SetProperty(ref _sortText, value); }
        }

        private string _indexStatusText = "准备索引…";
        public string IndexStatusText
        {
            get { return _indexStatusText; }
            private set { SetProperty(ref _indexStatusText, value); }
        }

        public bool IsIndexing
        {
            get { return _isIndexing; }
            private set { SetProperty(ref _isIndexing, value); }
        }

        // ── 预览状态 ───────────────────────────────────────────────────

        private ImageSource? _previewImage;
        public ImageSource? PreviewImage
        {
            get { return _previewImage; }
            private set { SetProperty(ref _previewImage, value); }
        }

        private string _previewText = "";
        public string PreviewText
        {
            get { return _previewText; }
            private set { SetProperty(ref _previewText, value); }
        }

        private string _previewMeta = "";
        public string PreviewMeta
        {
            get { return _previewMeta; }
            private set { SetProperty(ref _previewMeta, value); }
        }

        private bool _hasPreviewImage;
        public bool HasPreviewImage
        {
            get { return _hasPreviewImage; }
            private set { SetProperty(ref _hasPreviewImage, value); }
        }

        private bool _hasPreviewText;
        public bool HasPreviewText
        {
            get { return _hasPreviewText; }
            private set { SetProperty(ref _hasPreviewText, value); }
        }

        private bool _showNoPreview;
        public bool ShowNoPreview
        {
            get { return _showNoPreview; }
            private set { SetProperty(ref _showNoPreview, value); }
        }

        // ── 初始化 / 重建 ──────────────────────────────────────────────

        public async Task InitializeAsync()
        {
            if (_initialized) return;
            _initialized = true;
            await LoadOrBuildAsync().ConfigureAwait(true);
        }

        public async Task RefreshIndexAsync()
        {
            if (IsIndexing) return;
            await BuildIndexAsync().ConfigureAwait(true);
        }

        private async Task LoadOrBuildAsync()
        {
            IsIndexing = true;
            IndexStatusText = "正在加载索引…";

            string snapshot = SnapshotPath;
            var loaded = await Task.Run(() => FileIndex.Load(snapshot)).ConfigureAwait(true);
            if (loaded != null && RootsMatch(loaded))
            {
                _index = loaded;
                IsIndexing = false;
                IndexStatusText = "已加载快照：" + loaded.FileCount.ToString("N0") + " 文件 / "
                    + loaded.DirectoryCount.ToString("N0") + " 目录";
                await RunSearchAsync().ConfigureAwait(true);
                return;
            }

            await BuildIndexAsync().ConfigureAwait(true);
        }

        private async Task BuildIndexAsync()
        {
            IsIndexing = true;
            Rows = null;
            CountText = "";
            IndexStatusText = "正在扫描目录…";
            _indexCts?.Cancel();
            _indexCts = new CancellationTokenSource();
            var ct = _indexCts.Token;

            try
            {
                var options = BuildOptions();
                var progress = new Progress<IndexProgress>(p =>
                {
                    IndexStatusText = p.Files.ToString("N0") + " 文件 / " + p.Directories.ToString("N0")
                        + " 目录 · " + p.Phase + (string.IsNullOrEmpty(p.CurrentDirectory) ? "" : " · " + p.CurrentDirectory);
                });

                FileIndex index = await Task.Run(() =>
                {
                    var built = FileIndex.Build(options, progress, ct);
                    built.Save(SnapshotPath);
                    return built;
                }, ct).ConfigureAwait(true);

                _index = index;
                IsIndexing = false;
                IndexStatusText = "索引就绪：" + index.FileCount.ToString("N0") + " 文件 / "
                    + index.DirectoryCount.ToString("N0") + " 目录";
                await RunSearchAsync().ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                IndexStatusText = "索引已取消";
                IsIndexing = false;
            }
            catch (Exception ex)
            {
                IsIndexing = false;
                IndexStatusText = "索引失败：" + ex.Message;
                StatusMessage = "索引失败：" + ex.Message;
            }
        }

        private IndexOptions BuildOptions()
        {
            var options = new IndexOptions();
            string? overrideRoot = Environment.GetEnvironmentVariable("INDEXAPP_ROOT");
            if (!string.IsNullOrWhiteSpace(overrideRoot))
            {
                options.Roots.AddRange(overrideRoot.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            }
            else
            {
                // 原软件的默认行为就是全盘；M1 这里至少覆盖所有就绪的固定盘。
                // 仍然按名字/路径排除 node_modules、WinSxS 等海量目录，避免第一次扫描无谓地拖长。
                try
                {
                    foreach (var drive in DriveInfo.GetDrives())
                    {
                        try
                        {
                            if (drive.DriveType == DriveType.Fixed && drive.IsReady)
                                options.Roots.Add(drive.RootDirectory.FullName);
                        }
                        catch
                        {
                        }
                    }
                }
                catch
                {
                }

                if (options.Roots.Count == 0)
                    options.Roots.Add(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            }

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            options.ExcludedPaths.Add(Path.Combine(localAppData, "Temp"));
            options.ExcludedPaths.Add(Path.Combine(localAppData, "Microsoft", "Edge"));
            options.ExcludedPaths.Add(Path.Combine(localAppData, "Packages"));
            options.ExcludedPaths.Add(Path.Combine(localAppData, "Programs"));
            options.ExcludedPaths.Add(Path.Combine(localAppData, "npm-cache"));
            options.ExcludedPaths.Add(Path.Combine(localAppData, "NuGet"));
            options.ExcludedPaths.Add(Path.Combine(appData, "Code", "Cache"));
            options.ExcludedPaths.Add(Path.Combine(appData, "Code", "CachedData"));

            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (!string.IsNullOrWhiteSpace(windows))
            {
                options.ExcludedPaths.Add(Path.Combine(windows, "WinSxS"));
                options.ExcludedPaths.Add(Path.Combine(windows, "Installer"));
                options.ExcludedPaths.Add(Path.Combine(windows, "SoftwareDistribution", "Download"));
                options.ExcludedPaths.Add(Path.Combine(windows, "Temp"));
            }

            string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            if (!string.IsNullOrWhiteSpace(programData))
                options.ExcludedPaths.Add(Path.Combine(programData, "Package Cache"));

            options.ExcludedDirectoryNames.AddRange(new[]
            {
                "node_modules", ".git", ".vs", ".idea", "bin", "obj",
                "$Recycle.Bin", "System Volume Information"
            });

            options.MaxEntries = 4_000_000;
            return options;
        }

        private static string SnapshotPath
        {
            get
            {
                string? overridePath = Environment.GetEnvironmentVariable("INDEXAPP_SNAPSHOT");
                return string.IsNullOrWhiteSpace(overridePath) ? FileIndex.DefaultSnapshotPath : overridePath;
            }
        }

        private bool RootsMatch(FileIndex index)
        {
            // 快照里的根必须与本次配置一致；否则会把上一次别的根（比如测试目录）当成用户目录加载。
            var expected = BuildOptions().Roots
                .Select(NormalizeRoot)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var actual = index.Roots
                .Select(NormalizeRoot)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return expected.SequenceEqual(actual, StringComparer.OrdinalIgnoreCase);
        }

        private static string NormalizeRoot(string path)
        {
            string full = Path.GetFullPath(path);
            return full.Length > 3 ? full.TrimEnd('\\', '/') : full;
        }

        // ── 搜索 ───────────────────────────────────────────────────────

        private void ScheduleSearch()
        {
            _searchDebounce.Stop();
            _searchDebounce.Start();
        }

        private async Task RunSearchAsync()
        {
            var index = _index;
            if (index == null || IsIndexing) return;

            _searchCts?.Cancel();
            var cts = new CancellationTokenSource();
            _searchCts = cts;

            var query = new SearchQuery
            {
                Text = SearchText ?? "",
                Category = Category,
                ScopePath = ScopePath,
                CaseSensitive = CaseSensitive,
                WholeWord = WholeWord,
                Wildcard = Wildcard,
                Sort = Sort,
                Descending = Descending,
                MaxResults = 200_000
            };

            try
            {
                SearchResult result = await Task.Run(() => index.Search(query, cts.Token), cts.Token).ConfigureAwait(true);
                if (cts.IsCancellationRequested) return;

                Rows = new VirtualizingRows(index, result.Hits);
                SelectedRow = null;

                if (result.ScopeMissing)
                {
                    CountText = "范围不在索引中";
                    StatusMessage = "范围不在索引中：" + ScopePath + "。请按 F5 重建索引，或清除范围。";
                    return;
                }

                string shown = result.Hits.Length < result.Total ? "，显示前 " + result.Hits.Length.ToString("N0") + " 项" : "";
                CountText = "共 " + result.Total.ToString("N0") + " 项" + shown + " · " + result.ElapsedMs + " ms";
                IndexStatusText = "索引就绪：" + index.FileCount.ToString("N0") + " 文件 / "
                    + index.DirectoryCount.ToString("N0") + " 目录";
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                CountText = "";
                StatusMessage = "搜索失败：" + ex.Message;
            }
        }

        public void SetCategory(CategoryFilter category)
        {
            if (Category == category) return;
            Category = category;
            ScheduleSearch();
        }

        public void SetSort(SortField field)
        {
            if (Sort == field)
            {
                Descending = !Descending;
            }
            else
            {
                Sort = field;
                Descending = field == SortField.Modified;
            }

            SortText = "排序: " + SortLabel(field) + (Descending ? " ↓" : " ↑");
            ScheduleSearch();
        }

        private static string SortLabel(SortField field)
        {
            return field switch
            {
                SortField.Name => "文件名",
                SortField.Size => "大小",
                _ => "修改时间"
            };
        }

        public void SetScope(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                ClearScope();
                return;
            }

            string full;
            try { full = Path.GetFullPath(path); }
            catch
            {
                StatusMessage = "范围路径无效：" + path;
                return;
            }

            // 目录不在索引里时不能只改标签：用户会看到“范围已选但结果没变”。
            // 新建目录、被排除目录、未纳入索引根的盘都走这里，明确提示重建/扩根。
            if (_index != null && _index.FindDirectoryIndex(full) < 0)
            {
                StatusMessage = "该目录不在当前索引中（新建目录、被排除目录或未纳入索引根）。请按 F5 重建索引，或先扩大索引根。";
                CountText = "范围未生效：目录未索引";
                return;
            }

            ScopePath = full;
            ScopeLabel = "范围: " + (Path.GetFileName(full.TrimEnd('\\')) is { Length: > 0 } name ? name : full);
            ScheduleSearch();
        }

        public void ClearScope()
        {
            ScopePath = null;
            ScopeLabel = "范围: 全部索引";
            ScheduleSearch();
        }

        // ── 行操作 ─────────────────────────────────────────────────────

        public string? OpenSelected()
        {
            var row = SelectedRow;
            if (row == null || string.IsNullOrWhiteSpace(row.Path))
                return "未选择文件或目录。";

            if (!File.Exists(row.Path) && !Directory.Exists(row.Path))
                return "文件或目录已不存在：\n" + row.Path;

            try
            {
                Process.Start(new ProcessStartInfo(row.Path) { UseShellExecute = true });
                return null;
            }
            catch (Exception ex)
            {
                return "打开失败：" + ex.Message;
            }
        }

        public string? LocateSelected()
        {
            var row = SelectedRow;
            if (row == null || string.IsNullOrWhiteSpace(row.Path))
                return "未选择文件或目录。";

            if (!File.Exists(row.Path) && !Directory.Exists(row.Path))
                return "文件或目录已不存在：\n" + row.Path;

            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + row.Path + "\"")
                {
                    UseShellExecute = true
                });
                return null;
            }
            catch (Exception ex)
            {
                return "定位失败：" + ex.Message;
            }
        }

        public void CopySelectedPath()
        {
            var row = SelectedRow;
            if (row == null || string.IsNullOrWhiteSpace(row.Path)) return;

            try { Clipboard.SetText(row.Path); }
            catch (Exception ex) { StatusMessage = "复制失败：" + ex.Message; }
        }

        // ── 预览 ───────────────────────────────────────────────────────

        private async Task LoadPreviewAsync(FileRow? row)
        {
            _previewCts?.Cancel();
            var cts = new CancellationTokenSource();
            _previewCts = cts;

            PreviewImage = null;
            PreviewText = "";
            PreviewMeta = "";
            HasPreviewImage = false;
            HasPreviewText = false;
            ShowNoPreview = row == null;

            if (row == null) return;

            PreviewMeta = row.KindText + " · " + row.SizeText + " · " + row.ModifiedText + Environment.NewLine + row.Path;
            if (row.IsDirectory)
            {
                PreviewText = "文件夹" + Environment.NewLine + row.Path;
                HasPreviewText = true;
                return;
            }

            string ext = Path.GetExtension(row.Name).ToLowerInvariant();
            try
            {
                await Task.Delay(120, cts.Token).ConfigureAwait(true);
                if (cts.IsCancellationRequested) return;

                if (IsImage(ext))
                {
                    var image = await Task.Run(() => LoadImage(row.Path), cts.Token).ConfigureAwait(true);
                    if (cts.IsCancellationRequested) return;
                    if (image != null)
                    {
                        PreviewImage = image;
                        HasPreviewImage = true;
                        return;
                    }
                }
                else if (IsText(ext))
                {
                    string text = await Task.Run(() => ReadTextPreview(row.Path), cts.Token).ConfigureAwait(true);
                    if (cts.IsCancellationRequested) return;
                    if (!string.IsNullOrEmpty(text))
                    {
                        PreviewText = text;
                        HasPreviewText = true;
                        return;
                    }
                }

                ShowNoPreview = true;
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
                ShowNoPreview = true;
            }
        }

        private static bool IsImage(string ext)
        {
            return ext is ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" or ".ico" or ".tif" or ".tiff";
        }

        private static bool IsText(string ext)
        {
            return ext is ".txt" or ".md" or ".log" or ".json" or ".xml" or ".csv" or ".ini" or ".yaml" or ".yml"
                or ".toml" or ".cs" or ".java" or ".py" or ".js" or ".ts" or ".rs" or ".go" or ".c" or ".cpp"
                or ".h" or ".hpp" or ".sql" or ".ps1" or ".bat" or ".cmd" or ".sh" or ".html" or ".htm" or ".css";
        }

        private static ImageSource? LoadImage(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 320;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }

        private static string ReadTextPreview(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream, Encoding.UTF8, true, 1024, leaveOpen: false);
            var buffer = new char[64 * 1024];
            int read = reader.ReadBlock(buffer, 0, buffer.Length);
            return read <= 0 ? "" : new string(buffer, 0, read);
        }

        // ── 配色档 ─────────────────────────────────────────────────────

        private string _themeKey;
        public string ThemeKey
        {
            get { return _themeKey; }
            set
            {
                string key = (value ?? string.Empty).Trim();
                if (string.Equals(_themeKey, key, StringComparison.OrdinalIgnoreCase)) return;
                if (!ThemeService.IsAllowed(key))
                {
                    StatusMessage = "当前明暗策略（Theme.Policy = " + Theme.Policy + "）不允许「"
                                    + ThemeService.DisplayLabel(key) + "」，未切换";
                    return;
                }
                if (!ThemeService.Apply(key))
                {
                    StatusMessage = "库没接受这一档（套装未注册或键名不认识）：" + key;
                    return;
                }
                _themeKey = key;
                _settings.ThemeKey = key;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(AppliedThemeText));
            }
        }

        public IReadOnlyList<ThemeOption> ThemeChoices { get; private set; }
        public ICommand SelectThemeCommand { get; private set; }

        private void SelectTheme(string key) { ThemeKey = key; }

        public string AppliedThemeText
        {
            get { return ThemeService.DisplayLabel(_themeKey) + " → 生效键 " + ThemeService.ResolvedKey(_themeKey); }
        }

        private static string NormalizeThemeKey(string key)
        {
            string trimmed = (key ?? string.Empty).Trim();
            if (trimmed.Length > 0 && ThemeService.IsAllowed(trimmed)) return trimmed;
            var allowed = ThemeService.AvailableKeys();
            return allowed.Count > 0 ? allowed[0] : ThemeService.Light;
        }

        // ── 字体族与字号 ───────────────────────────────────────────────

        public IReadOnlyList<string> AvailableFonts { get; private set; }

        private string _selectedFontFamily;
        public string SelectedFontFamily
        {
            get { return _selectedFontFamily; }
            set
            {
                if (SetProperty(ref _selectedFontFamily, value))
                {
                    _settings.FontFamilyName =
                        string.Equals(value, Typography.DefaultFontFamilySource, StringComparison.Ordinal)
                            ? string.Empty : (value ?? string.Empty);
                    App.ApplyDisplaySettings();
                }
            }
        }

        private double _baseFontSize;
        public double BaseFontSize
        {
            get { return _baseFontSize; }
            set
            {
                if (SetProperty(ref _baseFontSize, Typography.ClampBase(value)))
                {
                    _settings.BaseFontSize = _baseFontSize;
                    App.ApplyDisplaySettings();
                }
            }
        }

        private string ResolveFamilyEntry(string familySource)
        {
            if (string.IsNullOrEmpty(familySource)) return Typography.DefaultFontFamilySource;
            return AvailableFonts.Contains(familySource)
                ? familySource : Typography.DefaultFontFamilySource;
        }

        public void ResetTypography()
        {
            _settings.FontFamilyName = string.Empty;
            _settings.BaseFontSize = Typography.DefaultBaseSize;

            _selectedFontFamily = Typography.DefaultFontFamilySource;
            RaisePropertyChanged(nameof(SelectedFontFamily));

            _baseFontSize = Typography.DefaultBaseSize;
            RaisePropertyChanged(nameof(BaseFontSize));

            App.ApplyDisplaySettings();
            StatusMessage = "已恢复默认字体与字号";
        }

        private double _zoomPercent;
        public double ZoomPercent
        {
            get { return _zoomPercent; }
            set
            {
                if (SetProperty(ref _zoomPercent, Typography.ClampZoom(value)))
                {
                    _settings.ZoomPercent = _zoomPercent;
                    RaisePropertyChanged(nameof(ZoomFactor));
                }
            }
        }

        public double ZoomFactor { get { return _zoomPercent / 100.0; } }

        public void ResetZoom()
        {
            ZoomPercent = Typography.DefaultZoomPercent;
            StatusMessage = "已恢复 100% 缩放";
        }

        // ── 应用信息 ───────────────────────────────────────────────────

        public string AppVersion
        {
            get
            {
                var v = Assembly.GetExecutingAssembly().GetName().Version;
                return v != null ? v.ToString(3) : "未知";
            }
        }

        public string RuntimeVersion { get { return RuntimeInformation.FrameworkDescription; } }

        public string LibraryVersion
        {
            get
            {
                var v = typeof(UI4Theme).Assembly.GetName().Version;
                return v != null ? v.ToString(3) : "未知";
            }
        }

        public string SettingsFolder { get { return SettingsService.SettingsDirectory; } }

        public string IndexRootsText
        {
            get
            {
                var roots = BuildOptions().Roots
                    .Select(NormalizeRoot)
                    .Distinct(StringComparer.OrdinalIgnoreCase);
                return string.Join("；", roots);
            }
        }

        public string PolicyText
        {
            get
            {
                string policy = Theme.Policy;
                if (string.IsNullOrEmpty(policy)) policy = "（未填）";
                string scope;
                if (string.Equals(policy, "both", StringComparison.OrdinalIgnoreCase)) scope = "亮 + 暗，允许跟随系统";
                else if (string.Equals(policy, "light-only", StringComparison.OrdinalIgnoreCase)) scope = "只做浅色档";
                else if (string.Equals(policy, "dark-only", StringComparison.OrdinalIgnoreCase)) scope = "只做深色档";
                else scope = "尚未决策（--selftest 会红）";
                return policy + "（" + scope + "）";
            }
        }
    }

    /// <summary>
    /// 命中索引的惰性视图：WPF 虚拟化只向索引器要可见行；
    /// 行对象缓存上限 4096，避免 200k 行一次性物化。
    /// </summary>
    public sealed class VirtualizingRows : IList<FileRow>, System.Collections.IList
    {
        private readonly FileIndex _index;
        private readonly int[] _hits;
        private readonly Dictionary<int, FileRow> _cache = new();
        private readonly object _lock = new();

        public VirtualizingRows(FileIndex index, int[] hits)
        {
            _index = index;
            _hits = hits ?? Array.Empty<int>();
        }

        public int Count => _hits.Length;
        public bool IsReadOnly => true;
        public bool IsFixedSize => true;
        public object SyncRoot => this;
        public bool IsSynchronized => false;

        public FileRow this[int index]
        {
            get
            {
                if ((uint)index >= (uint)_hits.Length) throw new ArgumentOutOfRangeException(nameof(index));
                lock (_lock)
                {
                    if (_cache.TryGetValue(index, out var row)) return row;
                    row = _index.GetRow(_hits[index]);
                    if (_cache.Count > 4096) _cache.Clear();
                    _cache[index] = row;
                    return row;
                }
            }
            set => throw new NotSupportedException();
        }

        public int IndexOf(FileRow item) => item == null ? -1 : _hits.AsSpan().IndexOf(item.EntryIndex);
        public bool Contains(FileRow item) => IndexOf(item ?? new FileRow()) >= 0;
        public void CopyTo(FileRow[] array, int arrayIndex)
        {
            for (int i = 0; i < _hits.Length; i++) array[arrayIndex + i] = this[i];
        }
        public IEnumerator<FileRow> GetEnumerator()
        {
            for (int i = 0; i < _hits.Length; i++) yield return this[i];
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public void Add(FileRow item) => throw new NotSupportedException();
        public void Clear() => throw new NotSupportedException();
        public void Insert(int index, FileRow item) => throw new NotSupportedException();
        public bool Remove(FileRow item) => throw new NotSupportedException();
        public void RemoveAt(int index) => throw new NotSupportedException();

        object? System.Collections.IList.this[int index]
        {
            get => this[index];
            set => throw new NotSupportedException();
        }

        bool System.Collections.IList.Contains(object? value) => value is FileRow row && Contains(row);
        int System.Collections.IList.IndexOf(object? value) => value is FileRow row ? IndexOf(row) : -1;
        int System.Collections.IList.Add(object? value) => throw new NotSupportedException();
        void System.Collections.IList.Insert(int index, object? value) => throw new NotSupportedException();
        void System.Collections.IList.Remove(object? value) => throw new NotSupportedException();

        void System.Collections.ICollection.CopyTo(Array array, int index)
        {
            for (int i = 0; i < _hits.Length; i++) array.SetValue(this[i], index + i);
        }
    }
}
