---
name: electron-window-chrome
description: 给 Electron + React 的 frameless(transparent / frame:false)应用一次装好自绘窗口壳:自绘拖拽(位移由主进程现读光标算)、拖拽带上右键浮出「最小化/最大化/关闭」三项浅色控制条、自绘最小化退场动画(Win10/Win11 表现一致)。当任务是去掉 -webkit-app-region 让右键归 DOM 管、弹出了不该有的深色系统菜单、窗口拖起来跟手差/瞬移、双击最大化后单击就恢复、frameless 最小化没有动画、或要把这套壳迁到另一个 Electron 工程时使用。附 5 个可直接拷入目标工程的源文件(assets/)与失效清单(references/);按步骤落位接线即完成,不含验证环节。
---

# Electron 自绘窗口壳

## Overview

三件事一起才能成立,所以它们必须作为一个包落地:

| 能力 | 做法 | 为什么不能用 OS 的 |
| --- | --- | --- |
| 拖拽 | `pointerdown` 起手 → IPC 报"动了一下" → 主进程 `screen.getCursorScreenPoint()` 现读光标搬窗 | `-webkit-app-region: drag` 会把右键一并交给 OS |
| 右键三项控制条 | `document` 上吃 `contextmenu`,`createPortal` 到 body 画浅色控制条 | OS 那个深色菜单只能置灰条目,改不了条目集合,也关不掉 |
| 最小化退场 | 给窗口根元素挂一条 `transition: transform/opacity` | Chromium 不给 frameless 窗口挂 `WS_SYSMENU`,系统不播最小化动画 |

**共同契约**(整套资产靠这三条对齐,任一条破了就表现为"点了没反应"而不是报错):

1. CSS 里**不存在** `-webkit-app-region`(连 `no-drag` 也删,类只留给 JS 读)。
2. 可拖的元素标 `.drag-region`,其内的按钮/输入框标 `.no-drag`。
3. `window.api` 暴露 7 个方法:`minimize / maximize / close / dragStart / dragMove / dragEnd / onRestore`,前 6 个零参数。

## 何时使用

- 新 frameless 应用要自绘标题带/窗口壳。
- 现有工程用了 `-webkit-app-region: drag`,右键弹系统深色菜单,与界面不协调且关不掉。
- 拖拽手感有问题:窗口追光标、拖到一半停在半路、贴边行为不对。
- 双击最大化之后**单击**就恢复(见 pitfalls ③)。
- frameless 窗口 `win.minimize()` 直接消失,想要"缩下去"那段动画,且要求 Win10/Win11 一致。
- 退场动画被反馈"帧率太低"。

不适用:需要 OS Aero Snap 左右分屏吸附(自绘拖拽拿不到,只能自己补顶边最大化那一条);macOS/Linux 的绿钮语义(资产按 Windows 语义写:双击切换最大化、贴顶卡住松手最大化)。

## 执行方式

- **一次成型**:按 Step 1 → Step 5 连续落完文件、接完主进程、preload、渲染层,核对完代码一致性后一次性交付。
- **不在写码过程中反复验证**:不跑审计脚本、不注入探针、不启动应用逐项对照,也不需要先证明一段再写下一段。本技能不提供验证环节;验收由宿主工程自己的流程负责。
- **资产即成品**:`assets/` 下 5 个文件逻辑完整,只改本文点名的接线点(落位路径、`DRAG_*` 常量、`rootSelector`、样式 `@import`),不要重写内部逻辑。

## 边界(落位前告知,不阻塞)

1. **放弃 OS 拖拽 = 放弃 Aero Snap**。左右边缘分屏吸附拿不到;顶边"卡住松手即最大化"可以自己实现(资产已实现)。
2. **OS 的最小化动画对 frameless 窗口不可得**。系统那条动画只挂在 `frame:true` 的窗口上,所以只能自绘;`Win+D` 和从任务栏点回来的那条路仍走 OS 路径,**没有动画**。
3. **自绘退场的收拢点瞄窗口自己的底边中心**,不是任务栏那颗按钮 —— 按钮位置要按系统版本/DPI/停靠边推,那正是 Win10 与 Win11 会不一致的地方。换来的是两端跑同一条 `transition`。

## 资源结构

```text
electron-window-chrome/
├── SKILL.md
├── assets/                            可直接拷进目标工程的示例源文件
│   ├── WindowChrome.tsx               渲染层组件(零第三方依赖,图标内联)
│   ├── window-chrome.css              拖拽带标记 + 退场动画 + 控制条(令牌全带默认值)
│   ├── window-chrome-main.ts          主进程模块:attachWindowChrome(win) + 三条控制 IPC + 拖拽三件套
│   ├── preload-window-chrome.ts       preload 要暴露的 7 个方法
│   └── window-api.d.ts                渲染层的 window.api 声明
└── references/
    └── pitfalls.md                    16 条失效:症状 → 真因 → 处置
```

**动手前先读 `references/pitfalls.md` 的 ①③⑦⑧** —— 这四条决定设计取舍,不知道就会走回头路。

## Step 1 — 落文件(骨架)

