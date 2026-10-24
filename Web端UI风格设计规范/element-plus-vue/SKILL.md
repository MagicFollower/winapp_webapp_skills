---
name: element-plus-vue
description: 用 Vue 3 + Element Plus 2.14.7 落地一套可交付的界面设计规范。覆盖设计令牌（颜色/排版/间距/圆角/阴影/动效/层级）、4dp 布局与组件内外间距、G2 连续曲率圆角（corner-shape + 曲率闸门 + clip-path 兜底）、Vue 声明式 v-model 与组合式写法、顶部提示三态、分页与数据 Grid 绑定、Modal 遮罩策略、卡片优先、全局过渡动效、组件目录与页面配方、主题与暗色模式、导航外壳、渲染后审查。当出现 "element plus"、"el-table"、"el-form"、"Element Plus 设计规范"、"Vue 3 后台"、"G2 圆角"、"布局间距"、"暗色主题"、"顶部提示"、"toast"、"分页布局"、"Modal"、"卡片风格"、"过渡动画"、"Element Plus 审查" 时使用。
when_to_use: 在 Vue 3 / Nuxt 项目里用 Element Plus 搭后台、写表单/表格/弹窗、统一设计令牌、修布局间距、落 G2 圆角、做暗色主题、统一顶部提示与弹窗行为、做卡片化改版、加页面/组件过渡动画或做界面审查时使用。
allowed-tools: Read, Write, Edit, Glob, Grep, Bash
---

# Element Plus · Vue 3 设计规范

## 概览

本技能把 Element Plus 从“组件拼装”收敛成一套可交付的设计系统：**令牌统一、间距可量化、圆角强制 G2、绑定全部声明式**。适用于中后台、管理台、SaaS 控制台与 Nuxt 管理端。

**版本基线**：`element-plus@2.14.7`、`@element-plus/icons-vue@2.3.2`、Vue `3.5+`；Nuxt 用 `@element-plus/nuxt@1.1.5`。版本不同先核对 `node_modules/element-plus/es/components/**/*.d.ts`，不要照抄 API。

**设计分层**：令牌 → 组件 → 布局三层。视觉与组件语义以 Element Plus 为准，不要混用其他组件库的令牌前缀或类名。

**三条硬约束（强制）：**

1. **声明式绑定**：模板不读 `$event.target.value`、不 `querySelector` 驱动状态；值同步一律 `v-model`，动作才用事件。没有 `v-model` 的组件按 `references/vue-patterns.md` 的替代方案。
2. **布局可量化**：**业务布局间距**落在 9 档 4dp 子集（0/4/8/12/16/24/32/48/64）；组件内边距、组件之间、区块之间按 `references/layout-rules.md`。Element Plus 组件内部未暴露的 1–3px 细节（如按钮横向 15px）不计入，设计层只归一化 Card/Form/Dialog/Drawer/Table/Message/Notification 的内边距。
3. **G2 圆角**：所有非零圆角必须同时有 `corner-shape`；正圆登记 `data-g2-exempt`；不支持时按 `references/corner-g2.md` 的策略降级。

### 五条体验基线（强制）

1. **顶部提示三态**：`toast.info / warn / error`（`assets/vue/use-toast.js`）；禁止业务层直接调 `ElMessage`，颜色走主题变量。
2. **分页绑定数据 Grid**：工具栏、表格、分页同属 `.ds-data-grid`，分页 `justify-self: end` 固定在右下角。
3. **Modal 默认禁止遮罩关闭**：统一用 `assets/vue/AppDialog.vue`（`close-on-click-modal=false`）；只读弹窗要放开时显式声明。
4. **卡片优先**：页面一级区块、表单、表格、图表都落在卡片表面；扁平只用于卡片内部工具条，禁止三层嵌套。
5. **统一动效**：路由 `ds-page`、面板 `ds-fade / ds-slide-up / ds-scale-fade`、列表 `ds-list`；只动 opacity/transform，内置 reduced-motion 降级。

完整实现、代码与验收见 `references/experience-rules.md`。

## 30 秒接入

```bash
pnpm add element-plus@2.14.7 @element-plus/icons-vue@2.3.2
```

