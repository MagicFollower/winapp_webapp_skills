# QuickPanel 项目全景

> 这份文档回答的是"**这个项目长什么样、为什么这么搭、东西在哪、怎么流起来**"。
> 怎么把它跑起来、怎么打包发版、每条命令的判据是什么，在 **[`doc/运行与构建（T0Level）.md`](doc/运行与构建（T0Level）.md)**；组件库自身的 API 手册在 **[`lib/README.md`](lib/README.md)**（那是 vendored 的上游文件，不要改）。
> 文中所有数字都是 2026-10-04 本轮在本机（Win11 26300 / .NET SDK 10.0.401）实测或机器校验过的值，改代码后请连带核对第 10 节的数字速查。

---

## 1. 创建目的与解决的问题

### 1.1 要解决的问题

Windows 自带的系统入口（控制面板项、管理控制台、设置页、命令行诊断工具）**知道在哪的人觉得简单，不知道的人一辈子找不到**：

- 入口散在四处：控制面板 / `Win+R` 运行框 / 开始菜单设置里翻三级页 / `System32` 下的 `.msc`；
- 名字不透明：普通用户不会把"想看机器上哪些端口开着"和 `netstat -ano` 对上；
- 社区命令表**质量差**：里面大量名字（`input.cpl`、`access.cpl`、`wmic.exe`、`fstrim.msc`…）在现代 Windows 上根本不存在，抄来的表要么点了报错，要么把"家庭版没有这个组件"和"要管理员权限"混成一句"启动失败"，用户只能瞎试。

### 1.2 这个项目的回答

一块**左侧按功能分组、右侧卡片式**的面板：一张卡 = 一个系统入口，卡上同时给出**中文功能名、命令原文、一句写给普通人的介绍、以及兼容性标签**；单击即执行。三条硬口径：

| 口径 | 落地方式 |
|---|---|
| 只收 Win10 与 Win11 都能解析的入口 | 每条命令本机逐条实测；行为不一致的写进卡片标签而不是删掉（11 项有兼容备注） |
| SKU 差异与权限差异是两件事 | `HomeEditionMissing`（7 项，家庭版根本没这个文件）与 `RequiresAdmin`（37 项）分列，失败文案也分开说 |
| 流传但实测不存在的名字不收 | 17 个误传名字进黑名单，由 `--selftest` 点名挡住，防止以后被"照社区表补回来" |

### 1.3 不做什么（边界）

- **不是**通用启动器 / 快捷方式管理器：只收系统自带入口，不收第三方程序与用户自建项。
- **不是**系统优化器：所有卡片都只是"把某个系统工具打开"，项目自己不写注册表、不改任何系统状态（唯一的例外是「重建图标缓存」那条，它会停一次 Explorer，卡片上打了危险标签）。
- **不做**执行确认：按"单击直接执行"的决策推进，危险项靠卡片文案提醒（`danger` 胶囊），不弹确认框。
- **不接**套装键与局部换肤：只用内置明暗两档 + 宿主自己的两套定义，不用 `UI4ThemePacks` 的 8 套业务档，也不用 `UI4ThemeScope`。

---

## 2. 项目技术栈

| 层 | 选型 | 版本 / 口径 | 为什么是它 |
|---|---|---|---|
| 运行时 | .NET | `net10.0-windows` | WPF 只在 Windows 桌面运行时上有；`app.manifest` 声明 `PerMonitorV2`（net10 的 WPF 仍按清单取 DPI 级别） |
| UI 框架 | WPF | `UseWPF=true` | 纯桌面、要原生标题栏染色与托盘这类系统集成面 |
| 组件库 | `StartUI4Controls` | **3.0.0**（`lib/StartUI4Controls.csproj`） | **零 XAML** 组件库：控件模板全由 C# 构建，主题是一张 38 令牌表（令牌数由自测钉住）。工程内是 **44 个 `.cs`**（`lib/` 38 + `lib/Internal/` 6），控件清单见 `lib/README.md`。以**整份源码工程引用**（`ProjectReference ../lib/`）而不是 dll / NuGet，交付包能独立 clone 重建 |
| 编辑器内核 | `AvalonEdit` | 6.3.1.120（lib 唯一第三方依赖） | 只被 `UI4CodeEditor` 用；本项目当前没用到该控件，但 restore 必须能拿到它 |
| MVVM | 手写，不引框架 | `ViewModelBase` + `RelayCommand`（各 23 / 56 行） | 只有 INotifyPropertyChanged 与 ICommand 两件事要解决，引 CommunityToolkit 不划算 |
| 语言约定 | C# latest | **`Nullable=disable`、`ImplicitUsings=disable`** | 全项目显式 `using`、不用可空引用类型注解；新增文件必须照此，否则会出现"只有我的文件带隐式 using"的分裂风格 |
| 设置存储 | 自研 kv1 纯文本 | `%APPDATA%\QuickPanel\settings.json`，9 个键 | 无 schema、可手改、坏文件改名留档、读回一律钳位；引 JSON 库反而多一个依赖 |
| 图标 | 本地生成的像素点阵 | `app/AppIcon.ico`（7 帧 32bpp ICO） | 由 Skill 的 `make-icon.ps1` 生成，不联网、不依赖 ImageMagick |
| 质量门禁 | 自测内建在 exe 里 | `QuickPanel.exe --selftest`，**107 条断言**，退出码 = 失败数 | 发布脚本拿它当闸门；见第 8.7 节 |
| 图标字体 | Segoe MDL2 Assets | 不用 Segoe Fluent Icons | Fluent 是 Win11 独有，Win10 上渲染成豆腐块 |

---

## 3. 项目架构

### 3.1 目录树（交付状态，78 个文件：75 份源码/文档 + 2 个产物 exe + 本文）

```text
QuickPanel/
├── README.md                     本文：项目全景
├── doc/
│   └── 运行与构建（T0Level）.md   零基础上手：八节、每条命令带判据与本机真实输出
├── app/                          应用本体（MVVM 五层 + 启动）
│   ├── App.xaml / App.xaml.cs            启动序列（顺序承重）+ --selftest 分支 + 全局异常落盘
│   ├── MainWindow.xaml / .xaml.cs        工具条 / 左栏 / 右栏常驻容器 / 卡片模板 / 设置浮层宿主
│   ├── QuickPanel.csproj                 版本、图标、单文件发布开关、ArtifactLabel 分档
│   ├── app.manifest                      PerMonitorV2
│   ├── AppIcon.ico
│   ├── 快速启动.txt                       四条命令 + "加/改一个快捷入口"的最常做改动指引
│   ├── Models/          ToolItem.cs（一张卡的全部数据与标签文案）
│   ├── ViewModels/      MainViewModel.cs · ToolSection.cs（含 SectionShell）· ViewModelBase.cs · RelayCommand.cs
│   ├── Services/        ToolCatalog.cs（数据源）· LaunchService.cs · SettingsService.cs + SettingsCodec.cs
│   │                    ThemeService.cs · SelfTest.cs
│   ├── Helpers/         Theme.cs（宿主配色单源 + Policy）· Typography.cs（字号/缩放/尺寸单源）
│   ├── Converters/      Converters.cs（BoolToVis · ZoomedSize）
│   └── Views/           SettingsOverlay.xaml(.cs)
├── lib/                          组件库整份源码拷贝（44 个 .cs + csproj + LICENSE.txt + README.md + 架构审计报告）
│   └── Internal/         6 个内部件（ColorToBrushConverter 等）
├── publish.cmd                   单文件 + 自带运行时
├── publish_no_runtime.cmd        单文件 + 框架依赖
├── publish/                      产物（只一个 exe）
└── publish_no_runtime/           产物（只一个 exe）
```

