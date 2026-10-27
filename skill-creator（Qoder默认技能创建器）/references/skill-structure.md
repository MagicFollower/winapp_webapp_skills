# 技能结构契约

本文是 `SKILL.md` 里各条原则的展开版：改结构、加判据、或拿不准某段该放哪一层时读它。

---

## 一、三层披露的边界

| 层 | 何时进上下文 | 容量 | 该放什么 |
|----|--------------|------|----------|
| `name` + `description` | **每次对话都在** | 约 100 词 | 足以让路由判断「该不该调用」的信息：做什么 + 什么话/什么文件类型/什么报错会触发 |
| `SKILL.md` 正文 | 技能被调用后 | ≤500 行 | 决策树、流程、判据、命令形态。不放长篇背景 |
| `references/` | agent 按需读 | 大 | schema、契约、API 清单、逐条映射表 |
| `scripts/` | **不进上下文**，直接执行 | 无限 | 重复写、脆弱、需要确定性的操作 |
| `assets/` | 不进上下文，进产出物 | 大 | 模板、图标、预制数据、示例工程片段 |

推论：**能执行的不要写成文字**。同一段流程如果每次都靠 agent 现场拼命令，就该沉成脚本；
沉成脚本后，SKILL.md 里只需要留「怎么调用 + 期望看到哪几行输出 + 失败时怎么办」。

---

## 二、references 与 assets 的归属判据

问一句：**这个文件会被读进来，还是会被复制/引用到产出物里？**

| 文件 | 归属 | 理由 |
|------|------|------|
| 字段约束表 | `references/` | agent 判断对错时要读 |
| 新工程骨架的 `package.json` | `assets/` | 直接拷进目标工程 |
| 一段可运行的接入代码片段 | `assets/` | 出现在用户代码里 |
| 「为什么当年这样设计」的历史说明 | 都不放 | 与干活无关的叙事 |

---

## 三、frontmatter 字段全表

| 字段 | 必需 | 约束 | 校验器判据 |
|------|------|------|------------|
| `name` | 是 | `^[a-z0-9][a-z0-9-]*$`，与目录名逐字符相等 | `NAME_MISSING` / `NAME_BAD` / `NAME_DIR_MISMATCH` |
| `description` | 是 | 单行标量，≤1024 字符 | `DESC_MISSING` / `DESC_MULTILINE` / `DESC_TOO_LONG` |
| `when_to_use` | 否 | 一句触发提示，别复制 description | 无 |
| `argument-hint` | 否 | `<占位>` 形态 | 无 |
| `arguments` | 否 | 结构化参数定义 | 无 |
| `model` / `tools` / `allowed-tools` | 否 | 逗号分隔；要与正文要求的动作匹配 | `NARROW_ALLOWED_TOOLS` / `WIDE_ALLOWED_TOOLS` |
| `license` / `version` / `metadata` | 否 | 已知可选 | 无 |
| 其他 | — | 未文档化 | `UNKNOWN_FM_KEY`（INFO） |

### 关于 `allowed-tools`

它限制技能内的工具面。两种典型错配：

- 正文要求「运行 `scripts/x.py` 验证」，但 `allowed-tools` 只给 `Edit, Write` → 校验器报 `NARROW_ALLOWED_TOOLS`。
- 给成 `*` → 报 `WIDE_ALLOWED_TOOLS`，提示收窄。

判断「正文是否要求执行」用的是这几个字面量：`python scripts/`、`bash scripts/`、`powershell`、`.ps1`。
命中任一就认为脚本会被执行。

---

## 四、校验器判据总表

`scripts/validate_skill.py` 的判据按严重级分三档：

**FAIL（阻断）**

| 判据 | 触发条件 |
|------|----------|
| `MISSING_SKILL_MD` | 技能根没有 SKILL.md |
| `FM_MISSING` / `FM_UNCLOSED` | 不以 `---` 开头 / 没有闭合的 `---` |
| `NAME_*` | 见上表 |
| `DESC_MULTILINE` | `description:` 后为空并接缩进行，或用 `|`/`>` 块标量 |
| `DESC_TOO_LONG` | 单行 > 1024 字符 |
| `TODO_RESIDUE` | 命中 `TODO:` / `待补充` / `FIXME` |
| `PLACEHOLDER_RESIDUE` | 残留 `【…】` 占位 |
| `EXTRANEOUS_DOC` | 根下 README/CHANGELOG/INSTALLATION_GUIDE/CONTRIBUTING/NOTES/FAQ/TODO |
| `BROKEN_REF` | SKILL.md 提到的 `scripts|references|assets/…` 相对路径不存在 |

**WARN（默认不阻断，`--strict` 阻断）**：`BODY_OVER_500`、`EMPTY_DIR`、`SCRIPT_NO_MAIN`、`NO_SELFTEST`、
`ABS_PATH_LEAK`、`NARROW_ALLOWED_TOOLS`、`WIDE_ALLOWED_TOOLS`、`ROOT_EXTRA_MD`。

**INFO**：`UNKNOWN_FM_KEY`。

### 机器特定路径的检测口径

