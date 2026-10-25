# JSON 源文件 ↔ 数据库 ↔ 打包发布

> 本文回答四个问题：映射怎么做、新增词库怎么做、发布包里装的是源文件还是库文件、`%APPDATA%` 下那个 `.db` 从哪来。
> 所有数字都是 2026-10-06 在本机实测得到的（`dataVersion=1.1.0`，7 模块数据源），命令与出处写在 §7，可复现。

## 0. 问题原文

1. 现在项目打包时是如何处理 json 源文件与数据表之间的映射的？
2. 如果新增一个新词库，要怎么处理才能将其映射到数据库？
3. 现在是将源文件打包到安装包/便携 exe 中，还是把处理好的有数据的 `.db` 打包进发布版本？
4. 为什么启动后 `%APPDATA%` 下数据目录会有 `.db` 文件，是从发布的包里自动解压出来的吗？

## 1. 结论速览

| 问题 | 答案 |
| --- | --- |
| 映射 | 三层：`bin/sync-data.mjs` 把源文件按 `N-模块名-顺序.json` 解析成 **模块码 + 计数** 写进 `resources/sources.json`；启动时 `seed()` 按这份 manifest 建 `modules` 表；`importData()` 按 manifest 的 `files` 列表读 JSON、按 (词, 模块) 合并去重后写 `words / word_modules / phrases`，最后重建两张 FTS 虚表。**没有任何模块码写死在 SQL 或 UI 里。** |
| 新增词库 | 4 步：文件放进源目录 → 在 `NAME_TO_CODE` 登记模块名 → `npm run sync-data` → **bump `DATA_VERSION`**。再加 2 处配色（`tokens.css` 明暗各一行、`app.css` 一条 chip 规则）。漏掉 bump 是唯一致命的（老用户不会重建库，永远看不到新词库）。 |
| 打包内容 | **打包的是 JSON 源文件，不是 `.db`**。实测 `app.asar` 里有 `\resources\*.json` 共 8 个文件，整个 `release/` 目录里 `*.db` / `*.sqlite*` 命中数为 0。 |
| `%APPDATA%` 的 `.db` | **不是解压出来的**，是应用第一次启动时自己建库+导入出来的：`whenReady` 里 `ensureDatabase(path.join(userData, 'words.db'), resourcesDir())`。`userData` 由 `productName: WordStudy` 决定，所以落在 `%APPDATA%\WordStudy\words.db`。 |

## 2. 映射链路：一条数据从 JSON 走到表

```
../英文单词数据源/N-模块名-顺序.json     （你维护的原始数据，7 个文件 52MB）
        │  npm run sync-data（bin/sync-data.mjs）
        │   · 文件名正则 ^(\d+)-(.+)-(顺序|乱序)\.json$
        │   · 模块名查 NAME_TO_CODE → 模块码（初中→junior、托福→toefl…）
        │   · 原样字节拷贝成 resources/<code>.json（不转换、不裁剪）+ sha256_16
        │   · 统计 entries / phrases / phrasesDup / phrasesUnique / uniqueWords 写进 sources.json
        ▼
resources/sources.json + resources/*.json     ← 这 8 个文件就是"发布物的一部分"，被 asar 收进去
        │  启动时 seed()
        │   · INSERT INTO modules (code, name, sort_order)  ← 顺序 = manifest 的 files 顺序
        ▼
electron/importer.ts  importData()
        │   · 逐文件 JSON.parse → 每条 {word, translations[], phrases[]}
        │   · 按 word.toLowerCase() 合并成一个 MergeRecord（modules[]、phrasesByModule{}）
        │   · meaning = translations 拼成 "type 文本；type 文本"
        │   · 同一 (词, 模块) 内 phrase 文本重复 → 丢弃；不重复 → 追加，sort_order 记顺序
        ▼
words / word_modules / phrases（+ 索引）
        │  INSERT INTO words_fts/phrases_fts VALUES('rebuild')
        ▼
words_fts / phrases_fts（fts5 虚表，content='words'/'phrases'，tokenize='unicode61'）
```

### 字段对应

| JSON | 表.列 | 说明 |
| --- | --- | --- |
| `word` | `words.word`（UNIQUE） | 原样；`word_lower` = trim+小写，用于查与去重 |
| `word` 首字母 | `words.first_letter` | A–Z 字母条用 |
| `translations[].type` + `.translation` | `words.meaning` | 拼成 `vt 离弃， 丢弃； 抛弃`；**顺序敏感**，先到先得（见 §2 对账） |
| — | `words.phonetic` | 恒为 NULL：数据源没有音标字段 |
| 文件名里的模块名 | `modules.code` / `modules.name` | 由 manifest 决定，不是 SQL 里枚举的 |
| `phrases[].phrase` | `phrases.phrase` | 同 (词, 模块) 内文本重复会被丢 |
| `phrases[].usage` / `.example` / `.example_translation` | `phrases.usage` / `.example` / `.example_translation` | 空串归一成 NULL |
| 数组下标 | `phrases.sort_order` | 卡面背面按它排序 |
| — | `word_modules` | 一个词属于哪几个模块（多对多） |

