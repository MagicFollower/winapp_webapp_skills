# USN 增量：原理与生效流程

> 归档问题（用户原文）：
> 1. “每次新增或删除文件，当前架构下文件列表是否会实时刷新？”
> 2. “USN增量是什么？原理和生效流程是什么？”
>
> 结论先行：**M1 当时不会实时刷新；M2.0 已补普通权限的实时路径**（`FileSystemWatcher` + 可增量索引 + 启动目录 mtime 对账）。USN 增量仍是 M2.1 的服务化方案：它读取 NTFS 卷的 USN Journal 中“上次 checkpoint 之后”的变更记录，应用到已有内存索引，而不是重新遍历目录。

---

## 0. 一句话解释

USN 增量 = **从 NTFS 卷级变更日志的某个位置继续读，只处理之后的增删改名等事件**。

---

## 1. 新人小结：借还登记簿，而不是每天重新盘点书架

全量扫描像每天把整个图书馆的书架重新数一遍：书越多越慢。
USN Journal 像图书馆门口的借还登记簿：谁进了书、谁拿走了、哪本书改名，都按顺序记一行。闭馆期间发生的变化也记着；开馆后从“上次看到的那一行”继续往下读即可。USN 增量就是“接着登记簿往下读”，不是“重新数书架”。

---

## 2. 为什么需要它

M1 当时的路径是：启动时全量目录枚举/加载快照 → 查询内存索引 → 按 F5 全量重建。M2.0 在此基础上加了 `IndexWatcher` 实时事件与后台目录 mtime 对账。

这带来三个问题：

1. 应用运行期间新增/删除的文件不会进入索引；删除的文件仍留在列表。
2. 关闭期间的变化也不会自动感知；只要快照根一致，下次启动仍加载旧快照。
3. 全量重建的代价随文件数增长；用它来追每一次文件变化不现实。

USN 增量把“每次变化都重扫全盘”换成“只处理变化记录”，是文件搜索工具做到实时更新的核心机制。

---

## 3. 原理

### 3.1 USN Journal 是什么

- **卷级**：每个 NTFS 卷各有一份 USN Journal，不是全局一份；多卷要分别记录 checkpoint。
- **环形日志**：记录按 USN 递增追加；空间满时最旧记录被覆盖，`FirstUsn` 前移，`NextUsn` 指向下一条写入位置。
- **Journal 状态**（`FSCTL_QUERY_USN_JOURNAL` 可查）：
  - `UsnJournalID`：日志身份；卷重建/日志删除后会变化。
  - `FirstUsn`：当前仍可读的最早位置。
  - `NextUsn`：下一条记录的写入位置。
  - `LowestValidUsn`、`MaxUsn`、`MaximumSize`、`AllocationDelta`：窗口大小与配额。
- **读取**：打开卷句柄（如 `\\.\C:`）后，用 `FSCTL_READ_USN_JOURNAL` 从 `StartUsn` 开始读；返回的是变长的 `USN_RECORD_V2`（NTFS）或 V3（ReFS）数组。也可以先 `FSCTL_ENUM_USN_DATA` 做全量名称枚举，但全量元数据以 `$MFT` 为准。

### 3.2 一条 USN 记录有什么

| 字段 | 用途 |
|---|---|
| `FileReferenceNumber`（FRN） | 卷内文件 ID；删除/改名/刷新元数据靠它定位 |
| `ParentFileReferenceNumber` | 父目录 FRN；用于路径 |
| `Usn` | 本条记录的序号；checkpoint 就是继续读的位置 |
| `TimeStamp` | **事件发生时间**，不是文件修改时间 |
| `Reason` | 位掩码：创建/删除/改名/写数据/改属性/硬链接/安全描述符等 |
| `FileAttributes` | 文件属性 |
| `FileName` / `FileNameLength` | 事件涉及的名称；删除记录的名称不一定可用于恢复完整路径 |
| `SourceInfo`、`SecurityId` 等 | 来源/安全信息；文件名索引通常不使用 |

