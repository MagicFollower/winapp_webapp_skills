using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using QuickPanel.Models;
using QuickPanel.Services;

namespace QuickPanel.ViewModels
{
    /// <summary>
    /// 一个导航分组在右侧的那块内容：标题 + 过滤后的卡片视图。
    ///
    /// 一份 Section 对应左栏一个导航项，但它不再直接当项的 Content（那会让右栏每次切换都重建，
    /// 见 <see cref="SectionShell"/>）；库的右栏绑 SelectedItem.Content，所以项的 Content 是共用的
    /// shell，由 shell 按 IsActive 决定这一节可不可见。「全部工具」与「收藏」也是一份 Section，
    /// 只是成员分别是所有分组的并集与该并集里已收藏的那部分。
    ///
    /// 收藏节的顺序<b>由一个 ObservableCollection 自己拿着</b>，不再靠"给共享 List 加 Filter + 按位次排序，
    /// 每次变更 Refresh"：Refresh 是 Reset，ListBox 会把容器全部重建，拖动松手时页面会肉眼可见地跳一下。
    /// 独立的集合让拖动提交可以走 ObservableCollection.Move —— 发的是 Move 事件，容器原地挪，
    /// 页面不重建，FLIP 收尾动画才有连续的起点。
    ///
    /// 查询词由外部 SetQuery 推进（只有一个真源），收藏增删由外部 NotifyFavoritesChanged 推进
    /// （同理，不在这里存第二份收藏真源；那份清单在 SettingsService 里）。
    /// </summary>
    public class ToolSection : ViewModelBase
    {
        private readonly List<ToolItem> _items;
        private readonly ObservableCollection<ToolItem> _favoriteItems;
        private readonly bool _crossGroup;
        private readonly bool _favoriteOnly;
        private readonly string _allCaption;
        private string _query = string.Empty;

        /// <param name="crossGroup">这一节的成员跨大类（全部工具、收藏），卡片要显示所属分组标签。</param>
        /// <param name="favoriteOnly">只显示已收藏的卡。成员顺序就是设置里那份收藏顺序。</param>
        public ToolSection(string key, string title, string glyph, List<ToolItem> items,
                           bool crossGroup = false, bool favoriteOnly = false)
        {
            Key = key;
            Title = title;
            Glyph = glyph;
            _items = items ?? new List<ToolItem>();
            _crossGroup = crossGroup;
            _favoriteOnly = favoriteOnly;
            _allCaption = "共 " + _items.Count + " 项";

            if (favoriteOnly)
            {
                // 收藏节的视图只跟这份有序集合走；排序不再交给 SortDescription，
                // 否则 CollectionView 会按位次重新解释 Move，拖动的原位重排就白做了。
                _favoriteItems = new ObservableCollection<ToolItem>();
                RebuildFavoriteItems();
                ItemsView = new ListCollectionView(_favoriteItems);
            }
            else
            {
                ItemsView = new ListCollectionView(_items);
            }
            ItemsView.Filter = Filter;
        }

        public string Key { get; private set; }
        public string Title { get; private set; }
        public string Glyph { get; private set; }

        /// <summary>
        /// 这一节是不是当前显示的那一节。宿主把各节内容一次性挂上去、只切 Visibility，
        /// 所以切回来不会重建卡片容器（数据是固定的，重建纯属白干）。
        /// </summary>
        public bool IsActive
        {
            get { return _isActive; }
            set { SetProperty(ref _isActive, value); }
        }
        private bool _isActive;

        /// <summary>
        /// 卡片要不要显示所属分组标签：跨组的屏（「全部工具」「收藏」）必显示（一屏跨十个大类，
        /// 不标就认不出归属），搜索中也显示（命中项来自哪些分组当时看不出来）。
        /// 单分组且没搜索时显示它等于把同一个词重复十遍。
        /// </summary>
        public bool ShowGroupTags
        {
            get { return _crossGroup || !string.IsNullOrEmpty(_query); }
        }