不该进版本控制的：`app/bin`、`app/obj`、`lib/bin`、`lib/obj`、两个 `publish*` 目录、`%APPDATA%\QuickPanel\`。

### 3.2 分层与依赖方向

```mermaid
flowchart TD
    subgraph VIEW["视图层（XAML + 控件）"]
        MW["MainWindow.xaml<br/>工具条 / 左栏 / 右栏 / 浮层"]
        SO["Views/SettingsOverlay.xaml<br/>四节设置内容"]
        LIB["lib/ StartUI4Controls<br/>UI4GridView · UI4NavigationView · UI4TextBox …"]
    end

    subgraph VM["视图模型层"]
        MAINVM["MainViewModel<br/>设置项 + Sections + Shell + Query"]
        SEC["ToolSection × 11<br/>ItemsView 过滤 / IsActive / ShowGroupTags"]
        SH["SectionShell<br/>右栏常驻内容（十节共用一个实例）"]
    end

    subgraph SVC["服务层（纯逻辑，可被 --selftest 直接出题）"]
        CAT["ToolCatalog<br/>90 项 / 10 组数据源 + 探测"]
        LAUNCH["LaunchService<br/>切命令 → 启动进程 → 失败文案"]
        SETT["SettingsService + SettingsCodec<br/>kv1 落盘与钳位"]
        THS["ThemeService<br/>稳定键 → 库的 SetTheme/Apply"]
        ST["SelfTest<br/>107 条断言，退出码=失败数"]
    end

    subgraph HELP["常量单源"]
        TH["Helpers/Theme.cs<br/>明暗两套令牌 + Policy"]
        TY["Helpers/Typography.cs<br/>字号/缩放/尺寸区间与派生"]
    end

    MW --> MAINVM
    MW --> SO
    SO --> MAINVM
    MW --> LIB
    SO --> LIB
    MAINVM --> SEC
    MAINVM --> SH
    SH --> SEC
    MAINVM --> CAT
    MAINVM --> LAUNCH
    MAINVM --> SETT
    MAINVM --> THS
    MAINVM --> TY
    SEC --> CAT
    CAT --> TH
    THS --> TH
    SETT --> TY
    ST -.->|"直接对纯函数与模板出题"| CAT
    ST -.-> SETT
    ST -.-> TY
    ST -.-> MW
```

依赖只往下走：`Models` 不引用 `Services`（卡片上"所属大类"那枚标签的文案，是建项时由 `ToolCatalog` 现算好塞进 `ToolItem.GroupTitle`，而不是绑定时反查）。`Services` 与 `Helpers` 里没有 WPF 之外的界面依赖，`SelfTest` 因此能绕开窗口直接验逻辑。

---

## 4. 代码地图（要改哪儿，看这张表）

| 文件 | 职责 | 关键成员 | 改它之前先看 |
|---|---|---|---|
| `app/Services/ToolCatalog.cs` | **数据源**：90 项 / 10 组，一行一张卡 | `Groups`（键/标题/图标）、`GroupTitleOf`、`Items`、`I(...)` 建项器、`Matches`、`ProbePath`、`ProbeAvailability`、`BuildSections` | §7.1 新增入口流程、§8.5 探测口径 |
| `app/Models/ToolItem.cs` | 一张卡的数据与**标签文案** | `GroupTitle`、`CompatNote/DangerNote/DetailNote`、`PlatformChip/AdminChip/EditionChip/MissingChip/DetailTip` | §8.4 标签体系 |
| `app/ViewModels/ToolSection.cs` | 一个分组在右栏的那块内容 + 常驻外壳 | `ItemsView`（`ListCollectionView` 过滤）、`SetQuery`、`MatchCaption`、`HasNoMatches`、`IsActive`、`ShowGroupTags`；`SectionShell.Activate` | §8.1 右栏常驻 |
| `app/ViewModels/MainViewModel.cs` | 窗口可绑定视图：设置项 + 数据源 + 执行 | `Sections`/`Shell`、`Query`、`Execute`、`ThemeKey`、`BaseFontSize`、`ZoomPercent`、`IsSettingsOpen`、应用信息只读项 | §8.6 设置流 |
| `app/Services/LaunchService.cs` | 命令 → 进程 | `Split`（**按第一个空格切** file/args）、`Launch`、`BuildFailureText`（SKU/缺文件/提权分开说） | §8.5 |
| `app/Services/SettingsService.cs` + `SettingsCodec.cs` | kv1 落盘（`%APPDATA%\QuickPanel\settings.json`，9 键） | `ToMap`/`ApplyMap`（**加设置键必须同时改这两处 + 自测键名点名清单**）、`Load`/`Save` 永不抛 | §8.6 |
| `app/Services/ThemeService.cs` | 配色档的稳定键通路 | `AvailableKeys`、`IsAllowed`（被 `Theme.Policy` 挡）、`Apply`、`DisplayLabel`、`ResolvedKey` | §8.3 |
| `app/Helpers/Theme.cs` | 宿主配色单源 + **明暗策略** | `Policy`（`both`/`light-only`/`dark-only`，留 `TODO` 时自测直接红）、`BuildLightDefinition`/`BuildDarkDefinition` | §8.2 |
| `app/Helpers/Typography.cs` | 字号/字体/缩放与固定件尺寸的**唯一常量出处** | 默认值与区间、`SizeOf(role,base)` 层级换算、`NavRailWidth`/`SearchBoxWidth`、`Publish`（写资源键）、`BuildFamilyChoices` | §8.2 |
| `app/MainWindow.xaml` | 结构 + 模板 | `Window.Resources` 里三块模板（`ToolCard` / `SectionShell` / `ToolSection`）、`Chip*` 样式、齿轮入口、浮层宿主、根 `LayoutTransform` | §5 |
| `app/MainWindow.xaml.cs` | 只有胶水 | `BuildNavigation`（十一个项共用一个 Content）、`ActivateSelectedSection`、`ToolGrid_SelectionChanged`（执行后清选择）、几何持久化与越界回正、`NavSectionItem` | §8.1 |
| `app/App.xaml.cs` | **启动顺序**（承重） | `--selftest` 分支在最前 → `Register` 明暗两套 → `ThemeService.Apply` → `ApplyDisplaySettings` → 建窗 | §8.2 |
| `app/Services/SelfTest.cs` | 门禁 | 四段：令牌/配色、设置 kv1、排印与缩放、数据源；共 107 条 | §8.7 |
| `app/Converters/Converters.cs` | 两个转换器 | `BoolToVis`（浮层与标签显隐）、`ZoomedSize`（缩放下窗口下限折算） | §8.2 |

---

## 5. 组件架构

### 5.1 窗口可视结构

```mermaid
flowchart TD
    W["Window「快捷工具面板」<br/>MinWidth/MinHeight 走 ZoomedSize 按系数折算"]
    W --> LT["根 Grid：LayoutTransform = ScaleTransform(ZoomFactor)<br/>全局缩放挂这一层"]
    LT --> G["UI4Grid（主题渐变底）"]
    G --> R0["工具条：标题 + UI4TextBox 搜索框 + 齿轮 UI4Button"]
    G --> R1["UI4NavigationView（左栏，LeftPanelWidth 随字号长）"]
    G --> R2["页脚：最近执行结果 + 数据源规模 + 生效键"]
    LT --> OV["设置浮层宿主 SettingsHost<br/>遮罩 + Border + Views/SettingsOverlay"]

    R1 -->|"SelectedItem.Content（十一个项指向同一个实例）"| SH["SectionShell 模板<br/>ItemsControl，ItemsPanel=Grid，十节叠在一格"]
    SH --> S1["ToolSection 视图（IsActive=true 的那节可见）"]
    S1 --> HDR["分组标题 + MatchCaption"]
    S1 --> GRID["UI4GridView（卡片网格）"]
    S1 --> EMPTY["空态文案（HasNoMatches）"]
    GRID --> CARD["ToolCard 模板：标题 / 命令条(等宽+悬浮详情) / 介绍 / 标签行"]
