# IndexApp 零基础上手（T0Level）

> 这份文档面向“从没碰过这个工程的人”。每条命令都给判据；所有实测输出都是本机跑出来的，未跑的行显式写“未验证 + 原因”。
> 本次四挡（无人值守默认，未经用户确认）：明暗 `both` / 分发两档都出 / 只用内置主题档 / 齿轮入口 + 四节全给（另加“索引”节）。
> 实现口径（M2.0）：目录遍历 + 可增量快照 + `FileSystemWatcher` 实时刷新 + 启动后台目录 mtime 对账（无需管理员）；默认索引根 = 所有就绪的固定盘（本机 C:\、D:\、E:\）；MFT/USN 服务模式为 M2.1，详见 `docs/M2升级规划报告.md` 与 `docs/adr/0001-adopt-vs-build.md`。

- 工程根：`C:\Users\webtu\Desktop\一份搜索软件重构项目\开源搜索工具`
- 本次填写人／日期：自动化实施（Qoder）／2026-10-27
- 实测环境：Windows 11 专业工作站版 10.0.26300 + .NET SDK 10.0.401 + Microsoft.WindowsDesktop.App 10.0.12

---

## 0. 前置要求

| 依赖 | 要求 | 判据命令 | 本机实测 |
|---|---|---|---|
| .NET SDK | 10.x（`net10.0-windows`） | `dotnet --list-sdks` | `10.0.401 [C:\Program Files\dotnet\sdk]` |
| WPF 桌面运行时 | `Microsoft.WindowsDesktop.App` 10.x（框架依赖档必需） | `dotnet --list-runtimes \| Select-String WindowsDesktop` | `Microsoft.WindowsDesktop.App 10.0.12 [C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App]` |
| NuGet 源可达 | lib 唯一第三方依赖 `AvalonEdit 6.3.1.120` 要能 restore | `dotnet build app\IndexApp.csproj` 首行无 NU 错误 | `已成功生成；restore 无 NU 错误` |
| Windows 版本 | Win10（LTSC/企业版）或 Win11；Win7/8.1 不支持（net10 的 OS 底线） | `Get-CimInstance Win32_OperatingSystem` | `Microsoft Windows 11 专业工作站版 10.0.26300` |

不该进版本控制的：`app\bin\`、`app\obj\`、`lib\bin\`、`lib\obj\`、`publish\`、`publish_no_runtime\`、`%APPDATA%\IndexApp\`、`%LOCALAPPDATA%\IndexApp\`。`lib/` 是组件库源码整份拷贝，日常不改。

---

## 1. 项目启动（首次构建）

    dotnet build app\IndexApp.csproj --no-incremental

判据：末尾 `已成功生成` + `0 个错误`；警告**固定 9 条**（`CS0414` 未使用字段 ×5、`CS1574` XML 注释 cref 解析不到 ×4），都来自 `lib/` 上游代码。增量构建可能显示 `0 个警告`，要数警告必须 `--no-incremental` 或先 `dotnet clean`。

实测输出（--no-incremental，2026-10-27）：

```text
  正在确定要还原的项目…
  所有项目均是最新的，无法还原。
