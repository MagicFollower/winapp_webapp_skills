"""校验一个技能目录是否符合 SKILL.md 契约。

用法：
  python validate_skill.py <技能目录> [--strict] [--json]
  python validate_skill.py --selftest [--keep]

退出码：0 = 无 FAIL；1 = 有 FAIL（--strict 时 WARN 也算）；2 = 用法错误。

判据清单（括号内为严重级）：
  MISSING_SKILL_MD(FAIL) FM_MISSING/FM_UNCLOSED(FAIL)
  NAME_MISSING/NAME_BAD/NAME_DIR_MISMATCH(FAIL)
  DESC_MISSING/DESC_MULTILINE/DESC_TOO_LONG(FAIL)
  TODO_RESIDUE/PLACEHOLDER_RESIDUE/EXTRANEOUS_DOC/BROKEN_REF(FAIL)
  BODY_OVER_500/ABS_PATH_LEAK/EMPTY_DIR/SCRIPT_NO_MAIN/NO_SELFTEST/
  NARROW_ALLOWED_TOOLS/WIDE_ALLOWED_TOOLS/WARN(WARN) UNKNOWN_FM_KEY(INFO)

--selftest 会造一个「干净样本」和一个「注入 6 类已知缺陷的样本」，
要求缺陷逐条命中、干净样本 0 FAIL 且 0 WARN；不满足就打 SELFTEST_INVALID 并以 1 退出。
真实目录报 0 缺陷之前，先确认这一条能红。
"""

import argparse
import io
import json
import os
import re
import shutil
import sys
import tempfile

NAME_RE = re.compile(r"^[a-z0-9][a-z0-9-]*$")
DESC_MAX = 1024
BODY_MAX = 500
EXTRANEOUS = ("readme.md", "changelog.md", "installation_guide.md", "contributing.md",
              "notes.md", "faq.md", "todo.md")
RES_PREFIX = re.compile(r"(?:scripts|references|assets)/[^\s`'\"()\[\]<>，。；、]+")
RES_EXTS = (".md", ".py", ".ps1", ".sh", ".js", ".ts", ".json", ".yaml", ".yml",
            ".svg", ".png", ".txt", ".csv", ".toml", ".template.md")
TODO_RE = re.compile(r"TODO:|待补充|FIXME")
PLACEHOLDER_RE = re.compile(r"【[^】\n]*】")
ABS_RE = re.compile(r"(?:[A-Za-z]:\\\\?[Uu]sers\\\\?|/home/[A-Za-z0-9_.-]+/|[Cc]:/[Uu]sers/[A-Za-z0-9_.-]+/)")


def read(path):
    return io.open(path, encoding="utf-8", errors="replace").read()


def parse_fm(text):
    """返回 (ok, fm_text, body_start, problems)。fm_text 为 None 表示无 front matter。"""
    probs = []
    if not text.startswith("---"):
        return None, None, 0, ["FM_MISSING"]
    nl = text.find("\n")
    if nl == -1:
        return None, None, 0, ["FM_MISSING"]
    end = text.find("\n---", nl)
    if end == -1:
        return None, None, 0, ["FM_UNCLOSED"]
    fm_text = text[nl + 1:end + 1]
    body_start = text.find("\n", end + 1) + 1
    return True, fm_text, body_start, probs


def top_keys(fm_text):
    """只取顶层键（无缩进），返回 {key: 值文本}；值文本含后续缩进行。"""
    out, cur, buf = {}, None, []
    for line in fm_text.split("\n"):
        m = re.match(r"^([A-Za-z][A-Za-z0-9_-]*):(.*)$", line)
        if m and not line.startswith((" ", "\t")):
            if cur is not None:
                out[cur] = "\n".join(buf).strip()
            cur, buf = m.group(1), [m.group(2)]
        elif cur is not None:
            buf.append(line)
    if cur is not None:
        out[cur] = "\n".join(buf).strip()
    return out


def strip_quotes(v):
    v = v.strip()
    if len(v) >= 2 and v[0] == v[-1] and v[0] in "\"'":
        return v[1:-1]
    return v