检测三类形态：Windows 上「盘符 + 用户目录 + 用户名」的绝对路径（含反斜杠被写成双份的转义形态）、同一形态的正斜杠写法、以及类 Unix 系统的用户主目录路径。

扫描范围只含 `SKILL.md`、`scripts/`、`references/`，**不含 `assets/`**：资产是载荷（模板、vendored 的上游源码），里面的机器路径属于别人的文件，不该由本技能判红。实测必要：主题副本 `assets/theme/…/bin/check-actions.py` 里带着 CI 的 `/home/runner/`，扫进来只会淹掉真正该修的那一条。
命中不阻断——有些技能确实需要记录本机绝对路径（比如指向某个固定工程），但要意识到它换机即坏。

---

## 五、自测（`--selftest`）的设计契约

`--selftest` 造两个样本：

1. **clean-sample**：合规最小技能，要求 **0 个 FAIL 且 0 个 WARN**。它证明校验器不会一律红。
2. **Broken_Skill**：注入 6 类已知缺陷（目录名不符 + 多行 description + `TODO:` + README 附属文档 + 断链引用 + 空目录），
   要求逐类命中；`SCRIPT_NO_MAIN` 作为附带项一并断言。

任一不满足就打 `SELFTEST_INVALID` 并退出 1，且保留 TEMP 样本目录供排查。

**为什么必须有这一节**：真实目录报 `findings=0` 有两种成因——技能确实干净，或者检测逻辑根本没跑起来
（正则不匹配、异常被吞、路径解析错）。只有「已知缺陷必被命中」这条能排除后者。

---

## 六、未确证项

写在这里而不是假装知道：

| 事项 | 现状 |
|------|------|
| 全局 `~/.qoder-cn/skills/` 与 app 内置同名技能（`skill-creator`）的**加载优先级** | 未实测。2026-10-17 新建的全局 `skill-creator` 在同一次会话里立刻出现在可用技能列表，且展示的是全局版的中文 description，但这只是单点观察，不能证明优先级规则；要确证需构造两份同 name 不同 description 的技能并比对调用时载入的正文 |
| Qoder 实际消费的 frontmatter 字段全集 | 以应用内置 SKILL.md 实际使用的（`name` / `description` / `allowed-tools`）加本技能补充的（`when_to_use` / `argument-hint` / `arguments` / `model` / `tools`）为准；`UNKNOWN_FM_KEY` 只列清单不判错 |
| `/skills reload` 与自动热加载的确切时机 | 未实测；观察到新建技能在同会话内即出现在列表 |

需要时按 `SKILL.md` 第七节的要求，把实测过程与数字补进本表。

---

## 七、来源与实测基线

### 打包来源

应用内置的 `skill-creator` 只有一个文件。2026-10-17 实测：

| 项 | 实测值 |
|----|--------|
| 路径 | app 安装目录下 `resources/app.asar.unpacked/…/_worker/builtin/skill-creator/`（另有 0.4.4 / 0.4.7 / 0.4.8 三份版本副本） |
| 目录内容 | **仅 `SKILL.md` 一个文件**（`find -type f` 计数 = 1） |
| 字节 / 行数 | 10,529 字节；全文 294 行，其中 frontmatter 之后正文 286 行 |
| 四份副本 | MD5 前 10 位全为 `728ee33721`，逐字节相同 |
| frontmatter | `name` / `description`（**多行折叠写法**）/ `allowed-tools: Edit, Write` |
| 章节结构 | 30 个标题：About Skills、Core Principles（Concise / Degrees of Freedom / Anatomy / Progressive Disclosure）、Skill Creation Process（Step 1–6 与 Skill Naming） |

两处必须记着的事实：

1. 内置目录里**没有** `agents/openai.yaml`、没有 `references/`、没有 `assets/`。
   本技能的这三个子目录是**新写的**，不是从内置路径拷来的。
   内置那份 `SKILL.md` 的 description 用的是多行折叠写法，正是本校验器的 `DESC_MULTILINE` 反例来源。
2. 「内置正文约 449 行」这个说法与实测量不符（实测全文 294 行 / 正文 286 行）。以实测为准。

### 本技能基线（写完时实测）

| 项 | 值 |
|----|-----|
| 文件数 / 总字节 | 6 个文件 / 44,847 字节 |
| `SKILL.md` | 全文 255 行、正文 248 行、13,215 字节；description 177 字符 |
| frontmatter 字段 | `name`、`description`（单行）、`when_to_use`、`argument-hint`、`allowed-tools` |
| `validate_skill.py .` | `findings=0 fail=0 warn=0 info=0`，退出码 0 |
| `validate_skill.py . --strict` | 同上，退出码 0 |
| `validate_skill.py --selftest` | `SAMPLES=Broken_Skill:9 clean-sample:0` + `SELFTEST_PASS`，退出码 0 |

### 观察到但未确证的行为

新建全局技能后，**同一次会话**里它就出现在了可用技能列表中（展示的是本技能的中文 description 与 `when_to_use`）。
这只证明热加载会发生，不证明与内置同名技能冲突时谁生效——见上一节「未确证项」。
