# WordStudy（单词学习）零基础上手

> 面向从没碰过这个工程的人。每条命令都在本机（Windows 10.0.26300 / x64）跑过，输出原样贴在下面；
> 没跑过的行会显式标「未验证」，不写想象中的命令。
> 版本口径：`package.json` version `1.0.0`，`productName` `WordStudy`，窗口标题「单词学习」。

## 0. 前置

- **Node**：`C:\Program Files\nodejs\node.exe`，实测 `v24.21.0`。构建脚本用全路径定位它，不依赖当前终端的 PATH 快照
  （`bin/electron-win-build.ps1` 里 `-NodeDir` 默认就是这个目录，找不到再回落到 `Get-Command node.exe`）。
- **npm registry**：`https://registry.npmjs.org` 可达（实测 200）。依赖已全部落在 `devDependencies`，
  `npm ls --omit=dev --depth=0` 实测输出 `└── (empty)`——交付物运行时不需要任何 `node_modules` 包。
- **二进制下载源**：默认注入镜像，不注入才走 GitHub 官方源。
  - `ELECTRON_MIRROR=https://npmmirror.com/mirrors/electron/`（Electron 运行时 zip）
  - `ELECTRON_BUILDER_BINARIES_MIRROR=https://npmmirror.com/mirrors/electron-builder-binaries/`（nsis / 7zip 等，只在打包阶段才失败，最容易漏）
  - 为什么默认注入而不是先探测：实测 `HEAD https://github.com/electron/electron/releases` 返回 200，
    而 electron-builder 去 GET release 对象时 `connect ETIMEDOUT`——HEAD 与 GET 打的是不同主机，探测证不住下载。
    要显式走官方源：`bin/electron-win-build.ps1 -NoMirror`。
- **数据源**：`resources/` 下 7 个 JSON 已随工程固化（`junior/senior/cet4/cet6/kaoyan/toefl/sat.json` + `sources.json`）。
  外部原始目录是 `../英文单词数据源/`，文件名口径 `N-模块名-顺序.json`。
  **换数据源不要手改 manifest**：跑 `npm run sync-data` —— 它按文件名字典映射模块码、原样字节拷进 `resources/`、
  重写 `sources.json` 的条目数/词组数/去重数/sha。`npm run sync-data:check` 只比对不写，种子过期就 exit 1（实测两边都验过）。
  新增模块要在 `bin/sync-data.mjs` 的 `NAME_TO_CODE` 登记，并在 `tokens.css`（明暗各一行）与 `app.css` 的 `.chip[data-module=…]` 各加一行；
  没登记的 code 会退化成中性灰 chip（看得见但没配色），不会变成看不见颜色的文字。
  映射链路、新增词库清单、"包里装的是种子 JSON 而不是 `.db`"、`%APPDATA%\WordStudy\words.db` 是首启现建的、
  以及"bump `DATA_VERSION` 会连 `study_records` 一起 drop"这个风险，见 `doc/Json源文件与数据库文件与打包发布.md`。
- **仓库里不该提交的**：`node_modules/`、`dist/`、`dist-electron/`、`release/`、`smoke-out.txt` 一类临时输出（已在 `.gitignore`）。

### 运行时数据与检索口径（改动前先读这段）

| 项 | 实测值 |
| --- | --- |
| 源文件 | 7 个模块（1-初中 … 7-SAT，共 52MB） |
| 源条目 | 34,169 条（junior 1,990 / senior 3,752 / cet4 4,543 / cet6 3,992 / kaoyan 5,056 / toefl 10,377 / sat 4,459） |
| 去重后唯一单词 | 14,618（7,808 个词跨模块重复） |
| 词组入库行数 | 185,942 = 源 190,926 − 同一 (词, 模块) 内文本重复 4,984 条 |
| 首次导入耗时 | rows+txn 932ms + FTS rebuild 752ms = 总 1,685ms（`dataVersion=1.1.0` 这轮） |
| 检索延迟 | 英文前缀 2 行/9ms、中文子串 23 行/7ms、词组 60 行/31ms、详情 1ms、抽样 20 词（闸门 ≤500ms 由用户指定） |

- 数据库落在 `%APPDATA%\<userData>\words.db`（WAL 模式），**不在**工程目录里。
- 全文检索用 Electron 自带的 `node:sqlite`（实测 Electron 44.5.1 内嵌 Node 24.21.0 / SQLite 3.53.4，
  `pragma_compile_options` 里有 `ENABLE_FTS5`）。**没有原生依赖，不需要 rebuild，也不会去下载预编译包。**
- 设计方案里的 `simple` 分词器不可用：实测 `db.loadExtension()` 抛 `extension loading is not allowed`（Electron 禁载扩展），
  所以 `words_fts` / `phrases_fts` 用 `tokenize='unicode61'`。后果是中文按标点切词，`放弃` 能命中、单字 `弃` 不命中；
  因此**含中文的查询直接走 LIKE 子串**，英文走 FTS5 前缀，FTS 零命中或语法不接受时回落 LIKE。14,618 行全表 LIKE 实测 7–9ms。
- 数据口径变了要 bump `electron/db.ts` 的 `DATA_VERSION`：版本不一致才 drop+重建+重导入。
  实测过这条路（把 `meta.dataVersion` 手工降级后重启）：
  `dataVersion 1.0.0 -> 1.0.1: rebuild + reimport` / `reimport ms=906` / 行数一致。
  本轮 1.0.1 → 1.1.0 走的是同一条路（7 模块重导入，`db-ready` 后 `words=14618 phrases=185942`）。
- **入库账目**（`data-source-accounting` 判据）：`sources.json` 的 `totals.files / uniqueWords / phrasesUnique`
  必须与库里的 `modules / words / phrases` 完全相等，且 `phrases − phrasesDup == phrasesUnique`。
  这条是防"导入静默少收"的：上一版少收 75 条词组、这一轮源里 190,926 条只入库 185,942 条，
  都靠这本账说清楚差额去哪了（4,984 条是同词同模块的重复文本，导入器按设计丢掉）。
- **库表结构本身**（6 张业务表 + 2 张 FTS5 虚拟表 + 8 张影子表 + 11 个索引的字段语义、关系图、数据流转、
  逐表磁盘占用、11 条热点 SQL 的执行计划、已知边界）见 `doc/数据库表结构与数据流转设计.md`。
  那份文档里的数字全部由 `node bin/db-report.mjs` 生成（默认建临时库出报告、跑完删；`--db <path>` 只读查现有库）。

## 1. 项目启动（首次构建）

    powershell.exe -NoProfile -ExecutionPolicy Bypass -File bin\electron-win-build.ps1 -Target dir

判据：末尾出现 `==> done`，且 `release\win-unpacked\WordStudy.exe` 存在。实测（`-SkipInstall`，依赖已在）：

    ==> artifacts
            234.4 MB  win-unpacked\WordStudy.exe
                0 MB  builder-debug.yml
            393.7 MB  [installed directory] win-unpacked
    ==> done

`-Target dir` 只出 `win-unpacked/`，不会去碰 `release/` 里上轮的 portable/setup——所以验收三口径前先删空 `release/`。

首次构建会多两步，都属正常：