```js
// main.js —— 顺序不可换
import { createApp } from 'vue';
import ElementPlus from 'element-plus';
import zhCn from 'element-plus/es/locale/lang/zh-cn';
import 'element-plus/dist/index.css';
import 'element-plus/theme-chalk/dark/css-vars.css';
import './styles/design-system.css';   // 本技能的 assets/styles/design-system.css
import App from './App.vue';

createApp(App).use(ElementPlus, { locale: zhCn }).mount('#app');
```

```js
// 可选：品牌色（一次写全 7 个派生，避免悬停/选中仍是默认蓝）
import { applyTheme } from './styles/theme.js';
applyTheme('#6750A4', false);
```

```vue
<!-- App.vue -->
<script setup>
import { reactive } from 'vue';

const form = reactive({ name: '', plan: 'pro', enabled: true });
</script>

<template>
  <div class="ds-page">
    <h1 class="ds-h1">新建项目</h1>

    <el-card shadow="never">
      <el-form :model="form" label-width="96px">
        <el-form-item label="名称" prop="name"
          :rules="[{ required: true, message: '请填写名称', trigger: 'blur' }]">
          <el-input v-model="form.name" maxlength="32" show-word-limit />
        </el-form-item>

        <el-form-item label="方案">
          <el-select v-model="form.plan">
            <el-option label="Free" value="free" />
            <el-option label="Pro" value="pro" />
          </el-select>
        </el-form-item>

        <el-form-item label="启用">
          <el-switch v-model="form.enabled" />
        </el-form-item>
      </el-form>

      <template #footer>
        <div class="ds-actions">
          <el-button>取消</el-button>
          <el-button type="primary">保存</el-button>
        </div>
      </template>
    </el-card>
  </div>
</template>
```

按需导入、TypeScript、Nuxt 与命令式 API 样式引入见 `references/vue-patterns.md`。

## 前置确认

- `package.json`：有 `vue@3`，无 React；`element-plus` 版本与导入方式（全量/按需）；是否已加载暗色样式表。
- 样式顺序必须是 `index.css → dark/css-vars.css → design-system.css`；顺序错了圆角、间距、暗色都会被库默认值覆盖。
- 接入后先跑最小验证：一个 Input、一个 Select、一个 Dialog、一个 Table；确认 `v-model` 双向、弹层有样式、控制台无警告。任何一项失败先修接入，不要继续写业务。
- 已有设计系统时先确认令牌命名，不要同时存在两套主色或两套间距刻度。

## 设计令牌

完整表、暗色值、对比度与变量清单见 `references/design-tokens.md`。

### 色彩

Element Plus 每个语义色有 8 个变量：`--el-color-<type>`、`-light-3/5/7/8/9`、`-dark-2`。

| 组 | 用途 |
|---|---|
| `primary` | 主操作、选中、链接、进度 |
| `success / warning / danger / error / info` | 完成 / 待处理 / 破坏 / 校验失败 / 中性 |
| `--el-text-color-primary/regular/secondary/placeholder/disabled` | 文本层级 |
| `--el-bg-color` / `-page` / `-overlay` | 卡片与控件 / 页面 / 弹层 |
| `--el-fill-color*` | 表头、斑马纹、禁用块 |
| `--el-border-color*` | 描边与分隔线 |

**只改 `--el-color-primary` 是错的**：派生色停留在默认蓝。用 `assets/styles/theme.js` 一次写全 7 个；默认 `#409EFF` 的生成值与官方一致，可回归。

### 排版（六级）

| 类 | 字号 / 行高 / 字重 | 用途 |
|---|---|---|
| `.ds-display` | 32 / 40 / 700 | 登录、空态首屏 |
| `.ds-h1` | 24 / 32 / 600 | 页面标题 |
| `.ds-h2` | 20 / 28 / 600 | 区块标题、Dialog |
| `.ds-h3` | 16 / 24 / 600 | 卡片标题、表单分组 |
| `.ds-body` | 14 / 22 / 400 | 正文、表格 |
| `.ds-caption` | 12 / 18 / 400 | 辅助说明 |

变量：`--text-<role>-size / -line / -weight`。不要写 13/15px 段落；13px 只属于组件内部 small。

### 间距（唯一刻度）

| 令牌 | 4px | 8px | 12px | 16px | 24px | 32px | 48px | 64px |
|---|---|---|---|---|---|---|---|---|
| 名称 | `--space-1` | `--space-2` | `--space-3` | `--space-4` | `--space-6` | `--space-8` | `--space-12` | `--space-16` |

