# 坑位清单:症状 → 真因 → 处置

按"什么时候会撞上"排序。改行为前先查对应条目。

---

## ① `-webkit-app-region: drag` 会把右键交给操作系统

**症状**:拖拽带的右键弹出深色系统菜单(还原/移动/大小/最大化/最小化/关闭 六项),与应用主题完全不搭,应用侧关不掉、条目也改不了。

**真因**:Chromium 对该属性返回 `HTCAPTION`,右键落进 `DefWindowProc`。`win.setMenuBarVisibility(false)`、`autoHideMenuBar`、`Menu.setApplicationMenu(null)` 都无效 —— 那个菜单不是菜单;`frame: false` 上的 `minimizable / maximizable / resizable` 也只能把条目置灰,改不了条目集合。

**处置**:整条撤掉 `-webkit-app-region`(连 `no-drag` 一起,免得日后误读),改自绘拖拽 + 自绘控制条。**代价要当面说清**:放弃 OS 拖拽 = 放弃 Aero Snap(拖到屏幕左右边缘的分屏吸附)、拖到顶边的最大化要靠我们自己补(已补)、OS 的"最大化窗口拖下来自动还原"也要自己补。

---

## ② 位移必须主进程现读光标,不能渲染层传坐标

渲染层每次 `pointermove` 把 `clientX/clientY` 发过去,主进程按增量搬窗 ⇒ **IPC 往返延迟被算进位移**,手感是"窗口追光标",而且丢一帧就永久错位。

正确做法:`pointerdown` 报一次"起手"(主进程记下 `窗口原点 + 光标原点`),之后每帧只报"动了一下",主进程用 `screen.getCursorScreenPoint()` 现读实时光标算 `目标 = 窗口原点 + (实时光标 - 光标原点)`。这样不累计误差,丢帧也只是下一帧补上。

**顺带的硬约束**:`drag*` 三条都走 `ipcRenderer.send`(不是 `invoke`)—— `pointermove` 是几十 Hz 的高频源,不需要每帧一个回执 promise。preload 里 `dragStart/dragMove/dragEnd` **必须是零参数**;一旦带参数,就等于把坐标重新引回来了。

---

## ③ 单击不能把最大化的窗口拽下来

**症状**:双击标题带最大化后,**再点一下**(不是双击)就恢复了。

**真因**:`drag-start` 一到就 `unmaximize()`。Windows 的语义是"位移超过起拖阈值才算拖拽",`SM_CXDRAG` 在 100% 缩放下约 **4px**。

**处置**:`drag-start` 只记 `wasMax` 不动窗口;`drag-move` 里 `Math.abs(cx-a.cx) >= 4 || Math.abs(cy-a.cy) >= 4` 成立才还原,还原后按新 bounds 重新锚定。不要把"起手就还原"当成预期行为写进代码或文档。

---

## ④ `setPointerCapture` 可能抛,必须兜住

窗口跟着光标跑,光标很容易甩出窗口 ⇒ 不捕获指针就收不到后续 `pointermove`,窗口停在半路。但捕获在 pointerId 无效或已释放时会抛 `InvalidPointerId`。

**处置**:`try { el.setPointerCapture?.(e.pointerId) } catch {}`。捕获失败不影响拖拽主链,下一次 `pointermove` 照样能把窗口带回来。

---

## ⑤ 恢复时不要反向播退场动画

**想法**:"从任务栏点回来"应该反向播一遍。

**问题**:restore 之后窗口已经可见,反向播会先把它压成 `scale .64 / opacity .09` 的缩小半透明态,观感是闪一下,与系统"恢复即时到位"不一致。

**处置**:挂 `.window--noanim`(`transition: none`)→ 摘 `.window--minimizing` → 强制回流 → 摘 `.window--noanim`。恢复即时到位,和系统原本行为一致。

**强制回流那一步不能省**:`void root.offsetWidth`。不挡这一下,加类和摘类会被合并进同一帧,`transition` 照样播 —— 这正好是"看着代码对但效果没有"的经典形态。

---

## ⑥ `transitionend` 是主路,兜底计时器不是可选

退场动画播完再真的 `minimize()`。但两种情况下事件永远不来:

1. 系统开了"减少动态效果" ⇒ CSS 是 `transition: none`;
2. 事件被吞(切窗、层被移除)。

后果不是"少一段动画",而是**那颗按钮变成点了没反应**。所以 `setTimeout(go, 400)` 这类兜底必须留,并用 `done` 标志保证只走一次。

同时要筛 `e.target === root && e.propertyName === 'opacity'` —— 子树里任何一条 transition 结束都会冒泡上来,不筛就会在动画播到一半时提前最小化。

---