1. `==> installing dependencies`（`node_modules` 不存在时）；
2. `==> Electron binary missing, fetching it`——新版 Electron 的 `node_modules/electron/package.json` 里
   **没有 `scripts` 字段**（实测 `scripts null`、`bin` 里有 `install-electron`），所以 `npm install` 退出码 0 时
   `node_modules/electron/dist/` 是空的。脚本因此走 `npm run ensure-electron`（等价 `install-electron`）。
   就绪的唯一判据是文件存在，实测 `node_modules\electron\dist\electron.exe` = 245,726,208 字节；
   不要用 install 的退出码代替这个检查。

只想跑构建不打包：`npm run build`（Vite 出 `dist/`，tsc 出 `dist-electron/`）。
只查类型：`npm run typecheck`，实测两侧都 0 错误（渲染层 `tsconfig.json` + 主进程 `electron/tsconfig.json`）。

## 2. 本地调试

    npm run dev

判据（实测输出）：

    vite dev server: http://localhost:5173/
    db-ready ms=1696 words=14618 phrases=185942 dataVersion=1.1.0
    ready-to-show ms=1711
    title=单词学习

- `bin/dev.mjs` 自己拉 Vite + spawn Electron，并把 `VITE_DEV_SERVER_URL` 注进子进程；改渲染层代码热重载。
- 主进程与 preload 走 CJS（`electron/tsconfig.json` 用 `module/moduleResolution: node16`，
  因根 `package.json` 无 `"type":"module"` 而产出 `dist-electron/*.js` 是 CJS）。
  注意：**TypeScript 7 已移除 `moduleResolution: "node"`**，写 `node` 会直接报 `TS5108`。
- 用 `Start-Process` 起 node 时，带空格的路径要自己包一层双引号，否则报 `Cannot find module 'C:\Program'`。
- 窗口内所有界面都能真点：主页热力图/统计、学习页翻转卡与评分、查询页搜索与详情。
  自证脚本会逐视图点导航并断言挂载（见第 3 节 `view-*` 三项）。

## 3. 打包与启动

    powershell.exe -NoProfile -ExecutionPolicy Bypass -File bin\electron-win-build.ps1 -Target all

只想双击出包（不开终端）时用这两个脚本，各自只构建一个目标：

    bin\build-portable.cmd    →  release\WordStudy_portable.exe
    bin\build-setup.cmd       →  release\WordStudy_setup.exe

脚本内部顺序是：定位 node（PATH 优先，回落 `C:\Program Files\nodejs`）→ 注入 `ELECTRON_MIRROR`
与 `ELECTRON_BUILDER_BINARIES_MIRROR`（不想走镜像就删那两行）→ `npm run build` →
`node_modules\.bin\electron-builder.cmd --win <portable|nsis>` → 打印产物绝对路径 → `pause`
（双击开的那扇窗口不会一闪而过，失败时 `[FAIL]` 与工具输出都留在屏上）。
两条都实测过，各自 `[OK] finished.`。注意单目标跑出的 setup 约 115MB，比 `-Target all` 的 99MB 大一圈，
内容一致、都能装；要两边严格一致得单独查压缩口径（见第五轮那条记录）。

三口径的语义差别（对比体积/启动时间前先对齐这一栏）：

| 口径 | 形态 | 代价 |
| --- | --- | --- |
| `portable` | 单 exe，拷了就跑 | 每次启动解压到 temp |
| `nsis` | 安装包 | 需要安装动作 |
| `dir` | `win-unpacked/` | 「安装后目录」口径，测启动与内存最公平 |

实测产物（`-Target all`，同一轮构建；`dataVersion=1.1.0`，种子从 4 个 26MB 换成 7 个 52MB）：

    234.4 MB  win-unpacked\WordStudy.exe
    101.9 MB  WordStudy_setup.exe
    101.7 MB  WordStudy_portable.exe
    420 MB    [installed directory] win-unpacked

种子多出 26MB 原始 JSON，装进去只涨了约 2.6MB（asar 里的 JSON 在安装包/便携包里被压缩掉了），
代价体现在首次运行：导入 862–927ms → 1,685ms。

装后目录自证（不靠人眼看弹窗）：

    powershell.exe -NoProfile -ExecutionPolicy Bypass -File bin\smoke.ps1