        public IReadOnlyList<ToolItem> Items { get { return _items; } }
        public ICollectionView ItemsView { get; private set; }

        /// <summary>
        /// 标题下那一行计数。收藏节的分母与分子都只算"已收藏"那部分——它的数据源是整表并集，
        /// 直接报 _items.Count 会写成"共 90 项"，那就是撒谎。
        /// </summary>
        public string MatchCaption
        {
            get
            {
                int hit = CountMatching();
                if (string.IsNullOrEmpty(_query))
                    return _favoriteOnly ? "收藏 " + hit + " 项" : _allCaption;
                return "匹配「" + _query + "」：命中 " + hit + " / 共 " + CountTotal() + " 项";
            }
        }

        /// <summary>
        /// 这一屏能不能拖动排序：只有收藏页、且搜索框是空的。
        /// 搜索态关掉是因为视图里只剩几条，"拖到这一格"落到全表顺序上是有歧义的
        /// （被拖的卡片与它旁边的可见卡之间可能隔着好几条没显示的收藏）。
        /// </summary>
        public bool IsReorderable
        {
            get { return _favoriteOnly && string.IsNullOrEmpty(_query); }
        }

        /// <summary>空态文案：两个成因要分开说——收藏是"还没加"，搜索是"词没命中"。</summary>
        public string EmptyHint
        {
            get
            {
                if (!_favoriteOnly) return "这一屏没有匹配的入口，换个词试试或清掉搜索框。";
                return string.IsNullOrEmpty(_query)
                    ? "还没有收藏。把鼠标移到卡片上，点右下角的星标即可加入。"
                    : "收藏里没有匹配「" + _query + "」的项，换个词试试。";
            }
        }

        public bool HasNoMatches
        {
            get { return ItemsView.IsEmpty; }
        }

        public string Query { get { return _query; } }

        /// <summary>搜索框的唯一入口：同值直接返回，不做无谓的 Refresh（Refresh 会重算列数并复位滚动）。</summary>
        public void SetQuery(string query)
        {
            string next = query == null ? string.Empty : query.Trim();
            if (string.Equals(_query, next, StringComparison.Ordinal)) return;

            _query = next;
            RefreshView();
        }

        /// <summary>
        /// 收藏增删的唯一入口（卡片镜像由 ToolCatalog.SyncFavorites 先写好）。
        /// 成员与顺序一起重建：增删会改变成员集合，Reset 一次没关系；拖动提交不走这里（见 MoveFavoriteItem）。
        /// </summary>
        public void NotifyFavoritesChanged()
        {
            if (!_favoriteOnly) return;
            RebuildFavoriteItems();
            RaiseViewStateChanged();
        }

        /// <summary>
        /// 拖动提交的唯一入口：在收藏视图上做一次真正的 Move。
        /// ObservableCollection.Move 发的是 NotifyCollectionChangedAction.Move，ListBox 只挪容器不重建，
        /// 所以页面不会跳，宿主的 FLIP 收尾动画能接着演进。位次镜像由调用方先同步好。
        /// </summary>
        public bool MoveFavoriteItem(int oldIndex, int newIndex)
        {
            if (!_favoriteOnly) return false;
            if (oldIndex < 0 || newIndex < 0) return false;
            if (oldIndex >= _favoriteItems.Count || newIndex >= _favoriteItems.Count) return false;
            if (oldIndex == newIndex) return false;

            _favoriteItems.Move(oldIndex, newIndex);
            return true;
        }

        /// <summary>按 FavoriteRank 重建收藏节的顺序（位次由 ToolCatalog.SyncFavorites 预先写好）。</summary>
        private void RebuildFavoriteItems()
        {
            var ordered = new List<ToolItem>();
            foreach (var item in _items)
                if (item.IsFavorite) ordered.Add(item);

            // 位次在收藏项之间唯一（就是设置里那份清单的下标），同值只可能出现在脏镜像里，稳定排序足够
            ordered.Sort((a, b) => a.FavoriteRank.CompareTo(b.FavoriteRank));

            _favoriteItems.Clear();
            foreach (var item in ordered) _favoriteItems.Add(item);
        }