lib\UI4ScrollViewer.cs(25,22): warning CS0414: 字段“UI4ScrollViewer._isAnimatingVertical”已被赋值，但从未使用过它的值
lib\UI4ProgressRing.cs(230,22): warning CS0414: 字段“UI4ProgressRing._isAnimating”已被赋值，但从未使用过它的值
lib\UI4ScrollViewer.cs(26,22): warning CS0414: 字段“UI4ScrollViewer._isAnimatingHorizontal”已被赋值，但从未使用过它的值
lib\UI4ProgressRing.cs(15,22): warning CS0414: 字段“UI4ProgressRing._isStartupAnimationRunning”已被赋值，但从未使用过它的值
lib\UI4ProgressRing.cs(14,22): warning CS0414: 字段“UI4ProgressRing._isLoaded”已被赋值，但从未使用过它的值
lib\UI4ComboBox.cs(26,62): warning CS1574: XML 注释中有未能解析的 cref 特性“HoverBorderColor”
lib\UI4ComboBox.cs(26,95): warning CS1574: XML 注释中有未能解析的 cref 特性“FocusBorderColor”
lib\UI4MultiLanguage.cs(29,29): warning CS1574: XML 注释中有未能解析的 cref 特性“SetLanguage(string)”
lib\UI4Panel.cs(21,28): warning CS1574: XML 注释中有未能解析的 cref 特性“Title”
  StartUI4Controls -> lib\bin\Debug\net10.0-windows\StartUI4Controls.dll
  IndexCore -> src\IndexCore\bin\Debug\net10.0\IndexCore.dll
  IndexApp -> app\bin\Debug\net10.0-windows\IndexApp.dll

已成功生成。
    9 个警告
    0 个错误
