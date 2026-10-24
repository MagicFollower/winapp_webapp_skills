# 布局与间距规则（强制）

Element Plus 的默认间距不是一套设计系统：Card 内边距 20、Form 项间距 18、Dialog 内边距 16、Drawer 20、Table 单元格横向 12、相邻按钮 12。直接拼页面会得到**十几种间距值**。本规则把所有间距收敛到 4dp 子集，并规定每个场景该用哪一档。

## 1. 唯一刻度

```css
--space-1: 4px;   --space-2: 8px;   --space-3: 12px;
--space-4: 16px;  --space-6: 24px;  --space-8: 32px;
--space-12: 48px; --space-16: 64px;
```

合法值只有 `0 / 4 / 8 / 12 / 16 / 24 / 32 / 48 / 64`。1、2、3、5、6、10、14、18、20、28、40 都非法——包括 `padding`、`margin`、`gap`、`width`、`height`、`top/left`、`border-radius`。

**适用范围**：这条规则约束**业务样式**与设计层已归一化的组件变量；Element Plus 内部未暴露的实现细节（按钮横向 15px、输入框横向 11px、图标 14px 等）不计入，也不要去改。审查只扫 `src/`，不扫 `node_modules`。

**三级间距**：

| 层级 | 档位 | 例子 |
|---|---|---|
| 组件内（可覆盖） | 4 / 8 / 12 / 16 / 24 | 卡片内 16、Dialog 内 24、字段上下 12 |
| 组件间 | 8 / 12 / 16 / 24 | 相邻按钮 8、字段 16、工具条项 12 |
| 区块间 | 24 / 32 / 48 | 页面分节 24、大区块 32、首屏 48 |

规则：**层级越高，间距越大**。同一容器内不要出现“组件间 24、区块间 16”的倒挂。

## 2. Element Plus 默认值与本设计层

`../assets/styles/design-system.css` 已归一化下表右侧的值。业务代码不需要再写这些覆盖，但审查时要按右侧判定。

| 场景 | Element Plus 默认 | 本设计层 | 归一化方式 |
|---|---|---|---|
| Card 内边距 | 20px | **16px** | `--el-card-padding: var(--space-4)` |
| Card 圆角 | `--el-border-radius-base`（4px） | **12px** | `--el-card-border-radius: var(--radius-md)` |
| Form 项下间距 | 18px | **16px** | `--el-form-item-margin-bottom` |
| Form 最后一项 | 18px | **0** | `.el-form-item:last-child` |
| Dialog 内边距 | 16px | **24px** | `--el-dialog-padding-primary` |
| Dialog 圆角 | 4px | **16px** | `--el-dialog-border-radius` |
| Drawer 内边距 | 20px | **24px** | `--el-drawer-padding-primary` |
| MessageBox 圆角 | 4px | **16px** | `--el-messagebox-border-radius` |
| Popover / Popper 圆角 | 4px | **8px** | `--el-popover-border-radius` / `--el-popper-border-radius` |
| Table 单元格横向 | 12px | **16px** | `.el-table .cell` |
| Table 单元格纵向 | 8px（default） | 8px | 沿用默认，随 `size` 变 4 / 8 / 12 |
| Message 内边距 | 11px 15px | **12px 16px** | `--el-message-padding` |
| Notification 内边距 | 14px 26px 14px 13px | **16px 24px 16px 16px** | `--el-notification-padding` |
| Notification 圆角 | 8px | 8px | 沿用 |
| 相邻 Button | `margin-left: 12px` | **gap 8px，margin 归零** | `.ds-actions` |

**变量写在 `:root` 无效的组件变量**：`--el-card-padding`、`--el-dialog-padding-primary` 等是声明在组件选择器上的局部变量。要覆盖必须命中组件选择器（如 `.el-card { ... }`），这也是 `design-system.css` 的写法。审查时看到 `:root { --el-card-padding: 16px }` 视为无效改动。

## 3. 页面骨架

| 视口 | 屏幕边距 | 区块间距 | 网格 gap |
|---|---|---|---|
| < 600 | 16 | 24 | 16 |
| 600–839 | 24 | 24 | 16 |
| 840–1199 | 24 | 32 | 24 |
| ≥ 1200 | 24 | 32 | 24 |

宽度上限：

