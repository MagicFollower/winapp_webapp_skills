---
name: rev-pipeline
description: Orchestrator and router for the rev-* mobile/native reverse-engineering skill suite. Maps a task to the right stage (acquire/dump → IDA load & script → symbol/struct recovery → Frida/Unicorn dynamic analysis) and hands each stage's artifacts to the next sub-skill. Activate when the user starts a reverse-engineering job on an APK/IPA/Unity IL2CPP/stripped native binary, is unsure which rev-* skill to use, wants the end-to-end pipeline, or asks how one stage's output feeds the next.
when_to_use: Reverse-engineering tasks that span multiple rev-* skills, or when the user needs to pick the right sub-skill for the stage they are at.
---

# rev-pipeline — Reverse-Engineering Pipeline Router
# 逆向流水线编排路由器

This skill does not perform analysis itself. It decides **which `rev-*` sub-skill to invoke**, in **what order**, and **what artifact to carry between them**. Read the target's stage, route to the matching sub-skill, then follow the handoff map to the next stage.

本技能不做具体分析，只负责判断当前处于哪一阶段、调用哪个 `rev-*` 子技能、以及把上一阶段的产物交给下一阶段。

---

## 1. Sub-skill catalog / 子技能清单

| Skill | Stage | Use it to | Key artifact out |
|-------|-------|-----------|------------------|
| `rev-ios-dump` | 0 Acquire | Decrypt (砸壳) an iOS app from a jailbroken device via frida-ios-dump | decrypted `.ipa` / Mach-O (`cryptid 0`) |
| `rev-dex-dumper` | 0 Acquire | Dump real DEX from a running, packed Android app via bundled `panda-dex-dumper` (needs root/ptrace) | `panda/*.dex` pulled to host |
| `rev-u3d-dump` | 0 Acquire | Extract Unity IL2CPP symbols from `global-metadata.dat` + native binary | `script.json`, `dump.cs`, `il2cpp.h`, `ida_py3.py` |
| `rev-idapython` | 1 Load & script | Write IDAPython (in-GUI) / IDALib (headless) scripts: batch decompile, pattern search, xrefs, types, breakpoints | IDB enrichment, decompiled JSON |
| `rev-symbol` | 2 Recover | Recover names of stripped functions from constants, strings, call patterns, xrefs, web search | suggested symbol names + confidence |
| `rev-struct` | 2 Recover | Reconstruct data structures from memory-access patterns across callers/callees | `struct` definition + field offsets |
| `rev-frida` | 3 Dynamic | Generate modern-Frida-API hook/trace scripts (native/ObjC/Java), loader-aware | runtime traces, dumped memory, confirmed hypotheses |
| `rev-unicorn-debug` | 3 Dynamic | Emulate an isolated function/fragment in Unicorn, simulating JNI/syscall/libc deps | algorithm decrypted/decoded without full env |

`rev-symbol` and `rev-struct` both consume the **IDA-NO-MCP** export (`decompile/`, `strings.txt`, `imports.txt`, `exports.txt`, `memory/`) or a live **IDA Pro MCP** connection. See §4.

---

## 2. Pipeline overview / 流水线全貌

```
STAGE 0  Acquire target            STAGE 1  Load & script       STAGE 2  Recover        STAGE 3  Dynamic
(choose by platform)               (in IDA/Ghidra)              (stripped binary)       (verify / emulate)
                                                                 
 iOS      → rev-ios-dump  ─┐                                     
 Android  → rev-dex-dumper ┼─→ binary ─→ rev-idapython ─→ IDA-NO-MCP ─→ rev-symbol ─┐─→ rev-frida
 Unity    → rev-u3d-dump  ─┘   (+ il2cpp.h/script.json)  export       rev-struct  ─┘  rev-unicorn-debug
```

The stages are a **default order, not a strict chain** — enter at whichever stage matches what the user already has. Skip Stage 0 if the binary is already decrypted; skip Stage 2 if symbols are intact.

各阶段是默认顺序而非强制链——按用户手上已有的产物决定从哪一阶段切入。已脱壳则跳过 Stage 0，符号完整则跳过 Stage 2。

---

## 3. Routing decision tree / 路由决策树

Start here. Answer the first question that matches, then invoke the named skill.

从这开始，命中第一条即调用对应技能。

**A. What does the user already hold? / 用户手上有什么？**

- **Nothing but a device + app** → Stage 0. Pick by platform:
  - iOS (jailbroken, App Store app encrypted) → **`rev-ios-dump`**
  - Android (packed app, real DEX only in memory) → **`rev-dex-dumper`**
  - Unity game (IL2CPP, `global-metadata.dat` present) → **`rev-u3d-dump`**
- **A decrypted/unpacked binary (Mach-O / ELF / DEX), unanalyzed** → Stage 1: **`rev-idapython`** (load, batch-decompile, search), then export for Stage 2.
- **IDA-NO-MCP export dir or live IDA MCP, functions named `sub_XXXX`** → Stage 2:
  - need to know **what a function is/does** → **`rev-symbol`**
  - need to know **the layout of a struct/object** → **`rev-struct`**