### 对账恒等式（实测全部成立）

| 量 | 源 | 库 | 差 |
| --- | --- | --- | --- |
| 唯一词 | `sources.json` uniqueWords = 14,618 | `COUNT(words)` = 14,618 | 0 |
| 词×模块行 | entries 34,169 − 同文件重复词 35 | `COUNT(word_modules)` = 34,134 | 0 |
| 词组行 | phrases 190,926 − phrasesDup 4,984 = phrasesUnique 185,942 | `COUNT(phrases)` = 185,942 | 0 |
| FTS 行 | 同上 | `words_fts` 14,618 / `phrases_fts` 185,942 | 0 |

`190,926 → 185,942` 这 4,984 条差额**不是丢数据**，是源文件里同一 (词, 模块) 下重复出现的同一句词组文本。
这条账由判据 `data-source-accounting` 守着：manifest 的 `files / uniqueWords / phrasesUnique` 必须与库里
`modules / words / phrases` 完全相等，且 `phrases − phrasesDup == phrasesUnique`。上一版曾静默少收 75 条词组，
就是靠这本账发现的——所以差额必须能算平，不接受"差不多"。

## 3. 新增一个词库（完整清单，按顺序做）

假设要加「雅思」：源文件 `8-雅思-顺序.json`。

1. **文件命名**要满足 `^(\d+)-(.+)-(顺序|乱序)\.json$`。前缀数字决定 `sort_order`（界面上模块的出现顺序）。
2. **登记模块码**：`bin/sync-data.mjs` 的 `NAME_TO_CODE` 加 `雅思: 'ielts'`。
   忘了会怎样：脚本打印 `UNKNOWN-FILES 8-雅思-顺序.json（模块名「雅思」不在 NAME_TO_CODE 里）` 并 **exit 2**，
   不会静默跳过——这是故意的，静默跳过等于新词库悄悄不进包。
   （实测：拿 `8-GRE考试-顺序.json` 试，输出正是这条、退出码 2；`9-初中-乱序.json` 这种"乱序"后缀也认。）
3. **同步**：`npm run sync-data`。产出 `resources/ielts.json` + 重写 `sources.json`（含新计数）。
   之后随时可用 `npm run sync-data:check` 验证种子没过期（不一致 exit 1）。
4. **配色两处**：
   - `src/styles/tokens.css`：`--module-ielts: <亮色>;` 与暗色块里 `--module-ielts: <亮色更亮>;`
   - `src/styles/app.css`：`.chip[data-module='ielts'] { background: color-mix(...); color: var(--module-ielts); }`

   忘了会怎样：`.chip[data-module]` 的中性兜底会接住它（可见的灰 chip，不会变成看不见颜色的文字），
   但配色不统一；而**对比度不会放过你**——`theme-audit-study` 现在把 7 个模块 chip 全列入探针（实测 probed=18），
   新加的第 8 个要记得同步加进 `auditSelectors.study`，否则它不参与审计。
5. **bump `electron/db.ts` 的 `DATA_VERSION`**（`1.1.0` → `1.2.0`），并在旁边的注释里写清口径变了什么。
   忘了会怎样：**这是唯一会静默失败的一步**——版本没变就不重建库，老用户（和你自己机器上已存在的库）
   永远看不到新词库，界面上一个模块都不会多。
6. 重新打包（`npm run dist` 或双击 `bin\build-portable.cmd` / `bin\build-setup.cmd`），跑 `bin\smoke.ps1`。

### 不需要动的地方（已经是数据驱动）

- `modules` 表：由 manifest 生成，SQL 里没有枚举模块码。
- 所有模块 UI：`getModules()`（按 `sort_order`，带进程内缓存）→ 学习页"抽样范围"、查询页模块筛选、卡面 `ModuleChips`。
- 检索与抽样：`m.code IN (?, ?, …)` 按传入码数动态拼，不假设个数。
- 实测 `src/` 里除了 `tokens.css` 的颜色定义，已无硬编码模块码。

### 两个已知边界