| 类型 | 最大宽度 | 类名 |
|---|---|---|
| 常规工作台 | 1040px | `.ds-page` |
| 数据密集页（宽表、看板） | 1440px | `.ds-page--wide` |
| 阅读/设置/单列表单 | 840px | `.ds-page--reading` |
| 单列表单 | 560px | `.ds-form` |

`.ds-page` 已包含 `padding: 16px`（<600）/ `24px`（≥600）、`margin-inline: auto`、`display: grid; gap: 24px`。页面内容直接作为它的子元素，不要在每个区块上再写 `margin-bottom`。

```vue
<template>
  <div class="ds-page">
    <header class="ds-row" style="justify-content: space-between">
      <h1 class="ds-h1">订单</h1>
      <el-button type="primary">新建</el-button>
    </header>
    <el-card>…</el-card>
  </div>
</template>
```

**不要用 `el-row/el-col` 做页面骨架**。24 栅格是组件级工具，页面宽度与分节用 `.ds-page` + CSS Grid/Flex。栅格只用于“同一行内需要不同宽度比例”的表单或指标卡。

## 4. 纵向节奏

| 关系 | 间距 | 说明 |
|---|---|---|
| 页面标题 → 第一个区块 | 24 | `.ds-page` 的 gap |
| 区块标题 → 内容 | 12 或 16 | 标题自身 `margin: 0`，用容器 gap |
| 两个区块 | 24（紧凑）/ 32（宽屏） | 同一个 gap |
| 卡片网格 | 16（<840）/ 24（≥840） | `.ds-card-grid` |
| 表单字段 | 16 | `--el-form-item-margin-bottom` |
| 表单分组之间 | 24 | 分组用 `<section class="ds-stack">`，组内表单字段 16 |
| 表单 → 操作区 | 24 | 操作区单独一行 |
| 表格 → 分页 | 16 | 分页在表格下方，右对齐 |
| 工具栏 → 表格 | 16 | 工具栏与表格是同级 |
| 空态 | 上下 32 / 48 | 用 `el-empty` 的默认留白，不加固定高度 |

**标题不要带默认外边距**：`.ds-h1/.ds-h2/.ds-h3` 已 `margin: 0`；用父级 `gap` 控制距离。直接写 `<h2>` 会带上浏览器默认 `margin: 0.83em`，破坏 4dp。

## 5. 横向布局

### 操作区

```vue
<div class="ds-actions">
  <el-button>取消</el-button>
  <el-button type="primary">保存</el-button>
</div>
```

- `.ds-actions`：右对齐、`gap: 8px`、允许换行，并把 Element Plus 相邻按钮的 12px 左外边距归零。
- 主操作在**最右**；危险操作与主操作之间留 16px 或加分隔。
- Dialog 的 `footer` 插槽内同样用 `.ds-actions`；不要写 `<span class="dialog-footer">` 再手写 margin。
- 工具条（搜索 + 筛选 + 刷新）用 `.ds-row`（gap 8），左侧查询、右侧动作；窄屏允许换行，不要横向滚动。

### 表单

| 项 | 规则 |
|---|---|
| 标签位置 | 桌面默认 `label-position="right"`，`label-width="96px"`（4 字加冒号）；窄屏 `<600` 切 `top` |
| 单列宽度 | ≤ 560px；超过 560 的两列表单用 24 栅格或 `grid-template-columns: 1fr 1fr` + gap 16 |
| 字段间距 | 16（由 form-item 变量控制） |
| 帮助文字 | 紧贴控件下方 4px，字号 12 |
| 校验信息 | 占用控件下方预留位置；不要用 toast 代替字段级错误 |
| 必填标记 | 用 `required`，不要手写红色星号 |
| 控件尺寸 | 默认 32px；登录/主要表单用 `size="large"`（40px）；触屏再加 `.ds-touch` |

### 表格

| 项 | 规则 |
|---|---|
| 单元格横向内边距 | 16（已由 `.el-table .cell` 统一） |
| 单元格纵向内边距 | default 8 / small 4 / large 12（沿用库） |
| 行高 | 默认约 40px，密集表格用 `size="small"`（约 32px） |
| 文本列 | `min-width` 而不是 `width`，让内容自然撑开 |
| 数字列 | 右对齐 + `tabular-nums` |
| 操作列 | 固定右侧，宽 120–160；最多 3 个文字按钮，更多收进 Dropdown |
| 工具栏 | 表格上方 16，左查询右动作；与表格、分页同属 `.ds-data-grid` |
| 分页 | **Grid 的右下角**：`class="ds-data-grid__pager"`（`justify-self: end`）；不要 `margin-left: auto` |
| 空态 | 用 `#empty` 插槽放 `el-empty`，不要在表格外另写判断 |

