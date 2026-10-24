# 设计令牌（Element Plus 2.14.7）

本文件是 Element Plus 设计层的唯一数值来源。所有页面颜色、字号、间距、圆角、阴影都从这里的令牌推导，**业务代码不写 hex、不写 px 字号、不写裸 `border-radius`**。

## 加载顺序

```js
// main.js —— 顺序不可换
import 'element-plus/dist/index.css';                 // 1. 组件基础样式
import 'element-plus/theme-chalk/dark/css-vars.css';  // 2. 暗色变量（只在 html.dark 生效）
import './styles/design-system.css';                  // 3. 本项目令牌与覆盖层
```

`design-system.css` 只做三件事：把 Element Plus 的默认尺寸归一到 4dp 子集、统一圆角档、给所有圆角元素加 G2 角形。它不改变组件结构，也不覆盖组件的布局方式。

> 使用按需导入时，每个组件的样式由 resolver 自动引入，但 `dark/css-vars.css` 与 `design-system.css` 仍要手动按上面的顺序导入。命令式 API 的组件样式（message / message-box / notification / loading）要在 `design-system.css` **之前**导入——这些文件会重新声明 `:root` 的 `--el-*` 默认值，晚到会把令牌重置。

## 色彩

### 语义色组

Element Plus 用 `type` 表达语义，每个 type 生成 8 个 CSS 变量：

| 组 | 变量 | 用途 |
|---|---|---|
| 主色 | `--el-color-primary` | 主操作、选中态、链接、进度 |
| 派生 | `--el-color-primary-light-3/5/7/8/9` | 悬停底色、禁用底、浅色提示、选中行 |
| 派生 | `--el-color-primary-dark-2` | 按下态、深色强调 |
| 成功 | `--el-color-success` + 同构派生 | 完成、已发布 |
| 警告 | `--el-color-warning` + 同构派生 | 待处理、额度将满 |
| 危险 | `--el-color-danger` + 同构派生 | 删除、不可恢复操作 |
| 错误 | `--el-color-error` + 同构派生 | 校验失败（与 danger 同值，语义不同） |
| 信息 | `--el-color-info` + 同构派生 | 中性提示、统计辅助 |

滚动条、遮罩、禁用态另有一组，不要拿来当主题色：

```
--el-color-white / --el-color-black
--el-mask-color / --el-mask-color-extra-light
--el-disabled-bg-color / --el-disabled-text-color / --el-disabled-border-color
```

### 自定义主色的正确做法

Element Plus 的浅色档是**编译期**按白/黑混合算好的。只改 `--el-color-primary` 会让 `light-*` 与 `dark-2` 停留在旧的蓝色上，悬停、选中行、禁用按钮会出现两套主色。

必须一次写全 7 个变量。用 `../assets/styles/theme.js` 生成：

```js
import { applyTheme } from './styles/theme.js';

applyTheme('#6750A4', false); // 主色 + 亮色模式
applyTheme('#6750A4', true);  // 主色 + 暗色模式
```

生成结果（`#6750A4` 亮色）：

| 变量 | 值 |
|---|---|
| `--el-color-primary` | `#6750a4` |
| `--el-color-primary-light-3` | `#9585bf` |
| `--el-color-primary-light-5` | `#b3a8d2` |
| `--el-color-primary-light-7` | `#d1cbe4` |
| `--el-color-primary-light-8` | `#e1dced` |
| `--el-color-primary-light-9` | `#f0eef6` |
| `--el-color-primary-dark-2` | `#524083` |

`applyPalette` 只写变量，`applyTheme` 额外切 `html.dark`。默认主色 `#409EFF` 生成值与 Element Plus 官方一致（`#79bbff / #a0cfff / #c6e2ff / #d9ecff / #ecf5ff / #337ecc`），可用来回归。

**暗色切换顺序**：先切 `html.dark`，再按 `mode: 'dark'` 重算变量。反过来会让暗色样式表被内联色覆盖，或者在暗色下仍用白底混合。

### 文本色

| 变量 | 亮色 | 用途 |
|---|---|---|
| `--el-text-color-primary` | `#303133` | 标题、正文主信息 |
| `--el-text-color-regular` | `#606266` | 常规正文 |
| `--el-text-color-secondary` | `#909399` | 辅助说明、时间戳 |
| `--el-text-color-placeholder` | `#a8abb2` | 占位符 |
| `--el-text-color-disabled` | `#c0c4cc` | 禁用文本 |