- **`sampleModules` 存的是码字符串**（`localStorage` 的 `word-app.sampleModules`），不做存在性校验。
  只加模块没问题；**改名或删模块**会让老用户存的旧码失效——若存的码全都不存在，抽样范围为空集会导致抽不到词，
  表现是学习页显示"这一天没有学习记录"。规避：删改模块码时一并 bump `DATA_VERSION` 并考虑清掉该 localStorage 键。
- **升级会连学习记录一起清空**（重要）：`DATA_VERSION` 不一致时走 `dropAllTables()` → `createTables()` → 重导入，
  而 `DROP` 列表里**包含 `study_records`**。也就是说换数据口径 = 丢历史学习记录与热力图。
  目前只有 `words.db` 落在 `%APPDATA%`，没有导出/迁移通道。要保留历史，得改成"只重建词库三表 + FTS，
  `study_records` 保留并按 `word_lower` 重挂"——那是另一次改动，这里先把风险写明。

## 4. 发布包里装的是什么

实测 `release/win-unpacked/resources/` 只有两样：`app.asar`（54,999,261 B ≈ 52.5 MiB）与 `elevate.exe`。
`app.asar` 内的相关文件（`asar list` 摘录）：

    \package.json
    \resources\cet4.json   \resources\cet6.json   \resources\junior.json
    \resources\kaoyan.json \resources\sat.json    \resources\senior.json
    \resources\toefl.json  \resources\sources.json

- **`.db` 一个都没有**：`find release -name "*.db" -o -name "*.sqlite*"` 输出为空。
- 白名单在 `electron-builder.yml`：`files: [dist/**, dist-electron/**, resources/**, package.json]`，
  `asar: true` 且没有 `asarUnpack` → 种子 JSON 是**读时按需从 asar 里取**，不落盘。
- 体积代价：种子从 4 个 26MB 换成 7 个 52MB，`setup` 99.3 → 101.9 MiB、`portable` 99.1 → 101.7 MiB
  （52MB 的 JSON 在安装包里被压缩掉，只涨 2.6MB）。真正的代价在**首次运行**：导入 862–927ms → 1,644–1,685ms。

### 一个容易踩的重名坑

工程里有两个不同含义的 `resources/`：

| 名字 | 实际位置 | 内容 |
| --- | --- | --- |
| Electron 的 resources 目录 | `release/win-unpacked/resources/` | `app.asar`（Electron 规定的布局） |
| 我们的种子目录 | `app.asar` 内的 `\resources\` | 7 个词库 JSON + `sources.json` |

代码里取的是后者：`resourcesDir() = path.join(__dirname, '..', 'resources')`，`__dirname` 在打包后是
`<asar>\dist-electron`，所以拼出来正好是 asar 内的 `\resources`，Electron 打过补丁的 `fs` 能直接读。
开发态 `__dirname = word-app/dist-electron` → 指向工程里的 `resources/`，同一条表达式两边都对。
**别把它改成 `process.resourcesPath`**——那会指向上表第一行（`win-unpacked/resources`），里面没有 JSON，
首次运行会在 `seed()` 读 manifest 时直接抛错。

## 5. `%APPDATA%` 下的 `.db` 从哪来

不是解压，是**现建的**。链路：

```
app.whenReady()
  ├─ userData = app.getPath('userData')      // 由 package.json 的 productName: "WordStudy" 决定
  │                                          // = C:\Users\<你>\AppData\Roaming\WordStudy
  ├─ settings = new Settings(userData/settings.json)
  └─ ensureDatabase(userData/words.db, resourcesDir())
       ├─ 文件不存在 → createTables() → seed()（读 asar 里的 JSON 导入）→ meta.dataVersion = 1.1.0
       ├─ 文件存在但 meta.dataVersion ≠ DATA_VERSION → dropAllTables() → createTables() → seed()
       └─ 版本相同 → 直接开库（只读 meta，不碰 JSON）
