using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using StartUI4Controls;

namespace __APPNAME__.Views
{
    /// <summary>
    /// UI4NavigationView 左侧导航栏的默认宿主模板（默认实现）。
    ///
    /// 库模板没有暴露右内容面板的底色/圆角，也没有左栏的 CornerRadius；直接摆一个 UI4NavigationView
    /// 会得到直角、贴在一起的两栏、以及左侧项悬浮灰底右缘被 ListBox 内边距裁平。下面这组 chrome
    /// 补丁就是 QuickPanel 现网验证过的默认口径，复制到新工程时不要重写成另一套：
    ///
    ///   右内容面板：UI4.Brush.Background 底、四角 12、左侧 12 与左栏形成两张卡片、顶部 20、
    ///               圆角 Clip 把内容裁进卡片轮廓；
    ///   左栏：      PART_LeftPanel 的 12 px 圆角 Clip；
    ///   导航项：    UI4ListBox 内部 ScrollViewer 的 4 px 内边距归零；项背景左 4 / 右 5（补 1 px
    ///               右边框）/ 上下 4；首个卡片避开栏顶圆角，上下对称使内容中心不动。
    ///
    /// 换主题时 UI4ListBox 会重建 Style/ControlTemplate，内部 ScrollViewer 的 Padding 会回到 4 px；
    /// 所以订阅 UI4Theme.ThemeChanged，并在 Loaded / ApplicationIdle 各重打一次。
    /// 布局侧：内容标题左边距 24（= 首卡 ItemMargin 20 + GridView 内部 4），首卡 ItemMargin=20。
    /// </summary>
    public partial class NavigationRailCase : UserControl
    {
        private const double RailCornerRadius = 12;
        private const double ContentGap = 12;
        private const double ContentTopInset = 20;
        private const double ItemSideInset = 4;
        private const double ItemRightInset = 5;
        private const double ItemVerticalInset = 4;

        private readonly HashSet<ListBox> _marginHooked = new HashSet<ListBox>();
        private Border _contentPanel;
        private Grid _leftRail;
        private bool _disposed;

        public NavigationRailCase()
        {
            InitializeComponent();

            var iconFont = new FontFamily("Segoe MDL2 Assets");
            AddItem("全部工具", "\uE700", "所有入口，点卡片即执行", iconFont);
            AddItem("收藏", "\uE734", "把常用入口固定在这里", iconFont);
            AddItem("控制面板", "\uE713", "系统设置类入口", iconFont);
            AddItem("命令与运行", "\uE756", "启动命令和脚本", iconFont);

            if (Nav.Items.Count > 0) Nav.SelectedItem = Nav.Items[0] as UI4NavigationViewItem;

            UI4Theme.ThemeChanged += OnThemeChanged;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            ApplyChrome();
        }

        private void AddItem(string title, string glyph, string caption, FontFamily iconFont)
        {
            Nav.Items.Add(new UI4NavigationViewItem
            {
                Header = title,
                TextIcon = glyph,
                TextIconFontFamily = iconFont,
                Content = new NavigationPage(title, caption)
            });
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_disposed)
            {
                _disposed = false;
                UI4Theme.ThemeChanged += OnThemeChanged;
            }
            ApplyChrome();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            DisposeChrome();
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            if (_disposed) return;
            Dispatcher.BeginInvoke(new Action(ApplyChrome), DispatcherPriority.Loaded);
            Dispatcher.BeginInvoke(new Action(ApplyChrome), DispatcherPriority.ApplicationIdle);
        }

        private void ApplyChrome()
        {
            if (_disposed) return;
            Nav.Background = Brushes.Transparent;
            StyleContentPanel();
            StyleLeftRail();
            StyleListBoxViewport();
        }