```

### 5.2 用到的库控件与宿主扩展

| 库控件 | 用在哪 | 宿主必须知道的点 |
|---|---|---|
| `UI4GridView` | 每个分组右栏 | `ItemWidth` 只是**算列数的基准单元**，卡片铺满所在列；`ItemHeight=NaN` 让卡片按内容长高；`ItemMargin=20` 同时是投影外溢余量（`ShadowDepth 8 + Blur 12 ≈ 20`）与悬浮放大余量（左右 ≥ 10）；`IsSynchronizedWithCurrentItem="False"` **必须显式关**，否则一改过滤就替你点第一张卡；`ItemsPanel` 是 `UniformGrid`，**不虚拟化** |
| `UI4NavigationView` | 左栏 | 项必须是 `UI4NavigationViewItem` 真身进 `Items`（库按类型分流）；右栏绑 `SelectedItem.Content`；它**不发 `SelectionChanged`**（`SelectedItem` 只是 `BindsTwoWayByDefault` 的 DP），宿主用 `DependencyPropertyDescriptor` 挂回调；六个颜色 DP 在库里是字面值，深色档下要逐个显式接回令牌 |
| `UI4TextBox` | 搜索框 | `ShowClearButton`；宽度走 `App.Size.SearchBox` 随字号长 |
| `UI4Button` | 齿轮、设置面板里所有按钮 | 钉死尺寸时必须显式给 `Padding="0"`（库默认 `10,0,10,0` 会把内容区挤成 8 px）；尺寸/圆角走 `App.Size.RoundButton` / `App.Radius.RoundButton` |
| `UI4MessageBox` | **只在执行失败时** | 返回 `bool?`，判 `== true`；模态框不用于成功提示 |
| `UI4Theme` / `UI4ThemeToken` | 主题接线 | 38 令牌 → `UI4.Color.<名>` 与 `UI4.Brush.<名>` 逐字派生，另有 `UI4.Brush.Text/Border` 两个真别名，以及 3 个排印键 `UI4.Font.Size.Base/Code/Family` |

宿主在**同一处资源根**上扩展了三层键（`Typography.Publish` 写）：

- 层级字号：`App.Font.Size.Caption/Small/Medium/Lead/Icon`（基准 15 时 = 11/12/13/14/16）；
- 固定件尺寸：`App.Size.RoundButton/Chip/NavRail/SearchBox`；
- 圆角：`App.Radius.RoundButton/Chip`（**必须是 `CornerRadius` 类型**——`DynamicResource` 不做类型转换）。

---

## 6. 数据流图

### 6.1 主数据流：数据源 → 视图 → 执行 → 回显

```mermaid
flowchart LR
    SRC["ToolCatalog.Items<br/>90 项静态数组（写死在源码里）"]
    PROBE["ProbeAvailability()<br/>启动时逐条 File.Exists"]
    SEC["BuildSections()<br/>10 组 + 「全部工具」= 11 份 ToolSection"]
    VM["MainViewModel.Sections / Shell"]
    NAV["UI4NavigationView<br/>十一个 NavSectionItem"]
    GRID["UI4GridView.ItemsSource<br/>= Section.ItemsView（ICollectionView）"]
    CARD["卡片可视"]
    CLICK["SelectionChanged（选中即执行）"]
    LS["LaunchService.Launch<br/>Split → ProcessStartInfo(UseShellExecute=true)"]
    FOOT["页脚 StatusMessage"]
    ERR["UI4MessageBox<br/>仅失败时"]

    SRC --> PROBE --> SEC --> VM --> NAV
    SEC --> GRID --> CARD --> CLICK --> LS
    LS --> FOOT
    LS -->|"启动失败"| ERR
    NAV-->|"Activate(section.Key)"| SEC
```

关键点：**执行链路上没有任何"重新加载数据"的步骤**。`ToolItem` 是静态数组里的同一批实例，切分组、搜索、执行都只是在换视图与换过滤条件。

### 6.2 搜索流（一个真源，十一个视图各自过滤）

```mermaid
flowchart LR
    TB["UI4TextBox.Text<br/>UpdateSourceTrigger=PropertyChanged"]
    Q["MainViewModel.Query（唯一真源）"]
    LOOP["foreach section.SetQuery(next)"]
    F["ListCollectionView.Filter<br/>= ToolCatalog.Matches(item, query)"]
    CAP["MatchCaption / HasNoMatches / ShowGroupTags<br/>三个派生值同时通知"]
    TB --> Q --> LOOP --> F --> CAP
    CAP -->|"ICollectionView 增量刷新，不重建集合"| GRID["UI4GridView"]
```

`SetQuery` 对同值直接返回——`Refresh()` 会重算列数并复位滚动位置，白刷一次就是一次可见跳动。

### 6.3 设置流（面板 → 落盘 → 生效）

```mermaid
sequenceDiagram
    participant U as 用户
    participant P as SettingsOverlay
    participant VM as MainViewModel
    participant S as SettingsService
    participant A as App
    participant R as Application.Resources

    U->>P: 齿轮打开浮层，拖动滑杆 / 选档
    P->>VM: 写属性（BaseFontSize / ZoomPercent / SelectedFontFamily / ThemeKey）
    VM->>VM: ClampBase / ClampZoom（越界值钳回区间）
    VM->>S: 写内存字段（不落盘）
    VM->>A: ApplyDisplaySettings()
    A->>R: Typography.Publish 写自有项（字号/尺寸/圆角键）
    R-->>P: DynamicResource 就地替换，界面即时跟随
    U->>P: 点「完成」/ 遮罩 / Esc
    P->>VM: IsSettingsOpen=false
    VM->>S: Save()（只在关闭时写盘；文本没变则不写）