组件内 4/8/12/16/24；组件间 8/12/16/24；区块间 24/32/48。父级 `gap` 排版，不给组件写 `margin`。

### 圆角

| 令牌 | 值 | 组件 |
|---|---|---|
| `--radius-xs` | 4 | Tag、Pagination |
| `--radius-sm` | 8 | Button、Input、Select、Popover |
| `--radius-md` | 12 | Card |
| `--radius-lg` | 16 | Dialog、MessageBox |
| `--radius-xl` | 24 | 大面板 |
| `--radius-pill` | 9999 | 药丸按钮、Switch |
| 50% | — | **仅正圆**（Avatar、圆形按钮） |

半径决定档位，`corner-shape` 决定曲率；两者必须同时存在。

### 阴影、动效、层级、尺寸

| 类别 | 令牌 | 用途 / 值 |
|---|---|---|
| 阴影 | `--el-box-shadow-lighter/light/` / `-dark` | 1/2/3/4 层；静置卡片用描边 |
| 动效 | `--el-transition-duration` 220ms，`-fast` 160ms；`--motion-*` 六档 | 进入 220ms，退出 160ms；只动 opacity/transform |
| 缓动 | `ease-in-out-bezier` / `fast-bezier` | 位置 / 淡入缩放 |
| 层级 | `--el-index-normal/top/popper` | 1 / 1000 / 2000 |
| 尺寸 | `--el-component-size-small/` / `-large` | 24 / 32 / 40 |

## 组件速查

完整模板、插槽、事件与决策表见 `references/component-catalog.md`。

| 类别 | 组件 | 关键点 |
|---|---|---|
| 操作 | `el-button` / `el-button-group` | `type/plain/text/link`；一个区域一个 primary |
| 操作 | `el-dropdown` | `@command`；危险项 `divided` |
| 操作 | `el-popconfirm` | 轻量二次确认 |
| 输入 | `el-input` | `type="textarea"`、`show-word-limit` |
| 输入 | `el-input-number` | 清空为 `null` |
| 输入 | `el-select` / `el-select-v2` | 多选数组；>1000 项用 v2 |
| 输入 | `el-autocomplete` | `fetch-suggestions(qs, cb)` |
| 输入 | `el-checkbox` / `el-checkbox-group` | 值用 `value`，`label` 只是文本 |
| 输入 | `el-radio` / `el-radio-group` / `el-radio-button` | 值用 `value` |
| 输入 | `el-switch` | 立即生效；异步用 `before-change` |
| 输入 | `el-slider` | `range` 时数组 |
| 输入 | `el-date-picker` / `el-time-picker` / `el-time-select` | 字符串提交加 `value-format` |
| 输入 | `el-cascader` / `el-tree-select` | `props.emitPath` / `check-strictly` |
| 输入 | `el-color-picker` / `el-rate` / `el-transfer` / `el-mention` | 各自 `v-model` |
| 输入 | `el-upload` | `v-model:file-list` |
| 输入 | `el-form` / `el-form-item` | `:model` + `prop` 才校验 |
| 数据 | `el-table` / `el-table-column` | `:data`，无 `v-model` |
| 数据 | `el-table-v2` | 虚拟滚动；必须数值宽高 |
| 数据 | `el-pagination` | **两个** `v-model:` |
| 数据 | `el-tree` | 无 `v-model`；ref 读写勾选 |
| 数据 | `el-tag` / `el-badge` / `el-avatar` | Avatar 正圆登记例外 |
| 数据 | `el-progress` / `el-statistic` / `el-descriptions` | 数字 `tabular-nums` |
| 数据 | `el-skeleton` / `el-empty` / `el-result` | 三态 |
| 数据 | `el-card` / `el-collapse` / `el-timeline` | Card 内边距 16 |
| 数据 | `el-carousel` / `el-calendar` | 轮播无 `v-model` |
| 导航 | `el-menu` / `el-sub-menu` | `:default-active` + `router` |
| 导航 | `el-tabs` / `el-tab-pane` | `v-model` 是 pane 的 `name` |
| 导航 | `el-breadcrumb` / `el-page-header` | 面包屑由路由生成 |
| 导航 | `el-steps` / `el-anchor` / `el-segmented` | `:active` / 锚点 / `v-model` |
| 反馈 | `ElMessage` / `ElNotification` | 命令式；按需导入要引样式 |
| 反馈 | `ElMessageBox` | 返回 Promise；取消会 reject |
| 反馈 | `el-dialog` / `el-drawer` | `v-model` 是显隐；懒渲染 |
| 反馈 | `el-tooltip` / `el-popover` / `el-alert` | 浮层圆角 8 |
| 反馈 | `v-loading` / `el-tour` | 指令 / `v-model` 显隐 |
| 其他 | `el-splitter` / `el-watermark` / `el-config-provider` | 分栏 / 水印 / 全局配置 |

