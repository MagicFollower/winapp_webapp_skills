---
name: skill-creator
description: 创建或更新 Qoder Skill 的完整流程——目录结构、frontmatter 规范、命名与描述写法、脚本/参考资料/资产文件的打包规则、可执行校验器与自测判据、Windows 环境兼容与路径处理。当用户想新建一个技能、把既有经验/流程/文档固化成技能、改进已有技能（含 skill-creator 自身），或询问 SKILL.md 该怎么写时使用。
when_to_use: 用户要求「做成技能」「封装成 skill」「把这套流程固化下来」「改进 /skill-creator」，或给出一个 SKILL.md 让你补全。
argument-hint: <技能名或要固化的主题/目录>
allowed-tools: Read, Write, Edit, Glob, Grep, Bash
---

# Skill Creator

## Overview

Skill 是给**另一个 Qoder 实例**看的说明书，不是给用户看的文章。判断标准只有一条：
一个没有本次对话上下文的 agent，能否只靠技能目录里的文件把活干对、并且知道自己在骗自己。

本技能覆盖：结构契约 → 写作规范 → 创建流程 → 机器校验 → 打包脚本/参考资料/资产 → 环境与路径坑 → 交付时要报什么数。

---

## 一、核心原则

### 1. 简洁是硬约束

上下文窗口是公共资源。技能正文要与系统提示、对话历史、其他技能的元数据、用户的真实请求抢同一份窗口。

默认假设：**agent 已经很聪明**。只写它不知道的东西。每加一段都问两句：
「agent 真的需要这段吗？这段值它占的 token 吗？」

能用一个例子说清的，不要写三段解释。

### 2. 自由度分级

| 自由度 | 用在哪 | 形态 |
|--------|--------|------|
| 高 | 多种做法都对、要靠上下文判断 | 文字原则 + 判断标准 |
| 中 | 有推荐做法、允许变体 | 伪代码、带参数的命令模板 |
| 低 | 操作脆弱、顺序必须固定、一致性至关重要 | 具体脚本，参数少，跑过验证 |

顺序错了会坏事的步骤，写成脚本或写成编号清单，不要写成「然后大致……」。

### 3. 渐进披露三层

1. **元数据（name + description）**——始终在上下文里，约 100 词。这是唯一的触发机制。
2. **SKILL.md 正文**——技能被调用后才载入，控制在 500 行内。
3. **打包资源（scripts/references/assets）**——按需读；脚本可以不读进上下文直接执行，所以容量近乎无限。

快到 500 行时就拆，不要压缩到失智能。

### 4. 目录结构

```
skill-name/
├── SKILL.md            必需
├── scripts/            可执行代码（Python/Node/Bash/PowerShell）
├── references/         供 agent 读进上下文的契约、schema、长说明
└── assets/             出现在产出物里的文件（模板、图标、预制数据）
```

只建**真正需要**的子目录，不要建空目录占位。

`assets/` 与 `references/` 的区别就一句话：**会不会被读进上下文**。
被读的是 references，被复制/引用进产出物的是 assets。放错位置会让技能体积白白进窗口。

### 5. 禁止放的东西

- `README.md`、`CHANGELOG.md`、`INSTALLATION_GUIDE.md`、`NOTES.md` 之类的附属文档。
- 空目录、占位文件、以及任何形式的待办字样占位。
- 与「agent 能把活干对」无关的历史叙事。

---

## 二、SKILL.md 规范

### Frontmatter

必需两项：

```yaml
---
name: skill-creator
description: 一句话说清「做什么」+「什么时候用」。单行，≤1024 字符。
---
```

可选：`model`、`tools`、`allowed-tools`、`when_to_use`、`argument-hint`、`arguments`。

| 字段 | 规则 | 常见错误 |
|------|------|----------|
| `name` | 小写字母/数字/连字符，正则 `^[a-z0-9][a-z0-9-]*$`，**必须与文件夹同名** | 用了下划线；文件夹叫 `My_Skill` 而 name 叫 `my-skill` |
| `description` | 单行标量、≤1024 字符；既写「是什么」也写「何时触发」 | 写成 YAML 折叠多行（`description:` 换行缩进），只写是什么不写何时用 |
| `when_to_use` | 给自动选路用的短句，不要重复 description | 写成第三段散文 |
| `allowed-tools` | 逗号分隔；技能内若要跑脚本，需包含执行所需工具 | 只给 `Edit, Write` 却要求 agent 运行 scripts 下的脚本 |