实测对比度（白底）：`primary` 13.02:1、`regular` 6.11:1、`secondary` 3.08:1、`placeholder` 2.30:1。因此 **`secondary` / `placeholder` 不承载正文或唯一信息**，只用于时间戳、辅助说明、占位提示；正文必须用 `primary` 或 `regular`。暗色下 `secondary` 对 `#141414` 为 5.98:1，可以承载正文。

### 背景、填充与描边

| 变量 | 亮色 | 用途 |
|---|---|---|
| `--el-bg-color` | `#ffffff` | 卡片、弹层、控件底 |
| `--el-bg-color-page` | `#f2f3f5` | 页面背景 |
| `--el-bg-color-overlay` | `#ffffff` | 下拉、Dialog、Popover |
| `--el-fill-color` | `#f0f2f5` | 表头、禁用块 |
| `--el-fill-color-light` | `#f5f7fa` | 斑马纹、hover 行 |
| `--el-fill-color-lighter` | `#fafafa` | 次级块 |
| `--el-fill-color-blank` | `#ffffff` | 空白底 |
| `--el-border-color` | `#dcdfe6` | 常规描边 |
| `--el-border-color-light` | `#e4e7ed` | 分隔线 |
| `--el-border-color-lighter` | `#ebeef5` | 表格行线 |
| `--el-border-color-extra-light` | `#f2f6fc` | 极浅分隔 |
| `--el-border-color-dark` | `#d4d7de` | 强调描边 |
| `--el-border-color-darker` | `#cdd0d6` | 输入框 hover |
| `--el-border-color-hover` | `var(--el-text-color-disabled)` | hover 边界 |

**配对规则**：文字只用文本色组，背景只用背景/填充色组，描边只用描边色组。不要拿 `--el-color-primary` 当正文色；不要把 `--el-fill-color-light` 当描边。

### 暗色

`html.dark` 下这四组值整体翻转：`--el-bg-color` 变 `#141414`，页面底变 `#0a0a0a`，弹层底变 `#1d1e1f`，文本主色变半透明白（约 `rgba(240,245,255,.95)` 合成后的实色）。派生色也按暗底重算。

暗色下不要做的事：

- 不要用 `#fff` 当卡片底（用 `var(--el-bg-color)`）。
- 不要用固定 `rgba(0,0,0,.1)` 当分隔线（用 `var(--el-border-color-lighter)`）。
- 不要给图片区写死浅灰占位（用 `--el-fill-color`）。

## 排版

字体栈在 `design-system.css` 定义为：

```css
--el-font-family: "Inter", "PingFang SC", "Microsoft YaHei", system-ui, sans-serif;
```

数字与拉丁字符用 Inter，中文回落到苹方/微软雅黑。Inter 需自行打包（`@fontsource/inter`）；未安装时回落到系统字体，字号与行高不变。

### 六级排版刻度

| 角色 | 类名 | 字号 / 行高 / 字重 | 用途 |
|---|---|---|---|
| Display | `.ds-display` | 32 / 40 / 700 | 登录页、空态首屏 |
| H1 | `.ds-h1` | 24 / 32 / 600 | 页面标题 |
| H2 | `.ds-h2` | 20 / 28 / 600 | 区块标题、Dialog 标题 |
| H3 | `.ds-h3` | 16 / 24 / 600 | 卡片标题、表单分组 |
| Body | `.ds-body` | 14 / 22 / 400 | 正文、表格、表单标签 |
| Caption | `.ds-caption` | 12 / 18 / 400 | 辅助说明、时间戳、表头注释 |

变量名为 `--text-<role>-size / -line / -weight`，例如 `--text-h2-size`。**不要在业务里写 `font-size: 15px`**：15 不在刻度上。

> Element Plus 自己的 `--el-font-size-*`（20/18/16/14/13/12）保持不变，供组件内部使用；页面排版用上表的 `.ds-*` 类或变量。13px 只出现在组件内部（small 尺寸），不作为段落字号。

### 行高与换行

- 正文行高 22px（约 1.57），表格单元格同样 22px；不要用 `line-height: 1`。
- 标题行高 ≥ 1.33；两行以上标题用 `max-width` 控制断行，不要用 `<br>`。
- 数字列右对齐，单位与数字同字号；金额用 `font-variant-numeric: tabular-nums`。
- 单行截断统一用 `.el-text--truncated` 或 `text-overflow: ellipsis`，不要用固定高度裁切。

## 间距

只允许 9 档（下表 8 个令牌 + 0），全部是 4 的倍数：