```

实测一次全新导入（写到临时目录，没动你现有的库）：

    first run: create schema + import
    import entries=34169 uniqueWords=14618 phraseRows=185942 rows+txn ms=941 fts-rebuild ms=702 total ms=1644
    info={"words":14618,"phrases":185942,"dataVersion":"1.1.0","importedInMs":1644}
    打开状态 words.db=48.0MiB wal=31.3MiB
    close 之后 words.db=48.0MiB wal存在=false
    重开校验 journal=wal integrity=ok words=14618

要点：

- 库文件稳态约 **48 MiB**（比 asar 里的 52MB JSON 还小：去掉了重复文本、只存必要字段、行式存储）。
- 开了 `PRAGMA journal_mode = WAL` + `synchronous = NORMAL`，所以运行时旁边会有 `words.db-wal` / `words.db-shm`；
  **正常关闭时 WAL 会合并回主库并删除**（实测 close 后 `-wal` 不存在）。看到残留的 `-wal` 通常说明上次是异常退出。
- `integrity_check` 返回 `ok`。
- 学习记录（`study_records`）与词库在**同一个 `.db`** 里，所以 §3 那条"升级会清记录"的风险是真实的。
- 你机器上现在的库：`C:\Users\webtu\AppData\Roaming\WordStudy\words.db`，28,844,032 B（27.5 MiB，20:56 建的）。
  只读打开实测：`words=9986 / word_modules=15485 / phrases=83895 / modules=4 / study_records=0 / dataVersion=1.0.1`
  —— 就是旧的 4 模块库，而且**学习记录是 0 条**。跑一次新包（`1.1.0`）会走"版本不符 → 重建"，
  库涨到约 48 MiB；这次不会丢任何历史记录（正式有学习数据之后再见 §3 的迁移问题）。

### 自证不会污染你的库

`--selftest` 走的是隔离 userData：在 `requestSingleInstanceLock()` **之前**把 `app.setPath('userData', …)`
指到 `SELFTEST_USERDATA`（未设则 `os.tmpdir()/word-app-selftest-<pid>`）。所以自证跑出来的 `words.db`
不在 `%APPDATA%\WordStudy`，也不会和你正开着的正式实例抢锁。

## 6. 什么时候才应该把 `.db` 打进包里

当前方案（发 JSON、首启建库）的代价是一次 ~1.7s 的导入。把建好的 `.db` 打进包里能省掉它，但要清楚换来什么：

| | 现在：发 JSON + 首启建库 | 发预建 `.db` |
| --- | --- | --- |
| 包体积 | asar 52.5 MiB（实测种子 gzip9 后 **9.7 MiB**，压缩比 5.31×） | 库 48.0 MiB，gzip9 后 **20.6 MiB**（压缩比只有 2.33×）→ 安装包要多约 **11 MiB** |
| 首启耗时 | 1.6–1.7s（实测 1,644–1,685ms） | 接近 0（省掉导入与 FTS 重建） |
| 用户数据位置 | 全在 `%APPDATA%`，包体只读 | 库在 asar 内**不可写**，仍要拷到 `%APPDATA%` 才能写 `study_records` → 变成"解压"，正是问题 4 猜的那种实现 |
| 升级 | `DATA_VERSION` 一个开关，重建即可 | 要做增量迁移脚本 + 校验，还要防半迁移状态 |
| 可审计性 | `sources.json` 的计数与库行数能对上账（§2 恒等式） | 库是黑盒，账目判据失效 |
| 平台 | 无原生依赖，单包跨机可用 | SQLite 版本差异要验证（内嵌 3.53.4） |

（体积那行的压缩数字是 `zlib.gzipSync(..., level:9)` 实测，不是估计；安装包用的是另一种压缩器，
但相对关系一致：JSON 比 SQLite 文件好压得多。）

结论：在"首启 1.7s"这个代价没有被用户明确抱怨之前，不值得为它引入迁移与只读库的复杂度。
真要提速，优先做的是把 `seed()` 挪到窗口显示之后异步跑（首屏先渲染，导入完再点亮数据），
而不是把 `.db` 塞进包里。

## 7. 复现本文数字的命令

```bat
:: 1) 换数据源后同步（生成 resources/*.json 与 sources.json 的计数）
npm run sync-data
npm run sync-data:check        :: 只比对，过期 exit 1

:: 2) 看映射结果：manifest 的期望值
node -e "console.log(require('./resources/sources.json').totals)"

:: 3) 首次导入耗时与行数（自证日志里）
npm run test:all               :: 看 import entries=… total ms=… 与 check data-source-accounting

:: 4) 库里真实行数（只读打开，不会写你的库；SQLite 在 Windows 下接受正斜杠路径，省掉转义坑）
node -e "const{DatabaseSync}=require('node:sqlite');const db=new DatabaseSync(process.env.APPDATA+'/WordStudy/words.db',{readOnly:true});for(const t of ['words','word_modules','phrases','modules','study_records'])console.log(t,db.prepare('SELECT COUNT(*) n FROM '+t).get().n);console.log('dataVersion',(db.prepare('SELECT value FROM meta WHERE key=?').get('dataVersion')||{}).value);db.close()"

:: 5) 包里装了什么
node node_modules\@electron\asar\bin\asar.js list release\win-unpacked\resources\app.asar | findstr /i "resources .db"
find release -name "*.db" -o -name "*.sqlite*"      :: 期望：无输出
```