        private void RefreshView()
        {
            ItemsView.Refresh();
            RaiseViewStateChanged();
        }

        private void RaiseViewStateChanged()
        {
            RaisePropertyChanged(nameof(MatchCaption));
            RaisePropertyChanged(nameof(HasNoMatches));
            RaisePropertyChanged(nameof(ShowGroupTags));
            RaisePropertyChanged(nameof(IsReorderable));
            RaisePropertyChanged(nameof(EmptyHint));
        }

        private int CountMatching()
        {
            int hit = 0;
            foreach (var item in _items) if (Filter(item)) hit++;
            return hit;
        }

        /// <summary>文案里的"共 N 项"：收藏组要按当前收藏数算，其余就是这一节的成员数。</summary>
        private int CountTotal()
        {
            if (!_favoriteOnly) return _items.Count;
            int total = 0;
            foreach (var item in _items) if (item.IsFavorite) total++;
            return total;
        }

        private bool Filter(object o)
        {
            var item = o as ToolItem;
            if (item == null) return false;
            // 收藏节的数据源已经只装收藏项，这一条是给"增删还没走 NotifyFavoritesChanged"的瞬间兜底；
            // 计数那半截走的是整表 _items，必须靠它把未收藏项排除掉。
            if (_favoriteOnly && !item.IsFavorite) return false;
            return ToolCatalog.Matches(item, _query);
        }

        /// <summary>
        /// 左栏项的 UIA Name 取自 Content.ToString()。默认会露出类型名，
        /// 读屏与自动化脚本就只能靠子级文字猜；返回分组标题后 Name 直接是"控制面板"这种。
        /// </summary>
        public override string ToString()
        {
            return Title;
        }
    }

    /// <summary>
    /// 右栏那块常驻内容：各分组（含全部工具与收藏）的视图一次挂上，切分组只改各节的 IsActive（翻 Visibility），
    /// 不换 Content、不重建模板。
    ///
    /// 为什么要有这一层：UI4NavigationView 的右栏绑的是 SelectedItem.Content，Content 一换，
    /// WPF 就把整棵子树推倒重建——88 张卡不虚拟化，重建一次要占住 UI 线程 190 ms（实测，
    /// 见 doc 第 7 节）。让所有导航项共用这同一个实例，切换就退化成"显示哪一节"的问题。
    /// </summary>
    public class SectionShell : ViewModelBase
    {
        public SectionShell(IReadOnlyList<ToolSection> sections)
        {
            Sections = sections ?? new List<ToolSection>();
        }

        public IReadOnlyList<ToolSection> Sections { get; private set; }

        /// <summary>切到某一节：只写标志。找不到对应键就全部不显示，宁可空屏也不留两节叠着。</summary>
        public void Activate(string key)
        {
            for (int i = 0; i < Sections.Count; i++)
                Sections[i].IsActive = string.Equals(Sections[i].Key, key, StringComparison.Ordinal);
        }

        public ToolSection Find(string key)
        {
            for (int i = 0; i < Sections.Count; i++)
                if (string.Equals(Sections[i].Key, key, StringComparison.Ordinal)) return Sections[i];
            return null;
        }

        /// <summary>
        /// 返回空串是有意的：导航项容器的 UIA 名字先取 Content 的纯文本，默认 ToString() 会把
        /// "QuickPanel.ViewModels.SectionShell" 报成项名，读屏与按名字点导航项的脚本就全废了。
        /// 留空让它回落到数据项自己——名字由导航项子类 NavSectionItem.ToString() 顶成所属分组标题。
        /// </summary>
        public override string ToString()
        {
            return string.Empty;
        }
    }
}
