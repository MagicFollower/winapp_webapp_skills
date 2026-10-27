# 完整小技能样例

这是一个可以整体拷走的最小技能。放在 `assets/` 是因为它的内容会被**复制进新技能目录**，
而不是让 agent 读进来当背景。下面的三个文件逐字可用（把 `csv-quickfix` 换成你的名字即可）。

---

## 目录清单

```
csv-quickfix/
├── SKILL.md
├── scripts/
│   └── check_columns.py
└── references/
    └── schema.md
```

只有这三样。没有 README、没有空目录、没有占位文件。

---

## SKILL.md

```markdown
---
name: csv-quickfix
description: 检查并修复 CSV/TSV 的列数不齐、BOM、换行符混用与分隔符歧义。当用户报「表格导入后错位」、
  提到 csv 列数对不上、或需要证明一份 CSV 结构干净时使用。
argument-hint: <csv 文件路径>
allowed-tools: Read, Write, Edit, Glob, Grep, Bash
---

# CSV Quickfix

## Overview

导入错位的根因九成落在这四类：列数不齐、行尾混用 CRLF/LF、UTF-8 BOM、分隔符歧义（字段里含逗号）。
本技能先量出这四类的真实数字，再动手改。

## 判据与前置确认

先跑结构检查，不要边看边改：

    python scripts/check_columns.py "<文件路径>"

期望输出四行计数 + 一个退出码。退出 0 表示四类全干净；非 0 时按报出的类别处理。
判据本身会自己红：先跑 `python scripts/check_columns.py --selftest`，必须看到 `SELFTEST_PASS`，
否则「全干净」这个结论不可信。

## 失败处理

| 现象 | 先查什么 | 处理 |
|------|----------|------|
| 列数计数为 0 | 文件是否被当成空/编码是否报错 | 用 `io.open(p, encoding="utf-8-sig")` 重读，别改判据 |
| selftest 不通过 | 解析函数是否被改坏 | 恢复解析函数，重跑，再谈业务问题 |

## Resources

- `references/schema.md` —— 四类问题的判定口径与修复顺序；改判据前必读。
- `scripts/check_columns.py` —— 结构计数与自证，退出码 0/1/2 = 干净/有缺陷/用法错。
```

---

## scripts/check_columns.py

```python
"""CSV 结构检查：列数一致性、BOM、行尾混用、分隔符歧义。

用法：
  python check_columns.py <文件> [--expect N]
  python check_columns.py --selftest
退出码：0 干净 / 1 有缺陷 / 2 用法错误
"""
import io
import os
import sys

GOOD = "a,b,c\r\nd,e,f\r\n"
BAD_RAGGED = "a,b,c\nd,e\n"


def inspect(path):
    raw = io.open(path, "rb").read()
    issues = []
    if raw.startswith(b"\xef\xbb\xbf"):
        issues.append("BOM")
    crlf, lf_only = raw.count(b"\r\n"), raw.count(b"\n") - raw.count(b"\r\n")
    if crlf and lf_only:
        issues.append("MIXED_NEWLINE")
    text = raw.decode("utf-8-sig", errors="replace")
    widths = {}
    for line in text.splitlines():
        if not line.strip():
            continue
        widths[line.count(",") + 1] = widths.get(line.count(",") + 1, 0) + 1
    if len(widths) > 1:
        issues.append("RAGGED_COLUMNS " + str(sorted(widths)))
    return issues, widths


def main(argv):
    if "--selftest" in argv:
        root = os.gettempdir()
        g, b = os.path.join(root, "sc_good.csv"), os.path.join(root, "sc_bad.csv")
        io.open(g, "w", encoding="utf-8", newline="").write(GOOD)
        io.open(b, "w", encoding="utf-8", newline="").write(BAD_RAGGED)
        gi, _ = inspect(g)
        bi, _ = inspect(b)
        ok = (not gi) and ("RAGGED_COLUMNS" in " ".join(bi))
        print("selftest good_issues=%s bad_issues=%s" % (gi, bi))
        print("SELFTEST_PASS" if ok else "SELFTEST_INVALID")
        return 0 if ok else 1
    if len(argv) < 1 or not os.path.isfile(argv[0]):
        print("用法：check_columns.py <文件> 或 --selftest")
        return 2
    issues, widths = inspect(argv[0])
    print("file=%s column_widths=%s" % (argv[0], sorted(widths)))
    print("issues=%d %s" % (len(issues), " ".join(issues)))
    return 1 if issues else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
```

要点都在上面这段里：结论行在前、traceback 被 `errors="replace"` 挡住、三种退出码、
判据自带 `--selftest`。

---

## references/schema.md

```markdown
# 判定口径

| 类别 | 判据 | 修复顺序 |
|------|------|----------|
| BOM | 前三个字节是 EF BB BF | 第 1 步去掉，否则后面所有列名都带脏字符 |
| 行尾混用 | CRLF 与裸 LF 同时出现 | 第 2 步统一成 LF |
| 列数不齐 | 逗号计数出现两种以上取值 | 第 3 步逐行定位，先怀疑字段内逗号 |
| 分隔符歧义 | 字段含逗号却被引号包裹 | 用标准 csv 模块重算，别自己 split |

改任何一条判据前先跑 `--selftest`，改后再跑一次：判据必须仍然会红。
```