def check(skill_dir):
    skill_dir = os.path.abspath(skill_dir)
    findings = []
    meta = {"skill": skill_dir, "folder": os.path.basename(skill_dir)}

    def fail(code, detail):
        findings.append(("FAIL", code, detail))

    def warn(code, detail):
        findings.append(("WARN", code, detail))

    def info(code, detail):
        findings.append(("INFO", code, detail))

    sk = os.path.join(skill_dir, "SKILL.md")
    if not os.path.isfile(sk):
        fail("MISSING_SKILL_MD", "技能根下没有 SKILL.md")
        return meta, findings
    text = read(sk)
    ok, fm_text, body_start, fm_probs = parse_fm(text)
    for p in fm_probs:
        fail(p, "front matter 分隔符或内容不对")
    if not ok:
        return meta, findings

    keys = top_keys(fm_text)
    body = text[body_start:]
    meta["fm_keys"] = sorted(keys)
    meta["name"] = strip_quotes(keys.get("name", ""))
    meta["desc_chars"] = len(strip_quotes(keys.get("description", "")))
    meta["total_lines"] = len(text.splitlines())
    meta["body_lines"] = len(body.splitlines())

    if "name" not in keys or not meta["name"]:
        fail("NAME_MISSING", "frontmatter 没有 name")
    elif not NAME_RE.match(meta["name"]):
        fail("NAME_BAD", "name=%r 不符 ^[a-z0-9][a-z0-9-]*$" % meta["name"])
    elif meta["name"] != meta["folder"]:
        fail("NAME_DIR_MISMATCH", '文件夹 "%s" != name "%s"' % (meta["folder"], meta["name"]))

    if "description" not in keys:
        fail("DESC_MISSING", "frontmatter 没有 description")
    else:
        raw = keys["description"]
        if raw == "" or raw.lstrip() in ("|", ">", "|-", ">-", ">+"):
            fail("DESC_MULTILINE", "description 写成了 YAML 多行/折叠标量，必须是单行")
        elif "\n" in raw:
            fail("DESC_MULTILINE", "description 跨行（值后仍有缩进续行）")
        desc = strip_quotes(raw)
        if not desc:
            fail("DESC_MISSING", "description 为空")
        elif len(desc) > DESC_MAX:
            fail("DESC_TOO_LONG", "%d 字符 > %d" % (len(desc), DESC_MAX))
        else:
            meta["desc_chars"] = len(desc)

    if meta["body_lines"] > BODY_MAX:
        warn("BODY_OVER_500", "正文 %d 行 > %d，把细节移到 references/" % (meta["body_lines"], BODY_MAX))

    known = {"name", "description", "when_to_use", "argument-hint", "arguments",
             "model", "tools", "allowed-tools", "license", "version", "metadata"}
    for k in sorted(set(keys) - known):
        info("UNKNOWN_FM_KEY", "frontmatter 出现未文档化字段 %s" % k)

    if TODO_RE.search(text):
        bad = [m.group(0) for m in TODO_RE.finditer(text)][:4]
        fail("TODO_RESIDUE", "残留占位/待办：%s" % "、".join(bad))
    if PLACEHOLDER_RE.search(text):
        fail("PLACEHOLDER_RESIDUE", "正文或 frontmatter 残留【】占位 %d 处" % len(PLACEHOLDER_RE.findall(text)))

    for entry in sorted(os.listdir(skill_dir)):
        p = os.path.join(skill_dir, entry)
        if os.path.isdir(p):
            if not os.listdir(p):
                warn("EMPTY_DIR", "空目录 %s（不该建占位目录）" % entry)
            continue
        if entry.lower() in EXTRANEOUS:
            fail("EXTRANEOUS_DOC", "%s 属于附属文档，技能只保留干活需要的文件" % entry)
        if entry.lower().endswith(".md") and entry != "SKILL.md" and os.path.dirname(p) == skill_dir:
            warn("ROOT_EXTRA_MD", "技能根下的额外 .md：%s（确认不是 README 类附属文档）" % entry)

    refs = {t for t in RES_PREFIX.findall(text) if t.lower().endswith(RES_EXTS)}
    for rel in sorted(refs):
        if not os.path.isfile(os.path.join(skill_dir, rel.replace("/", os.sep))):
            fail("BROKEN_REF", "SKILL.md 引用了不存在的 %s" % rel)

    scripts = []
    sdir = os.path.join(skill_dir, "scripts")
    if os.path.isdir(sdir):
        for dp, dns, fns in os.walk(sdir):
            dns[:] = [d for d in dns if d not in ("__pycache__", "node_modules") and not d.startswith(".")]
            for f in fns:
                if not f.startswith("."):
                    scripts.append(os.path.join(dp, f))
        for fp in scripts:
            if not os.path.isfile(fp):
                continue
            t = read(fp)
            if fp.endswith(".py") and "__main__" not in t and "argparse" not in t:
                warn("SCRIPT_NO_MAIN", "scripts/%s 没有可执行入口" % os.path.basename(fp))
        joined = "\n".join(read(fp) for fp in scripts if os.path.isfile(fp))
        if not re.search(r"selftest", joined, re.I):
            warn("NO_SELFTEST", "scripts/ 里没有任何自证模式：判据必须先能红")

    mentions_run = bool(re.search(r"python scripts/|bash scripts/|powershell|\.ps1", text))
    allowed = strip_quotes(keys.get("allowed-tools", "")) or strip_quotes(keys.get("tools", ""))
    if allowed and mentions_run:
        tokens = {x.strip().lower() for x in allowed.replace(";", ",").split(",") if x.strip()}
        if not tokens & {"bash", "shell", "run", "*"}:
            warn("NARROW_ALLOWED_TOOLS", 'allowed-tools=%r 不含执行工具，技能里却要求跑脚本' % allowed)
        if tokens & {"*"}:
            warn("WIDE_ALLOWED_TOOLS", "allowed-tools 为通配，考虑收窄")

    # 机器路径只扫自己写的部分：SKILL.md + scripts/ + references/。
    # assets/ 是载荷（模板、vendored 上游文件），里面出现别人的路径不是本技能的缺陷。
    for path, dirs, files in os.walk(skill_dir):
        dirs[:] = [d for d in dirs if d not in (".git", "__pycache__", "node_modules")]
        rel_dir = os.path.relpath(path, skill_dir).replace(os.sep, "/")
        if rel_dir.startswith("assets"):
            continue
        for f in files:
            if not f.endswith((".md", ".py", ".ps1", ".sh", ".yaml", ".yml", ".json")):
                continue
            p = os.path.join(path, f)
            if os.path.getsize(p) > 2 * 1024 * 1024:
                continue
            t = read(p)
            hits = set(ABS_RE.findall(t))
            if hits:
                warn("ABS_PATH_LEAK", "%s 含机器特定路径：%s" % (
                    os.path.relpath(p, skill_dir).replace(os.sep, "/"), "、".join(sorted(hits))[:120]))

    return meta, findings