```

### 6.4 主题流

```mermaid
flowchart TD
    POL["Helpers/Theme.cs 的 Policy<br/>both / light-only / dark-only"]
    TS["ThemeService.IsAllowed(key)?"]
    LIB["UI4Theme.SetTheme(mode) 或 Apply(套装键)"]
    TOK["UI4Theme.WriteTokens<br/>38 令牌 → UI4.Color.* / UI4.Brush.* + 3 个排印键"]
    DICT["Application.Resources.MergedDictionaries 里那份共享字典"]
    UI["所有绑 UI4.Brush.* 动态资源的消费者<br/>含卡片、胶囊、原生标题栏（DWM 染色）"]
    BAR["UI4WindowTitleBar.Apply 按当前主题染标题栏<br/>Win10 只有深/浅标志，Win11 才染三色"]

    POL --> TS -->|"不允许 → 页脚解释，不切换"| FOOT2["StatusMessage"]
    TS -->|"允许"| LIB --> TOK --> DICT --> UI
    LIB --> BAR
```

---

## 7. 工作流程

### 7.1 新增 / 修改一个快捷入口（本项目最常做的改动）

```mermaid
flowchart TD
    A["在 ToolCatalog.Groups 里定分组（已有就跳过）<br/>键 / 中文标题 / 单个私有区图标码位"]
    B["加一行 I(group, title, command, description, ...)"]
    C{"命令真正启动的那个文件<br/>（LaunchService.Split 的第一段）<br/>以 .exe/.cpl/.msc 结尾？"}
    D["给 probe，必要时给 probeKind<br/>explorer.exe 与 regedit.exe 在 %Windows% 根，不在 System32"]
    E["不给 probe（cmd /k …、powershell …、ms-settings:、control /name、shell: 都算这类）"]
    F{"要不要标签？"}
    G["admin / homeMissing / compat / danger / detail 按需写"]
    H["dotnet build + QuickPanel.exe --selftest"]
    I{"107 条全绿？"}
    J["完事。界面代码一行都不用改：<br/>左栏项、卡片、分组标签、搜索命中都自动跟上"]
    K["看 FAIL 名字改数据，别改断言"]

    A --> B --> C
    C -->|是| D --> F
    C -->|否| E --> F
    F --> G --> H --> I
    I -->|是| J
    I -->|否| K --> H
```

### 7.2 日常开发回路

```mermaid
flowchart LR
    E["改 app/ 下代码"] --> B["dotnet build app\\QuickPanel.csproj<br/>判据：0 错误 + 固定 9 条警告（都来自 lib 上游）"]
    B --> S["QuickPanel.exe --selftest<br/>退出码 = 失败断言数"]
    S --> R["dotnet run 起窗口点一遍"]
    R --> E
```

警告数**多于或少于 9 条**说明 `lib/` 被动过或文件不全；增量构建会出现 `0 个警告`（只重编 app），要数警告就 `--no-incremental`。

### 7.3 发版

```mermaid
flowchart TD
    V["改 app/QuickPanel.csproj 的 Version"] --> P1["rm 空 publish 与 publish_no_runtime"]
    P1 --> P2["publish_no_runtime.cmd（框架依赖，小）"]
    P2 --> P3["publish.cmd（自带运行时，大）"]
    P3 --> G1{"脚本第 2 步的 selftest<br/>exit code = 0？"}
    G1 -->|否| STOP["停：看 %APPDATA%\\QuickPanel\\selftest.txt"]
    G1 -->|是| G2{"产物 FileVersion == 刚写的版本？"}
    G2 -->|否| STOP2["停：publish 目录里坐的是旧构建"]
    G2 -->|是| G3{"目录里只有那一个 exe？"}
    G3 -->|是| DONE["交付：单 exe 就是全部<br/>（框架依赖档另需目标机装 Desktop Runtime 10.x）"]
    G3 -->|否| STOP
```

---

## 8. 工作原理（承重机制）

### 8.1 右栏常驻：为什么切分组不重建，以及它换来的速度

库的右栏绑的是 `SelectedItem.Content`，**Content 一换，`ContentControl` 就把整棵子树推倒重建**；而 `UI4GridView` 的面板是 `UniformGrid`（WPF 里不虚拟化），重建意味着所有卡片重新实体化。实测成本 ≈ **2.2 ms/卡 + 18 ms 截距**：77 卡那一次 181–328 ms，8–12 卡那几次 40–50 ms。

修法（**没有动 `lib/`**）：十一个导航项的 `Content` 全部指向同一个 `SectionShell` 实例 → 右栏只模板化一次；shell 的模板是一个 `ItemsPanel=Grid` 的 `ItemsControl`，十节叠在同一格；每节的根 `Grid` 绑 `Visibility ← ToolSection.IsActive`。切分组 = 改十个布尔。

```mermaid
flowchart TD
    BOOT["启动：只有「全部工具」可见 → 只建它的 90 张卡"]
    SW1["首次进某分组 → 那一节的子树第一次参与 measure → 建它那 8–12 张卡（56–102 ms，一次性）"]
    SW2["再次进任何一节 → 只翻 Visibility + 重新布局（3–25 ms）"]
    COL["Collapsed 子树不参与 measure<br/>ItemsControl 的容器生成因此天然惰性"]
    COL --> SW1
    BOOT --> SW1 --> SW2
```

代价与连带后果（都记在 `doc/运行与构建（T0Level）.md` §7.3）：十节都点过后常驻 180 张卡的容器（90 + 90，按数据源算出，工作集未量化）；导航项的 UIA 名字会退化成类型名，靠 `SectionShell.ToString()` 返回空串 + 宿主子类 `NavSectionItem.ToString()` 返回分组标题补回；`--selftest` 里"模板可实例化"从两条变三条。

### 8.2 三层排印键与"本地赋值即退订"

- 库的兜底层：`UI4Theme.WriteTokens` 随每份主题字典发布 `UI4.Font.Size.Base=15 / Code=14 / Family=Segoe UI`。**这层不能少**——控件模板引用的就是这些键，没人发布时所有 `UI4*` 控件静默掉到 WPF 裸默认 12 px，无编译错无异常。
- 宿主的覆盖层：`Typography.Publish` 把覆盖值写在 `Application.Resources` 的**自有项**上。自有项先于 `MergedDictionaries` 命中，所以切主题冲不掉覆盖；反过来把键塞进共享字典内部就会被下次重写顶掉。
- 界面层：`app/**/*.xaml` 里**不允许出现字面 `FontSize="数字"`**（静态扫描判据：输出为空）。
- **本地赋值就是退订主题**：写一个字面色（`Foreground="#333"`）会把 `SetResourceReference` 顶掉，之后 `ClearValue` 只能回到库内代码的字面默认值。这是设计，不是 bug。

字号与缩放是两件事：字号只带动**读排印键的那些东西**；缩放是整棵子树的 `LayoutTransform`（`LayoutTransform` 先把可用尺寸除以系数再交给子树，所以放大不裁切，只是窗里能看见的设计像素变少）。已知边界：WPF 的 `Popup`（下拉、右键菜单）住在自己的可视根里，**不吃祖先 `LayoutTransform`**，缩放档下弹层仍按 100% 渲染。

### 8.3 主题接线的四条硬顺序

```mermaid
flowchart LR
    A["base.OnStartup(e)"] --> B["--selftest 分支（排在最前）"]
    B --> C["UI4Theme.Register 明暗两套定义"]
    C --> D["ThemeService.Apply(设置里的键)"]
    D --> E["ApplyDisplaySettings() 写宿主覆盖键"]
    E --> F["new MainWindow() + Show()"]