### 数据 Grid（工具栏 + 表格 + 分页）

```vue
<el-card shadow="never">
  <div class="ds-data-grid">
    <div class="ds-row">筛选 / 工具栏</div>
    <el-table :data="rows" />
    <el-pagination … class="ds-data-grid__pager" />
  </div>
</el-card>
```

```css
.ds-data-grid {
  display: grid;
  grid-template-columns: minmax(0, 1fr);
  gap: 16px;
  min-width: 0;
}
.ds-data-grid__pager { justify-self: end; align-self: end; }
```

- 分页是 Grid 的最后一个网格项，天然落在右下角，不依赖父级 flex。
- 表格横向滚动只发生在表格容器内；分页始终可见。
- 查询/页大小变化时页码回到 1。

### 卡片网格

```css
.ds-card-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 16px;
}
@media (min-width: 840px) { .ds-card-grid { gap: 24px; } }
```

- 卡片最小宽 280；低于 280 会挤坏标题与操作。
- 卡片高度随内容；同一行要对齐时用 `align-items: stretch` + 卡片 `height: 100%`。
- 卡片内部：标题 16、正文 12、底部动作 16；卡片本身已有 16 内边距，**不要在外层再包 padding**。

### 卡片优先

**页面一级区块、表单、表格、图表、统计块一律落在卡片表面；扁平只用于卡片内部的工具栏、表头与分隔区。**

| 场景 | 做法 |
|---|---|
| 页面区块 | 每块一张 `el-card shadow="never"`，区块间 gap 24 |
| 非 `el-card` 容器 | `.ds-card`（同一背景/边框/圆角/内边距） |
| 表单 | 一张或多张卡片，按 24 分组；不要裸 `el-form` 铺页面 |
| 表格 | 工具栏 + 表格 + 分页放进同一张卡片 |
| 图表 | 每张图一张卡片，卡片内标题 12 |
| 静态卡片 | 1px 边框、无阴影；hover 才 `--el-box-shadow-lighter` |
| 嵌套 | 最多一层；卡片内分组用分隔线或 16 间距 |
| 例外 | 全屏画布、地图、沉浸式编辑器、登录页整屏背景 |

反例：整页白底、区块之间只靠间距；卡片里再套卡片再套卡片；给静态卡片加重阴影。

```css
.ds-card {
  padding: 16px;
  background: var(--el-bg-color);
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 12px;
  box-shadow: none;
}
.ds-card--raised { box-shadow: var(--el-box-shadow-lighter); }
```

## 6. 组件内部尺寸基准

| 组件 | 尺寸 | 备注 |
|---|---|---|
| Button | 24 / 32 / 40（small/default/large） | 图标按钮必须方形，`circle` 仅用于纯图标 |
| Input / Select | 24 / 32 / 40 | 与 Button 同高才能并排 |
| Input textarea | 行高 22，`rows` 至少 3 | 不要固定 height，用 `autosize` |
| Checkbox / Radio | 视觉 14–16，热区 ≥ 32 | 用 `border` 时高度 32 |
| Switch | 宽 40，高 20 | 文字两侧用 8 |
| Slider | 轨道高 6，按钮 20 | `show-input` 时输入框宽 120 |
| Table | 行 40（default） | `size="small"` 32 |
| Menu item | 高 56（vertical）/ 40（horizontal） | 图标与文字 8 |
| Tabs | 高 40 | 标签之间 24 |
| Tag | 高 24（default）/ 20（small）/ 32（large） | 相邻 8 |
| Pagination | 页码 32×32 | 与按钮同高 |
| Card | 内 16，圆角 12 | 标题与内容 12 |
| Dialog | 内 24，圆角 16，宽 50%（默认） | 表单弹窗建议 480–640 |
| Drawer | 内 24，宽 30%（默认） | 表单抽屉建议 400–560 |
| Message | 内 12/16，圆角 8 | 距顶部 20 |
| Notification | 内 16/24，圆角 8，宽 330 | 右上角，间距 16 |