def report(meta, findings, as_json=False):
    fails = [x for x in findings if x[0] == "FAIL"]
    warns = [x for x in findings if x[0] == "WARN"]
    infos = [x for x in findings if x[0] == "INFO"]
    if as_json:
        print(json.dumps({"meta": meta, "findings": findings}, ensure_ascii=False, indent=1))
    else:
        print("skill=%s" % meta["skill"])
        print("folder=%s name=%s total_lines=%s body_lines=%s desc_chars=%s fm_keys=%s" % (
            meta.get("folder"), meta.get("name"), meta.get("total_lines"), meta.get("body_lines"),
            meta.get("desc_chars"), ",".join(meta.get("fm_keys", []))))
        print("findings=%d fail=%d warn=%d info=%d" % (len(findings), len(fails), len(warns), len(infos)))
        for sev, code, detail in findings:
            print("%s %s :: %s" % (sev, code, detail))
    return len(fails), len(warns)


def make_clean(root):
    d = os.path.join(root, "clean-sample")
    os.makedirs(os.path.join(d, "scripts"), exist_ok=True)
    io.open(os.path.join(d, "SKILL.md"), "w", encoding="utf-8", newline="\n").write(
        "---\nname: clean-sample\ndescription: 校验器用的干净样本，说明何时触发。\n---\n\n"
        "# Clean Sample\n\n跑 `python scripts/probe.py --selftest` 验证。\n"
        "- `references/notes.md` 是契约。\n")
    io.open(os.path.join(d, "scripts", "probe.py"), "w", encoding="utf-8", newline="\n").write(
        "import sys\n\n\ndef main(argv):\n"
        "    if '--selftest' in argv:\n        print('SELFTEST_PASS')\n        return 0\n"
        "    print('ok')\n    return 0\n\n\nif __name__ == '__main__':\n    sys.exit(main(sys.argv[1:]))\n")
    os.makedirs(os.path.join(d, "references"), exist_ok=True)
    io.open(os.path.join(d, "references", "notes.md"), "w", encoding="utf-8", newline="\n").write("# 契约\n")
    return d