```

`Register/SetTheme` 若在 `base.OnStartup` 之前调（比如在构造函数或 `Main` 里），库第一句 `Application.Current == null` 就 return，**静默不装字典**：症状是宿主 `{DynamicResource UI4.*}` 全空，而库内控件照常好看。本项目不引套装键，所以没有 `UI4ThemePacks.RegisterAll()` 必须在 `Apply` 之前那条约束；一旦将来要接，顺序错了 `Apply` 只返回 `false`、不抛异常、界面纹丝不动。

### 8.4 卡片标签体系：四件事分开表达

| 标签 | 数据字段 | 含义 | 数量 |
|---|---|---|---|
| `Win10 · Win11 通用` 或兼容备注 | `CompatNote` | 两档都能开，但**行为/页名不一样** | 11 |
| `需管理员` | `RequiresAdmin` | 权限问题：能开，但要提权 | 37 |
| `家庭版没有` | `HomeEditionMissing` | SKU 问题：这台机器根本没这个文件 | 7 |
| 危险提示（强调色） | `DangerNote` | 点下去有后果（重启、改系统状态、关窗口） | 6 |
| `本机未找到`（强调色） | `Availability=Missing` | 运行期探测结论，**仍允许点击** | 本机 0 |
| 所属大类 | `GroupTitle` + `Section.ShowGroupTags` | 只在「全部工具」或搜索中显示 | — |

把 SKU 缺失误报成"要提权"会误导用户，所以这四类必须是四个独立字段、四枚独立胶囊，失败文案（`LaunchService.BuildFailureText`）也分开说。

### 8.5 命令形态与探测口径

- `LaunchService.Split` 按**第一个空格**切成 file + args，`UseShellExecute=true` 交给 Shell 解析——所以卡上写的就是运行框里能直接粘的原文。
- "是不是文件型入口"看的是**真正被启动的那个文件**（Split 的第一段）而不是整条命令的尾巴：`cmd /k … & start explorer.exe` 这种组合命令尾巴带 `.exe`，但它启动的是 `cmd`，不该去探。`--selftest` 里 `探测挡位与启动文件自洽` 一条同时校验"探测名要和启动的文件对得上"。
- 探测分三挡：`System32File`、`WindowsFile`（`explorer.exe` 与 `regedit.exe` 住在 `%Windows%` 根，拼进 System32 永远探不到）、`None`（URI / shell 名 / 组合命令）。
- 探不到只打标签，**不禁止点击**：家庭版可能被 DISM 补装，路径也可能在 SysWOW64。

### 8.6 设置的四条规矩

1. 浮层开合状态只有 `IsSettingsOpen` 一处，入口按钮不另存；关法三条（完成 / 遮罩 / Esc）。
2. **只在关闭时落盘**；`Save` 内部再比一次"序列化文本没变就不写"。
3. 默认值与区间只从 `Typography` 来，XAML 用 `x:Static` 引，代码后置里不写第二份常量。
4. 从文件读回的一切都要钳（字号 12–28、缩放 50–200、`NaN/Infinity` 回默认，解析不了的不覆盖现值）；枚举按**名**解析并拒绝数字串。kv1 没有 schema，**加设置键必须同时改 `ToMap`/`ApplyMap` 与自测的键名点名清单**，否则打错一个字母就是静默丢设置。

### 8.7 `--selftest` 为什么是门禁

`SelfTest.Run()` 返回**失败断言数**当退出码，四段共 107 条：

```mermaid
flowchart TD
    A["QuickPanel.exe --selftest"] --> B["分支排在 OnStartup 最前：<br/>不开窗、不读设置、不挂 UI 异常钩子"]
    B --> C1["段 1 令牌与配色：38 令牌、内置定义逐令牌可取色、<br/>宿主明暗两套对比度与亮度判据、Policy 已显式决策"]
    B --> C2["段 2 设置 kv1：九项哨兵值往返、写侧键名点名、<br/>CR/LF 必拒、越界钳位、非法值不覆盖现值"]
    B --> C3["段 3 排印与缩放：库兜底键在、宿主覆盖换档后还在、<br/>层级阶梯、固定件尺寸随字号单调、三块 XAML 能构造"]
    B --> C4["段 4 数据源：分组容量、介绍 10–48 字、命令唯一、<br/>探测自洽、17 个误传名字黑名单、SKU 集合点名、<br/>过滤通路、图标码位、分组标签时机、右栏常驻前提"]
    C1 --> D{"全绿？"}
    C2 --> D
    C3 --> D
    C4 --> D
    D -->|"否：退出码 = 失败数"| STOP["发布脚本挡下，报告在<br/>%APPDATA%\\QuickPanel\\selftest.txt"]
    D -->|是| GO["继续打包下一步"]