        private void StyleContentPanel()
        {
            Nav.ApplyTemplate();
            var content = Nav.Template == null
                ? null : Nav.Template.FindName("PART_ContentPresenter", Nav) as ContentPresenter;
            if (content == null) return;
            var panel = VisualTreeHelper.GetParent(content) as Border;
            if (panel == null) return;

            _contentPanel = panel;
            panel.SetResourceReference(Border.BackgroundProperty, "UI4.Brush.Background");
            panel.CornerRadius = new CornerRadius(RailCornerRadius);
            panel.Margin = new Thickness(ContentGap, 0, 0, 0);
            panel.Padding = new Thickness(0, ContentTopInset, 0, 0);
            panel.SizeChanged -= OnPanelSizeChanged;
            panel.SizeChanged += OnPanelSizeChanged;
            ApplyRoundedClip(panel);
        }

        private void StyleLeftRail()
        {
            Nav.ApplyTemplate();
            var rail = Nav.Template == null
                ? null : Nav.Template.FindName("PART_LeftPanel", Nav) as Grid;
            if (rail == null) return;

            _leftRail = rail;
            rail.SizeChanged -= OnPanelSizeChanged;
            rail.SizeChanged += OnPanelSizeChanged;
            ApplyRoundedClip(rail);
        }

        private void StyleListBoxViewport()
        {
            Nav.ApplyTemplate();
            if (Nav.Template == null) return;
            StyleListBox(Nav.Template.FindName("PART_ListBox", Nav) as ListBox);
            StyleListBox(Nav.Template.FindName("PART_BottomListBox", Nav) as ListBox);
        }

        private void StyleListBox(ListBox listBox)
        {
            if (listBox == null) return;
            listBox.ApplyTemplate();
            var scrollViewer = FindDescendant<ScrollViewer>(listBox);
            if (scrollViewer != null) scrollViewer.Padding = new Thickness(0);

            ApplyItemMargins(listBox);
            if (_marginHooked.Add(listBox))
            {
                listBox.ItemContainerGenerator.StatusChanged += (s, e) =>
                {
                    if (_disposed) return;
                    if (listBox.ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
                        ApplyItemMargins(listBox);
                };
            }
        }

        private static void ApplyItemMargins(ListBox listBox)
        {
            for (int i = 0; i < listBox.Items.Count; i++)
            {
                var container = listBox.ItemContainerGenerator.ContainerFromIndex(i) as ListBoxItem;
                if (container == null) continue;
                container.ApplyTemplate();
                var border = container.Template.FindName("PART_ItemBorder", container) as Border;
                if (border == null) continue;

                border.Margin = new Thickness(
                    ItemSideInset, ItemVerticalInset, ItemRightInset, ItemVerticalInset);
            }
        }

        private void OnPanelSizeChanged(object sender, SizeChangedEventArgs e)
        {
            ApplyRoundedClip(sender as FrameworkElement);
        }

        private static void ApplyRoundedClip(FrameworkElement element)
        {
            if (element == null) return;
            double w = element.ActualWidth;
            double h = element.ActualHeight;
            if (w <= 0 || h <= 0) return;

            element.Clip = new RectangleGeometry(new Rect(0, 0, w, h), RailCornerRadius, RailCornerRadius);
        }

        private static T FindDescendant<T>(DependencyObject root) where T : DependencyObject
        {
            if (root == null) return null;
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T match) return match;
                var found = FindDescendant<T>(child);
                if (found != null) return found;
            }
            return null;
        }

        private void DisposeChrome()
        {
            if (_disposed) return;
            _disposed = true;
            UI4Theme.ThemeChanged -= OnThemeChanged;

            if (_contentPanel != null)
            {
                _contentPanel.SizeChanged -= OnPanelSizeChanged;
                _contentPanel = null;
            }
            if (_leftRail != null)
            {
                _leftRail.SizeChanged -= OnPanelSizeChanged;
                _leftRail = null;
            }
        }
    }

    /// <summary>案例用的最小页面模型；真实工程换成自己的页面视图 / ViewModel。</summary>
    public sealed class NavigationPage
    {
        public NavigationPage(string title, string caption)
        {
            Title = title;
            Caption = caption;
        }

        public string Title { get; }
        public string Caption { get; }
    }
}