## Vue 3 声明式契约

完整表、表单/表格/弹窗模式与坑见 `references/vue-patterns.md`。

### 单值绑定（节选）

| 组件 | 写法 |
|---|---|
| `el-input` / `el-input-number` / `el-select` / `el-cascader` | `v-model` |
| `el-date-picker` / `el-time-picker` / `el-time-select` | `v-model` |
| `el-checkbox` / `el-radio` / `el-switch` / `el-slider` / `el-rate` | `v-model` |
| `el-tabs` / `el-collapse` / `el-segmented` | `v-model` |
| `el-dialog` / `el-drawer` / `el-tour` | `v-model`（显隐） |
| `el-pagination` | `v-model:current-page` + `v-model:page-size` |
| `el-upload` | `v-model:file-list` |

### 没有 `v-model`

- `el-table`：`:data` + 事件；行内编辑显式写回。
- `el-tree`：`:default-checked-keys` + ref 的 `getCheckedKeys()` / `setCheckedKeys()`。
- `el-carousel`：`:initial-index` + `@change` + ref 方法。
- `el-menu`：`:default-active="route.path"` + `router`。
- `el-steps`：`:active` 单向。

### 组合式函数

`assets/vue/use-crud.js` 提供 `usePagination` / `useDialogForm` / `useTableQuery`。

**必须解构返回的 ref**：

```js
const { visible, mode, form, open, close } = useDialogForm({ name: '' });
```

```vue
<!-- ❌ dialog.visible 是 Ref，不是 boolean；弹窗不显示 -->
<el-dialog v-model="dialog.visible">
```

模板只自动解包顶层 ref；嵌套对象里的 ref 不会解包。

### 表单三件套

- `el-form :model` + `el-form-item prop` 必须对应；否则校验静默失效。
- 规则 `trigger` 选 `blur` 或 `change`；字段错误落在字段上，不用 toast 顶替。
- 提交用 `native-type="submit"` + `@submit.prevent`，保留回车。

## 通用模式

### 页面

```vue
<div class="ds-page">
  <header class="page-head">
    <h1 class="ds-h1">订单</h1>
    <el-button type="primary">新建</el-button>
  </header>
  <el-card shadow="never">…</el-card>
</div>
```

`.ds-page` 已含 16/24 边距、1040 限宽、区块 gap 24。宽表用 `.ds-page--wide`（1440），阅读/设置用 `.ds-page--reading`（840）。

### 表格 + 分页（Grid 右下角）

```vue
<el-card shadow="never">
  <div class="ds-data-grid">
    <div class="ds-row">筛选 / 工具栏</div>

    <el-table v-loading="loading" :data="rows" row-key="id">
      <el-table-column prop="name" label="名称" min-width="180" show-overflow-tooltip />
      <el-table-column label="操作" width="140" fixed="right">
        <template #default="{ row }">
          <el-button link type="primary" @click="edit(row)">编辑</el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-pagination v-model:current-page="page" v-model:page-size="size" :total="total"
      :page-sizes="[10, 20, 50]" layout="total, sizes, prev, pager, next, jumper"
      background class="ds-data-grid__pager" />
  </div>
</el-card>
```

工具栏、表格、分页同属 `.ds-data-grid`（gap 16）；分页 `justify-self: end`，固定在右下角。文本列 `min-width`，数字/操作列 `width`。

### 弹窗（默认禁点遮罩关闭）

```vue
<script setup>
import AppDialog from '@/components/AppDialog.vue';
</script>

<template>
  <AppDialog v-model="visible" :title="mode === 'edit' ? '编辑' : '新建'" width="520px" destroy-on-close>
    <el-form ref="formRef" :model="form" label-width="96px">…</el-form>
    <template #footer>
      <div class="ds-actions">
        <el-button @click="close">取消</el-button>
        <el-button type="primary" @click="submit">保存</el-button>
      </div>
    </template>
  </AppDialog>
</template>
```