**description 是唯一触发依据**，写不好等于技能不存在。触发条件要具体：文件类型、命令形态、用户原话关键词。

### 正文

- 祈使句/不定式，不写「我觉得」。
- 第一段说明「这个技能解决什么问题」，第二段给判断标准，后面给流程。
- 引用打包资源时写清「相对技能根的路径 —— 什么时候必须读它」。校验器会逐个检查这些路径是否存在，
写错等于埋一条断链。

---

## 三、创建流程

### Step 1 用具体例子锁定范围

问清（最多两轮，别审问）：

- 这个技能支持哪些具体操作？
- 用户会说什么话触发它？
- 有没有真实样本（工程、截图、既有文档）可参照？

拿到例子再动手。用户没给就先提出一份功能清单让他改，而不是继续追问。

### Step 2 规划可复用内容

对每个例子逐一判：

| 会重复写的代码 | → `scripts/` | 边做边重写同一段脚本，就是信号 |
| 写作时要查的契约/schema | → `references/` | 需要读进上下文才能判断对错的 |
| 出现在产出物里的文件 | → `assets/` | 模板、图标、预制数据 |

只有文字就三层都不建。

### Step 3 定位置、建骨架

先问（或多选项提交）目标位置：

- **全局个人技能**：`~/.qoder-cn/skills/<skill-name>/`（跨项目可用）
- **项目级技能**：`<项目>/.qoder/skills/<skill-name>/`（只在本仓库可用）

两种放错都难受：项目专属经验进了全局会污染别的工程，通用流程进了项目会被遗忘。

骨架直接拷 `assets/skill-templates/SKILL.template.md`，改 frontmatter，删掉用不上的小节。

### Step 4 先写资源，再写正文

脚本要先**真跑通**（见第五节），再在 SKILL.md 里写「怎么用这条命令、期望看到什么输出」。
正文最后写，因为它要如实描述资源的实际行为，而不是你期望的行为。

### Step 5 机器校验

```
python scripts/validate_skill.py <技能目录>
python scripts/validate_skill.py --selftest          # 先证明校验器会自己红
```

校验器覆盖：SKILL.md 存在性、frontmatter 定界与字段、name 与目录名一致、
description 单行与长度、正文行数、`TODO`/占位符残留、多余附属文档、
`scripts|references|assets` 相对路径引用的完整性、脚本里的绝对路径泄漏。

`--strict` 时把警告也计入失败。**先跑 `--selftest`**：它构造一个含 6 类已知缺陷的技能并要求全部命中，
校验器报 0 缺陷往往意味着它没跑起来，而不是技能干净。

### Step 6 安装、实测、迭代

告诉用户技能就绪，验证路径三条：`/skills reload`（或重启会话）→ `/<name>` 调用 → `/skills list` 确认。
之后在真实任务里用，发现卡点就改——技能是活的，不是一次性交付。

---

## 四、命名

- 只用小写字母、数字、连字符；把用户给的名字规范化（`Plan Mode` → `plan-mode`）。
- 偏好「短动词开头」：`validate-skill` 好过 `skill-validation-helper`。
- 文件夹名必须与 `name` 字段完全一致。
- **与内置技能同名要有意识**：全局目录里放一个 `skill-creator`，就与 app 内置的 `skill-creator` 同名。
  谁生效取决于加载优先级，本技能**未实测该优先级**（见 references/skill-structure.md 的「未确证项」）。
  若不想承担覆盖风险，改名为 `skill-authoring` 之类别名即可。

---

## 五、打包脚本的规则

脚本是低自由度那一档，规则最硬：

1. **必须实测**。没跑过的脚本等于不存在。测试要包含「注入一个缺陷、看它是否报错」——只测成功路径的判据会假绿。
2. **stdout 面向 agent**：打印清晰的成功/失败结论与关键计数，抑制 traceback 噪声，长输出要截断或分页。
3. **非零退出码表示失败**，别让 agent 靠读文字猜。
4. **路径自包含**：技能内部引用一律相对技能目录解析（`os.path.dirname(os.path.abspath(__file__))` 往上找），
- 不要在技能里写死指向某台机器用户目录的绝对路径；不要依赖 `~` 展开；不要假设当前工作目录。
5. **依赖缺失要显式退场**：缺可选依赖就退回只报告不判定的模式，并打印退回原因，绝不静默通过。
6. 语言按环境选：跨平台用 Python；只在 PowerShell 工程里用 `.ps1`（并遵守 ASCII-only、`Join-Path` 等约束）。

### 判据的自证模式