已用时间 00:00:01.88
```

---

## 2. 本地调试

    run.cmd
    # 或：dotnet run --project app\IndexApp.csproj

判据：窗口出现，标题 `IndexApp`；默认底部状态行为 `生效键 paper-grey`；右上角齿轮打开设置浮层（Esc / 点遮罩 /「完成」都能关）；「配色」点深色，整窗与原生标题栏一起变，底部状态行的 `生效键` 从 `paper-grey` 变 `dark`；再点浅色回到 `light`。已知边界：下拉浮层与右键菜单不吃 `LayoutTransform`，缩放档下弹层内容仍按 100% 渲染。

设置落盘在 `%APPDATA%\IndexApp\settings.json`（kv1 纯文本）；索引快照在 `%LOCALAPPDATA%\IndexApp\index.bin`。默认索引根是所有就绪的固定盘；自动化可用 `INDEXAPP_ROOT` / `INDEXAPP_SNAPSHOT` 覆盖。范围按钮只接受索引中已有的目录；新建/被排除目录会提示重建。也可 `IndexApp.exe --path "D:\Apps"` 启动即限定范围。M2.0 运行中新增/删除/重命名文件会自动进入/退出搜索结果；应用关闭期间的文件名变化由启动后台对账补齐，状态栏显示 `实时刷新：开` / `后台校验` / `启动对账`。

无人值守自证（不开窗、退出码 = 失败断言数）：

    $p = Start-Process -FilePath app\bin\Debug\net10.0-windows\IndexApp.exe -ArgumentList '--selftest' -Wait -PassThru
    $p.ExitCode
    Get-Content "$env:APPDATA\IndexApp\selftest.txt"

实测输出：

```text
启动耗时（Start-Process 到 IndexStatus.HelpText=ready，--softrender，默认全盘快照）：1,474–1,745 ms
后台对账完成（IndexStatus.HelpText 离开 reconciling）：2,681–3,204 ms
selftest 退出码：0
报告（70 PASS + 1 INFO）：
PASS  令牌总数为 38  [实际 38]
PASS  light 全令牌可取色
PASS  dark 全令牌可取色
PASS  highcontrast 全令牌可取色
PASS  正文/底色对比度 ≥ 4.5  [15.77:1]
PASS  强调色对白对比度 ≥ 3.0  [4.56:1]
PASS  Mix(w<=0) 返回源色
PASS  Mix(w>=1) 返回目标色
PASS  IsDark 与标题栏同判据
PASS  明暗策略已显式决策（Theme.Policy）  [当前 "both"，要求 both / light-only / dark-only]
PASS  预置套装 × 38 令牌资源键齐全
PASS  字典底色与 Dark() 定义同源  [字典 #FF202026 vs 定义 #FF202026]
PASS  默认主题为灰纸套装  [paper-grey]
PASS  默认开启实时刷新  [True]
PASS  kv1 往返后十项设置不变  [实际 dark / Cambria / 17 / 125 / live=False / 1024x700 @ -1200,40 / True]
PASS  kv1 写侧键名齐全（格式是稳定契约）  [缺 ]
PASS  往返保持值 [a=b=c]  [得到 [a=b=c]，期望 [a=b=c]]
PASS  往返保持值 [C:\Users\me\数据]  [得到 [C:\Users\me\数据]，期望 [C:\Users\me\数据]]
PASS  往返保持值 [#不是注释因为在前头]  [得到 [#不是注释因为在前头]，期望 [#不是注释因为在前头]]
PASS  往返保持值 [  两端空格  ]  [得到 [两端空格]，期望 [两端空格]]
PASS  往返保持值 []  [得到 []，期望 []]
PASS  往返保持值 [Cambria]  [得到 [Cambria]，期望 [Cambria]]
PASS  含 CR/LF 的值被拒绝而不是撑破行格式  [值里带换行会多出一行没有 key= 的垃圾]
PASS  越界字号与缩放被钳回区间  [28 / 50]
PASS  非法值不覆盖已生效值（保留默认）  [windowWidth=980 themeKey=[light]]
PASS  超出合理范围的窗口坐标被拒绝  [windowLeft=NaN]
PASS  null / 空 map 不清现值  [dark / 20]
PASS  库发布 UI4.Font.Size.Base 兜底值  [解析到 15]
PASS  库发布 UI4.Font.Size.Code 兜底值  [解析到 14]
PASS  库发布 UI4.Font.Family 兜底值  [解析到 Segoe UI]
PASS  宿主覆盖的字号在换档后仍解析到宿主值  [期望 20，实际 20]
PASS  撤掉宿主覆盖后回到库默认  [解析到 15]
PASS  发布后 UI4.Font.Size.Base 可解析  [15 vs 15]
PASS  发布后 UI4.Font.Size.Code 可解析  [14 vs 14]
PASS  发布后 App.Font.Size.Caption 可解析  [11 vs 11]
PASS  发布后 App.Font.Size.Small 可解析  [12 vs 12]
PASS  发布后 App.Font.Size.Medium 可解析  [13 vs 13]
PASS  发布后 App.Font.Size.Lead 可解析  [14 vs 14]
PASS  发布后 App.Font.Size.Icon 可解析  [16 vs 16]
PASS  发布后 App.Size.RoundButton 可解析  [28 vs 28]
PASS  发布后 App.Size.Chip 可解析  [24 vs 24]
PASS  半径键是 CornerRadius 而不是 double  [DynamicResource 不做类型转换，挂 double 到 CornerRadius 上会在运行期炸]
PASS  发布后字体族键是 FontFamily
PASS  层级 Caption 在基准 15 下是 11  [实际 11]
PASS  层级 Small 在基准 15 下是 12  [实际 12]
PASS  层级 Medium 在基准 15 下是 13  [实际 13]
PASS  层级 Lead 在基准 15 下是 14  [实际 14]
PASS  层级 Icon 在基准 15 下是 16  [实际 16]
PASS  层级单调（基准 12）  [把层级抹平成同一个基准就是这里会红的来源]
PASS  层级单调（基准 15）  [把层级抹平成同一个基准就是这里会红的来源]
PASS  层级单调（基准 28）  [把层级抹平成同一个基准就是这里会红的来源]
PASS  未知层级名退回基准本身而不是崩
PASS  默认基准下圆钮直径 28  [28]
PASS  默认基准下胶囊高度 24  [24]
PASS  12–28 全区间固定件都装得下字身
PASS  ClampBase 钳上下限并让 NaN/Infinity 回默认  [7→12 33→28]
PASS  ClampZoom 钳 50–200
PASS  空/非法字体名回退出厂栈
PASS  候选表为空时只剩出厂项  [实际 1 项]
PASS  候选表＝出厂项首位 + 去重 + 丢空白 + 其余按序  [Segoe UI, Microsoft YaHei UI, sans-serif | Arial | Cambria | Segoe UI]
PASS  出厂字体栈可当 FontFamily 用
PASS  W:900 在 150% 下折算正确  [实际 1350]
PASS  窗口下限越过工作区时被钳住  [2512 vs 2512]
PASS  参数缺 W:/H: 前缀时回 UnsetValue
PASS  参数里的像素值不是数字时回 UnsetValue
PASS  light-only 策略下不提供深色档  [Policy=light-only 但档位里有 light,dark,system,paper-grey]
PASS  dark-only 策略下不提供浅色档  [Policy=dark-only 但档位里有 light,dark,system,paper-grey]
PASS  至少有一档可选且首档能应用  [档位=light,dark,system,paper-grey]
PASS  设置面板 XAML 可解析
PASS  主窗口 XAML 可解析
INFO 字号阶梯：基准=15 → 胶囊=11 标签=12 次要=13 正文=15 提示=14 图标=16；固定件 基准15 圆钮=28 胶囊=24，基准28 圆钮=44 胶囊=36
```


- `dotnet run` 会占住终端；自动化用 `Start-Process -PassThru` 起 exe、按 pid 读 `MainWindowTitle`，验完 `Stop-Process -Id` **只关自己起的那个 pid**。
- 带空格/中文的路径要用引号包住再传给 `Start-Process -FilePath`。

---

## 3. 打包与启动

两个脚本，产物名靠 `-p:ArtifactLabel=` 分档、互不覆盖；脚本末尾有 `pause`，无人值守用空输入过去：

    '' | .\publish_no_runtime.cmd
    '' | .\publish.cmd

| 档位 | 产物 | 目标机要求 | 本机实测 |
|---|---|---|---|
| 框架依赖 | `publish_no_runtime\IndexApp_v0.2.0_no_runtime_build_20261027200103.exe` | 必须已装 `Microsoft.WindowsDesktop.App` 10.x | `1,758,754 bytes`；selftest 0；30s 空闲私有 240.3 MB |
| 自带运行时 | `publish\IndexApp_v0.2.0_self_contained_build_20261027200113.exe` | 免装 .NET | `70,337,651 bytes`；selftest 0；30s 空闲私有 316.5 MB（超过 300MB 闸门） |

判据：

1. 脚本第 2 步打 `selftest exit code: 0`；非 0 看 `%APPDATA%\IndexApp\selftest.txt`。
2. 产物目录里只有那一个 exe（`DebugType=embedded` 也作用于 lib，没有散落 pdb）。
3. 起**本轮**产物读回窗口标题；拿旧 exe 验本轮改动会假红。

实测输出：

```text
[1/3] publish single-file, FRAMEWORK-DEPENDENT ...
[2/3] self-test of the packaged exe ...
selftest exit code: 0
  publish_no_runtime\IndexApp_v0.2.0_no_runtime_build_20261027200103.exe   1758754 bytes

[1/3] publish single-file, SELF-CONTAINED ...
[2/3] self-test of the packaged exe ...
selftest exit code: 0
  publish\IndexApp_v0.2.0_self_contained_build_20261027200113.exe   70337651 bytes

MainWindowTitle 回读（自包含单文件）：IndexApp
冷启动到首屏（自包含单文件，默认全盘快照）：1,605 ms；框架依赖：1,476 ms
```

---

## 4. 修改标题

真源只有一处：`app\MainWindow.xaml` 的 `Title="…"`。

    Select-String -Path app\MainWindow.xaml -Pattern 'Title="'

判据：改完重新构建并起进程，`MainWindowTitle` 回读为新值。不要自绘标题栏：窗口里有 UI4 控件，库会在 `Loaded` 时按主题染原生标题栏。

实测输出：

```text
app\MainWindow.xaml:6:        Title="IndexApp" Width="1280" Height="780"
MainWindowTitle（自包含单文件）：IndexApp
```

---

## 5. 修改图标

一个文件三处引用：`app\AppIcon.ico` → csproj 的 `<ApplicationIcon>`（exe 内嵌）＋ `<Resource>`（窗口图标）。默认图标由本机 Skill 的 `make-icon.ps1` 生成；换字母/配色重跑覆盖即可：

    powershell -NoProfile -ExecutionPolicy Bypass -File "C:\Users\webtu\.qoder-cn\skills\startui4-wpf\scripts\make-icon.ps1" `
      -Name IndexApp -Out app\AppIcon.ico -Back "#4F6BE8" -Fore "#F7F9FB"

实测输出（脚手架生成 + ExtractAssociatedIcon 回读）：

```text
probe 16 -> 16x16  corner.alpha=0  center=#F7F9FB glyph
probe 32 -> 32x32  corner.alpha=0  center=#F7F9FB glyph
OK AppIcon.ico  letter=I  back=#4F6BE8  fore=#F7F9FB  104599 B  frames=7
size=32x32
corner_alpha=0
left=Color [A=255, R=79, G=107, B=232]
center=Color [A=255, R=247, G=249, B=251]
```

- 体积不是判据：同尺寸同深度换图后字节数可能一样，必须逐像素取色。
- 要换外部 ico 就直接覆盖 `app\AppIcon.ico`，并在本节记录来源 URL、许可与是否改过；不要只写在聊天里。

---

## 6. 发布 / 分发

发版顺序：

1. 改 `app\IndexApp.csproj` 的 `<Version>`（当前 `0.2.0`）。
2. 重新打包要交付的那一档。
3. 守门比对 `FileVersion`：

       $exe = Get-ChildItem .\publish\IndexApp_v*_self_contained_build_*.exe | Sort-Object LastWriteTime | Select-Object -Last 1
       $exe.VersionInfo.FileVersion

4. 每个包交付前再跑一次 `--selftest`，退出码 0。
5. 需要分发校验和时：`Get-FileHash $exe.FullName -Algorithm SHA256`，把 `.sha256` 与 exe 放同目录。

实测输出：

```text
FileVersion=0.2.0.0
publish_no_runtime=1,758,754 bytes
publish_self_contained=70,337,651 bytes
self_contained_selftest_exit=0
no_runtime_selftest_exit=0
SHA256(no_runtime)=D5B6F4B534285C542A669DECF67ED544CD8816B65CEF78D5A1CD66570B0E2E87
SHA256(self_contained)=5A366192DE3D4FD4774CC61838FC0CA95F4CB341B21C4C1AF1F1124B1C290A40
```

目标机排查：`dotnet --list-runtimes` 有没有 WindowsDesktop 10.x（框架依赖档）→ 首次解包是否被杀软拦 → `%APPDATA%\IndexApp\selftest.txt` 有没有红。

---

## 7. 注意事项

- **`lib/` 是 StartUI4Controls v3.0.0 的自持基线（48 文件）**：日常不改；少文件/改过文件导致构建异常时，跑 Skill 的 `fetch-source.ps1` 补齐。改 `lib/` 要按 Skill 的“源码来源与升级”回灌种子并重钉清单，不要只改本工程。
- **本地赋值就是退订主题**（设计，不是 bug）：要跟主题走就写 `{DynamicResource UI4.Brush.*}`；代码里现取资源要兜底，`Application.Current.FindResource` 对缺失键抛异常。
- **主题接线四条硬顺序**：`Register/SetTheme` 只能在 `base.OnStartup(e)` 之后、`OnStartup` 内；`UI4ThemePacks.RegisterAll()` 在 `Apply(套装键)` 之前；装字典早于第一个窗口 `Show()`；`--selftest` 分支排最前。
- **明暗策略是显式决策**（`app\Helpers\Theme.cs` 的 `Theme.Policy = "both"`）。留 `TODO` 时 selftest 会红并挡住发布。
- **界面自适应**：`MinWidth/MinHeight` 960×640（本工程）；字体走 `Segoe UI, Microsoft YaHei UI, sans-serif`；`app.manifest` 的 PerMonitorV2 别删；窗口几何恢复要做越界回正。
- **间距刻度**：`Margin/Padding` 只用 4 的倍数；本次静态扫描输出 `(empty)`。卡片外边距要 ≥ `ShadowDepth + ShadowBlurRadius`；不要用 `ClipToBounds` 兜投影。
- **托盘 `UI4NotifyIcon`** 必须在 `OnClosing` 里 `Visibility = Collapsed; Dispose();`（本工程 M2.0 仍未启用托盘）。
- **`UI4PasswordBox.Password` 绑定要显式 `Mode=TwoWay`**；`UI4ListView/UI4GridView` 的悬浮放大是按像素预算反算钳过的。
- **`UI4CodeEditor` 的语法高亮不跟主题**（XSHD 决定），库只染外壳底色与前景。
- **PowerShell 侧**：`.ps1` 里有中文必须存带 BOM 的 UTF-8；脚本首句设 `[Console]::OutputEncoding = UTF8`；带中文的路径别用 `powershell -Command` 内联，走 `-File` + 参数。
- **验证陷阱**：拿上一轮产物验本轮改动、关键命令接管道（`build | grep` 的退出码是 grep 的）、增量构建的 `0 个警告`——这三条会把“没通过”报成“通过”或反之。
- **默认扫描范围**：所有就绪的固定盘（本机 C:\、D:\、E:\），按名字/路径排除 `WinSxS`、`Installer`、`Package Cache`、`node_modules` 以及应用自身数据目录；如需收窄用 `INDEXAPP_ROOT`。范围按钮只接受已在索引中的目录，新建/被排除目录会明确提示重建。
- **扫描不会伤硬盘**：只读文件名/大小/时间等元数据，不读文件内容、不写被扫描盘；快照写到 `%LOCALAPPDATA%`。首次全量枚举是一次性 I/O，之后加载快照；F5/重建才会再次全量扫描。M2.0 的实时刷新用目录变更通知，不轮询文件内容。正常读取不会损坏硬盘，频繁重建只会增加 I/O。
- **本次 M2.0 UI 冒烟实测（UIA）**：独立测试根 `C:\Users\webtu\IndexAppM2UiTest\root`；运行中创建 `live-new.txt` → 搜索命中，状态 `实时刷新：开 · 已同步 1 项`；删除 → 搜索 0 项；关闭应用后新增/删除/重命名，重启后台对账 `新增 2 / 删除 2 / 更新 0` 并搜索一致；设置浮层实时刷新勾选 On，主题区 `灰纸套装（预置） → 生效键 paper-grey`。截图：`docs/screenshot-indexapp.png`。
- **未验证项汇总**：
  - MFT/USN 全量索引与服务化：本机进程非管理员（`fsutil usn readjournal C:` 拒绝访问），无法验证卷句柄/USN；M2.0 走目录监视 + 对账，M2.1 再做。
  - 自包含包内存闸门：30s 空闲私有 316.5 MB（同机读数 309–372 MB），超过 300 MB；框架依赖包 240.3 MB 达标。已启用 `System.GC.ConserveMemory=9` 与对账后 Compact，仍不稳定压线。
  - 空查询 UI 延迟：76.6 万命中全量排序，201–359 ms，超过 200 ms 闸门；关键词查询达标。
  - 字号滑杆 12–28 与全局缩放 150%/200% 的逐档目测：只做了设置浮层开合与深浅主题切换；排印/缩放的机器断言已由 selftest 覆盖。
  - 系统托盘、Explorer 右键菜单、颜色标签、自动更新、内容检索、多用户 ACL、MSI 安装器、代码签名：M2.0 未实现或未验证。
  - 自包含包首次运行时包下载耗时：本机 NuGet/运行时包已缓存，未单独计时。