> 尺寸值取自 Element Plus 的 `--el-component-size*`、`--el-font-size*` 与组件 SCSS 默认值；本设计层只改了圆角与内边距，没有改控件高度。

## 7. 触达与可点击区域

- 主操作、导航项、图标按钮的**热区** ≥ 48×48（触屏），桌面 ≥ 32×32。
- 视觉尺寸可以小于热区：用透明 padding 或 `::before` 扩大，不要把图标本身放大。
- 相邻热区之间 ≥ 8px；表格行内按钮之间 ≥ 8px。
- 文字链接的点击区至少覆盖整行文字高度（≥ 32），不要只有 14px 高的文字。
- `el-button` 默认 32 高；在触屏页给它套 `.ds-touch`（`min-height: 48px`）或使用 `size="large"` + 8px 行内 padding。
- 图标按钮必须有 `aria-label`；仅图标没有可访问名称是审查失败项。

## 8. 响应式

| 断点 | 变化 |
|---|---|
| < 600 | 页面边距 16；表单标签置顶；表格横向滚动；操作区换行；侧栏改抽屉 |
| 600–839 | 页面边距 24；两列表单可并排；卡片网格 2 列 |
| 840–1199 | 侧栏常驻（240）；卡片网格 3 列；区块间距 32 |
| ≥ 1200 | 内容限宽 1040/1440；不要无限拉伸表格与表单 |

- 只切布局，不切字号；正文始终 14/22。
- 表格窄屏用 `el-table` 的横向滚动（外层 `overflow-x: auto`），不要隐藏列。
- Dialog 在 <600 用 `width="92%"` 或 `fullscreen`；Drawer 在 <600 用 `direction="btt"` + 高度 80%。
- 断点用 `min-width` 递进，不要混用 `max-width` 造成重叠。

## 9. 反模式

- **给组件写 `margin` 排布**：父级 `gap` 是唯一排版手段；`margin` 只用于组件内部微调。
- **固定高度裁内容**：`height: 40px` 卡住多行文本；用 `min-height`。
- **`el-row :gutter="20"`**：20 不在刻度上；用 16 或 24。
- **`gap: 10px` / `padding: 18px`**：非 4 倍数，直接判失败。
- **Dialog 宽度写死 800px**：窄屏溢出；用 `width="50%"` + `max-width` 或响应式宽度。
- **表格列全用 `width`**：内容一变就截断；文本列用 `min-width`。
- **操作按钮塞满表格行**：超过 3 个收进 Dropdown。
- **布局靠 `position: absolute`**：优先 Flex/Grid；绝对定位只用于角标、关闭按钮。
- **用空 `div` 撑高度**：用 gap 或 padding。
- **在 Element Plus 组件外层包一层 `div` 只为加 padding**：优先用组件自己的 padding 变量。

## 10. 审查清单（逐条打勾）

1. 业务样式的 `padding/margin/gap` 都在 9 档内；无 10/18/20（Element Plus 内部实现值除外）。
2. 页面使用 `.ds-page`（或等价规则）：边距 16/24、限宽、区块 gap 24。
3. 表单字段间距 16，最后一项无多余底距；标签宽度统一。
4. 操作区用 `.ds-actions`：gap 8、右对齐、相邻按钮无 12px 残留。
5. 卡片内边距 16、圆角 12；网格 gap 16/24。
6. Dialog 内边距 24、Drawer 24；表单弹窗宽度在 480–640。
7. 表格单元格横向 16；文本列 `min-width`；操作列固定且 ≤ 3 个按钮。
8. 分页在表格下方 16、右对齐，页码尺寸 32。
9. 触屏主操作热区 ≥ 48；图标按钮有 `aria-label`。
10. <600 下页面无横向滚动（表格自身滚动除外）。
11. 数据页的工具栏、表格、分页在同一个 `.ds-data-grid`；分页右边缘与表格右边缘对齐。
12. 页面一级区块都有卡片边界；卡片内边距 16、卡片间 gap 16/24；没有三层嵌套。
13. 静态卡片用边框而非重阴影；卡片内工具栏保持扁平。
14. 路由/面板/列表切换有统一过渡，且只动 opacity/transform。
15. `prefers-reduced-motion: reduce` 下无长动画与位移。