**关键缺口**：USN 记录**没有文件大小和文件修改时间**。如果只靠 USN，列表里的“大小/修改时间”会过期；所以创建、写数据、改属性等事件发生后，还要按 FRN 读一次 MFT 记录（`FSCTL_GET_NTFS_FILE_RECORD`）或文件信息，刷新 size/mtime。

### 3.3 常见 Reason 与处理

| Reason 标志 | 含义 | 索引动作 |
|---|---|---|
| `FILE_CREATE` | 新建 | 读 MFT 记录，插入 Entry（目录则继续枚举/等待其子项事件） |
| `FILE_DELETE` | 删除 | 按 FRN 删除该文件的全部名称/硬链接 |
| `RENAME_OLD_NAME` + `RENAME_NEW_NAME` | 重命名 | 成对处理，更新名称与父 FRN |
| `DATA_OVERWRITE` / `DATA_EXTEND` / `DATA_TRUNCATION` | 内容/大小变化 | 刷新 size/mtime |
| `BASIC_INFO_CHANGE` | 时间/属性变化 | 刷新属性/时间 |
| `HARD_LINK_CHANGE` | 硬链接增删 | 增加/移除对应名称 |
| `SECURITY_CHANGE` | ACL 变化 | 文件名索引可忽略（若按权限过滤则另算） |
| `CLOSE` | 句柄关闭 | 常用于合并/去重，不单独改索引 |

### 3.4 checkpoint：增量的“书签”

每个卷保存一个 checkpoint：

```text
JournalID = <查询到的 UsnJournalID>
NextUsn   = <已经应用到索引的最后位置>
```

下次启动只读 `NextUsn` 之后的内容。判断 checkpoint 是否还能用：

- `JournalID` 与当前卷不一致 → 日志被删除/重建 → 全量重建。
- `checkpoint < FirstUsn` → 记录已被环形日志覆盖（回绕） → 全量重建。
- 卷不是 NTFS、USN 被禁用、卷离线 → 降级为目录遍历 + `ReadDirectoryChangesW`（或提示用户）。

### 3.5 权限

打开卷句柄和读取 USN 通常需要管理员权限。原软件和 Everything 的做法是**索引服务（LocalSystem）+ UI 普通权限**；本项目 M1 的目录遍历不需要管理员，M2.0 也仍用普通权限 watcher/对账，所以还没有服务化。USN 增量应放在 M2.1 的服务里，而不是要求 UI 每次提权。

---

## 4. 生效流程（从全量到实时）

1. **全量建索引**：首次或回退时，用 `$MFT`（推荐，带 size/mtime）或目录遍历建立内存索引。
2. **记 checkpoint**：在开始全量扫描前查 `FSCTL_QUERY_USN_JOURNAL` 拿 `startUsn`；扫描完成后从 `startUsn` 回放到当前，确保扫描期间的变化不丢；保存 `JournalID + NextUsn` 到快照。
3. **启动校验**：加载快照后查当前 Journal；ID 不一致、checkpoint 早于 `FirstUsn`、或快照损坏 → 走第 1 步全量重建。
4. **增量读取**：`FSCTL_READ_USN_JOURNAL` 从 checkpoint 读到现在；用 1MB 级缓冲循环处理 `ERROR_MORE_DATA`；按 `Reason` 解析。
5. **合并与应用**：同一批内按 FRN 合并（例如一次复制产生多个写事件，只刷新一次元数据）；创建/写数据/改名按需读 MFT 记录；删除按 FRN 清掉全部名称；改名成对处理 old/new。
6. **更新 checkpoint**：每批应用成功后把 checkpoint 推进到本批返回的 `NextUsn`；按阈值/定时把索引快照与 checkpoint 落盘（不是每条事件都写盘）。
7. **通知 UI**：服务发 `IndexChanged(revision)`；UI 防抖后重跑当前查询（或由 UI 轮询 revision）。列表只重建命中索引，不重建 1M 个行对象。
8. **回退**：日志回绕/删除/卷重建/权限丢失 → 全量重建并显示状态；非 NTFS/网络盘 → 目录遍历 + watcher 或明确不支持。
9. **关闭**：保存快照与 checkpoint；下次启动从第 3 步继续。