`AppDialog` 默认 `close-on-click-modal=false`、`append-to-body`；宽度 480–640；窄屏 92%；测量/聚焦等 `@opened`。只读弹窗要允许遮罩关闭时显式传 `:close-on-click-modal="true"`。

### 顶部提示

```js
import { toast } from '@/composables/use-toast.js';

toast.info('已保存');
toast.warn('额度将在 3 天后到期');
toast.error('保存失败，请重试');
```

三态颜色由 `ds-toast--info / --warn / --error` + Element Plus 主题变量驱动，自动适配亮/暗；业务层不要直接调 `ElMessage`，也不要用 toast 代替字段校验或确认框。

## 布局约束

细节、组件尺寸、响应式与反模式见 `references/layout-rules.md`。

| 场景 | 约束 |
|---|---|
| 页面 | 边距 16（<600）/24；限宽 1040，宽表 1440，阅读 840 |
| 表单 | 字段 16；分组 24；单列 ≤560；标签宽 96 |
| 卡片 | 内 16；网格 gap 16（<840）/24；最小宽 280；区块/表单/表格/图表优先用卡片，扁平只用于卡片内部；页面底 `--el-bg-color-page` 与卡片底 `--el-bg-color` 必须分层 |
| 弹窗 | 内 24；圆角 16；宽 480–640；默认禁止点击遮罩关闭 |
| 表格 | 单元格横向 16；行高 40/32；分页在同一数据 Grid 右下角 |
| 操作区 | gap 8；右对齐；相邻按钮 12px 默认边距要归零 |
| 触达 | 桌面 ≥32；触屏主操作 ≥48；相邻热区 ≥8 |
| 动效 | 页面 `ds-page`、面板 `ds-fade/slide-up/scale-fade`、列表 `ds-list`；160/220/320ms |

## G2 圆角（强制）

完整规则、闸门与 SVG/Canvas 见 `references/corner-g2.md`。

```css
:root { --g2-shape: squircle; }

@supports (corner-shape: squircle) {
  *, *::before, *::after { corner-shape: var(--g2-shape, squircle); }
}
[data-g2-exempt], [data-g2-exempt] * { corner-shape: round; }
```

- Element Plus 是轻 DOM，通配规则即可覆盖组件与伪元素；**不需要** Shadow DOM 注入。
- `corner-shape` 不继承，不能只在 `:root` 声明。
- 正圆：Element Plus 内部的 Avatar、圆形按钮、Radio 圆点、Switch 拇指、Slider 按钮、Backtop、Timeline 节点、Tour 指示点已自动豁免；**自建**正圆用 `data-g2-exempt`。扁条禁止 `50%`，用 `9999px`。
- 描边闸门：1px 全档安全；2px 需 r≥8；3px 需 r≥12；4px 需 r≥16。
- 兜底 `clip-path` 只给 `.ds-g2-surface` opt-in；Dialog/Popover 有阴影，不要裁。

验证：

```js
getComputedStyle(document.querySelector('.el-card')).cornerShape; // "squircle"
```

## 主题与暗色

完整机制、SCSS 方案与对比度见 `references/theming.md`。

```js
import { applyTheme } from './styles/theme.js';
applyTheme('#6750A4', true); // 先切 html.dark，再按暗色重算 7 个变量
```

- 顺序必须“先 class、后算色”；反过来会留下亮色派生档。
- 首屏防闪白：`index.html` 内联脚本按 `localStorage` / `prefers-color-scheme` 预设 `html.dark`。
- 暗色下链接/图标用 `--el-color-primary-dark-2`（暗色里它反而更亮）；`light-N` 更暗，只能当背景。
- 局部主题变量不进入 teleport 弹层；Dialog 需 `append-to-body="false"` 或全局主题。
- 不要改 `namespace`：`design-system.css` 的选择器以 `el-` 开头。

## 反模式