| 令牌 | 值 | 典型用途 |
|---|---|---|
| `--space-1` | 4px | 图标与文字、徽标内缩 |
| `--space-2` | 8px | 相邻按钮、标签与输入、紧凑行内间距 |
| `--space-3` | 12px | 卡片内小分组、表单项内上下 |
| `--space-4` | 16px | 卡片内边距、表单字段间距、网格 gap（紧凑） |
| `--space-6` | 24px | 页面边距（≥600）、区块间距、Dialog 内边距、网格 gap（展开） |
| `--space-8` | 32px | 大区块间距、空态上下留白 |
| `--space-12` | 48px | 页面首屏分节、触达高度 |
| `--space-16` | 64px | 营销区块、登录页留白 |

- 组件**内部**内边距用 4/8/12/16/24（设计层只覆盖 Card/Form/Dialog/Drawer/Table/Message/Notification；Element Plus 内部 15px、11px 这类实现值不改）；组件**之间**用 8/12/16/24；**区块之间**用 24/32/48。
- 用父级 `gap` 排版，不给组件写 `margin`。唯一例外是 Element Plus 自带的 `.el-button + .el-button { margin-left: 12px }`，用 `.ds-actions` 归零并改用 8px gap。
- 0 是合法值；不要用 1/2/3/5/6/10/14/18/20 这类非 4 倍数。

完整约束与场景表见 `layout-rules.md`。

## 圆角

Element Plus 默认半径是 `base: 4px / small: 2px / round: 20px / circle: 100%`。本设计层改为：

| 令牌 | 值 | 映射的 Element Plus 变量 | 典型组件 |
|---|---|---|---|
| `--radius-xs` | 4px | `--el-border-radius-small` | Tag、Pagination 页码、Badge 容器 |
| `--radius-sm` | 8px | `--el-border-radius-base` | Button、Input、Select、Card 内小块、Popover |
| `--radius-md` | 12px | `--el-card-border-radius` | Card |
| `--radius-lg` | 16px | `--el-dialog-border-radius`、`--el-messagebox-border-radius` | Dialog、MessageBox、Drawer 面板 |
| `--radius-xl` | 24px | — | 大面板、展示卡 |
| `--radius-pill` | 9999px | `--el-border-radius-round` | 药丸按钮、Switch、Tag round |
| `50%` | — | `--el-border-radius-circle` | **仅正圆**：Avatar、圆形按钮、单选框 |

- 半径决定**档位**，`corner-shape` 决定**曲率族**；两者必须同时存在。只写 `border-radius` 是 G1。
- 禁止用 `50%` 做扁条药丸：椭圆四角曲率不一致。扁条用 `9999px`。
- Element Plus 内部正圆（Avatar、圆形按钮、Radio 圆点、Switch 拇指、Slider 按钮等）已自动豁免 G2；自建正圆加 `data-g2-exempt`。
- 单组件覆盖优先用组件变量（`--el-card-border-radius`、`--el-dialog-border-radius`），不要直接写 `border-radius` 改库内组件。
- 嵌套圆角：内层半径 = 外层半径 − 内边距，且不小于 4px；否则内角会凸出。

G2 强制规则见 `corner-g2.md`。

## 阴影

Element Plus 的四档阴影（未改动）：

| 变量 | 值 | 用途 |
|---|---|---|
| `--el-box-shadow-lighter` | `0 0 6px rgba(0,0,0,.12)` | 轻浮起：Tooltip、小卡片 hover |
| `--el-box-shadow-light` | `0 0 12px rgba(0,0,0,.12)` | 常规浮层：Popover、Dropdown |
| `--el-box-shadow` | `0 12px 32px 4px rgba(0,0,0,.04), 0 8px 20px rgba(0,0,0,.08)` | 模态层：Dialog、Drawer |
| `--el-box-shadow-dark` | `0 16px 48px 16px rgba(0,0,0,.08), 0 12px 32px rgba(0,0,0,.12), 0 8px 16px -8px rgba(0,0,0,.16)` | 最高层 |

层阶规则：

| 层 | 表现 | 组件 |
|---|---|---|
| 0 | 无阴影 + 描边或填充 | 页面、Card、Table、Button |
| 1 | `--el-box-shadow-lighter` | 可点击 Card hover、Tooltip |
| 2 | `--el-box-shadow-light` | Menu、Dropdown、Popover、Select 下拉 |
| 3 | `--el-box-shadow` | Dialog、Drawer、MessageBox |
| 4 | `--el-box-shadow-dark` | 全屏对话框、图片预览 |

静置卡片不用阴影表达层级，用描边；hover 才浮起一级。**同一屏不超过两级阴影**。

### 卡片表面