```bash
SKILL="$HOME/.qoder-cn/skills/electron-window-chrome"      # 或项目级 .qoder/skills/...
DST="."                                                     # 目标工程根
cp "$SKILL/assets/WindowChrome.tsx"        "$DST/src/components/WindowChrome.tsx"
cp "$SKILL/assets/window-chrome.css"       "$DST/src/styles/window-chrome.css"
cp "$SKILL/assets/window-chrome-main.ts"   "$DST/electron/window-chrome.ts"
cp "$SKILL/assets/preload-window-chrome.ts" "$DST/electron/preload-chrome.ts"
cp "$SKILL/assets/window-api.d.ts"         "$DST/src/window-api.d.ts"
```

目录名按目标工程的约定改(`src/renderer/`、`electron/main/` 都行),**文件名和内容不用改**。工程已有 `WindowChrome`/同名样式时把资产改名再落,别覆盖宿主的东西。

## Step 2 — 主进程接线

在窗口创建处调一次(多窗口各调一次;IPC handler 用 `BrowserWindow.fromWebContents(e.sender)` 定位,本来就是按发送者解析的):

```ts
import { attachWindowChrome } from './window-chrome';

const win = new BrowserWindow({
  width: 1100, height: 720,
  frame: false,            // 必须
  transparent: true,       // 必须:退场过程要露出真桌面,观感才与系统动画同向
  backgroundColor: '#00000000',
  webPreferences: { preload: preloadPath, sandbox: true, contextIsolation: true },
});
attachWindowChrome(win);
```

两个常量按宿主改:

- `DRAG_GRAB_Y`(默认 24)= 从最大化拖下来之后光标落在标题带的哪条横线 ⇒ 取标题带高度的视觉中心附近。
- `DRAG_SLOP`(默认 4)= 起拖阈值,对齐 Windows 的 `SM_CXDRAG`。**单位是 DIP,不要乘 `devicePixelRatio`**。

## Step 3 — preload 与类型

已有 `exposeInMainWorld('api', {...})` 的工程:把 `preload-chrome.ts` 里那 7 个字段**并进现有对象**,不要新开第二个全局名(两个键名会让渲染层不知该用哪个,`.d.ts` 也会打架)。没有的,直接保留该文件的 `exposeInMainWorld('api', chromeApi)`。

`sandbox: true` 时 preload 必须是 CJS 且只能拿 `electron` —— 所以这份片段里不掺业务 IPC。

`window-api.d.ts` 落到被 `tsconfig.include` 覆盖的位置即可,不需要 import。工程已有 `WindowApi` 就把 7 个成员并进那个接口。

## Step 4 — 渲染层:挂组件 + 标带

```tsx
import WindowChrome from './components/WindowChrome';

<div className="window">                        {/* 视觉上就是那张贴桌面的卡片;类名不同就传 rootSelector */}
  <header className="drag-region">              {/* 可起手拖 */}
    <input className="no-drag" />               {/* 带内控件必须反向标,否则点输入框会拖窗 */}
  </header>
  <WindowChrome />                              {/* 挂在根元素子树末尾即可,内部 portal 到 body */}
</div>
```

样式入口 `@import` 一次 `window-chrome.css`。它自带 `--chrome-*` 令牌默认值(含浅色/深色两套),宿主有主题变量就在 `:root` 覆盖那 9 个键,**不要改资产里的选择器名**(`.window` / `.window--minimizing` / `.win-menu*`)—— 改了要同步 `rootSelector` 与 CSS 两处。

退场两段的正确形状:

```css
.window { transition: transform var(--chrome-t) var(--chrome-ease-exit), opacity ... }
.window--minimizing {
  transform: translateY(28px) scale(0.6);
  transform-origin: 50% 100%;          /* 底边中心,不是默认 50% 50% */
  opacity: 0;
  will-change: transform, opacity;     /* 只在退场期间挂;常驻会一直占一份层纹理 */
}
```

曲线必须是**强加速** `cubic-bezier(.4,0,1,1)`。把入场常用的强减速曲线拿去做退场,行程会挤进开头几十毫秒,观感就是"一帧一格 / 帧率太低"。

## Step 5 — 交付

代码一致性核对(不需要启动应用、不需要跑脚本):

- 5 个资产已落位,import 路径与 `tsconfig.include` 覆盖到 `.d.ts`。
- `BrowserWindow` 是 `frame:false` + `transparent:true`,`attachWindowChrome(win)` 已在每个窗口调过一次。
- preload 暴露 7 个方法;工程原有 `api` 对象时已并入,没有第二个全局名。
- 根元素类与组件的 `rootSelector` 一致,样式入口只 `@import` 一次。
- 拖拽带标 `.drag-region`,带内控件标 `.no-drag`;全工程无 `-webkit-app-region` 声明(含 `no-drag` 遗留)。
- 改过 `WIN_MENU_W/H` 或 CSS 的 `padding/gap/按钮尺寸` 时,两处已同步。
- `DRAG_GRAB_Y` 与标题带高度匹配,`DRAG_SLOP` 仍是 4 DIP。

写完全部文件后一次性交付,向用户说明落位路径与改动点即可。

## Resources

- `assets/` — 5 个直接拷入目标工程的示例源文件;通用化的关键是那三条契约,不含任何具体应用的业务。
- `references/pitfalls.md` — 16 条失效与对策。改行为前先查这里。