def make_broken(root):
    """注入 6 类已知缺陷：目录名不符 / 多行 description / TODO 残留 / README 附属文档 / 引用断链 / 空目录。"""
    d = os.path.join(root, "Broken_Skill")
    os.makedirs(os.path.join(d, "scripts"), exist_ok=True)
    os.makedirs(os.path.join(d, "assets"), exist_ok=True)
    io.open(os.path.join(d, "SKILL.md"), "w", encoding="utf-8", newline="\n").write(
        "---\nname: broken-skill\ndescription:\n  这是一个多行 description，\n  校验器必须判它 FAIL。\n---\n\n"
        "# Broken\n\nTODO: 这里没写完。\n\n见 `scripts/gone.py` 与 `references/gone.md`。\n")
    io.open(os.path.join(d, "scripts", "gone.py"), "w", encoding="utf-8", newline="\n").write(
        "print('x')\n")
    io.open(os.path.join(d, "README.md"), "w", encoding="utf-8", newline="\n").write("安装说明\n")
    return d


def selftest():
    root = tempfile.mkdtemp(prefix="skillcheck-")
    expected = {
        "clean-sample": set(),
        "Broken_Skill": {"NAME_DIR_MISMATCH", "DESC_MULTILINE", "TODO_RESIDUE",
                         "EXTRANEOUS_DOC", "BROKEN_REF", "EMPTY_DIR", "SCRIPT_NO_MAIN"},
    }
    ok = True
    results = {}
    try:
        clean = make_clean(root)
        broken = make_broken(root)
        for path, name in ((clean, "clean-sample"), (broken, "Broken_Skill")):
            meta, findings = check(path)
            codes = {c for _, c, _ in findings}
            fails = {c for s, c, _ in findings if s == "FAIL"}
            results[name] = codes
            want = expected[name]
            if name == "clean-sample":
                blockers = fails | {c for s, c, _ in findings if s == "WARN"}
                if blockers:
                    ok = False
                    print("SELFTEST_FAIL 干净样本被判出缺陷：%s" % "、".join(sorted(blockers)))
                    for sev, code, detail in findings:
                        print("  %s %s :: %s" % (sev, code, detail))
            else:
                miss = want - codes
                if miss:
                    ok = False
                    print("SELFTEST_FAIL 注入的缺陷没被命中：%s" % "、".join(sorted(miss)))
                    print("  实际命中：%s" % "、".join(sorted(codes)))
        print("SAMPLES=" + " ".join("%s:%d" % (k, len(v)) for k, v in sorted(results.items())))
        print("SELFTEST_PASS" if ok else "SELFTEST_INVALID")
    finally:
        if not ok:
            print("保留样本目录供排查：%s" % root)
        else:
            shutil.rmtree(root, ignore_errors=True)
    return 0 if ok else 1


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("skill_dir", nargs="?")
    ap.add_argument("--selftest", action="store_true")
    ap.add_argument("--strict", action="store_true", help="WARN 也计为失败")
    ap.add_argument("--json", action="store_true")
    a = ap.parse_args()

    if a.selftest:
        sys.exit(selftest())
    if not a.skill_dir:
        print("用法：validate_skill.py <技能目录> [--strict] [--json] 或 --selftest")
        sys.exit(2)
    if not os.path.isdir(os.path.abspath(a.skill_dir)):
        print("FAIL 目录不存在：%s" % a.skill_dir)
        sys.exit(2)

    meta, findings = check(a.skill_dir)
    n_fail, n_warn = report(meta, findings, a.json)
    bad = n_fail + (n_warn if a.strict else 0)
    sys.exit(1 if bad else 0)


if __name__ == "__main__":
    main()