- **Need to confirm behavior at runtime / trace args / bypass a check** → Stage 3: **`rev-frida`**
- **Need to run one function (e.g. a decrypt routine) without the whole app/env** → Stage 3: **`rev-unicorn-debug`**

**B. Special signals / 特殊信号**

- "脱壳 / 砸壳 / unpack / dump DEX / decrypt IPA" → Stage 0 (platform-specific).
- "Unity / IL2CPP / global-metadata / dump.cs / script.json" → **`rev-u3d-dump`**, then import into IDA (§4).
- "符号 / 函数名 / 这是什么函数 / stripped / sub_ / magic number" → **`rev-symbol`**.
- "结构体 / 字段偏移 / vtable / this + 0x18 / struct" → **`rev-struct`**.
- "hook / trace / Frida 脚本 / 运行时 / 参数 / 返回值" → **`rev-frida`**.
- "模拟执行 / emulate / Unicorn / 脱离 JNI 环境 / 只跑这个函数" → **`rev-unicorn-debug`**.
- "IDAPython / IDALib / headless / 批量反编译 / IDB 脚本" → **`rev-idapython`**.

---

## 4. Handoff map — what feeds what / 产物流转

Carry the artifact forward; do not re-derive it.

把产物往下带，不要重复推导。

- **`rev-ios-dump` → Stage 1**: decrypted Mach-O into IDA/Ghidra/Hopper. Then `class-dump` ObjC headers; feed interesting funcs to `rev-symbol`.
- **`rev-dex-dumper` → DEX analysis**: pulled `*.dex` go to a DEX tool (jadx/IDA). For native libs inside the same APK, treat as ELF → Stage 1.
- **`rev-u3d-dump` → `rev-idapython`**: run `ida_py3.py` against `script.json` to auto-rename functions; parse `il2cpp.h` (`File → Load file → Parse C header`) for types. `dump.cs` field offsets (`// 0x20`) are the base-relative offsets to inspect at runtime with **`rev-frida`**.
- **`rev-idapython` → `rev-symbol`/`rev-struct`**: export via IDA-NO-MCP (`Ctrl-Shift-E`) producing `decompile/*.c` (each with `callers:`/`callees:` metadata), `strings.txt`, `imports.txt`, `exports.txt`, `memory/`. Both Stage-2 skills read this layout; prefer a live **IDA Pro MCP** connection when available (no export needed).
- **`rev-symbol`/`rev-struct` → `rev-frida`/`rev-unicorn-debug`**: recovered names + struct offsets let you hook the *right* function and read the *right* field. Confirm a static hypothesis dynamically with `rev-frida`; isolate and emulate a single recovered routine with `rev-unicorn-debug`.
- **`rev-frida` ↔ `rev-unicorn-debug`**: Frida traces on a live process reveal the target and its inputs; feed those bytes/registers into Unicorn to emulate the fragment offline (e.g. reproduce a decryption without the app's environment).

---

## 5. Router rules / 路由规矩

1. **Route, don't inline.** Invoke the named sub-skill and follow its own SKILL.md; do not copy its commands into this conversation. Its details are authoritative.
   只负责路由，不要把子技能的命令抄进来——以子技能自己的 SKILL.md 为准。
2. **One stage at a time.** Invoke the single best-matching sub-skill for the current stage. Chain to the next stage only after its artifact exists.
3. **Enter where the user is.** If the user already holds a decrypted binary or an IDA export, skip ahead (§3.A). Never re-run Stage 0 on an already-unpacked target.
4. **State the handoff.** When finishing a stage, tell the user which artifact was produced and which skill consumes it next (§4).
5. **Prefer MCP over export** for Stage 2 when an IDA Pro MCP connection is live; fall back to IDA-NO-MCP export otherwise.
6. **Dynamic before blind static guessing.** When a static inference (`rev-symbol`/`rev-struct`) is low-confidence and the process is runnable, verify with `rev-frida` rather than guessing further.

---

## 6. Full-pipeline example / 完整流水线示例

> Task: reverse a packed Unity IL2CPP Android game to recover its save-decryption routine.
> 任务：逆向一个加壳的 Unity IL2CPP 安卓游戏，还原其存档解密函数。

1. **Stage 0** — `rev-dex-dumper` if the Java layer is packed; `rev-u3d-dump` on `libil2cpp.so` + `global-metadata.dat` → `script.json`, `dump.cs`, `il2cpp.h`.
2. **Stage 1** — load `libil2cpp.so` in IDA, run `ida_py3.py` (auto-rename from `script.json`), parse `il2cpp.h`; use `rev-idapython` to batch-decompile and grep strings for "save"/"crypt". IDA-NO-MCP export (`Ctrl-Shift-E`).
3. **Stage 2** — `rev-symbol` on the `sub_XXXX` candidates (magic numbers → AES/MD5?); `rev-struct` on the save-buffer object to map field offsets.
4. **Stage 3** — `rev-frida` to hook the recovered decrypt function live and capture real key/IV/args; if the function needs JNI/libc it can't run standalone, `rev-unicorn-debug` to emulate it offline with hooked dependencies and reproduce the decryption.

Each step consumes the previous step's artifact — see §4.