```

它比"人眼看一遍"硬的地方在于：把**只有运行期才暴露的静默失效**（库没发布排印键、宿主覆盖被冲掉、`Policy` 还留着 `TODO`、数据源被误传命令污染、两块 XAML 构造即抛）变成了编译期之后、交付之前的确定闸门。反证实验做过两次：把 `telephon.cpl` 改成误传的 `telephonc.cpl` → 只红黑名单那条；把 `ShowGroupTags` 改成恒 `false` → 正好红 2 条、退出码 2。

---

## 9. 已知边界与未验证项

| 边界 | 性质 |
|---|---|
| 下拉浮层与右键菜单不吃 `LayoutTransform`，缩放档下仍按 100% 渲染 | WPF 机制，不补偿；写进交付文档让用户判断观感 |
| 设置浮层在 200% 缩放下要往下滚才看到「全局缩放」一节 | 已知观感问题，未做自适应高度 |
| `UI4CodeEditor` 的语法高亮不跟主题 | 本项目没用到该控件 |
| 明暗策略是显式决策（`Policy="both"`，出厂跟随系统） | 有意为之，不是遗漏；留 `TODO` 时自测会红 |
| 所有"家庭版没有 / Win10 上如何"的结论 | 本机是 Win11 26300 `ProfessionalWorkstation`，**未在 Win10 与家庭版真机复测**；靠运行期探测打标签兜底 |
| `ipconfig /flushdns` 免管理员 | 只在 Win11 上证实 |
| 常驻 180 张卡容器的内存占用 | 只验了"切换不卡 + 功能正确"，未量化工作集 |
| 卡片上「详细差别」的长文本 | 只在悬浮时可见（正文那行受 ≤48 字约束，因为网格等分高度会连带撑高全部卡片） |

---

## 10. 数字速查（改动后请核对）

| 数字 | 值 | 出处 / 校验方式 |
|---|---|---|
| 快捷入口 | **90 项 / 10 组**（左栏 11 项，含「全部工具」） | `--selftest` 的 INFO 行 + 页脚 `CatalogCaption` |
| 需管理员 / 家庭版缺 / 兼容备注 / 危险提示 | 37 / 7 / 11 / 6 | 同上 |
| 黑名单误传命令 | 17 个 | 自测点名 |
| 自测断言 | **126 条**（2026-10-25 实测），退出码 = 失败数 | `grep -c '^PASS' selftest.txt` |
| 主题令牌 | 38（+ 2 个别名 + 3 个排印键） | 自测段 1 |
| 字号默认 / 区间 | 15 / 12–28 | `Typography` |
| 缩放默认 / 区间 | 100% / 50–200 | `Typography` |
| 窗口默认 / 下限 | 1120×760 / 860×560×系数（钳到工作区） | `SettingsService`、`MainWindow.xaml` |
| 切分组耗时（右栏常驻后） | 再次进入 3–25 ms；首次进入 56–102 ms；改前 181–328 ms | `doc` §7.3 |
| 构建警告 | 固定 **9 条**（CS0414×5、CS1574×4，全来自 `lib/`） | `dotnet build --no-incremental` |
| 产物 | `1,679,030 B`（框架依赖）/ `70,291,809 B`（自带运行时），各**只有一个 exe** | `publish*.cmd` 输出 |
| 版本 | app `0.1.0`（→ `FileVersion 0.1.0.0`）；lib `3.0.0` | csproj + 发布守门比对 |
| `lib/` 偏离上游 | **2 个文件、4 处改动**（`UI4NavigationView.cs`、`UI4GridView.cs`），种子未回灌 | `doc` §7.1 的 `diff -rq` |
| 设置文件 | `%APPDATA%\QuickPanel\settings.json`，kv1，9 个键 | `SettingsService` |

---

## 11. 收藏方案：架构、实现与顺序落盘

> 本节记录当前收藏功能（加星 / 已收藏标签 / 顶部提示 / 顺序落盘）的架构与实现；
> 拖动排序单独记在第 12 章。
> **记录时间：2026-10-25 18:38:07（本机时间）**。改收藏相关代码时先对照本节，尤其是 §11.4 的"顺序三条链"。

### 11.1 设计边界

| 决定 | 当前做法 | 原因 |
|---|---|---|
| 存储 | 与其它设置共用 `settings.json`（kv1），不新建文件 | 备份与迁移只有一处；现有的原子写、超限隔离、诊断日志直接复用 |
| 键格式 | 一项一个键 `fav0`、`fav1`…，**数字后缀就是位次** | kv1 不做转义；逗号拼成一条会撞上 `rundll32.exe shell32.dll,Options_RunDLL 7` 这类自带逗号的命令 |
| 卡片数据 | 收藏只是同一批 `ToolItem` 上的两个镜像字段，不复制卡片 | 收藏页与其它页共享引用；标签、星标、位次改一处两处同时正确 |
| 收藏页顺序 | 独立的有序 `ObservableCollection<ToolItem>`，视图只保留搜索 Filter | 收藏顺序只存一份；视图不额外排序，避免视图与清单各自维护一套顺序 |
| 写入时机 | 点星立即落盘 | 收藏是用户主动动作，不该等关窗；崩了不该丢 |

### 11.2 四层职责

| 层 | 位置 / 类型 | 职责 | 明确不做 |
|---|---|---|---|
| 真源 | `SettingsService.Favorites`（`List<string>`，命令原文、有序） | 收藏成员与顺序的唯一写入口 | 不碰 UI、不碰卡片对象 |
| 持久 | `settings.json` 的 `fav<n>=<command>` | 跨进程保存；序号即顺序 | 不解释视图、不推断显示 |
| 卡片镜像 | `ToolItem.IsFavorite` / `ToolItem.FavoriteRank` | 星标与「已收藏」胶囊读 `IsFavorite`；收藏页重建时按 `FavoriteRank` 排 | 不保存收藏清单 |
| 收藏视图 | `ToolSection(favoriteOnly)` 的 `ObservableCollection<ToolItem>` + `ListCollectionView` | 只装收藏项、按真源顺序排列；搜索时只做 Filter | 不改真源、不直接读写文件 |
| 交互 | `MainWindow` 的右下角星标；`MainViewModel` 的 `ToggleFavoriteCommand` | 悬浮显隐、点击、顶部提示 | 不自己维护第二份收藏清单 |

`FavoriteRank = int.MaxValue` 表示未收藏。收藏项之间的位次唯一，就是它在真源列表里的下标。

### 11.3 配置文件契约

路径：`%APPDATA%\QuickPanel\settings.json`。主题、字号、窗口几何等 9 个固定键之外，收藏项另占 `fav0..favN-1`。

```ini
# 示意（kv1：一行一个 key=value，值原样、不转义）
themeKey=dark
fav0=sysdm.cpl
fav1=appwiz.cpl
fav2=rundll32.exe shell32.dll,Options_RunDLL 7
```

| 规则 | 行为 |
|---|---|
| 写侧 | `ToMap()` 按 `Favorites` 当前下标逐项写 `fav0..favN-1`；删除一项后整表重排，文件里**不留空号** |
| 读侧 | 只认 `fav<非负整数>`；按**数字后缀**排序而不是文件行序；丢空值；`OrdinalIgnoreCase` 去重；最多 200 条 |
| 容错 | 非法键、负数/越界序号、无法识别的行静默忽略，手改文件不影响启动 |
| 空 map | 文件里完全没有 `fav*` 时保留内存现值（`HasFavoriteKeys` 守卫），不会把收藏清空 |
| 上限 | `MaxFavorites = 200`；超限时"加星"返回未加入，卡片镜像不会被置为已收藏 |
| 未知命令 | 设置里仍保留原文；收藏页只显示目录里真实存在的卡片（`SyncFavorites` 只给目录项打位次） |
| 命令去重 | 大小写不敏感；手改文件出现重复命令时保留序号最小的一条 |

**读取一次的顺序链**（应用启动）：

```text
settings.json
  └─ SettingsService.Load / ApplyMap
       └─ ReadFavorites：按 fav 后缀排序、去重、丢空、截断
            └─ ToolCatalog.SyncFavorites：给每个目录项写 IsFavorite 与 FavoriteRank
                 └─ ToolSection.NotifyFavoritesChanged：按 FavoriteRank 重建 _favoriteItems
                      └─ 收藏页按 _favoriteItems 的顺序显示卡片
