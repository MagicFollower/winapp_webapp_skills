#nullable enable
using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using IndexCore;
using IndexApp.Helpers;
using IndexApp.ViewModels;
using StartUI4Controls;

namespace IndexApp
{
    public partial class MainWindow : Window
    {
        private const int HotkeyId = 0x4A01;
        private const uint ModAlt = 0x0001;
        private const uint ModNoRepeat = 0x4000;
        private const uint VkOem3 = 0xC0;
        private const int WmHotkey = 0x0312;

        private HwndSource? _source;
        private FrameGapMonitor? _frameMonitor;
        private bool _closeStarted;
        private Task<bool>? _saveTask;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel(App.Settings);

            UI4Theme.ThemeChanged += OnThemeChanged;
            UpdateFooter();
            UpdateCategoryButtons();

            RestoreGeometry();
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
            Closed += MainWindow_Closed;

            if (Environment.GetCommandLineArgs().Contains("--perf"))
            {
                _frameMonitor = new FrameGapMonitor();
                CompositionTarget.Rendering += _frameMonitor.OnRendering;
                SearchBox.TextChanged += (_, _) => _frameMonitor?.Begin(2500);

                // 让 WPF 在测量窗口内持续出帧；否则空闲时没有 Rendering 事件，
                // 帧间隔会被误读成“卡顿”。1x1 的脉冲元素几乎不可见，只负责驱动合成。
                var pulse = new Border
                {
                    Width = 8,
                    Height = 8,
                    Background = new SolidColorBrush(Color.FromRgb(0x4F, 0x6B, 0xE8)),
                    Opacity = 0.3,
                    IsHitTestVisible = false,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Bottom
                };
                if (Content is Grid root) root.Children.Add(pulse);
                pulse.BeginAnimation(OpacityProperty, new DoubleAnimation(0.3, 0.6, TimeSpan.FromMilliseconds(500))
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever
                });
            }
        }

        private MainViewModel? Vm => DataContext as MainViewModel;

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            SearchBox.Focus();
            if (Vm == null) return;

            await Vm.InitializeAsync();
            UpdateCategoryButtons();

            string[] args = Environment.GetCommandLineArgs();
            int pathIndex = Array.IndexOf(args, "--path");
            if (pathIndex >= 0 && pathIndex + 1 < args.Length)
                Vm.SetScope(args[pathIndex + 1]);
        }

        private void OnThemeChanged(object? sender, EventArgs e)
        {
            UpdateFooter();
            UpdateCategoryButtons();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var helper = new WindowInteropHelper(this);
            _source = HwndSource.FromHwnd(helper.Handle);
            _source?.AddHook(WndProc);
            RegisterHotKey(helper.Handle, HotkeyId, ModAlt | ModNoRepeat, VkOem3);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
            {
                if (IsVisible && IsActive)
                {
                    Hide();
                }
                else
                {
                    Show();
                    Activate();
                    SearchBox.Focus();
                }
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            if (_frameMonitor != null)
            {
                CompositionTarget.Rendering -= _frameMonitor.OnRendering;
                _frameMonitor.WriteReport();
                _frameMonitor = null;
            }

            if (_source != null)
            {
                _source.RemoveHook(WndProc);
                _source = null;
            }

            var helper = new WindowInteropHelper(this);
            if (helper.Handle != IntPtr.Zero) UnregisterHotKey(helper.Handle, HotkeyId);
            UI4Theme.ThemeChanged -= OnThemeChanged;

            if (_saveTask != null)
            {
                // 窗口已经消失，这里最多再等 1.2s：等得到就干净退出，等不到就强制结束进程。
                // 快照是「写 .tmp → Move」的原子替换，中途被杀最多是这次没存上，不会留下半份索引。
                try
                {
                    if (!_saveTask.Wait(TimeSpan.FromMilliseconds(1200)) && !_saveTask.IsCompleted)
                        Environment.Exit(0);
                }
                catch (AggregateException)
                {
                }
            }
        }

        private void SettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            if (Vm != null) Vm.IsSettingsOpen = true;
        }

        private void SettingsOverlay_BackgroundClick(object sender, MouseButtonEventArgs e)
        {
            if (Vm != null) Vm.IsSettingsOpen = false;
        }

        private void Category_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement element || element.Tag is not string tag) return;
            if (!Enum.TryParse<CategoryFilter>(tag, out var category)) return;

            Vm?.SetCategory(category);
            UpdateCategoryButtons();
        }

        private void UpdateCategoryButtons()
        {
            if (CategoryPanel == null) return;

            try
            {
                Color accent = UI4Theme.Current.ColorOf(UI4ThemeToken.Accent);
                Color off = UI4Theme.Current.ColorOf(UI4ThemeToken.OffBackground);
                var onAccent = new SolidColorBrush(UI4Theme.Current.ColorOf(UI4ThemeToken.OnAccent));

                foreach (var button in CategoryPanel.Children.OfType<UI4Button>())
                {
                    bool selected = button.Tag is string tag
                        && Enum.TryParse<CategoryFilter>(tag, out var category)
                        && Vm != null && category == Vm.Category;

                    button.GradientStart = selected ? accent : off;
                    button.GradientEnd = selected ? accent : off;

                    if (selected) button.Foreground = onAccent;
                    else button.SetResourceReference(Control.ForegroundProperty, "UI4.Brush.Text");
                }
            }
            catch
            {
                // --selftest 构造窗口时主题尚未注册；构造函数里的这次着色允许静默跳过。
            }
        }

        private void ScopeButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "选择搜索范围",
                Multiselect = false
            };

            if (dialog.ShowDialog(this) == true)
                Vm?.SetScope(dialog.FolderName);
        }

        private void ScopeButton_RightClick(object sender, MouseButtonEventArgs e)
        {
            Vm?.ClearScope();
            e.Handled = true;
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (Vm == null) return;

            RefreshButton.IsEnabled = false;
            try { await Vm.RefreshIndexAsync(); }
            finally { RefreshButton.IsEnabled = true; }
        }

        private void ResultsList_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            _frameMonitor?.Begin(1200);
        }

        private void ResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject source
                && ResultsList.ContainerFromElement(source) is ListViewItem)
            {
                ShowIfError(Vm?.OpenSelected());
            }
        }

        private void ResultsList_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject source
                && ResultsList.ContainerFromElement(source) is ListViewItem item)
            {
                item.IsSelected = true;
                item.Focus();
                if (item.DataContext is FileRow row && Vm != null) Vm.SelectedRow = row;
                return;
            }

            // 右键空白/表头：清掉上一行的选择，ContextMenuOpening 会因此取消菜单。
            if (Vm != null) Vm.SelectedRow = null;
        }

        private void ResultsList_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (Vm?.SelectedRow == null) e.Handled = true;
        }

        private void ContextOpen_Click(object sender, RoutedEventArgs e)
        {
            ShowIfError(Vm?.OpenSelected());
        }

        private void ContextLocate_Click(object sender, RoutedEventArgs e)
        {
            ShowIfError(Vm?.LocateSelected());
        }

        private void ContextCopyPath_Click(object sender, RoutedEventArgs e)
        {
            Vm?.CopySelectedPath();
        }

        private void ShowIfError(string? error)
        {
            if (!string.IsNullOrEmpty(error)) ShowError(error);
        }

        private void ShowError(string message)
        {
            UI4MessageBox.Show(message, "操作失败", UI4MessageBoxButtons.OK, owner: this);
        }

        private void ColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not GridViewColumnHeader header) return;
            string? name = header.Content?.ToString();

            SortField field;
            switch (name)
            {
                case "文件名": field = SortField.Name; break;
                case "文件大小": field = SortField.Size; break;
                case "修改时间": field = SortField.Modified; break;
                default: return;
            }

            Vm?.SetSort(field);
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var vm = Vm;

            if (e.Key == Key.Escape)
            {
                if (vm != null && vm.IsSettingsOpen)
                {
                    vm.IsSettingsOpen = false;
                    e.Handled = true;
                    return;
                }

                if (vm != null && !string.IsNullOrEmpty(vm.SearchText))
                {
                    vm.SearchText = "";
                    e.Handled = true;
                }
                return;
            }

            if (e.Key == Key.L && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                SearchBox.Focus();
                SearchBox.SelectAll();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.F && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                SearchBox.Focus();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.F5)
            {
                _ = vm?.RefreshIndexAsync();
                e.Handled = true;
                return;
            }

            if (!ResultsList.IsKeyboardFocusWithin) return;

            if (e.Key == Key.Enter)
            {
                ShowIfError(vm?.OpenSelected());
                e.Handled = true;
            }
            else if (e.Key == Key.C && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                vm?.CopySelectedPath();
                e.Handled = true;
            }
            else if (e.Key == Key.O && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                ShowIfError(vm?.LocateSelected());
                e.Handled = true;
            }
        }

        private void UpdateFooter()
        {
            ThemeFooter.Text = "生效键 " + UI4Theme.ResolvedKey;
        }

        // ── 窗口几何持久化：存了要能回正 ────────────────────────────────

        private void RestoreGeometry()
        {
            var settings = App.Settings;
            if (settings == null) return;

            if (settings.WindowMaximized) WindowState = WindowState.Maximized;
            if (settings.WindowWidth > 0) Width = settings.WindowWidth;
            if (settings.WindowHeight > 0) Height = settings.WindowHeight;
            if (double.IsNaN(settings.WindowLeft) || double.IsNaN(settings.WindowTop)) return;

            double left = settings.WindowLeft, top = settings.WindowTop;
            double vsLeft = SystemParameters.VirtualScreenLeft;
            double vsTop = SystemParameters.VirtualScreenTop;
            double vsRight = vsLeft + SystemParameters.VirtualScreenWidth;
            double vsBottom = vsTop + SystemParameters.VirtualScreenHeight;

            bool visible = left < vsRight - 40 && top < vsBottom - 40
                           && left + Width > vsLeft + 40 && top + Height > vsTop + 40;
            if (!visible) return;

            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = left;
            Top = top;
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            if (!_closeStarted)
            {
                // 第一次 Close() 一律拦下来：先播退场动画，同时把慢活（排空 watcher + 写快照）丢后台，
                // 动画结束后再真正 Close()。在 UI 线程同步写 65 MB 快照就是「关闭卡一下」的成因。
                _closeStarted = true;
                e.Cancel = true;

                var vm = Vm;
                vm?.BeginShutdown();
                _ = CloseWithAnimationAsync(vm);
                return;
            }

            e.Cancel = false;

            var settings = App.Settings;
            if (settings == null) return;

            bool maximized = WindowState == WindowState.Maximized;
            if (maximized)
            {
                settings.WindowMaximized = true;
                var normal = RestoreBounds;
                settings.WindowWidth = normal.Width;
                settings.WindowHeight = normal.Height;
            }
            else
            {
                settings.WindowMaximized = false;
                settings.WindowWidth = ActualWidth;
                settings.WindowHeight = ActualHeight;
                settings.WindowLeft = Left;
                settings.WindowTop = Top;
            }

            settings.Save();
        }

        private async Task CloseWithAnimationAsync(MainViewModel? vm)
        {
            if (vm != null && vm.IsIndexing)
            {
                // 全量构建/重建中途关窗：先给后台任务一个取消的机会，别让它和退出抢索引锁。
                ClosingStatusText.Text = "正在取消后台任务…";
                ClosingOverlay.Visibility = Visibility.Visible;
                await Task.Delay(250);
            }

            ClosingStatusText.Text = "正在保存索引…";
            ClosingOverlay.Visibility = Visibility.Visible;
            if (vm != null) _saveTask = vm.FinishShutdownAsync();

            var duration = TimeSpan.FromMilliseconds(170);
            var fade = new DoubleAnimation(1d, 0d, duration)
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            var shrinkX = new DoubleAnimation(1d, 0.975d, duration)
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            var shrinkY = new DoubleAnimation(1d, 0.975d, duration)
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            RootGrid.BeginAnimation(OpacityProperty, fade);
            RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, shrinkX);
            RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, shrinkY);

            await Task.Delay(200);
            Close();
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }
}
