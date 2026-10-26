using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using StartUI4Controls;
using QuickPanel.Models;
using QuickPanel.ViewModels;

namespace QuickPanel
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // 设置与缩放只认这一个视图模型；App.Settings 为空时（--selftest 里构造窗口）VM 自己兜一份默认值
            DataContext = new MainViewModel(App.Settings);

            BuildNavigation();
            ReapplyNavChrome();
            Loaded += (s, e) => ReapplyNavChrome();

            // 不要 {Binding Path=(ui:UI4Theme.CurrentMode)}：库发的是 StaticPropertyChanged，
            // 而 WPF 绑静态 CLR 属性找的是同名 <属性>Changed 静态事件，那样会停在初值不再刷新。
            UI4Theme.ThemeChanged += OnThemeChanged;
            UpdateFooter();

            RestoreGeometry();
            Closing += MainWindow_Closing;
        }

        private void ReapplyNavChrome()
        {
            StyleNavContentPanel();
            StyleNavLeftRail();
            StyleNavListBoxViewport();
        }

        /// <summary>
        /// 主题切换会触发 UI4ListBox 的 OnStyleRefresh：它重建 Style/ControlTemplate，模板里的
        /// ScrollViewer 又带着写死的 4 px 内边距。宿主启动时归零的那份补丁因此被换掉，左栏项的
        /// 右侧圆角在换档后又被裁平。Loaded 一轮处理模板套用，ApplicationIdle 再兜一次晚到的重建。
        /// </summary>
        private void OnThemeChanged(object sender, EventArgs e)
        {
            UpdateFooter();
            Dispatcher.BeginInvoke(new Action(ReapplyNavChrome), DispatcherPriority.Loaded);
            Dispatcher.BeginInvoke(new Action(ReapplyNavChrome), DispatcherPriority.ApplicationIdle);
        }

        /// <summary>导航项背景的左右内缩：库模板原本只有左边 2 px，宿主统一改成对称的 4 px。</summary>
        private const double NavItemSideInset = 4;

        /// <summary>左栏模板的 Border 有 1 px 右边框，ListBox 视口比项容器窄 1 px；右侧再补 1 px，
        /// 让两侧“可见白边”都是 4 px。</summary>
        private const double NavItemRightInset = NavItemSideInset + 1;

        /// <summary>上下也留 4 px：首个悬浮卡片避开栏顶的 12 px 圆角；上下对称，内容垂直中心不变。</summary>
        private const double NavItemVerticalInset = 4;

        /// <summary>防止同一个内部 ListBox 重复挂 StatusChanged；容器在虚拟化/主题重建后要重新补右边距。</summary>
        private readonly HashSet<ListBox> _navListBoxesWithMarginHook = new HashSet<ListBox>();

        private MainViewModel Vm
        {
            get { return DataContext as MainViewModel; }
        }

        /// <summary>
        /// 左栏项必须由 UI4NavigationViewItem 真身进 Items：库在 OnItemsChanged 里按类型分流到
        /// RegularItems / BottomItems，直接往 RegularItems 加不会进左栏，下次 Items 变化还会被整表清空。
        ///
        /// 每个导航项的 Content 都指向同一个 SectionShell 实例（不是各自的分组对象）：右栏绑的就是
        /// SelectedItem.Content，Content 一换整棵子树要重建，而卡片不虚拟化——那正是"切到全部工具卡一下"的成因。
        /// 分组挂在 NavSectionItem.Section 上，切换时只翻各节的 IsActive。
        /// </summary>
        private void BuildNavigation()
        {
            var vm = Vm;
            if (vm == null) return;

            var iconFont = new FontFamily("Segoe MDL2 Assets");
            foreach (var section in vm.Sections)
            {
                var navItem = new NavSectionItem
                {
                    Section = section,
                    Header = section.Title,
                    TextIcon = section.Glyph,
                    TextIconFontFamily = iconFont,
                    Content = vm.Shell
                };
                Nav.Items.Add(navItem);
            }

            // 库的 NavigationView 不发 SelectionChanged（SelectedItem 只是个 BindsTwoWayByDefault 的 DP），
            // 所以挂属性变化回调。
            System.ComponentModel.DependencyPropertyDescriptor
                .FromProperty(UI4NavigationView.SelectedItemProperty, typeof(UI4NavigationView))
                .AddValueChanged(Nav, (s, e) => ActivateSelectedSection());

            if (Nav.Items.Count > 0) Nav.SelectedItem = Nav.Items[0] as UI4NavigationViewItem;
        }

        /// <summary>
        /// 右栏内容面板的底色与圆角在库模板里没有对应 DP，宿主动 PART_ContentPresenter 的父 Border：
        /// 背景挂 DynamicResource 跟随明暗档；四角都圆，左侧留 12 与左栏形成两张独立卡片；顶部留 20
        /// 让「全部工具」不再贴着面板上沿。Padding 落在 ContentPresenter 外侧，切分组的内容滑入动画
        /// 仍只在面板内部跑；圆角 Clip 再把内容裁进卡片轮廓，避免卡片/滚动条盖住下圆角。
        /// </summary>
        private void StyleNavContentPanel()
        {
            Nav.ApplyTemplate();
            var content = Nav.Template == null
                ? null : Nav.Template.FindName("PART_ContentPresenter", Nav) as ContentPresenter;
            if (content == null) return;
            var panel = VisualTreeHelper.GetParent(content) as Border;
            if (panel == null) return;

            panel.SetResourceReference(Border.BackgroundProperty, "UI4.Brush.Background");
            panel.CornerRadius = new CornerRadius(12);
            panel.Margin = new Thickness(12, 0, 0, 0);
            panel.Padding = new Thickness(0, 20, 0, 0);

            panel.SizeChanged -= NavPanel_SizeChanged;
            panel.SizeChanged += NavPanel_SizeChanged;
            ApplyRoundedClip(panel);
        }

        /// <summary>
        /// 左栏没有 CornerRadius DP，也不能像右栏那样只垫一层背景 Border：模板里那条 1px 分隔线
        /// 会从圆角外露出来。宿主直接给 PART_LeftPanel 套圆角矩形 Clip，四角一起圆；
        /// 左栏宽度随字号变、高度随窗口变，所以 SizeChanged 时重算 Clip 的 Rect。
        /// </summary>
        private void StyleNavLeftRail()
        {
            Nav.ApplyTemplate();
            var rail = Nav.Template == null
                ? null : Nav.Template.FindName("PART_LeftPanel", Nav) as Grid;
            if (rail == null) return;

            rail.SizeChanged -= NavPanel_SizeChanged;
            rail.SizeChanged += NavPanel_SizeChanged;
            ApplyRoundedClip(rail);
        }

        private void NavPanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ApplyRoundedClip(sender as FrameworkElement);
        }

        /// <summary>
        /// 导航项容器的宽度绑定的是 LeftPanelWidth，而 UI4ListBox 模板里的 ScrollViewer 自带
        /// 4 px 内边距；容器比视口宽 8 px，右侧连同 6 px 圆角一起被裁平。宿主把内部 ListBox 的
        /// ScrollViewer 内边距归零，让悬浮/选中的灰底在栏内完整显示。
        /// 同时统一项背景的左右内缩：库模板只写了 Margin="2,0,0,0"，现在左 4 px、右 5 px、
        /// 上下各 4 px（右多 1 px 补左栏 Border 的 1 px 右边框；上下让首个卡片避开栏顶圆角），
        /// 逐项补，并挂 StatusChanged 覆盖新容器。
        /// </summary>
        private void StyleNavListBoxViewport()
        {
            Nav.ApplyTemplate();
            if (Nav.Template == null) return;
            StyleNavListBox(Nav.Template.FindName("PART_ListBox", Nav) as ListBox);
            StyleNavListBox(Nav.Template.FindName("PART_BottomListBox", Nav) as ListBox);
        }

        private void StyleNavListBox(ListBox listBox)
        {
            if (listBox == null) return;
            listBox.ApplyTemplate();
            var scrollViewer = FindDescendant<ScrollViewer>(listBox);
            if (scrollViewer != null) scrollViewer.Padding = new Thickness(0);

            ApplyNavItemMargins(listBox);
            if (_navListBoxesWithMarginHook.Add(listBox))
            {
                listBox.ItemContainerGenerator.StatusChanged += (s, e) =>
                {
                    if (listBox.ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
                        ApplyNavItemMargins(listBox);
                };
            }
        }

        private static void ApplyNavItemMargins(ListBox listBox)
        {
            for (int i = 0; i < listBox.Items.Count; i++)
            {
                var container = listBox.ItemContainerGenerator.ContainerFromIndex(i) as ListBoxItem;
                if (container == null) continue;
                container.ApplyTemplate();
                var border = container.Template.FindName("PART_ItemBorder", container) as Border;
                if (border == null) continue;

                border.Margin = new Thickness(NavItemSideInset, NavItemVerticalInset, NavItemRightInset, NavItemVerticalInset);
            }
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

        private static void ApplyRoundedClip(FrameworkElement element)
        {
            if (element == null) return;
            double w = element.ActualWidth;
            double h = element.ActualHeight;
            if (w <= 0 || h <= 0) return;

            element.Clip = new RectangleGeometry(new Rect(0, 0, w, h), 12, 12);
        }

        /// <summary>切换分组 = 只改显示哪一节。取不到对应分组就整屏不显示，宁可空屏也不让两节叠在一起。</summary>
        private void ActivateSelectedSection()
        {
            var vm = Vm;
            if (vm == null || vm.Shell == null) return;

            var picked = Nav.SelectedItem as NavSectionItem;
            vm.Shell.Activate(picked != null && picked.Section != null ? picked.Section.Key : null);
        }

        /// <summary>
        /// 单击即执行：卡片网格是一次"选择变化"，执行完把选择清掉，
        /// 否则再点同一张卡不会产生新的事件（看起来就是"第二下没反应"）。
        /// </summary>
        private void ToolGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var grid = sender as ListBox;   // UI4GridView 的基类就是 ListBox
            if (grid == null) return;

            var item = grid.SelectedItem as ToolItem;
            if (item == null) return;

            grid.SelectedIndex = -1;

            var result = Vm.Execute(item);
            // 模态框只留给错误报告：成功只写页脚，不打断连续操作
            if (!result.Ok)
                UI4MessageBox.Show(result.Message, "执行失败", UI4MessageBoxButtons.OK, owner: this);
        }

        private void SettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            var vm = Vm;
            if (vm != null) vm.IsSettingsOpen = true;
        }

        // ── 收藏页的拖动排序 ────────────────────────────────────────────
        //
        // 三件事都在宿主层完成，不改库：
        //   1) 跟手：按住右上角标记后，被拖那张卡的模板根（PART_ItemRoot）用 TranslateTransform 跟着指针走，
        //      原槽位留在布局里当"洞"，容器抬到 ZIndex 最上层。
        //   2) 挤位：指针落到最近的槽位就重算"松手后的顺序"，其余卡片用 150ms 位移动画挪到新槽位；
        //      拖动期间绝不碰数据，所以跨多少格都不会触发 Refresh / 容器重建，中途取消也能干净回去。
        //   3) 落点：插值只看指针到各槽位中心的距离——首格之前、末格之后、行间空隙都算数，
        //      不要求命中某张卡（旧实现走 InputHitTest 找卡，空隙里直接丢手势）。
        //
        // 位移必须挂在 PART_ItemRoot 上：库在 PrepareContainerForItemOverride 里把 ListBoxItem 的
        // RenderTransform 拿去跑 HoverScale 了，替换容器那层会连带把悬浮放大弄坏。
        // 输入挂网格的 Preview 事件并让网格自己捕获鼠标：子元素吃冒泡事件或焦点变化都不影响手势。

        private const string FavoriteGripTag = "FavoriteGrip";
        private const double DragLiftThreshold = 3;
        private const int ShiftAnimationMs = 150;
        private const int SettleAnimationMs = 180;
        private const double AutoScrollEdge = 42;
        private const double AutoScrollStep = 26;

        private ToolItem _dragItem;
        private UI4GridView _dragHost;
        private ListBoxItem _dragContainer;
        private FrameworkElement _dragRoot;
        private TranslateTransform _dragFollow;
        private UniformGrid _dragPanel;
        private ScrollViewer _dragScroll;
        private int _dragOriginIndex;
        private int _dragInsertIndex;
        private Point _dragStartPoint;
        private Point _dragLastPoint;
        private Point _dragLastScrollPoint;
        private Point _dragGrabOffset;
        private bool _dragLifted;
        private DispatcherTimer _autoScrollTimer;
        private DispatcherTimer _settleTimer;
        private readonly List<FrameworkElement> _settleRoots = new List<FrameworkElement>();
        private UI4GridView _settleHost;

        private void ToolGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;

            var host = sender as UI4GridView;
            if (host == null) return;

            // 只有收藏页、且搜索框为空时才允许拖：搜索态里"拖到这一格"落到全表顺序上有歧义
            var section = host.DataContext as ToolSection;
            if (section == null || !section.IsReorderable) return;

            var container = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
            if (container == null) return;

            // 按几何判定"这一下按在标记上"：即使命中结果是卡片内容（标记没吃到），只要指针在标记矩形里
            // 就开拖。多留 4px 容错，仍然不会把卡片正文的点击误判成拖动。
            var grip = FindFavoriteGrip(container);
            if (grip == null) return;
            var pointer = e.GetPosition(host);
            var gripTopLeft = grip.TranslatePoint(new Point(0, 0), host);
            var gripRect = new Rect(gripTopLeft.X - 4, gripTopLeft.Y - 4,
                                    grip.ActualWidth + 8, grip.ActualHeight + 8);
            if (!gripRect.Contains(pointer)) return;

            var item = container.DataContext as ToolItem;
            if (item == null) return;
            int origin = host.Items.IndexOf(item);
            if (origin < 0) return;

            container.ApplyTemplate();
            var root = container.Template != null
                ? container.Template.FindName("PART_ItemRoot", container) as FrameworkElement : null;
            var panel = FindVisualChild<UniformGrid>(host);
            if (root == null || panel == null) return;

            // 上一轮松手的收尾动画先落地，否则两套位移会叠在同一批卡片上
            FinishSettle();

            var rendered = root.TranslatePoint(new Point(0, 0), host);
            _dragItem = item;
            _dragHost = host;
            _dragContainer = container;
            _dragRoot = root;
            _dragPanel = panel;
            _dragScroll = FindVisualChild<ScrollViewer>(host);
            _dragOriginIndex = origin;
            _dragInsertIndex = origin;
            _dragStartPoint = pointer;
            _dragLastPoint = pointer;
            _dragLastScrollPoint = _dragScroll != null ? e.GetPosition(_dragScroll) : pointer;
            _dragGrabOffset = new Point(pointer.X - rendered.X, pointer.Y - rendered.Y);
            _dragFollow = EnsureTranslate(root);
            _dragFollow.X = 0;
            _dragFollow.Y = 0;
            _dragLifted = false;

            // 捕获挂网格而不是标记：标记是模板内容，刷新/过滤时可能被重造；网格活得更久。
            // 捕获失败就当作普通按下处理掉，避免把这次按下变成"执行卡片"。
            if (!host.CaptureMouse())
            {
                DetachFavoriteDragInput(true);
                e.Handled = true;
                return;
            }

            // 不吃掉这一下，卡片会把按下当成"选中→执行"，拖一下顺序就弹出一个系统工具
            e.Handled = true;
        }

        private void ToolGrid_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_dragItem == null || !ReferenceEquals(sender, _dragHost)) return;

            if (e.MouseDevice.LeftButton != MouseButtonState.Pressed)
            {
                CancelFavoriteDrag();
                return;
            }

            var pointer = e.GetPosition(_dragHost);
            _dragLastPoint = pointer;
            if (_dragScroll != null) _dragLastScrollPoint = e.GetPosition(_dragScroll);

            if (!_dragLifted)
            {
                double dx = pointer.X - _dragStartPoint.X;
                double dy = pointer.Y - _dragStartPoint.Y;
                if (dx * dx + dy * dy < DragLiftThreshold * DragLiftThreshold) return;
                _dragLifted = true;
                Panel.SetZIndex(_dragContainer, 500);
                Mouse.OverrideCursor = Cursors.SizeAll;
                EnsureAutoScrollTimer().Start();
            }

            UpdateFavoriteDrag(pointer);
        }

        private void ToolGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_dragItem == null || !ReferenceEquals(sender, _dragHost)) return;
            e.Handled = true;

            if (!_dragLifted)
            {
                // 只是点了一下标记：不动顺序、不写盘、更不执行卡片
                DetachFavoriteDragInput(true);
                return;
            }

            var host = _dragHost;
            var item = _dragItem;
            int insert = _dragInsertIndex;
            int origin = _dragOriginIndex;

            // 收尾要跨一次集合重排（收藏视图走 ObservableCollection.Move，不是 Refresh），
            // 先把各卡"现在看起来在哪"记下来，换位后把差值装回去再动画到 0——
            // 被拖的卡从指针处滑进新格，其余卡从半路继续归位。
            var before = SnapshotFavoritePositions(host);

            bool moved = insert != origin;
            if (moved)
            {
                var vm = Vm;
                moved = vm != null && vm.CommitFavoriteReorder(item, origin, insert);
            }

            // 保留卡上的位移，让下面的 FLIP 从当前视觉位置接着走；清状态必须在释放捕获前把 _dragItem 置空，
            // 否则 ReleaseMouseCapture 同步抛出的 LostMouseCapture 会按"取消"把所有位移清掉（页面就跳了）。
            DetachFavoriteDragInput(false);
            host.UpdateLayout();
            if (!SettleFavoriteDrag(host, before, item))
                ResetFavoriteItemRoots(host);
        }

        /// <summary>鼠标捕获被系统拿走（Alt+Tab、弹 UAC）：直接归位并清状态，不提交顺序。</summary>
        private void ToolGrid_LostMouseCapture(object sender, MouseEventArgs e)
        {
            if (_dragItem == null || !ReferenceEquals(sender, _dragHost)) return;

            var host = _dragHost;
            DetachFavoriteDragInput(true);
            ResetFavoriteItemRoots(host);
        }

        /// <summary>
        /// 拖动中的每帧：被拖的卡按指针走，落点按"最近槽位"算，位次一变就让其余卡片滑到新槽位。
        /// 槽位从布局系统现取（LayoutInformation），自动滚动导致内容上移时也能立刻跟上。
        /// </summary>
        private void UpdateFavoriteDrag(Point pointer)
        {
            var slots = CollectFavoriteSlots();
            if (slots == null || slots.Count != _dragHost.Items.Count) return;
            if (_dragOriginIndex >= slots.Count) return;

            // 被拖的卡按指针 1:1 跟手；槽位随自动滚动移动时 home 会变，视觉上才钉在指针下
            Point home = slots[_dragOriginIndex].TopLeft;
            SetTranslate(_dragFollow,
                pointer.X - _dragGrabOffset.X - home.X,
                pointer.Y - _dragGrabOffset.Y - home.Y);

            var probe = new Point(
                Math.Max(0, Math.Min(_dragHost.ActualWidth, pointer.X)),
                Math.Max(0, Math.Min(_dragHost.ActualHeight, pointer.Y)));
            int insert = NearestSlot(slots, probe);
            if (insert == _dragInsertIndex) return;

            _dragInsertIndex = insert;
            AnimateFavoriteShift(slots);
        }

        /// <summary>其余卡片挪到"松手后的位次"对应的槽位；方向与距离都由真实槽位算，行内换行也正确。</summary>
        private void AnimateFavoriteShift(List<Rect> slots)
        {
            int count = _dragHost.Items.Count;
            for (int i = 0; i < count; i++)
            {
                if (i == _dragOriginIndex) continue;

                var item = _dragHost.Items[i] as ToolItem;
                if (item == null) continue;
                var root = GetFavoriteRoot(item);
                if (root == null) continue;

                int preview = PreviewIndexFor(i);
                if (preview < 0 || preview >= slots.Count) continue;

                AnimateTranslate(root, slots[preview].X - slots[i].X, slots[preview].Y - slots[i].Y,
                                 ShiftAnimationMs);
            }
        }

        /// <summary>把原列表里第 index 项在"被拖项落到 insert"之后的位次算出来（列表这一格没动，只是预览）。</summary>
        private int PreviewIndexFor(int index)
        {
            if (_dragInsertIndex < _dragOriginIndex)
                return index >= _dragInsertIndex && index < _dragOriginIndex ? index + 1 : index;
            if (_dragInsertIndex > _dragOriginIndex)
                return index > _dragOriginIndex && index <= _dragInsertIndex ? index - 1 : index;
            return index;
        }

        /// <summary>松手后的收尾：每张卡从"当前渲染位置"滑回它的最终布局槽位。返回是否真的挂上了动画。</summary>
        private bool SettleFavoriteDrag(UI4GridView host, Dictionary<ToolItem, Point> before, ToolItem lifted)
        {
            if (host == null || before == null || before.Count == 0) return false;

            var panel = FindVisualChild<UniformGrid>(host);
            if (panel == null) return false;

            FinishSettle();
            Point origin = panel.TranslatePoint(new Point(0, 0), host);
            bool applied = false;
            for (int i = 0; i < host.Items.Count; i++)
            {
                var item = host.Items[i] as ToolItem;
                if (item == null) continue;

                Point start;
                if (!before.TryGetValue(item, out start)) continue;

                var container = host.ItemContainerGenerator.ContainerFromIndex(i) as ListBoxItem;
                if (container == null) continue;
                container.ApplyTemplate();
                var root = container.Template != null
                    ? container.Template.FindName("PART_ItemRoot", container) as FrameworkElement : null;
                if (root == null) continue;

                Rect slot = LayoutInformation.GetLayoutSlot(container);
                var translate = EnsureTranslate(root);
                translate.BeginAnimation(TranslateTransform.XProperty, null);
                translate.BeginAnimation(TranslateTransform.YProperty, null);
                translate.X = start.X - (origin.X + slot.X);
                translate.Y = start.Y - (origin.Y + slot.Y);
                AnimateTranslate(root, 0, 0, SettleAnimationMs);

                _settleRoots.Add(root);
                applied = true;
                if (ReferenceEquals(item, lifted)) Panel.SetZIndex(container, 500);
            }

            if (!applied) return false;

            _settleHost = host;
            EnsureSettleTimer().Start();
            return true;
        }

        /// <summary>Esc 取消：按当前渲染位置滑回原槽位，顺序真源一个字都没动过，所以不需要回滚数据。</summary>
        private void CancelFavoriteDrag()
        {
            if (_dragItem == null) return;

            var host = _dragHost;
            var before = host != null ? SnapshotFavoritePositions(host) : null;
            DetachFavoriteDragInput(false);
            if (host == null) return;

            host.UpdateLayout();
            if (before == null || !SettleFavoriteDrag(host, before, null))
                ResetFavoriteItemRoots(host);
        }

        /// <summary>
        /// 清掉输入状态。必须在释放捕获之前把 _dragItem 置空：ReleaseMouseCapture 会同步抛 LostMouseCapture，
        /// 那时还认得 _dragItem 的话会再走一遍"取消"，把刚算好的收尾位移清掉。
        /// clearVisuals=false 给松手/取消这两条"要接 FLIP"的路径用，位移由 SettleFavoriteDrag 接管。
        /// </summary>
        private void DetachFavoriteDragInput(bool clearVisuals)
        {
            var host = _dragHost;
            var container = _dragContainer;
            var root = _dragRoot;

            _dragItem = null;
            _dragHost = null;
            _dragContainer = null;
            _dragRoot = null;
            _dragFollow = null;
            _dragPanel = null;
            _dragScroll = null;
            _dragLifted = false;

            if (_autoScrollTimer != null) _autoScrollTimer.Stop();
            Mouse.OverrideCursor = null;
            if (host != null && host.IsMouseCaptured) host.ReleaseMouseCapture();
            if (container != null) Panel.SetZIndex(container, 0);
            if (clearVisuals && root != null) root.RenderTransform = null;
        }

        /// <summary>每个已实体化的收藏卡：条目 → 模板根。非虚拟化网格里逐项都拿得到。</summary>
        private FrameworkElement GetFavoriteRoot(ToolItem item)
        {
            if (_dragHost == null) return null;
            var container = _dragHost.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem;
            if (container == null) return null;
            container.ApplyTemplate();
            return container.Template != null
                ? container.Template.FindName("PART_ItemRoot", container) as FrameworkElement : null;
        }

        /// <summary>当前视图每一项的布局槽位（宿主坐标系，已含滚动偏移）。缺容器就放弃这一帧。</summary>
        private List<Rect> CollectFavoriteSlots()
        {
            if (_dragHost == null || _dragPanel == null) return null;

            int count = _dragHost.Items.Count;
            Point origin = _dragPanel.TranslatePoint(new Point(0, 0), _dragHost);
            var slots = new List<Rect>(count);
            for (int i = 0; i < count; i++)
            {
                var container = _dragHost.ItemContainerGenerator.ContainerFromIndex(i) as FrameworkElement;
                if (container == null) return null;
                Rect slot = LayoutInformation.GetLayoutSlot(container);
                slots.Add(new Rect(origin.X + slot.X, origin.Y + slot.Y, slot.Width, slot.Height));
            }
            return slots;
        }

        /// <summary>落点 = 离指针最近的槽位中心。首格前、末格后、行间空隙都能落到最近的一格。</summary>
        private static int NearestSlot(IList<Rect> slots, Point pointer)
        {
            int best = 0;
            double bestDistance = double.MaxValue;
            for (int i = 0; i < slots.Count; i++)
            {
                Rect slot = slots[i];
                double dx = pointer.X - (slot.X + slot.Width / 2);
                double dy = pointer.Y - (slot.Y + slot.Height / 2);
                double distance = dx * dx + dy * dy;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>指针停在上/下边缘时持续滚动：不滚的话"列表外"的位置根本拖不到。</summary>
        private DispatcherTimer EnsureAutoScrollTimer()
        {
            if (_autoScrollTimer != null) return _autoScrollTimer;

            _autoScrollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(45) };
            _autoScrollTimer.Tick += (s, e) => AutoScrollFavoriteDrag();
            return _autoScrollTimer;
        }

        private void AutoScrollFavoriteDrag()
        {
            if (!_dragLifted || _dragItem == null || _dragScroll == null) return;
            if (_dragLastScrollPoint.X < 0 || _dragLastScrollPoint.X > _dragScroll.ActualWidth) return;

            double step;
            if (_dragLastScrollPoint.Y < AutoScrollEdge) step = -AutoScrollStep;
            else if (_dragLastScrollPoint.Y > _dragScroll.ActualHeight - AutoScrollEdge) step = AutoScrollStep;
            else return;

            double max = Math.Max(0, _dragScroll.ExtentHeight - _dragScroll.ViewportHeight);
            double next = Math.Max(0, Math.Min(max, _dragScroll.VerticalOffset + step));
            if (Math.Abs(next - _dragScroll.VerticalOffset) < 0.5) return;

            _dragScroll.ScrollToVerticalOffset(next);
            _dragHost.UpdateLayout();
            UpdateFavoriteDrag(_dragLastPoint);
        }

        /// <summary>
        /// 收尾计时器结束：清掉所有位移与抬升，让下一轮从干净的布局开始。
        /// 计时器只建一次；新拖动开始时会先 Stop 再复用。
        /// </summary>
        private DispatcherTimer EnsureSettleTimer()
        {
            if (_settleTimer != null) return _settleTimer;

            _settleTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(SettleAnimationMs + 60) };
            _settleTimer.Tick += (s, e) => FinishSettle();
            return _settleTimer;
        }

        private void FinishSettle()
        {
            if (_settleTimer != null) _settleTimer.Stop();

            for (int i = 0; i < _settleRoots.Count; i++)
            {
                var root = _settleRoots[i];
                if (root != null) root.RenderTransform = null;
            }
            _settleRoots.Clear();

            if (_settleHost != null)
            {
                for (int i = 0; i < _settleHost.Items.Count; i++)
                {
                    var container = _settleHost.ItemContainerGenerator.ContainerFromIndex(i) as ListBoxItem;
                    if (container != null) Panel.SetZIndex(container, 0);
                }
                _settleHost = null;
            }
        }

        private static void ResetFavoriteItemRoots(UI4GridView host)
        {
            if (host == null) return;
            for (int i = 0; i < host.Items.Count; i++)
            {
                var container = host.ItemContainerGenerator.ContainerFromIndex(i) as ListBoxItem;
                if (container == null) continue;
                container.ApplyTemplate();
                var root = container.Template != null
                    ? container.Template.FindName("PART_ItemRoot", container) as FrameworkElement : null;
                if (root != null) root.RenderTransform = null;
                Panel.SetZIndex(container, 0);
            }
        }

        private Dictionary<ToolItem, Point> SnapshotFavoritePositions(UI4GridView host)
        {
            var positions = new Dictionary<ToolItem, Point>();
            if (host == null) return positions;

            for (int i = 0; i < host.Items.Count; i++)
            {
                var item = host.Items[i] as ToolItem;
                if (item == null) continue;
                var container = host.ItemContainerGenerator.ContainerFromIndex(i) as ListBoxItem;
                if (container == null) continue;
                container.ApplyTemplate();
                var root = container.Template != null
                    ? container.Template.FindName("PART_ItemRoot", container) as FrameworkElement : null;
                if (root == null) continue;
                // 含 RenderTransform 的当前渲染位置——半路中的动画也从这里接着走
                positions[item] = root.TranslatePoint(new Point(0, 0), host);
            }
            return positions;
        }

        private static void AnimateTranslate(FrameworkElement root, double x, double y, int milliseconds)
        {
            var translate = EnsureTranslate(root);
            var duration = new Duration(TimeSpan.FromMilliseconds(milliseconds));
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            translate.BeginAnimation(TranslateTransform.XProperty,
                new DoubleAnimation(x, duration) { EasingFunction = ease });
            translate.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(y, duration) { EasingFunction = ease });
        }

        private static void SetTranslate(TranslateTransform translate, double x, double y)
        {
            translate.BeginAnimation(TranslateTransform.XProperty, null);
            translate.BeginAnimation(TranslateTransform.YProperty, null);
            translate.X = x;
            translate.Y = y;
        }

        private static TranslateTransform EnsureTranslate(FrameworkElement root)
        {
            var translate = root.RenderTransform as TranslateTransform;
            if (translate == null)
            {
                translate = new TranslateTransform();
                root.RenderTransform = translate;
            }
            return translate;
        }

        private static FrameworkElement FindFavoriteGrip(DependencyObject container)
        {
            if (container == null) return null;

            int count = VisualTreeHelper.GetChildrenCount(container);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(container, i);
                var element = child as FrameworkElement;
                if (element != null && string.Equals(element.Tag as string, FavoriteGripTag, StringComparison.Ordinal))
                    return element;

                var found = FindFavoriteGrip(child);
                if (found != null) return found;
            }
            return null;
        }

        private static T FindAncestor<T>(DependencyObject node) where T : DependencyObject
        {
            while (node != null)
            {
                var typed = node as T;
                if (typed != null) return typed;
                if (!(node is Visual)) return null;
                node = VisualTreeHelper.GetParent(node);
            }
            return null;
        }

        private static T FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
        {
            if (obj == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = VisualTreeHelper.GetChild(obj, i);
                var typed = child as T;
                if (typed != null) return typed;

                var found = FindVisualChild<T>(child);
                if (found != null) return found;
            }
            return null;
        }

        private void SettingsOverlay_BackgroundClick(object sender, MouseButtonEventArgs e)
        {
            var vm = Vm;
            if (vm != null) vm.IsSettingsOpen = false;
        }

        /// <summary>浮层开合的四个入口（齿轮 / 完成 / 遮罩 / Esc）里只有这里需要按键；开着时遮罩盖住齿轮，所以齿轮只负责开。
        /// 拖动中的 Esc 先把卡片送回原位（顺序真源还没改过），再轮到设置浮层。</summary>
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape) return;

            if (_dragItem != null)
            {
                CancelFavoriteDrag();
                e.Handled = true;
                return;
            }

            var vm = Vm;
            if (vm != null && vm.IsSettingsOpen)
            {
                vm.IsSettingsOpen = false;
                e.Handled = true;
            }
        }

        private void UpdateFooter()
        {
            ThemeFooter.Text = "生效键 " + UI4Theme.ResolvedKey
                             + "　请求模式 " + UI4Theme.CurrentMode
                             + "　解析 " + UI4Theme.ResolvedMode;
        }

        // ── 窗口几何持久化：存了要能回正 ────────────────────────────────
        //
        // 换显示器（或拔外接屏）之后旧坐标可能落在屏幕外，窗口边看不见就没法拖回来，
        // 所以恢复前判一次虚拟桌面边界，不在界内就回正中。

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

            // 只要窗口的一个角还在屏内就接受，否则回 CenterScreen
            bool visible = left < vsRight - 40 && top < vsBottom - 40
                           && left + Width > vsLeft + 40 && top + Height > vsTop + 40;
            if (!visible) return;

            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = left;
            Top = top;
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            var settings = App.Settings;
            if (settings == null) return;

            bool maximized = WindowState == WindowState.Maximized;
            if (maximized)
            {
                // 最大化时拿到的 Bounds 是屏幕尺寸，存的应该是还原后的 Normal 尺寸，否则下次启动窗口巨大
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
    }

    /// <summary>
    /// 左栏项的真身，只为把 UIA 名字留住：导航项容器的名字取的是"数据项的纯文本"，
    /// 而 Content 现在是指向共用 SectionShell 的（右栏不重建的前提），shell 的 ToString() 又是空串，
    /// 于是回落到数据项自己的 ToString()——默认会报 "StartUI4Controls.UI4NavigationViewItem"。
    /// 在这里覆写成所属分组的标题，读屏与按名字点导航项的脚本才有得读。库的分流按
    /// is UI4NavigationViewItem 判断，子类照样进左栏。
    /// </summary>
    sealed class NavSectionItem : UI4NavigationViewItem
    {
        public ToolSection Section { get; set; }

        public override string ToString()
        {
            return Section != null ? Section.Title : base.ToString();
        }
    }
}