## ⑦ frameless 窗口拿不到 OS 的最小化动画

**症状**:`win.minimize()` 直接消失,没有"缩进任务栏"那段。

**真因**:Chromium 不给 frameless 窗口挂 `WS_SYSMENU`,系统那条最小化动画只出现在 `frame: true` 的窗口上。注册表动画开关(`MinAnimate`/`EnableAnimations`)开不开都一样。

**结论**:frameless 应用只能自绘退场。好处是自绘的那条 `transition` 不读任何系统渲染路径,**Win10 与 Win11 表现一致**;代价是收拢点只能瞄窗口自己的底边中心,不瞄任务栏按钮(按钮位置要按系统版本/DPI/任务栏停靠边推,那正是不一致的来源)。`Win+D`、任务栏点击触发的最小化走 OS 路径,**应用拦不到,所以确实没有动画** —— 这是已声明的未覆盖范围,不是 bug。

---

## ⑧ "帧率太低"多半是曲线用错了

**症状**:退场动画看着一格一格。

**真因**:把入场用的强减速曲线拿去做退场 —— 85% 左右的行程会挤进开头那几十毫秒,后面全是肉眼分不出的尾;同时整窗按新缩放逐帧重栅,里面有 WebGL 或 backdrop blur 时更明显。

**处置**:退场用**强加速** `cubic-bezier(.4,0,1,1)`,并只在退场期间挂 `will-change: transform, opacity`。常驻 `will-change` 会一直占一份层纹理。

---

## ⑨ `position: fixed` 必须挂在包裹层,不能挂在菜单本身

控制条的入场动效是在包裹层自己的元素上做 `translate/scale`。若 `fixed` 留在 `.win-menu` 上,包裹层一带 transform 就成了 fixed 的包含块 ⇒ **菜单位置跟着位移抖 8px**。

同理:控制条要 `createPortal` 到 `document.body`。活在拖拽带的层叠上下文里时,祖先的 `transform` / `backdrop-filter` / `overflow` 会把它裁掉或压住。

---

## ⑩ clamp 用的盒尺寸常量与 CSS 是同一笔账的两半

`WIN_MENU_W/H` 用来把菜单夹回屏幕内。它是 `2*padding + 3*btnW + 2*gap` / `2*padding + btnH` —— 改 CSS 里任何一项(按钮 34、内边距 5、间距 4)都必须同步改常量,否则贴屏幕边缘右键时菜单画到屏幕外。

---

## ⑪ 控制条不会因为"刚打开就被自己关掉"而失效 —— 别去"修"它

关闭逻辑监听 `pointerdown`(捕获相)+ `Escape` + `blur`。右键自己那一下发生在监听器挂上**之前**,所以不会立刻关掉刚开的菜单。如果哪天有人把它"优化"成监听 `click`,就会变成"菜单点不开"。

---

## ⑫ 失焦必须结束拖拽

`window.addEventListener('blur', endDrag)`。少了它:拖到一半切到别的程序,回来再动一下鼠标,窗口会跟着光标**隐形地**搬走。`pointercancel` 同理。

---

## ⑬ DPI 与阈值单位

`screen.getCursorScreenPoint()` 返回 **DIP**,所以 `DRAG_SLOP = 4` 是 4 DIP,不要再乘 `devicePixelRatio`(125%/150% 缩放下乘了就变成"要拖很久才动")。
`DRAG_GRAB_Y`(从最大化拖下来后光标落点)是物理布局像素,改标题带高度要同步改。

---

## ⑭ 多窗口共享一个拖拽锚点

资产里的 `dragAnchor` 是模块级单槽:同一时刻只跟一次拖拽。单窗口应用无所谓;真要多窗口并发拖,把它改成 `Map<webContents.id, DragAnchor>`,并把 `attachWindowChrome(win)` 的调用保持在每个窗口上各一次(IPC handler 用 `BrowserWindow.fromWebContents(e.sender)` 解析,本来就是按发送者定位窗口的)。

---

## ⑮ sandbox 下 preload 的形态

`sandbox: true` 时 preload 必须是 **CJS**,且只能拿到 `electron`,没有 `fs/path/node:*`。所以 `preload-window-chrome.ts` 里不掺业务 IPC,只暴露这 7 个方法。已有 `exposeInMainWorld('api', …)` 的工程要把字段**并进现有对象**,不要新开第二个全局名。

---

## ⑯ 纯浏览器 dev(Vite 直开)要能整块降级

`window.api` 在浏览器里是 `undefined` ⇒ 组件必须 `if (!api || !menuAt) return null`,各 effect 内再各自 `if (!api) return`。否则纯 Web 预览直接白屏,而且报错点在无关的地方。