```

**写出一条的顺序链**（点星）：

```text
真源 Favorites 的当前下标
  └─ ToMap：fav0..favN-1
       └─ Save 原子写 settings.json
```

### 11.4 顺序的唯一事实链

用户能看到的顺序，本质只有一条链，三处必须始终相同：

1. **收藏页卡片的显示顺序** = `_favoriteItems` 的枚举顺序；
2. **真源顺序** = `SettingsService.Favorites` 的下标；
3. **文件顺序** = `settings.json` 中 `fav<数字后缀>` 的数值顺序。

各操作对这条链的影响：

| 操作 | 对真源 | 对文件 | 对收藏视图 |
|---|---|---|---|
| 加星 | `Favorites.Add(command)`，落到**末尾** | 紧接着的 `Save()` 写出 `fav<Count-1>` | 重建集合，新项排最后 |
| 取消星 | `RemoveAt(index)`，后面的项整体前移 | 紧接着的 `Save()` 重写 `fav0..N-1`，无空号 | 重建集合，去掉该项 |
| 手改文件 | 不改内存 | 用户直接编辑 | 下次启动按数字后缀还原 |

新增与取消是"成员变化"，收藏视图重建一次即可；保持顺序不变的增删不会让其它节的卡片顺序变化。拖动排序只改下标，其手势、动画与落盘时序见 §12.6。

### 11.5 添加 / 移除的完整通路

1. 鼠标悬浮卡片 → 容器的 `IsMouseOver` 触发右下角星标显形；空心星 `E734`（添加）与实心星 `E735`（移除）互斥显示，都带 UIA 名字。
2. 点星走 `ToggleFavoriteCommand`；按钮会吃掉左键，**不会**连带执行卡片。
3. `MainViewModel.ToggleFavorite(item)` → `SettingsService.ToggleFavorite(command)`：改真源，返回"切换后是否在收藏中"。
4. `SyncFavoriteViews()`：`ToolCatalog.SyncFavorites(Favorites)` 重写所有卡片的 `IsFavorite` / `FavoriteRank`；收藏节 `NotifyFavoritesChanged()` 按新位次重建 `_favoriteItems`。
5. `Save()` 立即落盘；顶部提示条给出「已添加至收藏 / 已从收藏移除」，同时写入页脚留痕，3 秒后自动收起。
6. 非收藏节不参与重建：`NotifyFavoritesChanged` 对它们本来就是空操作。

### 11.6 关键判据与当前实测

| 判据 | 覆盖点 |
|---|---|
| 文件契约 | `fav0` 起连续编号；往返保持顺序与内容；手改表按序号回正、去重、丢空、截断；null/空 map 不清收藏 |
| 真源语义 | `ToggleFavorite` 返回切换后状态；收藏清单往返与手改容错 |
| 收藏节视图 | 只放已收藏项；搜索叠加时分母按收藏数算；位次排序；空态文案 |
| 交互入口 | 两颗星互斥且都有 UIA 名字；点星不触发卡片执行 |

**截至本节记录时间（2026-10-25 18:38:07）的实测**：`QuickPanel.exe --selftest` 退出码 **0**，报告 **126 PASS / 0 FAIL**（报告写在 `%APPDATA%\QuickPanel\selftest.txt`）。

---

## 12. 收藏拖动排序：需求分析与实现方案

> **记录时间：2026-10-25 18:42:35（本机时间）**
> 本节只谈拖动排序；收藏的存储契约、增删通路与"顺序三条链"见第 11 章。

### 12.1 需求分析

| 编号 | 需求 | 可验收的状态 |
|---|---|---|
| R1 | 只在收藏页提供排序手柄 | 收藏页卡片右上角有「拖动排序」标记；其它分组不显示 |
| R2 | 搜索态不提供排序 | 搜索框非空时手柄隐藏，网格拒绝开拖 |
| R3 | 按住手柄可拖动，卡片跟手 | 指针移动时被拖卡片贴着指针走，原位置空出 |
| R4 | 其余卡片要有让位动画 | 跨越位次时，受影响的卡片用位移动画滑到新槽位，不瞬移 |
| R5 | 可落在列表的任意位置 | 首格之前、末格之后、行间空隙、滚动后的位置都能落 |
| R6 | 松手才提交 | 拖动中真源与文件不变；松手才改顺序并落盘 |
| R7 | 取消不产生副作用 | Esc / 捕获被系统抢走时回到原顺序，不写盘、不弹提示 |
| R8 | 拖动不能误执行工具 | 按住或点击手柄都不触发卡片执行 |
| R9 | 不破坏既有交互与库约束 | 悬浮星标、HoverScale、悬停标签照旧；不改 `lib/` |

验收主判据：松手后 `settings.json` 的 fav 键顺序 = 收藏页显示顺序 = 真源下标（§11.4）。

### 12.2 约束与难点

| 约束 | 对实现的影响 |
|---|---|
| `lib/UI4GridView` 在容器（`ListBoxItem`）的 `RenderTransform` 上跑 HoverScale | 位移不能挂容器，必须挂模板内的 `PART_ItemRoot` |
| 收藏视图带 `SortDescription` 时 `ListCollectionView` 会忽略底层 Move | 拖动提交若要 Move，收藏视图必须去掉排序（第 11 章已改） |
| `Refresh()` 是 Reset，会重建卡片容器 | 松手不能 Refresh，否则 FLIP 断帧、页面跳动 |
| 搜索态下视图只是子集 | 拖动必须在搜索为空时禁用，否则"落到第几格"映射不回完整清单 |
| UniformGrid 不虚拟化、卡片高度由内容决定 | 落点不能靠固定步长算，必须从布局系统读真实槽位 |
| 列表可能超出一屏 | 需要在边缘自动滚动，否则"任意位置"不成立 |

### 12.3 方案选型

| 方案 | 结论 | 理由 |
|---|---|---|
| `DragDrop.DoDragDrop` | 不用 | 系统拖影 + drop 握手无法逐帧控制"其余卡片让位"，落点仍要自算 |
| 子元素 MouseDown/Move + 子元素捕获 | 不用 | 命中层或模板内容一变手势就丢；空隙处 `InputHitTest` 找不到卡 |
| 网格 Preview 事件 + 网格自身捕获 | **采用** | 输入从源头可见，不受子元素处理影响；落点用几何而非命中 |
| 拖动中实时改真源（跨一格 Refresh 一次） | 不用 | Reset 重建容器、动画断帧；中途取消还要回滚 |
| 拖动中只做视觉预览，松手提交一次 | **采用** | 真源只有一次写入；取消零成本；动画连续 |
| 落点用"压在哪张卡上" | 不用 | 首格前/末格后/空隙没有卡，手势会失效 |
| 落点用"最近槽位中心" | **采用** | 任意位置都有最近槽位；槽位来自 `LayoutInformation.GetLayoutSlot` |
| `SortDescription + Refresh` 提交 | 不用 | 排序视图忽略 Move；Refresh 会跳 |
| `ObservableCollection.Move` 提交 | **采用** | 发 Move 事件，容器原地挪，不 Reset |

### 12.4 交互状态机

```text
Idle
  │ 按下在标记矩形内（收藏页、搜索为空）
  ▼
