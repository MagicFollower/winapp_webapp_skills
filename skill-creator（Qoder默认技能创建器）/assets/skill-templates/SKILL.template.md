---
name: <skill-name>
description: <一句话做什么> + <什么场景/什么话/什么文件类型会触发>。单行，≤1024 字符。
when_to_use: <给自动选路用的短句，不要复制 description>
argument-hint: <技能名或目标路径>
allowed-tools: Read, Write, Edit, Glob, Grep
---

# <技能标题>

## Overview

<这个技能解决什么问题，一到两句。写判据，不写背景故事。>

## 判据与前置确认

- <动手前必须先确认的那件事，附一条能直接跑的检查命令>
- <不满足时怎么办：退出并报告缺什么，不要现场猜>

## 工作流

1. <第一步：命令形态写全，参数用尖括号占位>
2. <第二步>
3. <验收步：期望看到哪几行输出、退出码含义>

## 失败处理

| 现象 | 先查什么 | 处理 |
|------|----------|------|
| <真实遇到过的报错或假绿形态> | <定位手段> | <动作，不要写「检查一下」> |

## Resources

- `references/<file>.md` —— <什么时候必须读它>
- `scripts/<file>.py` —— <怎么调用、期望输出、退出码>
- `assets/<file>` —— <拷到目标位置的哪里、要不要改名>

<!--
建完必做三件事：
1. 把 name 改成与目录名逐字符相同的小写连字符串；
2. 删掉用不上的小节与整段占位（不要留空标题）；
3. 跑一遍自证：
     python scripts/validate_skill.py --selftest
     python scripts/validate_skill.py <本技能目录>
   前者必须 SELFTEST_PASS，后者必须 fail=0。
只建真正需要的子目录；没有脚本就不要 scripts/，没有产出物素材就不要 assets/。
-->