任何「验收脚本」都应带 `--selftest`，做法统一为：构造一个已知会红的样本，断言每一条已知缺陷都被命中，
命中不齐就打 `SELFTEST_INVALID` 并非零退出。之后真实样本上的「0 缺陷」才有意义。

---

## 六、Windows / Git Bash 环境兼容

这些坑与技能内容无关，但会直接摧毁你的自证数字：

| 现象 | 根因 | 对策 |
|------|------|------|
| `grep -c '💡'` 恒为 0；中文输出乱码 | Git Bash 控制台默认 GBK 码页 | 计数一律交给 Python：`PYTHONIOENCODING=utf-8` + `io.open(p, encoding="utf-8")` + `str.count()` |
| `grep -c` 数值偏小 | 它数的是**行数**不是出现次数 | 用 `len(re.findall(...))` |
| `python /tmp/x.py` 找不到文件 | Git Bash 的 `/tmp` 与 Windows Python 看到的不是一处 | 脚本与产物落在工作区目录，全部用绝对路径 |
| 每条 Bash 命令的 cwd 会漂回工作区根 | shell 状态不跨调用保留 | 不要依赖上一条 `cd` |
| heredoc 里 `\\` 变成 `\` | shell 展开 | 脚本里不写反斜杠，用 `os.path.join` |
| 中文文件名解压后乱码 | bsdtar 按 OEM 码页写名 | 按 manifest 逐条核对文件名 |
| 往 stdout 批量打印中文名抛 `OSError [Errno 22]` | 控制台写入限制 | 结果先落文件再读，或分小批打印 |

**技能里凡涉及统计，就把上面这张表原样传给 agent**，否则它一定会用 `grep -c` 报一个假的 0。

### 路径处理

- 技能内部：一律相对技能根解析；脚本自定位用 `__file__`。
- 调用别人的工程：让参数传绝对路径（如 `--site`、`--project`），脚本不猜。
- 写产出物时优先正斜杠；跨平台读回时再归一化。
- 别把开发机路径写进 SKILL.md 的示例里——用 `<项目目录>` 或 `/absolute/path/...` 形态。

---

## 七、交付时要报什么

「我建好了」不算交付。报告里要有**实测数字**（必须来自某条命令的真实输出，不许凭记忆）：

1. 技能绝对路径 + `tree`（哪些子目录、几个文件）。
2. `SKILL.md` 行数与字节数；frontmatter 各字段值与 `description` 字符数。
3. `validate_skill.py <本技能>` 的完整输出（应为 0 缺陷，或明确列出残留项与原因）。
4. `validate_skill.py --selftest` 的输出——证明判据会红。
5. 每个脚本的实际调用命令行与其真实输出（含失败样本的退出码）。
6. 若改了全局目录：与内置同名技能的**冲突说明**与回退办法。
7. 未确证项单独列一节，写清「没测的原因」而不是不提。

用户偏好「问题 + 答案」原样归档时，把结论写进技能文件本身（SKILL.md 或对应 reference），
不要只留在对话里。

---

## 八、改进已有技能（含本技能）

1. 先跑校验器拿现状基线（行数、缺陷、脚本清单）。
2. 只在有证据处改：证据 = 真实任务里的失败、用户的明确纠正、校验器的一条 fail。
3. 改动后重跑校验器 + `--selftest`，并把正文行数与 description 长度重新报一遍。
4. 破坏性动作（删目录、覆盖既有技能、把项目技能上收到全局）先征求确认。
5. 本技能自身的来源与实测基线见 `references/skill-structure.md` 末节。

---

## Resources

- `references/skill-structure.md` —— 结构契约细节：三层披露的边界、references/assets 归属判据、
  frontmatter 各字段完整约束表、校验器的判据清单与未确证项。改结构或写新校验规则前先读。
- `references/examples.md` —— 好/坏对照集：frontmatter 反例（多行 description、name 与目录不符、超长 description）、
  description 触发力写法、正文段落瘦身、脚本 stdout 形态。不确定「这样写行不行」时查它。
- `assets/skill-templates/SKILL.template.md` —— 新技能骨架模板，含 frontmatter 与最小正文结构。
- `assets/examples.md` —— 一个完整小技能（含脚本与 references）的样例目录清单与逐文件内容。
- `scripts/validate_skill.py` —— 可执行校验器。`python scripts/validate_skill.py <技能目录> [--strict] [--json]`；
  `--selftest` 构造缺陷样本自证。退出码 0/1/2 = 干净/有缺陷/用法错误。