Armed（已捕获鼠标，还没抬卡）
  │ 指针移动 > 3px
  ▼
Dragging（卡片跟手、其余卡片让位预览、自动滚动生效）
  │ 松手                        │ Esc / LostMouseCapture
  ▼                             ▼
Drop（提交、落盘、提示）        Cancel（不写盘，归位动画）
  │                             │
  └──────── 收尾动画结束 ───────┘
                 ▼
               Idle
```

- 只在标记上点一下（没有超过 3px）→ 直接回 Idle，不排序、不写盘、不执行卡片。
- 状态由 `_dragItem` 与 `_dragLifted` 两个字段判定；`_dragLifted` 之前不显示跟手位移。

### 12.5 关键实现

#### 12.5.1 手势入口

- 事件：`UI4GridView.PreviewMouseLeftButtonDown / PreviewMouseMove / PreviewMouseLeftButtonUp / LostMouseCapture`。
- 开拖：从 `e.OriginalSource` 找到 `ListBoxItem`，再按 `Tag="FavoriteGrip"` 找到标记；用 `TranslatePoint` 把标记矩形换算到网格坐标，指针落在矩形 **±4px** 内才开拖。这样即使命中的是卡片内容，手柄区域仍然有效。
- 前置条件：`ToolSection.IsReorderable`（收藏节且搜索为空）。
- 捕获：`host.CaptureMouse()`；失败就按普通按下处理，避免变成"执行卡片"。

#### 12.5.2 跟手

- 模板根 `PART_ItemRoot` 上放 `TranslateTransform`；按下时记录 `pointer - 渲染位置` 作为抓取偏移。
- 每次移动：`translation = pointer - grabOffset - 当前槽位左上角`。槽位随自动滚动变化，因此每帧重算，视觉上钉在指针下。
- 容器 `ZIndex=500` 抬到最上层，`Mouse.OverrideCursor = SizeAll`；结束时恢复。

#### 12.5.3 落点与挤位预览

- 槽位：`LayoutInformation.GetLayoutSlot(container)` 给出容器在面板内的布局槽，加上面板到网格的偏移，得到网格坐标系下的真实槽位（含滚动）。
- 落点：对每个槽位算中心到指针的平方距离，取最小；指针先夹到网格边界内。首格前/末格后/行间空隙都会落到最近的槽位。
- 预览顺序：
  - `insert < origin`：原下标 `[insert, origin)` 的卡片右移一格（预览下标 +1）；
  - `insert > origin`：原下标 `(origin, insert]` 的卡片左移一格（预览下标 -1）；
  - 其余不动；被拖卡片不参与位移。
- 动画：受影响卡片从当前槽位到目标槽位的差值用 150ms `CubicEase(EaseOut)` 位移；位次连续变化时新动画从当前值接管。

#### 12.5.4 自动滚动

- 指针在滚动视口上/下边缘 42px 内时，每 45ms 滚动 26px（夹在 `[0, Extent - Viewport]`）。
- 每次滚动后 `UpdateLayout()`，并用最后的指针位置重算槽位与落点——拖到屏幕外的位置也能落。

#### 12.5.5 松手提交与 FLIP 收尾

1. 记录每张卡当前**渲染位置**（含正在跑的位移动画）作为 FLIP 起点。
2. `SettingsService.MoveFavoriteToIndex(dragged, newIndex)`：从真源摘出再插入；越界夹紧；最终位次没变返回 `false`。
3. `ToolCatalog.SyncFavorites(Favorites)` 更新 `FavoriteRank`（`IsFavorite` 不变，不触发标签重建）。
4. 收藏节 `MoveFavoriteItem(oldIndex, newIndex)` → `ObservableCollection.Move` → `ListCollectionView` 以 Move 事件透传 → ListBox 移动容器，**不 Refresh / 不 Reset**。
5. `Save()` + 顶部提示「已调整收藏顺序：「标题」放到第 N 位」。
6. 清拖动态：**先把 `_dragItem` 置空，再释放鼠标捕获**，否则 `ReleaseMouseCapture()` 同步触发的 `LostMouseCapture` 会把这次松手当成取消。
7. `UpdateLayout()` 后，对每张卡：`translation = FLIP 起点 - 最终布局位置`，再从该值动画到 0（180ms）。被拖卡片从指针处滑入新格，其余卡片从预览位置归位。

#### 12.5.6 取消

- `Esc` 或捕获被系统抢走：快照当前渲染位置 → 不提交 → 归位动画；真源与文件一个字都没改。
- 松手时位次没变：不写盘、不弹提示，只做归位动画。

### 12.6 顺序与配置文件的关系（拖动视角）

- 拖动**只改变下标**，不改变收藏成员；松手后 `ToMap()` 按新下标重写 `fav0..favN-1`，文件里没有空号。
- 拖动过程中显示的"让位"只是 `PART_ItemRoot` 的位移，真源、`FavoriteRank` 和 `settings.json` 都不动；因此 Esc / 丢捕获可以无成本取消。
- 重启后按第 11 章的读取链还原：`fav` 后缀排序 → `FavoriteRank` → `_favoriteItems`，得到与松手时相同的顺序。

### 12.7 边界与异常

| 场景 | 行为 |
|---|---|
| 收藏为空 / 只有一张 | 0 张时没有卡片；1 张时手柄显示，但拖动不会产生顺序变化 |
| 搜索框非空 | 手柄隐藏，网格按 `IsReorderable=false` 直接拒绝开拖 |
| 普通点击手柄 | 不进入 Dragging，不排序、不写盘、不执行卡片 |
| 拖到列表外 | 指针先夹到网格边界，落点仍是最近的槽位 |
| 长列表 | 边缘自动滚动；槽位随滚动重算 |
| Alt+Tab / UAC 抢走捕获 | `LostMouseCapture` 归位并清状态，不提交 |
| 松手前位次没变 | 归位动画，不写盘、不提示 |
| 视图未跟上 Move（异常） | `CommitFavoriteReorder` 退回一次全量重建，至少保证界面与真源一致 |

### 12.8 判据与实测

- 结构判据：`IsReorderable` 只在收藏页且无搜索时为真（自测覆盖）；卡片模板中存在带 UIA 名字「拖动排序」的标记（自测覆盖模板）。
- 行为判据：松手后收藏页顺序 = `settings.json` 的 fav 顺序；拖动不执行工具；Esc 不写盘（按此设计，视觉与拖拽手感需人工确认）。
- 截至本次记录（2026-10-25 18:42:35）：`--selftest` 退出码 0、**126 PASS / 0 FAIL**；本轮未跑 UIA/E2E 拖动，动画观感请以手动测试为准。

