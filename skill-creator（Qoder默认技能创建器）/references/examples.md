# 写法对照集

每条都是「反例 → 为什么不行 → 正例」。拿不准某个写法时先在这里找同类。

---

## 一、frontmatter

### description 写成多行（真实反例）

应用内置的 `skill-creator` 就是这么写的：

```yaml
---
name: skill-creator
description:
  Guide for creating effective skills. This skill should be used when users want
  to create a new skill (or update an existing skill) that extends the CLI's
  capabilities with specialized knowledge, workflows, or tool integrations.
allowed-tools: Edit, Write
---
```

为什么不行：`description:` 后为空、值靠缩进续行，是 YAML 折叠标量。路由层按「单行摘要」消费这个字段，
跨行写法在不同读取路径下可能被截成空串。校验器对它报 `DESC_MULTILINE`。

正例：压成一行，并把触发场景写进去。

```yaml
---
name: skill-creator
description: 创建或更新 Qoder Skill 的完整流程——目录结构、frontmatter 规范、脚本与资产打包、可执行校验器。当用户想新建技能、把既有流程固化成技能、或改进已有 SKILL.md 时使用。
---
```

### name 与目录名不符

```
目录：My_Skill/          frontmatter：name: my-skill
```

两处都得改：目录名用小写连字符，且与 `name` 逐字符相等。否则 `NAME_BAD` + `NAME_DIR_MISMATCH` 一起报。

### description 只写「是什么」

反例：`description: 处理 Excel 文件。` —— 等于没写，路由无法判断该不该调用。

正例：把「用户会说什么」「会碰到什么文件/报错」写进去：

```
description: 读取、清洗、合并 .xlsx/.csv 并导出。当用户提到表格去重、多表 join、透视表、
  或报 openpyxl/pandas 编码错误时使用。
```

### description 超长

上限 1024 字符。超过就砍触发场景的数量，不要砍「做什么」那句。真的写不下说明技能范围该拆。

---

## 二、正文瘦身

反例（把背景写成三段）：

```markdown
## 背景

早在 2015 年，团队就遇到了……当时我们选择了方案 A，后来因为……最终形成了现在的做法。
```

正例（留判据、去叙事）：

```markdown
## 判据

操作前先确认目标工程装了 X：`grep -q '"x"' package.json`。没装就直接退出并报告缺什么，
不要现场猜版本号。
```

判断标准：**这段删掉后，下一个 agent 会不会做错事？** 不会就删。

### 长清单沉到 references

本仓库现有全局技能里有三份正文超 500 行（实测全文行数）：`g2-corner-integration` 559 行、
`startui4-wpf` 550 行、`windows-electron-scaffold` 572 行。它们把完整流程都写在正文里，
每次调用都整份进上下文。改进方向一致：正文留「决策树 + 命令形态 + 失败处理」，
逐步骤细节移到 `references/*.md`，并在正文点名「改 X 前先读 references/Y.md」。

---

## 三、脚本的 stdout 形态

反例：

```
Traceback (most recent call last):
  File "...", line 12, in <module>
    ...
ZeroDivisionError: division by zero
```

正例（结论先行、噪声抑制、非零退出码）：

```
skill=C:\path\to\skill
findings=3 fail=2 warn=1 info=0
FAIL NAME_DIR_MISMATCH :: 文件夹 "My_Skill" != name "my-skill"
FAIL BROKEN_REF :: SKILL.md 引用了不存在的 scripts/gone.py
WARN BODY_OVER_500 :: 正文 523 行 > 500，把细节移到 references/
```

三条硬要求：

1. 第一行就能看出「在检查什么」；
2. 每条问题一行，带判据代码，方便 agent 复述给用户；
3. 退出码只有一种含义：0 干净 / 非 0 有问题 / 2 用法错。

---

## 四、自由度选错档位

反例（该给脚本却给了散文）：

```markdown
然后用 python 统计一下文件的行数、图的数量、表格数量，看看够不够。
```

正例（沉成脚本 + 固定调用形态）：

```markdown
python scripts/audit.py --site <站点目录> --matrix matrix.json
期望：problems=0 且退出码 0；非 0 时按 by_kind 里的类别逐条修，别改判据阈值。
```

反向错档也常见：把「有多种合理做法」的判断写成 8 条死步骤，agent 会在不适用的场景硬套。
那种就该写判据 + 一个例子，把选择权留回去。

---

## 五、什么时候必须问用户

| 情形 | 为什么要问 |
|------|-----------|
| 目标位置（全局 `~/.qoder-cn/skills/` 还是项目 `.qoder/skills/`） | 放错代价不对称，且看不出来 |
| 技能要覆盖的边界（只讲接入？还是含发版/回滚？） | 决定正文与 references 的拆法 |
| 与既有技能重叠 | 可能应该改旧的而不是新建一个 |
| 要删/覆盖已有技能目录 | 不可逆 |
| 需要往用户机器写预制资产（几十 MB 级） | 体积与隐私 |

一次问一到两个，给多选项，别连续追问。