判据：末行 `smoke=PASS`，且 `marker=PASS ... remaining-processes=0`。实测 39 项断言全绿（对**打包后的 exe** 跑的，2026-10-06 换 7 模块数据源后重跑；`text` / `attrs` 这类长字段与每次随机抽到的 `cardText` 已省略，其余照抄日志）：

    check ipc-roundtrip PASS pong=pong
    check title-kept-by-main PASS renderer-title="单词学习" main="单词学习"
    check default-bounds PASS bounds=1645x1215 want=1645x1215
    check default-view-is-search PASS data-view="search"
    check theme-light PASS main=false renderer=false bg=rgb(246, 248, 251) expect=rgb(246, 248, 251) token=#f6f8fb data-theme=(unset)
    check theme-dark PASS main=true renderer=true bg=rgb(16, 21, 28) expect=rgb(16, 21, 28) token=#10151c data-theme=(unset)
    check theme-bg-flips PASS light=rgb(246, 248, 251) dark=rgb(16, 21, 28)
    check renderer-mounted PASS {"children":1,"shell":true,"topbar":true,"main":true}
    check grid-breakpoints PASS cols@1440=2 cols@1100=2 cols@684=1
    check min-size-enforced PASS after=680x560 min=680x560
    check view-home PASS root-children=1 selector=[data-grid-probe] hit=true
    check layout-fills-home PASS {"h":409,"w":953,"vh":835,"hostH":785,"ratio":0.52} min=0.45/320px
    check no-horizontal-scroll-home PASS widths=1440/1100/900/800/700 clean
    check heatmap-fits-without-tail-cut PASS {"clientW":915,"scrollW":915,"svgW":806,"weeks":27,"lastColBlocks":3,"futureCells":0,"monthLabels":"4月,5月,6月,7月,8月,9月","text":"周一周三周�
    check selection-marker-present PASS {"found":true,"selFound":true,"left":854.5,"right":877.5,"top":273.9,"bottom":296.9,"midY":285.4,"midX":866,"viewportW":1424}
    check selection-stroke-painted-on-all-edges PASS edges={"left":true,"right":true,"top":true,"bottom":true} box=[854.5,273.9,877.5,296.9] raw={"right":[255,140,91],"left":[155,91,61]}
    check home-word-cards-grid PASS {"count":20,"radius":14,"sameRowWidths":true,"fillRight":2,"minColGap":14,"rows":4}
    check stack-blocks-aligned-home PASS {"blocks":3,"leftSpread":0,"rightSpread":0,"minGap":0}
    check theme-audit-home PASS probed=10 problems=0
    check view-search PASS root-children=1 selector=.searchbar input hit=true
    check layout-fills-search PASS {"h":528,"w":681,"vh":835,"hostH":785,"ratio":0.67} min=0.45/320px
    check no-horizontal-scroll-search PASS widths=1440/1100/900/800/700 clean
    check search-phrase-list-behavior PASS {"items":5,"hasBody":true,"worstBlank":17,"minItemW":627,"scrollable":true,"overflow":39,"bodyBottomInView":169,"hoverMovedSelection":false,"hoveredStillActi
    check stack-blocks-aligned-search PASS {"blocks":4,"leftSpread":0,"rightSpread":0,"minGap":0}
    check theme-audit-search PASS probed=11 problems=0
    check view-study PASS root-children=1 selector=.flip-card hit=true
    check layout-fills-study PASS {"h":438,"w":699,"vh":835,"hostH":598,"ratio":0.73} min=0.55/340px
    check no-horizontal-scroll-study PASS widths=1440/1100/900/800/700 clean
    check heatmap-no-overflow-study PASS widths=9 failing=
    check card-hover-stays-in-column PASS widths=1440:699/1336 900:720/796 760:621/656
    check study-date-switch-no-flicker PASS {"placeholderIn":0,"svgOut":0,"landed":"2026-10-05","cardCount":1,"cardText":"soldier"}
    check card-back-reachable PASS flipClicked=true {"shown":"false","blocks":20,"scrollH":2544,"clientH":368,"overflowY":"auto","lastWithin":true}
    check viewing-a-day-creates-no-records PASS {"futureIso":"2026-10-08","attrs":"x,y,width,height,rx,ry,fill,data-date,data-level,style","hasFutureCell":false,"afterFutureClick":0,"activeIsCell":fal
    check stack-blocks-aligned-study PASS {"blocks":4,"leftSpread":0,"rightSpread":0,"minGap":0}
    check theme-audit-study PASS probed=18 problems=0
    check data-layer PASS eng=2/10ms cjk=23/7ms phrases=60/33ms daily=20 idempotent=true today=20/1 browse-a=30/1141
    check data-source-accounting PASS manifest files=7 words=14618 phrases=190926−dup4984=185942 | db modules=7 words=14618 phrases=185942 version=1.1.0
    check font-base PASS rootPx=16px bodyPx=14px
    check renderer-no-errors PASS count=0
    marker=PASS elapsed-ms=35947 SELFTEST-SUMMARY checks=39 pass=39 fail=0 expected=39 remaining-processes=0

设 `SELFTEST_SHOTS=<目录>` 时，每个视图各存亮暗两张（`home.light.png` / `home.dark.png` …），
上面那次就是对 `release\win-unpacked\WordStudy.exe` 跑的，图直接来自交付产物。

### 界面自适应与主题审计（这一轮加的判据）

- 布局：Dock 已移除，`.app-shell` 改成 `height:100vh` 的列容器，`.main-area` 与三视图的栅格都 `flex:1; min-height:0`，
  卡片不再写死尺寸——`react-window` 的行容器与 `FlipCard` 只吃显式像素，所以由 `src/lib/useFillSize.ts` 量容器真实盒尺寸再喂进去。
  判据 `layout-fills-*` 同时要求**比例**与**绝对高度下限**：只比列高的话，宿主本身很小也会"PASS"
  （实测踩过 `260/289=0.9` 这种假绿，`.flashcard-wrap` 里 `min-height:400px` 被同规则末尾的 `min-height:0` 覆盖了）。
  `no-horizontal-scroll-*` 会在出现页面级横向滚动条时，把最靠右的溢出元素直接报出来
  （实测抓到 `tilted-card-figure` 右边界 1432 > 容器 1370：TiltedCard 的 figure 不跟容器宽收缩，得给它固有宽度）。
- `useFillSize` 必须用 **callback ref**：卡区是条件渲染的，用普通 ref 时 `useLayoutEffect` 在元素还不存在时跑完，
  依赖里只有两个数字就再也不会重跑，观察器根本没装上——表现是尺寸永远停在默认值（实测 `.card-slot` 已 1042px 而 `.flip-card` 还是 320px）。
- `theme-audit-*`：逐视图取 9~10 个代表元素（面板、统计卡、chip、进度条、按钮、卡面文字、词组块…），
  在 `themeSource` 亮/暗两挡各取一次"有效背景"（向上找第一个不透明层）与文字色，断言
  ①两挡背景不同 ②有文字的元素对比度 ≥3 ③无文字的图形件（进度条填充）与父层对比 ≥3。
  这条一上就抓到两处真问题：暗色下 `.progress > div` 用 accent 压在 `--bg-inset` 上只有 2.71（新增 `--progress` 令牌，暗色 `#86adff`），
  亮色下 `.card-index` 用 `--text-faint` 只有 2.88（改用 `--text-muted`）。
  注意 `.progress > div` 这类无文字元素不能拿继承来的文字色去比，那会产生 1.91 的误报。
- 学习页闪烁：`study-date-switch-no-flicker` 用 MutationObserver 数"占位符插入次数"与"日历 SVG 被移除次数"，
  实测 `placeholderIn=0 svgOut=0`。旧版每次点日期都会闪一下，成因见下条。
- 悬浮溢出：`FlipCard` 的 `hoverScale` 默认 1.03 且作用在 `transform` 上，卡片按容器算满时一悬浮就超出列宽。
  现在卡片宽度按 `(列宽 - 16) / 1.03` 收，`hoverScale` 显式传同一个常量，两边不会漂移。
  判据 `card-hover-stays-in-column` 在 1440/900/760 三挡窗口各测一次——**只在宽窗口测会空过**
  （列远宽于 720 上限时不等式恒成立），760 挡实测列 690、卡 654，`654×1.03=673.6 ≤ 690` 才算真过。
  另外：`executeJavaScript` 里合成 `pointerenter/pointerover` 触发不了 React 的合成 hover（实测 `grew=1`），
  所以这条判的是几何不变量而不是"hover 后的实际盒"。
- 热力图右端：网格停在今天，不再补齐整周（`futureCells=0`），末列允许 1~7 格。

- `viewing-a-day-creates-no-records`——「学习页点热力图会变绿」的真因**不是样式**：`getDailyWords(date)` 走的是
  `ensureDailySample`，点任意一天都会静默给那天抽 20 个随机词并写 `study_records`。
  取证方式很直白：查真实 userData 的库，18 天里 17 天都是 `n=20 且全部 reviewed_at IS NULL`，
  **还包含四个未来日期**——只有"点一下就抽样"能造出这种分布。
  修法是拆开两个动作：只有"今天"允许自动抽样，其他日期一律只读（`getWordsByDate`），
  要显式生成才点「抽样这一天」；热力图同时屏蔽未来格的点击（`pick()` 里比 ISO 串）。
  断言照机制写：点未来格与把日期选择器改到 2020-01-02 之后，那天的记录数必须仍是 0。
- 「蓝框白框同时存在、移动不同步」：白框是浏览器给**可聚焦格子**画的焦点环。我原先给每个 rect 加了 `tabIndex:0`，
  点未来格时 `pick()` 拒绝改选中（蓝框留在原地），焦点却照样落上去 → 两框脱节。
  现在格子不再逐个可 Tab，未来格直接 `pointer-events: none`（不选也不聚焦），CSS 那条 `rect:focus { outline: none }` 只作兜底。
  代价是日历格子失去键盘直达，选日期走学习页那个日期输入框（断言 `restored=true` 走的就是这条路）。
- **选中框右边线缺失：这条我连着修错两次，教训值得记。**
  第一版用 `outline`（画在盒外，被 SVG 视口裁）→ 换 `stroke` + `[data-heatmap] svg { overflow: visible }` → 仍裁，
  因为库自己的 svg 样式优先级更高，外溢的那半条根本没画。
  更糟的是**当时的判据是瞎的**：`selection-stroke-not-clipped` 比的是盒模型 `rect.right <= wrap.right`，
  实测 `PASS` 的同时像素取色 `raw=[[39,30,23],[39,30,23],[39,30,23]]`（无蓝）——盒模型看不见描边裁切。
  最终改法是不依赖任何溢出语义：**在格子内侧另画一个 inset 描边矩形**（`x+1.5 / width-3`，`fill=none stroke=accent`），
  整条线都落在格子几何内，视口裁不到它。判据同步换成像素级 `selection-stroke-painted-on-all-edges`：
  逐边在框线上取色，四边都要蓝；`raw.right=[255,140,91]` 按 BGRA 解出 `#5b8cff`，正是深色主题 accent，说明取色通路本身可信。
  顺带记两个 API 坑：`electron.d.ts` 把 `nativeImage.getBitmap()` 的返回类型**声明成 `void`**（实测返回 Buffer，BGRA 序）；
  日历格子暴露的属性是 `data-date` / `data-level`，不是 `date=`。
- `heatmap-no-overflow-study`——上一轮的宽度判据只在 home 的 1440 挡测过，那一挡格子被 `maxBlockSize` 夹住，
  恰好掩盖了别处的溢出。现在学习页扫 9 挡宽度（2560/1920/1440/1280/1120/1000/880/760/700），主页那条走 `heatmap-fits-without-tail-cut`，
  三条一起判：容器不横向溢出、末列在容器内、末列在 **SVG 自身视口**内（最后这条是补上的空洞）。
  格子边长也不再纯算：组件的 SVG 宽度对不上 `weeks*(block+margin)`（实测 806 vs 预测 810），
  所以先按余量估一挡，渲染后量真实宽度、超出就收缩（上限 8 次），保证"宁可格子小也不让末列点不着"。

两条界面判据的来历（都是被截图报出来后量清再修的，别照字面理解成"布局裁切"）：

- `heatmap-fits-without-tail-cut`——「主页热力图最右侧被异常截断」的真因**不是容器裁切**（实测 `clientW=908`、`scrollW=908`，压根没横向滚），
  而是窗口右端停在"本周已过的那几天"，末列只有 2 格，看着像被切掉。（当时用 `endOfWeek` 补齐整周解决——**该口径已被后续要求推翻**：现在网格停在今天，`futureCells=0`，末列允许 1~7 格，判据随之改成"不得出现今日之后的格子"。）
  同时组件自己按 14px 那挡算宽度（`svgW=456`）白占 908px 容器，所以加了 ResizeObserver 反推格子边长（主页 26px、学习页 30px）。
  断言盯三件事：不横向溢出、末列 1~7 格且 `futureCells=0`、周数 ≥26。
  **注意 `monthLabels="4月,5月,6月,7月,8月,9月"` 里没有 10月 是库行为不是缺陷**（早期版本这条字段叫 `hasOct`）：
  `getMonthLabels` 用"该周首日所属月份"当刻度，9月 刻度落在 9/27 那一周，
  10月 的首周只隔 1 周，被它的最小间距规则直接不生成元素（实测把窗口右端再多延一周仍然没有，故排除"末列才不给标签"的猜测）。
  日期范围由面板标题 `2026-04-05 → 2026-10-05 · 27 周 · 格子 26px` 兜住。
- `card-back-reachable`——「背面内容显示不全」的成因是 `.flip-card__face` 是 `position:absolute; inset:0` 的定高盒，
  背面那层滚动容器缺 `min-height:0`，flex 子项不收缩被顶出去。修完断言"滚到底时最后一块完整落在可视区内"（`lastWithin=true`）。
  同一轮把切词条从卡内横滑改成按钮 + ← →，因为 FlipCard 只在位移超 slop 时抑制点击翻转（`FlipCard.tsx:232`），
  压不住"卡内横滑"和"拖拽翻面"两个手势抢同一根指针。

### 学习页多挡尺寸审计（本轮）

启动默认值两条，都是**在任何点击与改尺寸之前**读的，否则测的就是自证脚本自己改出来的状态：

- `default-bounds` → `bounds=1645x1215 want=1645x1215`（下限仍是 680×560；**已存档的窗口大小会覆盖默认值**，
  `clampBounds(saved ?? DEFAULT_BOUNDS)` 这条口径按用户要求保留，老机器要看到新默认值得手动拖一次或删 `settings.json`）。
- `default-view-is-search` → `data-view="search"`（首屏落在查询页；`.app-shell` 上的 `data-view` 就是判据落点）。

定向测试只审学习页，不跑全局回归：

    npm run test:study-overflow          # 7 挡宽度 × 正/反两面；4 挡窗高（竖向）+ 最小挡背面滚动 + 3D 倾斜投影；共 14 条判据
    npm run test:all                     # 全局 39 条

启动器 `bin/selftest.mjs` 现在**先跑 `bin/build.mjs` 再拉 Electron**，并带 300s 超时。这条是被坑出来的：
上一轮改完 `electron/main.ts` 直接跑定向测试，跑的是 08:20 那份旧 `dist-electron/main.js`，
判据照打 PASS、进程却永不退出（旧版 `runStudyAudit` 末尾把汇总块粘进了函数自身 → 自递归，日志里 15 层重复 check 行）。
带构建后，同一条命令立刻把 `TS2451: Cannot redeclare block-scoped variable 'b0'` 顶出来，而不是拿旧二进制当证据。
自证卡住时不要用进程名批量收尾：`bin/list-selftest-proc.ps1 -Match .selftest-userdata [-Kill]` 只挑命令行里带自证 userData 的那几个 pid。

`study-components-no-overflow` 的三类判据：比父容器宽、不可滚却内容溢出、被窗口右边裁。**本轮补了竖向**——
只查 `scrollWidth` 的检测器看不见竖向裁切，而学习页真正的病灶全在竖向上：

- `div.panel`（抽样范围工具条）被压成 `h=34 / clientHeight=32 / scrollHeight=48`：视图根 stack 纵向不够分时，
  `.panel { min-height:0 }` 允许它收缩，整行 chip 被裁到面板外——这就是截图里工具条"压不住自己内容"的那条错位。
  改法 `.panel { flex-shrink: 0 }`（`.panel-grow` 的 `flex:1` 在后，仍然能长）。
- 卡片左边缘 183 vs 工具条左边缘 22：`.card-slot { justify-content:center }` 在卡片被 720 上限夹住时留出一条左侧空带，
  空带宽度随窗口变（1440 挡 161px、860 挡 0），看着就是"上下两块没对齐"。改成 `flex-start`，`leftSpread` 七挡全 0。
- 海报（TiltedCard）比卡片低 14px、右移 40px：UA 默认 `figure { margin: 1em 40px }`，而 TiltedCard 的根节点就是 `figure`。
  全局补 `figure { margin: 0 }`；另加 `.flashcard-wrap > .stack-side { align-self: start }`，
  否则侧栏被 grid 拉伸到 520 高、`figure { height:100% }` 又把海报垂直居中，顶边永远对不上（`figTop` 198.6 → 184.6）。
- ≤960 单栏时评分按钮整排消失：`.flashcard-wrap` 的 `min-height:520` + 两个 `.stack` 的 `min-height:0`
  把两行压进 520（实测主列 282、侧列 220），`.rating-row` 落在 y=561 已经在主列盒子（185..467）之外，被 `overflow:hidden` 裁掉。
  单栏挡改成按内容排布（`grid-auto-rows:auto; align-content:start; flex:0 0 auto; min-height:0`），页面纵向交给 `.main-area` 滚。
- `.study-heat { max-height:30vh; overflow:auto }` 的竖向滚动条吃掉 10px，热力图右边缘 1370 对不上工具条 1380，
  末几行还被上下裁。去掉限高后七挡宽度都是 `sw==cw`、`sh==ch`（`cw` 1380/1220/1040/900/800/700/620，`sh` 289~394），`rightSpread=0`。

竖向判据本身也返工过一次：先用 `scrollHeight > clientHeight`，结果卡片装饰阴影（`aria-hidden` 的 `.flip-card__shadow`）
把 `.card-slot` 的滚动高度撑到 428（盒子 408）报假阳性。现在改成量**非 aria-hidden 子元素的底边**是否越过盒子底边，
`div.panel` 这种真裁切仍然报，阴影不再算账。

`study-blocks-edge-aligned-{w}` 是这轮新加的错位判据，四条一起判：工具条/卡片/评分行/热力图左边缘差 ≤2、
工具条与热力图右边缘差 ≤2、海报顶边（并排时）或侧栏左边缘（换栏时，且两栏不得上下重叠）对齐、热力图既不内滚也不被裁。
盒模型溢出判不出"左边空出一条 161px 的带"，所以这条必须单独存在。这一轮收尾时定向 8/8 PASS、全局 36/36 PASS
（下一节又给它加了四条竖向判据，最终 12/12）。

### 卡片高度不跟窗体（第二轮，竖向）

用户第二轮报的两条：顶部"抽样范围"超出背景栏、悬浮时卡片高度显示截断且不随窗体变化。
先把定向审计加了**第二轮竖向扫**（宽度固定 1280，高度 700/820/1000/1180，四挡各存一张 `study-h-<h>.png`），新增判据四条：

- `study-toolbar-chips-inside-bar`——量工具条面板自己框内有没有把每一行 `.row` 包住（`chipsOut` 必须是 0）。
  实测 `panelH=66 chipsOut=0`，**这条在上一轮的 `.panel { flex-shrink: 0 }` 之后就已经是好的**，
  他看到的仍是旧实例（本地跑的旧包），不是没修。
- `study-card-hover-fits-vertically`——卡片按 `hoverScale=1.03` 放大后仍要落在裁切祖先（`.flashcard-wrap > .stack`，`overflow:hidden`）内。
- `study-card-height-tracks-window`——四挡窗高下卡片高度必须**真的变**（单调不降且极差 ≥60px）。

第二条一上就量到病灶：`cardHeights=408→408→408→408 span=0`，而视口从 635 拉到 1115。
成因链很清楚——卡片高 = `Math.max(360, slot.height)`，`slot.height` 来自 `.flashcard-wrap`，
而它写着 `min-height: 520px` 且是 `flex: 1` 的项：视口不够大时它永远停在 520 底线，
卡片于是恒为 `520 - 112（评分行+按钮行+间距）= 408`。**容器被 min-height 钉住时，容器的测量值已经不是视口信号了。**

改法（三处，缺一不可）：

1. `useFillSize` 除宽高外再报 `top`（元素在视口里的顶端）、`below`（同列排在它后面的兄弟总高 + rowGap）、`vh`。
   `vh` 必须进 state：只比 width/height/top/below 的话，纯拖高度可能一个量都不变，观察器白跑一趟。
2. 卡片高度改成 `clamp(300, (vh - top - below - 8) / hoverScale, 620)`——除 `hoverScale` 与宽度那侧对称，
   否则一悬浮就压到下面的评分行（这就是"悬浮显示截断"的另一半）。
3. `.card-slot` 由 `flex:1; min-height:360px` 改成 `flex:0 0 auto; min-height:0`，`.flashcard-wrap` 由 `flex:1; min-height:520px`
   改成 `flex:0 0 auto; min-height:0`。**这一步是踩过才写下来的**：只把 `min-height:520` 去掉而留着 `flex:1`，
   这一栏就变成"视口减掉兄弟"的收缩项，实测 `clipH` 在 635 视口下直接是 0，卡片被 `overflow:hidden` 裁得比原来更狠。

最终一轮：`cardHeights=300→407→582→620 span=320`，四挡全部 `inside=true / hoverFits=true`，`chipsOut=0`；
再加一条最小挡的背面可达性 `study-card-back-reachable-at-min-height`（700 窗高 → 卡 300px，
背面滚动区 `clientH=204` 装 `scrollH=1278` 的 10 块词组，滚到底 `lastWithin=true`）。
定向 12/12 PASS，全局 36/36 PASS（`layout-fills-study` 随窗高变成 `484/596=0.81`）。
截图口径：`study-h-700.png` 里 300px 的卡连同评分行、按钮行整屏可见（改前卡片 408px 被视口切断、按钮看不见）。

### 3D 倾斜把近端甩出窗口（第三轮）

现象：鼠标打到卡片右侧时，卡片**左边**那半截显示不全。成因是 FlipCard 的 `perspective(1100px) rotateY(±12°)`
叠加 `hoverScale=1.03`——指针在右端时左端是**近端**，投影后往外扩，而卡片左边缘离窗口只有 22px。

新判据 `study-card-tilt-stays-in-window`：不猜，真去驱动倾斜与抬升，量**投影后**的实际盒，四挡宽度各测左右两端。
写它的时候踩了两个坑，都值得记：

1. **量的元素错了。** tilt 与 scale 都打在 `.flip-card__rotor` 上，而根盒 `.flip-card` 的
   `getBoundingClientRect()` 不随子元素变换变化——第一版实测 `base=22..742`、两端投影后仍是 `22..742`，
   外扩被量成 0。换成量转子才拿到真实投影。
2. **`lift` 走 React 合成 `onPointerEnter`，光发 `pointerenter` 进不去**（与第 2 轮 `grew=1` 同源）。
   要发 `pointerover` 且**带 `relatedTarget`（指向卡片外的元素）**，React 的 enter/leave 插件才会合成出 enter。
   探针里加了 `lift` 回报值并把它写进判据前置条件（`lift > 1.02` 且转子矩阵确实变了），
   否则"投影没越界"可能是探针空转出来的假绿。

驱动成功后实测：`lift=1.03`，布局盒 `22..742`，指针钉在右边缘时近端投影到 **`−7`**——7px 被窗口裁掉，与他截图一致。
修法落在布局：`.study-stack { padding-inline: 22px }`（与 `.main-area` 的 22px 合计 44px），
整版一起内缩，所以工具条/卡片/评分行的左边缘仍然对齐（`leftSpread=0`，不会退回第 1 轮那条空带）。
改后同一条判据：`base=44..764 → R=15..744`，近端留 15px 余量；现场图 `study-tilt-right-960.png`。

顺带修掉判据自身的两处假阳性（都是被这一轮的 FAIL 逼出来的）：

- 盒模型检测器把 3D 投影当裁切：转子投影比布局盒宽 22px，会把父盒 `scrollWidth` 撑到 742>720 报 `clipX`。
  现在**自己或子层带 `matrix3d` 变换的盒**不再参与 `overW/clipX/clipY`，它们的越界交给倾斜判据用真实投影去量。
- 卡面参照盒取的是未变换的根盒：鼠标恰好停在卡片上时 1.03 缩放会让 `front/back` 与根盒差 22px，
  被误判成"卡面没对齐"。改成与**转子**比（两个卡面与转子同生同灭）。

本轮收尾：定向 `npm run test:study-overflow` **13/13 PASS**，全局 `npm run test:all` **36/36 PASS**。

### 三卡行：主卡居中 + 左右预览卡（第四轮）

需求：主卡居中，左右各一张小卡显示序列里的前一个/后一个词，点侧卡就把那个词换到中间并刷新两侧；
右侧那张与主卡内容重复的海报（TiltedCard）按确认**去掉**。

实现口径（最终形态，含他随后两次的修正）：`.card-row` 是 `align-items: flex-start` + `padding-top: 48px` 的一行，
左右侧卡是**正方形**并推到行的两端（左上角/右上角），主卡居中，三张卡**顶边齐平**——
注意方向：是侧卡下移去齐主卡（整行下移），不是把主卡拉上去齐侧卡。
侧卡边长 `clamp(190, 列宽×0.22, 320)`，主卡宽 `列宽 − 2×边长 − 2×18` 后再除 `hoverScale`；
列宽 < 900（`SIDE_BREAKPOINT`）收起侧卡、主卡回到整列宽。
侧卡是 `<button class="side-card" data-side-card="上一个|下一个">`，**只做预览不做 3D**——三张卡都投影就会互相重叠。
卡片高度算式里 `ROW_DROP` 也要一起扣，否则主卡底部会压到评分行。

判据两条：

- `study-side-cards-navigate`——点右侧卡之后：中间必须等于刚才右侧那个词、左侧必须等于刚才中间那个词、右侧必须是再往后一个。
  实测 `start=coal / p0=extortion / n0=success` → 点一下 → `after=success / p1=coal / n1=gambol`，三件事同时成立才算过。
- `study-blocks-edge-aligned-{w}` 换成三卡行版：工具条/整行/评分行/热力图左边缘差 ≤2，**主卡在行内真居中**
  （卡中心与行中心差 ≤2），侧卡必须是正方形（宽高相等）、贴在行两端、顶边与主卡齐平，
  并且整行确实下移了 `padding-top` 那么多（`padding-top` 从 CSS 现读，判据里不再抄一遍 48，免得两处数字各自漂移）。

踩的坑值得单记：**侧卡最初写成无条件渲染**，只靠 `SideCard` 在 `data` 为空时 `return null`。
于是窄列下三张卡仍在行里，flex 把主卡从 720 挤到 **160px**（`aspect=0.39`），`faces` 判据当场报"卡面异常"。
修法是回到调用点用 `showSides ?` 真门控。教训：**组件自己 return null 不等于没参与布局**——
占位发生在它被渲染的那一次，宽度/显隐这类门控要在算宽度的地方做，别指望子组件兜底。

本轮收尾：定向 **14/14 PASS**（七挡宽度 `centered=true`，≥1100 `sides=2`、≤960 `sides=0`），全局 **36/36 PASS**，
重新打包（18:43）后对打包 exe 跑 `bin\smoke.ps1` → `smoke=PASS checks=36 pass=36 elapsed-ms=32317`。
截图 `.selftest-shots/study-default.png`（1645×1215 下的三卡行）。

### 主页卡片墙与查询页词组列表（第五轮）

**主页底部**。成因还是那句：`AnimatedList` 的 `.scroll-list-container { width: 500px }` 写死，
面板再宽它也只占 500px，右边必然空着。改成自己的网格：`.word-cards` 用
`repeat(auto-fill, minmax(212px, 1fr))`，每格是 `<button class="word-card">`（序号 / 词 / 释义 / 模块 chips），点它跳到查询页该词详情。

这里我自己制造过一个回归，值得记：**第一版把卡片墙做成不限高**，于是它把上面的热力图压成一条、还被自己盖住——
而 `layout-fills-home`、左右对齐、`no-horizontal-scroll` 全绿，**只有截图看得见**。
两处一起修：`.home-grid { min-height: 420px }`（宁可整页滚也不塌），并给 `stack-blocks-aligned-*` 补 `minGap`
（相邻块"后块顶边 − 前块底边"，小于 −2 即判重叠）。三视图现在都是 `minGap=0`。

新判据 `home-word-cards-grid` 实测 `count=20 radius=14 sameRowWidths=true fillRight=2 minColGap=14 rows=4`。
它第一版返回 `null`——隔离库刚建好时当天没有记录，走的是空态分支。改成先点面板自己的「重新抽样这一天」按钮再量，
顺带把那条路径也验了：**判据在空数据上"跳过"等于没判**，宁可主动造数据。

**查询页右侧词组列表**。他报两条：选中边框鼠标移开还在、列表滚不动。根因都在 `AnimatedList`：

- `handleItemMouseEnter` 里直接 `setSelectedIndex(index)`（`AnimatedList.tsx:85`）——**悬停即选中**，
  所以高亮会留在最后悬停过的那项，移开也不消失。这不是样式能治的。
- 容器写死 500px 宽，`.scroll-list { max-height: 400px }` 与面板实际高度脱钩 → 面板被撑高后没有可用的滚动体。

所以这一处换成自己渲染：`.detail-body`（`flex:1; min-height:0; overflow-y:auto`）包住各模块分组，
组内是 `.phrase-item` 按钮；悬停只给描边，点过才留 `is-active`（底色 + 左侧 3px 强调条）。
判据 `search-phrase-list-behavior` 一次验六件事，实测
`worstBlank=17 minItemW=627 scrollable=true overflow=39 bodyBottomInView=169 hoverMovedSelection=false hoveredStillActive=false clickSelected=true activeCount=1`。
代价：`AnimatedList` 在本工程变成零引用（与 Dock / Waves / Carousel / TiltedCard 同列）。

**两个双击构建脚本**：`bin\build-portable.cmd`、`bin\build-setup.cmd`。各自流程是
定位 node（PATH 优先，回落 `C:\Program Files\nodejs`）→ 注入两个镜像变量 → `npm run build` →
`node_modules\.bin\electron-builder.cmd --win <portable|nsis>` → 打印产物绝对路径 → `pause`（双击的窗口不会一闪而过）。
实测两条都打 `[OK] finished.` 并各自产出对应 exe。
一条如实记录：单目标跑出的 setup 约 115MB，而 `npm run dist`（三目标一次跑）的 setup 约 104MB，
内容相同、都能装；我没动压缩参数，这条差异留在这里，需要两边一致再单独查。

本轮收尾：全局 `npm run test:all` **38/38 PASS**，重新打包（20:27）后对打包 exe 跑
`bin\smoke.ps1` → `smoke=PASS checks=38 pass=38 elapsed-ms=35138 remaining-processes=0`。
中途有一次打包冒烟报 `theme-audit-study problems=4` 且 `remaining-processes=4`——
那是跑测期间窗口被人手动拖动（同轮日志里 `sweep[study] client=2434` 对不上设定宽度），
不是代码问题；补上问题字符串明细后复跑为 0。

### 换 7 模块数据源（第六轮）

用户把 `../英文单词数据源/` 换成完整版：7 个文件（编号 1–7），新增 **高中 / CET4 / 托福** 三个模块。
规模变化：条目 15,500 → 34,169、词组 83,899 → 190,926、唯一词 9,986 → 14,618、跨模块重复词 4,261 → 7,808。
（`junior/kaoyan/sat` 三个文件的条目数各比旧 manifest 少 1：新文件数组长度就是 1,990 / 5,056 / 4,459，
`cet6` 没被改动、计数与旧 manifest 完全一致 3,992。）

代码里有四处是按"4 个模块"写死的，换数据源就露出来，逐条改成数据驱动或生成：

| 写死处 | 改法 |
| --- | --- |
| `resources/sources.json` 手抄的 entries | 由 `bin/sync-data.mjs` 生成（含 sha256_16），`--check` 能查过期 |
| `tokens.css` 只有 4 组 `--module-*` | 补齐 7 组（明暗各 7），新色为 senior 玫红 / cet4 青 / toefl 橄榄 |
| `app.css` 只有 4 条 `.chip[data-module=…]` | 补到 7 条，并加 `.chip[data-module]` 中性兜底：没登记的新 code 至少是可见的灰 chip |
| `App.tsx` 顶栏 `初中 · CET6 · 考研 · SAT` | 改成从 `getModules()` 取，顺序跟 `sort_order`；`.brand-sub` 加 `max-width:46vw` + 省略号，7 个名字在 680px 窄窗口也不会挤走右侧导航 |

新增 `npm run sync-data` / `npm run sync-data:check`。脚本按 `N-模块名-顺序.json` 解析，模块名不在
`NAME_TO_CODE` 里就 **直接报错退出**（不静默跳过），种子是原样字节拷贝、不做任何转换。
`--check` 两边都实测过：同步后 `CHECK-OK` 退出码 0；拿一份改过一个字节的源副本去 check，报
`CHECK-FAIL staleSeeds=true` 退出码 1。

`DATA_VERSION` 1.0.1 → **1.1.0**，老库走既有的"版本不符就 drop+重建+重导入"路径。

两条判据为这一轮服务：

- `data-source-accounting`——`sources.json` 的 `files / uniqueWords / phrasesUnique` 必须与库里
  `modules / words / phrases` 完全相等。实测 `manifest files=7 words=14618 phrases=190926−dup4984=185942`
  == `db modules=7 words=14618 phrases=185942`。源与库差的那 4,984 条是同一 (词, 模块) 内的重复文本，
  导入器按设计丢弃——**差额必须能算平，不能靠"看起来差不多"**。
- `theme-audit-study` 的探针从 12 个加到 18 个，把 7 个模块的 chip 配色全纳入对比度审计
  （新模块的颜色只要不够对比度就会在这里被拦），实测两主题下 `problems=0`。

判据自己也有一处与代码不一致被这轮暴露出来：`study-components-no-overflow` 的"卡面不塌陷"下限写的是 340px，
而 `StudyView` 的地板 `CARD_MIN_H` 是 300。7 个模块 chip 让工具条在 680/760 挡换行、控件行变高，
卡片就落到 544×300 与 621×328 —— 报"卡面异常"其实是判据在拿旧地板量新设计。改成两边同为 300（长宽比闸门不变），
不是把布局抬回 340 去迎合判据。

实测数字：首次导入 `rows+txn 932ms + fts-rebuild 752ms = 1,685ms`（旧数据是 862–927ms）；
检索延迟英文 9ms / 中文 7ms / 词组 31ms（闸门 ≤500ms）；`browse a` 段从 873 词涨到 1,141 词。
全局自证 **39/39 PASS**，重新打包（21:55）后对打包 exe 跑 `bin\smoke.ps1`
→ `smoke=PASS checks=39 pass=39 elapsed-ms=35947 remaining-processes=0`。
产物体积：setup 99.3 → 101.9 MiB、portable 99.1 → 101.7 MiB（种子多 26MB，压缩后只涨 2.6MB）。

- `--selftest` 的判据强度：只有「渲染层从 `file://` 加载成功 且 contextBridge 与 IPC 往返都通」才会打 `SELFTEST-OK`。
- 自适应四组断言对应上表的 `theme-*`（主进程 `shouldUseDarkColors` 与渲染层 `matchMedia` 一致 **且背景 token 真翻转**）、
  `grid-breakpoints`（1440/1100/684 三挡列数 2/2/1）、`min-size-enforced`（强设 100×100 后仍 680×560）、
  以及几何纠偏（下面单独跑）。
- 几何纠偏单独验（伪造越界的持久化几何，走正常建窗路径）：

      .\node_modules\electron\dist\electron.exe . --selftest --selftest-geometry

  实测：

      geometry-seed bounds={"x":-24000,"y":-24000,"width":9000,"height":9000}
      bounds-clamped from={...} to={"x":0,"y":0,"width":2560,"height":1392}
      geometry-from-settings bounds={"x":0,"y":0,"width":2560,"height":1392} within-workarea=true
      SELFTEST-OK

- 自证跑在隔离的 userData，不会污染你真实的学习记录。**隔离必须在 `whenReady` 之前做**：
  单例锁与 Chromium 缓存目录都在更早的启动阶段按 `userData` 定死，晚一步设就会和正在运行的正式实例撞锁，
  表现是 `single-instance-lock refused, quitting` 加 `Unable to move the cache: 拒绝访问 (0x5)`（实测踩过）。
  `SELFTEST_USERDATA` 指到固定目录可以复用同一份自证库，做版本门这类实验很方便。
- `smoke-out.txt` 是 UTF-8，PowerShell 5.1 控制台按 ANSI 渲染会把中文显示成乱码——文件内容是对的，用编辑器看。

便携版单独验（stdout 不冒泡，靠标记文件）：

    powershell.exe -NoProfile -ExecutionPolicy Bypass -File bin\smoke-portable.ps1

实测：

    markers=checks-done,probed,window-shown
    window-shown-ms=6806 probed-ms=7598 checks-done-ms=12892
    portable-stdout-bytes=0 (0 is expected: the host does not bubble child stdout)
    remaining-processes=0
    smoke-portable=PASS

即便携 6806ms vs 装后 986ms 到 `ready-to-show`（约 6.9 倍），解压代价在这条口径上。
便携宿主进程在应用自退出后仍会短暂驻留，脚本只按自己记下的 pid 停掉它。

打包期已知的一个环境坑：解包 Electron 后立刻 `rename win-unpacked.tmp -> win-unpacked` 会被实时扫描挡住，报
`EPERM: operation not permitted, rename ...` 或 `EBUSY: ... unlink ...\default_app.asar`。
实测等约一分钟后同一文件 `probe=free`，重跑 `-Target all` 就过了——**先按瞬时锁重试，别急着怀疑工程配置或杀软配置**。

asar 白名单核对（交付物里只能有四族）：

    "C:\Program Files\nodejs\node.exe" -e "console.log(require('@electron/asar').listPackage('release/win-unpacked/resources/app.asar').length)"

实测 24 条条目，全部落在 `\dist\*`、`\dist-electron\*`、`\resources\*`（数据文件，白名单第 4 项）、`\package.json`；
没有 `node_modules` 族。`src/`、`electron/`、`bin/`、`doc/`、`reactbits/` 都不进包。

## 4. 修改标题

真源只有一处：`electron/main.ts` 的 `APP_TITLE` → `new BrowserWindow({ title: APP_TITLE })`，再加一行

    win.on('page-title-updated', (e) => e.preventDefault());

没有它，`index.html` 的 `<title>` 或渲染层的 `document.title` 会盖掉主进程设定。
判据：`bin/smoke.ps1` 输出 `check title-kept-by-main PASS renderer-title="单词学习" main="单词学习"`（实测一致）。

## 5. 修改图标

图标本地生成，不联网、不依赖 ImageMagick/Python：

    node bin\make-icon.mjs --letter=W --out=electron\icon.ico --bg=#2f6fed

实测输出（5×7 点阵落进 11 格逻辑网格，每格 `floor(size/11)` 整数倍像素，所以每挡都是硬边）：

    layer  16px BMP  cell=1px glyph=5x7px bytes=1128
    layer  24px BMP  cell=2px glyph=10x14px bytes=2440
    layer  32px BMP  cell=2px glyph=10x14px bytes=4264
    layer  48px BMP  cell=4px glyph=20x28px bytes=9640
    layer  64px BMP  cell=5px glyph=25x35px bytes=16936
    layer 128px BMP  cell=11px glyph=55x77px bytes=67624
    layer 256px PNG cell=23px glyph=115x161px bytes=1235
    ico -> electron\icon.ico bytes=103385 layers=7 bg=#2f6fed fg=auto(#ffffff)

三条口径：像素规则只写一处（BMP 层与 PNG 层共用同一个 argb 缓冲）；`--letter` 表外字符直接退出码 1
（实测 `!! -Letter 只支持 A-Z / 0-9，收到 "?"`），不画空方块；字母色省略时按背景相对亮度自动选 `#111`/`#fff`。

判据不靠肉眼，而是把 exe 内嵌图标取回来逐像素比：

    powershell.exe -NoProfile -ExecutionPolicy Bypass -File bin\verify-icon.ps1 -Exe release\win-unpacked\WordStudy.exe

实测：

    icon-loaded size=32x32 from WordStudy.exe
    stroke-left PASS rgb(255,255,255)
    stroke-right PASS rgb(255,255,255)
    plate-bg PASS rgb(47,111,237)
    plate-blue-fraction=40.6% (min 40%)
    icon-verify=PASS

`electron-builder.yml` 的 `win.icon: electron/icon.ico` 指的是**文件路径**，写错会静默回落默认图标
（只打一条 `default Icon is used` 告警就继续打包），所以换图标后必须重跑上面这条判据。
想换品牌 logo 才去 selfh.st/icons（`https://cdn.jsdelivr.net/gh/selfhst/icons@main/ico/<slug>.ico`，
slug 先用 GitHub tree API 核对；该库的 ico 只有 16–128 五帧、没有 256，且许可要求署名）——本工程当前未使用。

## 6. 发布 / 分发

    powershell.exe -NoProfile -ExecutionPolicy Bypass -File bin\publish.ps1 -Share <共享目录或 \\server\apps> -DryRun
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File bin\publish.ps1 -Share <同上> -KeepVersions 2

实测（`-Share ./.share-probe -KeepVersions 2`，跑完已删除探针目录）：

    version-ok WordStudy_portable.exe FileVersion=1.0.0
    version-ok WordStudy_setup.exe FileVersion=1.0.0
    ==> destination: .\.share-probe\WordStudy\1.0.0
        copied WordStudy_portable.exe bytes=103891326 sha256=4bc7c0b9c44fc942
        copied WordStudy_setup.exe bytes=104099998 sha256=a56c5939e3ecd47c
    retained-versions=1
    publish=PASS

判据：`<Share>\WordStudy\<version>\` 下两个 exe 加两个 `.sha256`，末尾 `retained-versions=N`。

版本发布流程：改 `package.json` 的 version → 重新打包（`-Target all`）→ publish → 旧版按 `-KeepVersions` 淘汰。
守门比对的是产物的 `VersionInfo.FileVersion`，所以「只 bump 版本号不重打包」会被拒（`!! version gate: ...`），
这正是它的用途——`release/` 里坐着旧构建时不许拷出去。

## 7. 换组件 / 补 ReactBits 种子

ReactBits 组件按 copy-in 用法落进工程：原文固化在 `reactbits/raw/`（种子，不改写），
`bin/fetch-reactbits.mjs` 做确定性转换后写入 `src/components/ui/`。三挡命令：

    npm run reactbits            # 默认：本地命中就零联网，缺文件才回源取并写回 raw/
    npm run reactbits:offline    # 禁网：有缺口退出码 1 并列出缺口名（CI 口径，也是「raw/ 真能替代仓库」的判据）
    npm run reactbits:fetch      # 只补种子不写产物（补完再跑一次全量）

实测 `npm run reactbits:offline`：`种子：本地命中 29/29，联网取回 0，缺口 0`。
实测 `node bin\fetch-reactbits.mjs --check`：`check: 种子 29 条，漂移 0 条`——它比对 `reactbits/lock.json` 里的逐文件哈希，能抓到种子被改动或丢失。

已落位 16 个组件 / 29 个文件（清单见 `reactbits/manifest.json`）。转换规则也记在 `manifest.json.transforms`，
其中一条是**上游缺陷的绕行**：`Waves.css` 把 `calc(-0.5rem - 50%)` 写成 `calc(var(-0.5rem) - 50%)`，
Vite 8 的 lightningcss 压缩器直接报 `SyntaxError: [lightningcss minify] Unexpected token Dimension`——
构建期失败点看着像工程配置，其实是这一行。规则是「非自定义属性的 `var(X)` 展开回 X」，
`reactbits/raw/` 保持原样以便日后核对上游是否修好。

加/换组件：往 `manifest.json.components` 里加一条（`name` + `category` + `files`），跑 `npm run reactbits`，
产物就出现在 `src/components/ui/`。取源走 `source.hosts` 顺序（实测 `raw.githubusercontent.com` 可用，
jsDelivr 那条路径当时返回 503）。**许可状态见 `reactbits/ATTRIBUTION.md`：该仓库根没有 LICENSE 文件
（实测 `LICENSE` 404，GitHub API 标 `NOASSERTION`），对外分发前需要人工确认条款。**

## 8. 交付前精简

删：`dist/`、`dist-electron/`（`npm run build` 能重建）、`smoke-out.txt`、`build-out.txt` 一类一次性输出、
`release1/`、`.share-probe/`、`.selftest-userdata/`、`.selftest-shots/`（自证产物）。

留（每一项都归得进五类）：

    .gitignore  index.html  package.json  package-lock.json  tsconfig.json  vite.config.mts  electron-builder.yml
    bin/  electron/  src/  resources/  reactbits/  doc/     ← 代码 / 脚本 / 配置 / 数据与种子 / 文档
    node_modules/  release/                                 ← 依赖与产物

两条判据（均实测）：

1. **已打好的包不依赖中间目录**：删掉 `dist/` 与 `dist-electron/` 后再跑 `bin\smoke.ps1`，
   仍 `marker=PASS checks=32 pass=32 fail=0 remaining-processes=0`（两者已在 `resources/app.asar` 里）。
2. **删完能一把重建**：`powershell.exe -NoProfile -ExecutionPolicy Bypass -File bin\electron-win-build.ps1 -Target all -SkipInstall`
   从最小清单重新产出三口径全部产物，`npm run typecheck` 仍为 0 错误。实测（先 `rm -rf release dist dist-electron`）：

       234.4 MB  win-unpacked\WordStudy.exe
        99.3 MB  WordStudy_setup.exe
        99.1 MB  WordStudy_portable.exe
       393.9 MB  [installed directory] win-unpacked
       ==> done