| 令牌 | 值 | 用途 |
|---|---|---|
| `--ds-card-bg` | `var(--el-bg-color)` | 卡片底，随暗色翻转 |
| `--ds-card-border` | `var(--el-border-color-lighter)` | 1px 静态边框 |
| `--ds-card-radius` | `12px` | 与 `--radius-md` 一致 |
| `--ds-card-padding` | `16px` | 卡片内边距 |

`el-card` 的 `--el-card-padding` / `--el-card-border-radius` 已映射到这四个值；非 `el-card` 容器用 `.ds-card`。静态卡片无阴影，`el-card` 默认阴影降为 `--el-box-shadow-lighter`。`design-system.css` 把 `body` 背景设为 `--el-bg-color-page`、卡片用 `--el-bg-color`，两者才有对比；卡片优先规则见 `experience-rules.md`。

## 动效

Element Plus 自身令牌：

| 令牌 | 值 | 用途 |
|---|---|---|
| `--el-transition-duration` | `0.3s`（本项目映射为 **220ms**） | 展开、对话框、抽屉 |
| `--el-transition-duration-fast` | `0.2s`（本项目映射为 **160ms**） | 颜色、描边、阴影、hover |
| `--el-transition-function-ease-in-out-bezier` | `cubic-bezier(0.645, 0.045, 0.355, 1)` | 位置/尺寸变化 |
| `--el-transition-function-fast-bezier` | `cubic-bezier(0.23, 1, 0.32, 1)` | 淡入、缩放 |

项目级动效令牌（`design-system.css`），业务过渡只用这六档：

| 令牌 | 值 | 用途 |
|---|---|---|
| `--motion-duration-fast` | 160ms | 退出、hover、颜色 |
| `--motion-duration-base` | 220ms | 常规进入、页面切换 |
| `--motion-duration-slow` | 320ms | 大面板、复杂布局 |
| `--motion-ease-standard` | `cubic-bezier(0.2, 0, 0, 1)` | 标准进出 |
| `--motion-ease-emphasized` | `cubic-bezier(0.05, 0.7, 0.1, 1)` | 强调进入 |
| `--motion-ease-exit` | `cubic-bezier(0.3, 0, 0.8, 0.15)` | 退出 |

- 只过渡 `opacity` 与 `transform`；不要给 `width`、`height`、`margin` 加过渡。
- 进入 220ms，退出 160ms；退出不应比进入更慢。
- 路由/面板/列表的现成类见 `experience-rules.md`（`ds-page`、`ds-fade`、`ds-slide-up`、`ds-scale-fade`、`ds-list`）。
- `prefers-reduced-motion: reduce` 时全局过渡降到 1ms、取消位移与缩放（已内置）。

## 层与 z-index

| 变量 | 值 | 使用者 |
|---|---|---|
| `--el-index-normal` | 1 | 普通定位元素 |
| `--el-index-top` | 1000 | 固定页头、侧栏 |
| `--el-index-popper` | 2000 | 所有 teleport 弹层起点 |

Element Plus 的 popper 从 2000 起自增，`ElMessage` / `ElNotification` 也在同一管理器内。业务层不要写 `z-index: 9999`；需要提升时用 `ElConfigProvider` 的 `:z-index` 调起点。页面内固定元素最高用 1000，且必须低于任何弹层。

## 组件尺寸

| 变量 | 值 | 说明 |
|---|---|---|
| `--el-component-size-small` | 24px | 表格内按钮、密集工具条 |
| `--el-component-size` | 32px | 默认表单控件 |
| `--el-component-size-large` | 40px | 主要表单、登录页 |

Element Plus 默认没有 48px 控件。触屏主操作通过 `.ds-touch { min-height: 48px }` 或 `size="large"` + 父级行高补足；桌面密集型允许 32px，但相邻热区间距 ≥ 8px。触达细则见 `layout-rules.md`。

## 反模式

- **只改 `--el-color-primary`**：派生色仍是默认蓝；用 `theme.js` 一次写全 7 个。
- **暗色切换后不重算内联色**：内联变量优先级高于暗色样式表；顺序必须是先切 class 再按 dark 重算。
- **硬编码 hex / px 字号 / 2px、20px 圆角**：绕过令牌后暗色、主题、G2 全部失效。
- **用 `--el-color-primary` 当文字色**：正文对比度不足；用文本色组。
- **拿 `--el-border-radius-circle`（50%）做胶囊**：椭圆曲率不一致；胶囊用 `9999px`。
- **给 Element Plus 组件写 `margin` 排布**：破坏 4dp 与响应式；用父级 `gap`。
- **给 `transition: all`**：会把布局属性也动画化，产生抖动。
