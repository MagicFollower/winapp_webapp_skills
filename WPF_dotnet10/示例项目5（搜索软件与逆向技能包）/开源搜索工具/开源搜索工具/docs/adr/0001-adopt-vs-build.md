# ADR-0001：adopt-vs-build 与 M1 引擎路径

- 日期：2026-10-27
- 状态：已接受（本轮自主执行，用户已授权 P0 起无需确认）
- 后续（2026-10-27）：M2.0 已用普通权限 watcher + 目录对账补实时刷新；MFT/USN/服务仍归 M2.1，见 `docs/M2升级规划报告.md`。

## 背景

`_rev` 的静态分析证明原软件是 2014 年的 VC9/C++ 实现：`FinderFS.dll` 依赖 `Utility.dll` 的 MSVC 修饰类与 `MSVCP90`，导出全为 C++ 修饰名，无头文件/结构体布局；`FinderPro.exe` 与 `FinderFS.dll` 的 Authenticode 均为 HashMismatch（二次打包）。原二进制不能作为开源项目的代码基座，也不能作为可信的差分 oracle。

## 候选

| 方案 | 证据 | 结论 |
|---|---|---|
| 复用原 DLL | 32 位 + C++ ABI + 被篡改 + 商业许可 | 否决 |
| fork Lertaro / SwiftList | 2026-10-27 GitHub API：均 MIT、C#/WPF + NT 服务、Lertaro 1305★、SwiftList 457★、仓库描述相同 | 作为架构对照；M1 不复制代码 |
| clean-room 重写 | 本方案 | **采用** |

## 决策

1. **clean-room**：只使用 `_rev` 的导出名、字符串与截图作为行为契约；不复制反编译代码（本次也没有可编译源码），不使用 XSkin/ShanFian.xf/Foxit/原图标。
2. **M1 引擎路径**：目录遍历（`System.IO.Enumeration.FileSystemEnumerable`）+ 内存索引 + 自定义快照 + 查询，不依赖管理员权限。
3. **MFT/USN 延后**：本机当前进程不是管理员（`net session` 退 2），无法在 P1 验证卷句柄/USN；把 MFT 全量 + USN 增量列为 M2，并在 `IndexEngine` 接口层保留替换点。M1 不虚报“已实现 MFT”。
4. **服务化延后**：M1 采用 UI 进程内后台索引（UI 线程不阻塞），因为目录遍历不需要管理员；Windows 服务 + 命名管道作为 M2 的权限/多用户方案。这个偏离在 T0Level 文档与验证台账里显式记录。

## 后果

- 优点：无需 UAC、可测试、可交付；查询与快照路径可先达到 1M 级闸门。
- 代价：首次全量扫描比 MFT 慢；无 USN 级实时增量（M1 用 F5 重建/刷新）；索引范围默认用户目录，不是全盘。
- 复核：P1 在同一 1M 数据集上量查询/冷启动/内存；若目录遍历首建超出 15s 目标，先记录真实值，不把 MFT 的账算进来。