- **只改 `--el-color-primary`**：派生色仍是默认蓝，出现两套主色。
- **`border-radius` 单独使用 / 扁条 `50%` / 正圆不登记例外**：G1 或几何错误。
- **`gap: 10px`、`padding: 18px`、`margin: 20px`**：非 4 倍数。
- **给 Element Plus 组件写 `margin` 排布**：破坏 4dp 与响应式；用父级 `gap`。
- **在 `:root` 改 `--el-card-padding` 等组件变量**：无效，必须命中组件选择器。
- **`el-pagination` 只绑 `:current-page`**：页码不回写。
- **`el-table` / `el-tree` 写 `v-model`**：不存在，状态不同步。
- **`el-radio` / `el-checkbox` 用 `label` 当值**：2.6+ 起 `label` 只是文本，值用 `value`。
- **把组合式函数整体绑给 `v-model`**：嵌套 ref 不解包，弹窗不显示。
- **用 toast 代替字段校验**：错误落点错误。
- **业务层直接调 `ElMessage`**：颜色/时长失控；统一走 `toast.info/warn/error`。
- **分页用 `margin-left: auto` 或放在表格外**：与 Grid 脱节；用 `.ds-data-grid` + `justify-self: end`。
- **Dialog 允许点击遮罩关闭**：未保存内容丢失；用 `AppDialog`。
- **整页扁平、卡片套三层**：层级混乱；卡片优先且最多一层嵌套。
- **`transition: all` / 页面切换无过渡**：动画抖动或生硬；用 `ds-*` 过渡类，只动 opacity/transform。
- **`!important` 堆叠**：动画与主题难覆盖。
- **固定高度裁内容 / 页面 375 横向滚动 / 移动端无导航入口**：布局不合格。
- **给 teleport 弹层设固定宽度或放在 `overflow: hidden` 容器**：被裁或错位。

## 审查

以 `/element-plus-vue audit` 或“审查 Element Plus 界面”触发。按 10 类评分（令牌、布局、G2、排版、组件、表单数据、导航、无障碍、暗色、可维护性），输出总分、阻断项、关键问题（带 `file:line`）、警告、通过项、未验证项与修复清单。

**阻断项**包括：375 横向滚动、G2 未覆盖、主色派生缺失、暗色不可读、分页单向绑定或不在数据 Grid 右下角、表格/树误用 `v-model`、表单校验失效、破坏操作无确认、控制台警告、正文对比度不足、绕过 `toast` 直接调 `ElMessage`、Modal 点击遮罩即关闭、页面大面积扁平无卡片、切换无过渡或 `transition: all`。

评分细节、grep 命令、运行态测量脚本见 `references/audit-checklist.md`。

## Resources

- `references/design-tokens.md` —— 颜色/排版/间距/圆角/阴影/动效/层级/尺寸；暗色与对比度。
- `references/layout-rules.md` —— **布局强制项**：9 档间距、Element Plus 默认值归一、页面/表单/表格/卡片/弹窗、触达、响应式、审查清单。
- `references/corner-g2.md` —— **G2 强制项**：三层策略、半径映射、曲率闸门、正圆例外、clip-path 兜底、SVG/Canvas、验收。
- `references/vue-patterns.md` —— 安装/按需/Nuxt、`v-model` 全表、表单、表格分页、弹窗、上传、反馈、组合式函数、常见错误。
- `references/component-catalog.md` —— 组件逐个模板与关键 API；选择决策表。
- `references/theming.md` —— 运行时/SCSS 主题、暗色、ConfigProvider、对比度、局部主题。
- `references/navigation-and-layout.md` —— 应用外壳、侧栏/顶栏/抽屉、面包屑、页面骨架、双栏、滚动。
- `references/experience-rules.md` —— **体验基线**：顶部提示三态、分页 Grid、Modal 遮罩、卡片优先、统一动效与验收。
- `references/recipes.md` —— 列表 CRUD、设置、登录、仪表盘、三态、头像上传、URL 筛选、详情。
- `references/audit-checklist.md` —— 10 类评分、阻断项、grep 命令、运行态测量、报告格式。
- `assets/styles/design-system.css` —— 令牌、组件间距归一、布局工具类、卡片表面、分页 Grid、toast 样式、过渡类、G2 全局规则。
- `assets/styles/theme.js` —— 主色 7 派生生成、亮/暗切换。
- `assets/g2/g2-corners.mjs` —— G2 几何、曲率闸门、矩形/裁剪路径。
- `assets/g2/g2-light.js` —— 不支持 `corner-shape` 时的 clip-path opt-in 兜底。
- `assets/vue/use-crud.js` —— 分页、弹窗表单、表格查询组合式函数。
- `assets/vue/use-toast.js` —— 顶部提示 info/warn/error 三态封装。
- `assets/vue/AppDialog.vue` —— 默认禁止遮罩关闭的 Modal 包装。