---

## 5. M1 / M2.0 / M2.1（USN）的差别

| 能力 | M1 | M2.0（已交付） | M2.1（USN 增量） |
|---|---|---|---|
| 全量建索引 | 目录遍历 + 快照 | 目录遍历 + 可增量快照 | `$MFT` 全量 + USN 回放 |
| 运行中新增文件 | 搜不到 | watcher `Created` 后进入索引 | USN `FILE_CREATE` 后进入索引 |
| 运行中删除文件 | 仍在列表，打开失败 | watcher `Deleted` 后移除 | USN `FILE_DELETE` 后移除 |
| 改名/大小/时间 | 旧值 | watcher `Renamed/Changed` 刷新 | USN 改名/写事件 + MFT 元数据刷新 |
| 重启后的变化 | 不感知 | 后台目录 mtime 对账补文件名变化；内容大小变化不保证 | 从 checkpoint 追平 |
| 更新入口 | F5/工具栏刷新/设置重建 | 自动 + F5；溢出提示 F5 | 服务自动推进；UI 收到 `IndexChanged` |
| 权限 | 普通用户目录遍历 | 普通用户目录监视/对账 | 需要卷句柄 → 服务/管理员 |
| 日志回绕 | 不存在 | 不存在 | 检测到回绕 → 全量重建 |
| 写盘 | 每次全量后写快照 | 全量后 + 正常退出保存；对账后 Compact | 批量/定时写快照 + checkpoint |

**因此，回答第一个问题：M1 当时不会实时刷新；M2.0 已用无需管理员的目录监视做到运行期实时刷新。USN/MFT/服务仍是 M2.1，用来补齐关闭期间的大小/时间变化与更高吞吐。**

---

## 6. 验收判据（M2.1 落地时）

- 10,000 次创建/改名/删除后，从最后一次操作到索引一致 **≤2s**；用哨兵文件验证“新增可见、删除消失、改名生效”。
- 1M 条目下，增量追平不触发全量目录遍历；从 checkpoint 读取并应用，P95 与内存单独记录。
- 冷启动：加载快照 + 从 checkpoint 追平，不重扫全盘；日志回绕时能自动回退全量并显示状态。
- UI 收到 `IndexChanged` 后只重跑当前查询并刷新可见行；滚动/首屏/帧间隙按原 UI 闸门复测。
- 独立真值：增量后的索引集合与朴素遍历测试树的结果集合相等；不能只比总数。
- 故障注入：USN 禁用、日志删除/回绕、卷卸载、权限被拒、服务重启；每种都有明确的用户可见状态与恢复路径。

---

## 7. 边界与风险

- USN 只记录元数据事件，**不记录文件内容**；内容搜索是另一套索引。
- USN 记录没有 size/mtime，必须二次读 MFT/文件信息；否则列表元数据会过期。
- 日志是环形的，长时间停机可能回绕；checkpoint 失效必须全量重建。
- 多卷、多硬链接、改名、删除、目录子树变更都要逐项处理；不能只处理“新增文件”。
- 需要卷句柄/管理员；不能用 UI 进程反复提权。M2.1 应走服务 + 命名管道。
- 索引包含全盘文件名（敏感）；服务 ACL、按用户隔离、日志脱敏要一起设计。

---

## 8. 参考

- Microsoft Learn：`FSCTL_READ_USN_JOURNAL`、`FSCTL_QUERY_USN_JOURNAL`、`USN_RECORD_V2`、Change Journal Records。
- 本项目架构方案 §4.4（USN 状态机）与 `docs/adr/0001-adopt-vs-build.md`。
- 当前实现：`src/IndexCore/FileIndex.cs` 支持目录遍历、追加式增量、墓碑、父子链、后台对账与 Compact；`src/IndexCore/IndexWatcher.cs` 是普通权限 watcher。USN/MFT 代码尚未落地。
- M2.0 的边界与实测：`docs/M2升级规划报告.md` §13。
