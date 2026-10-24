---
name: material-3-vue
description: 用 Vue 3 实现 Google Material Design 3（Material You / M3E）。主路线为 @m3e/web 自定义元素（与 @m3e/react 同源同版本，完整 Expressive），备选 Vuetify 4（原生 Vue、MD3 近似）。覆盖设计令牌（--md-sys-*）、主题与动态配色、声明式 v-model 包装、布局与间距约束、G2 圆角强制、导航与响应式布局、MD3 合规审查。当出现 "material design"、"MD3"、"material you"、"m3e"、"@m3e/web"、"--md-sys-color"、"vuetify md3"、"G2 圆角"、Vue 3 表单/导航/主题改造时使用。
when_to_use: 在 Vue 3 / Nuxt 项目里搭 MD3 界面、选 MD3 组件库、接 @m3e/web 或 Vuetify 4、做布局/G2 圆角改造、做 MD3 合规审查时使用。
allowed-tools: Read, Write, Edit, Glob, Grep, Bash
---

# Material Design 3 · Vue 3

## 概览

在 **Vue 3** 上实现 MD3。`@m3e/react` 只是 `@m3e/web` 的 React 绑定，**不存在 `@m3e/vue`**（npm 404）；因此 Vue 的主路线是直接消费 `@m3e/web` 自定义元素，组件、令牌、动效与 React 版一致。Vuetify 4 是原生 Vue 备选，组件更全但主题令牌不同、不实现 Expressive 物理动效。

**与 MD2 的关键区别：** 色调表面取代阴影表达层阶；单一主色可生成整套配色；默认全圆角；弹簧物理动效；三级对比度。

**实现库选择：**

| 库 | 推荐度 | 说明 |
|---|---|---|
| **@m3e/web + Vue 3** | ⭐ 首选 | 58 个模块，与 `@m3e/react` 同版本（2.10.2），真正的 Material 3 Expressive；走 `--md-sys-*` 令牌 |
| **Vuetify 4** | 备选 | 原生 Vue、100+ 组件、表格生态强；MD3 对齐但主题用 `--v-theme-*`，无 Expressive 弹簧/形状变换 |
| @material/web | ⚠️ 维护模式 | Google 官方但组件少、无 Expressive |
| Varlet | 移动端备选 | Vue 3、MD2/MD3 双主题、移动优先，桌面覆盖弱 |
| MUI / Element Plus / Naive UI | ❌ | 不是 MD3，不要用其组件冒充 MD3 |

**与 frontend-design 的关系：** 同时激活时，MD3 提供令牌、组件与布局约束，frontend-design 只在这些约束内做创意。**Roboto / Roboto Flex 是 MD3 Web 的正确默认字体**。

## 路线决策

- 要像素级对齐 React 版 / 需要 Expressive → `@m3e/web` + Vue（下方“30 秒接入”）。
- 要原生 Vue 组件与数据表格生态 → Vuetify 4（`references/vuetify-alternative.md`）。
- 移动端 H5 → Varlet（`StyleProvider('md3')`）；现有 `@material/web` 项目可继续，新项目不选。

标签与 Vuetify 对应见下方速查表；完整属性/插槽/事件见 `references/component-catalog.md`。

**三条硬约束（强制）：**

1. **声明式绑定**：业务模板不直接读 `$event.target`。单值/布尔控件用 `assets/vue/use-m3e-model.js` 的包装组件 + `v-model`；只读集合才退回子项 `selected` + 事件读取。
2. **布局可量化**：所有间距落在 4dp 子集（0/4/8/12/16/24/32/48/64）；组件内边距、触达与页宽按 `references/layout-rules.md`。
3. **G2 圆角**：所有非零圆角必须配 `corner-shape`；不支持时走 `clip-path` 兜底；Shadow DOM 用 `assets/g2/g2-shadow.js` 注入。见 `references/corner-g2.md`。

## 30 秒接入（@m3e/web）

```bash
pnpm add @m3e/web@2.10.2
```

**Vite（必须）** —— 不认识 `m3e-*` 会被当成 Vue 组件：

```js
// vite.config.js
import { defineConfig } from 'vite';
import vue from '@vitejs/plugin-vue';

export default defineConfig({
  plugins: [vue({
    template: { compilerOptions: {
      isCustomElement: (tag) => tag.startsWith('m3e-'),
    } },
  })],
});
```

**Nuxt** —— 同样在 `nuxt.config.ts` 配 `vue.compilerOptions.isCustomElement`；组件模块放到 `plugins/m3e.client.ts`，并用 `<ClientOnly>` 包住依赖 DOM 的首屏。

**入口按需导入（必须在 mount 前完成注册）：**

```js
// main.js —— 每个 m3e-* 标签都要先注册，且必须在 mount 前执行
import { createApp } from 'vue';
import App from './App.vue';
import '@m3e/web/theme';
import '@m3e/web/button';
import '@m3e/web/icon';
import '@m3e/web/form-field';
import '@m3e/web/select';
createApp(App).mount('#app');
```

```html
<!-- index.html：m3e-icon 依赖 Material Symbols 字体 -->
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Material+Symbols+Outlined:opsz,wght,FILL,GRAD@20..48,100..700,0..1,-50..200" />
```

```vue
<!-- App.vue -->
<script setup>
import { ref } from 'vue';
const email = ref('');
</script>

<template>
  <m3e-theme color="#6750A4" scheme="auto" motion="expressive" strong-focus>
    <m3e-button variant="filled">
      <m3e-icon slot="icon" name="add" />
      新建
    </m3e-button>

    <m3e-form-field variant="outlined">
      <label slot="label" for="email">邮箱</label>
      <input id="email" v-model="email" type="email" required />
      <span slot="hint">不会公开</span>
      <span slot="error">邮箱格式不正确</span>
    </m3e-form-field>
  </m3e-theme>
</template>

<style>
body { font-family: 'Roboto Flex', Roboto, sans-serif; }
</style>
```

## 判据与前置确认

- 先确认项目栈与目标：`package.json` 里有 `vue` 且无 `react`；有 `vuetify` 时先读 `references/vuetify-alternative.md`，不要混用两套令牌。
- `@m3e/web` 版本以 `npm view @m3e/web version` 为准；本技能按 2.10.2 的 58 个模块与 API 编写。版本不同先核对 `dist/src/*/` 下的 `.d.ts`，不要照抄。
- 接入后跑一次最小渲染（见 `references/vue-integration.md` 的验收清单）：自定义元素已升级、布尔属性能开能关、具名插槽有投影、主题令牌能读出。任何一项失败先修接入，不要继续写业务组件。

## 设计令牌

所有令牌是 CSS 自定义属性，可跨 Shadow DOM 继承。`@m3e/web` 读取 `--md-sys-*` 语义令牌；单个组件内部再用 `--m3e-<component>-*` 细化。**优先用语义令牌，不要硬编码 hex。**

### 色彩（`--md-sys-color-*`）

| 组 | 令牌 | 用途 |
|---|---|---|
| 强调 | `primary` / `on-primary` / `primary-container` / `on-primary-container` | 最高强调、主操作、FAB |
| 强调 | `secondary*` / `tertiary*` 同上 | 次级强调、对比强调 |
| 错误 | `error` / `on-error` / `error-container` / `on-error-container` | 错误状态，不随动态色变化 |
| 表面 | `surface` / `on-surface` / `on-surface-variant` | 默认背景与文本 |
| 容器 | `surface-container-lowest/low/container/high/highest` | 色调层阶（不是阴影） |
| 固定 | `surface-dim` / `surface-bright` / `surface-variant` | 保持相对亮度 |
| 反色 | `inverse-surface` / `inverse-on-surface` / `inverse-primary` | Snackbar 等对比元素 |
| 轮廓 | `outline` / `outline-variant` | 交互边界 / 装饰分隔线 |
| 固定强调 | `primary-fixed*` / `secondary-fixed*` / `tertiary-fixed*` | 不随明暗翻转，慎用 |

**配对规则：** 只按 `X` + `on-X` 配对；分隔线用 `outline-variant`，输入框等 3:1 边界用 `outline`。完整 48 个角色、基准色值与暗色表见 `references/color-system.md`。

### 字体（`--md-sys-typescale-*`）

15 个基线角色 + 15 个强调角色，每个角色 4 个轴向令牌：

```
--md-sys-typescale-<role>-<size>-font-size
--md-sys-typescale-<role>-<size>-font-weight
--md-sys-typescale-<role>-<size>-line-height
--md-sys-typescale-<role>-<size>-tracking
```

`role` = display / headline / title / body / label，`size` = large / medium / small；强调角色在前面加 `emphasized-`（如 `--md-sys-typescale-emphasized-title-large-font-weight`）。**没有 `-font`、`-size`、`-weight` 简写令牌**；字体族通过 `body { font-family: … }` 继承。优先用 `<m3e-heading variant="headline" size="large">`，不要手拼字号。完整表见 `references/typography-and-shape.md`。

### 形状

组件读取圆角值令牌 `--md-sys-shape-corner-value-*`：

| 令牌 | 值 | 典型组件 |
|---|---|---|
| `none` | 0 | — |
| `extra-small` | 4px | Snackbar |
| `small` | 8px | 输入框、菜单、Chip |
| `medium` | 12px | Card |
| `large` | 16px | FAB、抽屉 |
| `large-increased` | 20px | Expressive |
| `extra-large` | 28px | Dialog、Bottom Sheet |
| `extra-large-increased` | 32px | Expressive |
| `extra-extra-large` | 48px | Expressive |
| `full` | 9999px | Button、Badge、Slider |

单组件覆盖优先用 `--m3e-<component>-shape`（如 `--m3e-card-shape`），不要直接写 `border-radius` 改库内组件。半径只决定档位，曲率统一由下方 G2 规则接管。

### 层阶

| Level | DP | 色调表面 | 静置组件 |
|---|---|---|---|
| 0 | 0 | `surface` | 扁平 App Bar、填充/描边 Button、Card、List |
| 1 | 1 | `surface-container-low` | Elevated Card/Button、模态抽屉 |
| 2 | 3 | `surface-container` | Menu、Nav Bar、滚动后的 App Bar |
| 3 | 6 | `surface-container-high` | FAB、Dialog、Search、日期/时间选择器 |

悬停/聚焦再升一级。阴影只在繁忙背景上做额外保护时使用。

### 动效

`<m3e-theme motion="expressive">` 启用弹簧动效（默认）；`motion="standard"` 退回实用型。自定义 CSS 过渡用：

| 令牌 | 值 |
|---|---|
| `--md-sys-motion-easing-emphasized` | `cubic-bezier(0.2, 0, 0, 1)` |
| `--md-sys-motion-easing-emphasized-decelerate` | `cubic-bezier(0.05, 0.7, 0.1, 1)` |
| `--md-sys-motion-easing-emphasized-accelerate` | `cubic-bezier(0.3, 0, 0.8, 0.15)` |
| `--md-sys-motion-easing-standard*` | 标准进出/加速/减速 |
| `--md-sys-motion-duration-short-1..4` | 50 / 100 / 150 / 200ms |
| `--md-sys-motion-duration-medium-1..4` | 250 / 300 / 350 / 400ms |
| `--md-sys-motion-duration-long-1..4` | 450 / 500 / 550 / 600ms |
| `--md-sys-motion-duration-extra-long-1..4` | 700 / 800 / 900 / 1000ms |

## 组件速查表

标签一律 kebab-case；React 版的 `M3e*` 只是 `@m3e/web` 的包装，**不要照搬 React 组件名与属性**。

| 类别 | 组件（`m3e-*`） | 关键变体 / 备注 | Vuetify 4 |
|---|---|---|---|
| 操作 | `m3e-button` | `variant` filled/tonal/elevated/outlined/text；`size` xs–xl；`shape`；`toggle` | `v-btn` |
| 操作 | `m3e-button-group` | standard/connected | `v-btn-group` / `v-btn-toggle` |
| 操作 | `m3e-fab` | 7 种 `variant`；`size` small/medium/large；`extended` + `label` 插槽 | `v-fab` |
| 操作 | `m3e-fab-menu` + `m3e-fab-menu-trigger` | FAB 弹出动作菜单 | `v-speed-dial` |
| 操作 | `m3e-icon-button` | standard/filled/tonal/outlined/elevated；`toggle` | `v-btn icon` |
| 操作 | `m3e-segmented-button` + `m3e-button-segment` | `multi` | `v-btn-toggle` |
| 通信 | `m3e-badge` | `for` 指向目标元素；`size` | `v-badge` |
| 通信 | `m3e-linear-progress-indicator` | `mode` determinate/indeterminate/buffer/query；`variant` flat/wavy | `v-progress-linear` |
| 通信 | `m3e-circular-progress-indicator` | `indeterminate`；`variant` | `v-progress-circular` |
| 通信 | `m3e-snackbar` | `open`、`action`、`duration`、`dismissible` | `v-snackbar` |
| 通信 | `m3e-tooltip` / `m3e-rich-tooltip` | 用 `for="目标id"` 关联 | `v-tooltip` |
| 容器 | `m3e-card` | filled/outlined/elevated；`orientation`；`actionable`；header/content/actions/footer 插槽 | `v-card` |
| 容器 | `m3e-dialog` + `m3e-dialog-trigger` | `open`、`alert`、`dismissible`；默认内容 + header/actions 插槽 | `v-dialog` |
| 容器 | `m3e-bottom-sheet` | `open`、`modal`、`detents`、`handle` | `v-bottom-sheet` |
| 容器 | `m3e-drawer-container` | start/end 抽屉，`side/over/push/auto` | `v-navigation-drawer` |
| 容器 | `m3e-expansion-panel` + `m3e-accordion` | `open`；`multi` | `v-expansion-panels` |
| 容器 | `m3e-split-pane` | 可拖拽；`orientation`、`min/max/value` | — |
| 输入 | `m3e-checkbox` | `checked`、`indeterminate`、`required` | `v-checkbox` |
| 输入 | `m3e-radio-group` + `m3e-radio` | 互斥选择 | `v-radio-group` |
| 输入 | `m3e-switch` | `checked`；`icons` none/selected/both | `v-switch` |
| 输入 | `m3e-slider` + `m3e-slider-thumb` | 离散 `discrete`、`labelled`、竖向；范围 = 两个 thumb | `v-slider` / `v-range-slider` |
| 输入 | `m3e-form-field` | 包裹 **原生** `input`/`textarea`/`m3e-select`；filled/outlined | `v-text-field` |
| 输入 | `m3e-select` + `m3e-option` | 单选/`multi`；配 `m3e-form-field` | `v-select` |
| 输入 | `m3e-autocomplete` | 过滤模式 contains/starts-with/ends-with/none | `v-autocomplete` |
| 输入 | `m3e-chips` 系 | `m3e-assist-chip` / `filter-chip` / `input-chip` / `suggestion-chip` + 对应 set | `v-chip` / `v-chip-group` |
| 输入 | `m3e-datepicker` + `m3e-datepicker-toggle` | docked/modal/auto；`range` | `v-date-picker` |
| 输入 | `m3e-timepicker` + `m3e-timepicker-toggle` | dial/input；12/24h | `v-time-picker` |
| 输入 | `m3e-date-input` | 分段日期/时间输入，配 form-field | `v-date-input` |
| 导航 | `m3e-app-bar` | `size` small/medium/large；`centered`；`for` 滚动目标 | `v-app-bar` |
| 导航 | `m3e-nav-bar` + `m3e-nav-item` | compact/expanded/auto；3–5 项 | `v-bottom-navigation` |
| 导航 | `m3e-nav-rail` + `m3e-nav-item` | compact/expanded/auto；可加 `m3e-nav-rail-toggle` | `v-navigation-drawer rail` |
| 导航 | `m3e-nav-menu` + `m3e-nav-menu-item` | 层级侧边导航 | `v-list nav` |
| 导航 | `m3e-tabs` + `m3e-tab` + `m3e-tab-panel` | primary/secondary；`for` 关联面板 | `v-tabs` |
| 导航 | `m3e-toolbar` | standard/vibrant；`elevated`、`shape` | `v-toolbar` |
| 数据 | `m3e-list` + `m3e-list-item` | standard/segmented；leading/supporting-text 插槽 | `v-list` |
| 数据 | `m3e-action-list` / `m3e-selection-list` / `m3e-list-action` | 可点击 / 可选中列表 | `v-list` |
| 数据 | `m3e-avatar` | 图片/图标/缩写 | `v-avatar` |
| 数据 | `m3e-heading` | variant display/headline/title/label；size；`emphasized` | 排版类 |
| 数据 | `m3e-stepper` + `m3e-step` + `m3e-step-panel` | linear；orientation | `v-stepper` |
| 数据 | `m3e-tree` + `m3e-tree-item` | `multi`、`cascade` | `v-treeview` |
| 通信 | `m3e-icon` | `name`；variant outlined/rounded/sharp；`filled` | `v-icon` |
| 主题 | `m3e-theme` | color/scheme/contrast/variant/motion/density/strong-focus | `v-theme-provider` |

> 日期/时间选择器、抽屉、菜单、对话框不是受控弹层：用配套 **trigger 元素 + `for`** 或 `open` 属性/`toggle` 事件。完整插槽、属性、事件与 Vue 示例见 `references/component-catalog.md`。

## Vue 3 集成契约（必读）

1. **`isCustomElement` 必须配**；否则 Vue 把 `m3e-*` 当未注册组件，控制台报错、插槽不渲染。
2. **先导入模块再 mount**。元素未升级时 Vue 会把 prop 写成字符串属性，数组/对象会变成 `[object Object]`、`false` 会变成 `"false"`。
3. **布尔属性**：静态写 `<m3e-switch checked>`；动态写 `:checked="flag"`。不要写 `checked="false"`（字符串非空，Lit 判为 true）。
4. **具名插槽用真实元素**：`<div slot="header">…</div>` 或 `<span slot="title">…</span>`。**不要用 `<template #header>`**，自定义元素没有 Vue 的插槽机制。
5. **裸 `v-model` 不适用于自定义元素**（已实测：不报错、不更新）。用 `assets/vue/use-m3e-model.js` 生成包装组件后即可 `v-model`；原生 `input` / `textarea` 继续直接 `v-model`。示例见下方“声明式绑定”。
6. **事件名用连字符原样绑定**：`@change`、`@view-change`、`@opening`。Vue 3 没有 `.native` 修饰符。
7. **数组/对象属性要绑定成 prop**：`:detents="['fit','half','full']"`、`:page-sizes="[10,25,50]"`；不要写成普通 attribute。
8. **`m3e-form-field` 包裹原生控件**；2.10.2 **没有** `m3e-text-field`。多行用 `<textarea>` + `m3e-textarea-autosize`。
9. **SSR/Nuxt 仅客户端**：组件依赖 DOM 升级；用 `.client` 插件 / `<ClientOnly>`，不要在服务端期待有意义的 HTML。
10. **样式穿透**：语义令牌与组件 `--m3e-*` 变量能穿透 Shadow DOM；不要用 `::v-deep` 戳内部结构，库不承诺内部 DOM。

### 声明式绑定

把“属性 + 原生事件”的差异封进包装层，业务模板只写 `v-model`：

```js
// src/m3e.js
import { defineM3eModel } from './use-m3e-model.js';

export const M3eSelect = defineM3eModel('m3e-select');
export const M3eSwitch = defineM3eModel('m3e-switch', { prop: 'checked', defaultValue: false });
export const M3eCheckbox = defineM3eModel('m3e-checkbox', { prop: 'checked', defaultValue: false });
export const M3eSliderThumb = defineM3eModel('m3e-slider-thumb', { prop: 'value', event: 'input', defaultValue: 0 });
```

```vue
<M3eSelect id="plan" v-model="plan">
  <m3e-option value="free">Free</m3e-option>
  <m3e-option value="pro">Pro</m3e-option>
</M3eSelect>

<M3eSwitch v-model="dark" />
<m3e-slider labelled><M3eSliderThumb v-model="volume" /></m3e-slider>
```

- 工厂已导出 select / switch / checkbox / toggle button / icon-button / drawer-container / slider-thumb / date-input / datepicker / timepicker / split-pane。
- 只读集合（segmented-button、filter-chip-set、selection-list、tabs、nav-bar、tree、radio-group）不能设初值：初始态写子项 `selected` / `checked`，变更用事件读取。
- 实测（Vue 3.5 + `@m3e/web` 2.10.2）：上述包装的初值写入与用户操作回写均正确。完整表见 `references/vue-integration.md`。

## 通用模式

### 应用外壳

```vue
<script setup>
import { ref } from 'vue';
import { M3eDrawerContainer } from './use-m3e-model.js';
const drawer = ref(true); // 模块在入口统一导入，mount 前完成注册
</script>

<template>
  <m3e-theme color="#6750A4" scheme="auto" motion="expressive">
    <M3eDrawerContainer id="shell" v-model="drawer">
      <m3e-nav-menu slot="start">
        <m3e-nav-menu-item selected>
          <m3e-icon slot="icon" name="home" /><span slot="label">首页</span>
        </m3e-nav-menu-item>
      </m3e-nav-menu>

      <main>
        <m3e-app-bar size="small">
          <m3e-icon-button slot="leading" aria-label="菜单">
            <m3e-icon name="menu" />
            <m3e-drawer-toggle for="shell" />
          </m3e-icon-button>
          <span slot="title">页面标题</span>
        </m3e-app-bar>
        <div class="md3-body"><slot /></div>
      </main>
    </M3eDrawerContainer>
  </m3e-theme>
</template>

<style scoped>
main { display: flex; flex-direction: column; min-height: 100vh; }
.md3-body {
  flex: 1; width: 100%; box-sizing: border-box;
  max-width: 1040px; margin-inline: auto;
  padding: var(--md-sys-measurement-space200, 16px);
}
@media (min-width: 600px) {
  .md3-body { padding: var(--md-sys-measurement-space300, 24px); }
}
</style>
```

> `m3e-drawer-toggle` 依赖容器的 `id`（这里是 `shell`）；抽屉开合由包装层 `v-model` 管理。

### 响应式导航

紧凑（<600）用 `m3e-nav-bar`，中等（600–839）用 `m3e-nav-rail`，展开（840+）用 `m3e-drawer-container` + `m3e-nav-menu`。断点只切布局，不切字号。完整结构与 CSS 切换见 `references/navigation-patterns.md`。

### 卡片网格

```css
.md3-card-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: var(--md-sys-measurement-space200, 16px);
}
@media (min-width: 840px) {
  .md3-card-grid { gap: var(--md-sys-measurement-space300, 24px); }
}
```

卡片本身用 `m3e-card variant="outlined|filled|elevated"`，媒体放默认插槽（无内边距），文字放 `slot="content"`（已有 16px，不要再包 padding）。

### 表单

```vue
<script setup>
import { ref } from 'vue';
import { M3eSelect } from './m3e.js';
// 模块已在入口导入：form-field / select / option / button
const name = ref('');
const plan = ref('pro');
</script>

<template>
  <form class="md3-form" @submit.prevent>
    <m3e-form-field variant="outlined">
      <label slot="label" for="name">全名</label>
      <input id="name" v-model="name" required />
      <span slot="hint">与证件一致</span>
      <span slot="error">请填写全名</span>
    </m3e-form-field>

    <m3e-form-field variant="outlined">
      <label slot="label" for="plan">方案</label>
      <M3eSelect id="plan" v-model="plan">
        <m3e-option value="free">Free</m3e-option>
        <m3e-option value="pro">Pro</m3e-option>
      </M3eSelect>
    </m3e-form-field>

    <div class="actions">
      <m3e-button variant="text" type="reset">取消</m3e-button>
      <m3e-button variant="filled" type="submit">提交</m3e-button>
    </div>
  </form>
</template>

<style scoped>
.md3-form { display: grid; gap: var(--md-sys-measurement-space200, 16px); max-width: 560px; }
.actions { display: flex; justify-content: flex-end; gap: var(--md-sys-measurement-space100, 8px); }
</style>
```

## 布局约束

只使用 4dp 子集：`0 / 4 / 8 / 12 / 16 / 24 / 32 / 48 / 64`。用父级 `gap`，**不要给 `m3e-*` 写 `margin` 或固定 `height`**。

| 场景 | 约束 |
|---|---|
| 页面 | 边距 16（<600）/ 24（≥600）；阅读宽 ≤1040（长文 840） |
| 表单 | 字段之间 16；组间 24；操作按钮 8；单列，≤560 |
| 卡片 | `content` 内边距 16；网格 gap 16（紧凑）/ 24（≥840）；高度随内容 |
| 触达 | ≥48×48；相邻热区 ≥8；视觉 40 可以，热区不能缩 |
| 库默认 | button 40（small）；chip 32/触达 48；list 56/72/88；app bar 64/112/120；nav bar 64；form field 56 |

完整规则、反模式与审查清单见 `references/layout-rules.md`。

## G2 圆角（强制）

`border-radius` 单独使用永远是 G1；所有非零圆角必须再加 `corner-shape`。`squircle` = `superellipse(2)` = p4。

```css
:root { --g2-shape: squircle; }

@supports (corner-shape: squircle) {
  *, *::before, *::after { corner-shape: var(--g2-shape, squircle); }
}
```

```js
import { createApp } from 'vue';
import { patchG2, observeG2 } from './g2-shadow.js';

createApp(App).mount('#app');
patchG2();   // light DOM + 所有已挂载 shadow root
observeG2(); // 后续新增节点
```

- `corner-shape` **不继承**，Shadow DOM 必须用 `assets/g2/g2-shadow.js` 注入；只写 `:root` 无效。
- 描边上限约为 `0.3964 × r ÷ 1.2`：r=8 → 2.64px、r=12 → 3.96px、r=24 → 7.93px；超了先升半径或减描边。
- 禁止 `border-radius: 50%` 做扁条药丸（用 9999px）；`clip-path` 兜底不能和 `box-shadow` 同元素。
- 完整规则、SVG / Canvas 与验收见 `references/corner-g2.md`。

## Vuetify 4 备选

```bash
pnpm add vuetify@4.3.0
```

```js
// plugins/vuetify.js
import 'vuetify/styles';
import { createVuetify } from 'vuetify';
import { md3 } from 'vuetify/blueprints';

export default createVuetify({ blueprint: md3, theme: { defaultTheme: 'system' } });
```

配色、主题桥接、表格与日期/时间选择器见 `references/vuetify-alternative.md`。

**Vuetify 的边界：**

- 主题令牌是 `--v-theme-*`（RGB 三元组，用 `rgb(var(--v-theme-primary))`），**不是** `--md-sys-*`；不要指望 `--md-sys-color-primary` 能驱动 Vuetify 组件。
- 没有 M3E Expressive 的弹簧物理与形状变换；`md3` blueprint 只提供 MD3 颜色、圆角、排版与组件默认值。
- 按钮形态用 `variant` + `color` + `rounded`；MD3 的 “filled” 在 Vuetify 里是 `variant="flat"` + `color="primary"`。

## 反模式

- **混用两套令牌**：`@m3e/web` 用 `--md-sys-*`，Vuetify 用 `--v-theme-*`；同一页面混用会出现两套主色。
- **硬编码颜色/圆角**：始终用 `var(--md-sys-color-*)` 与 `var(--md-sys-shape-corner-value-*)`；否则动态配色、暗色、对比度全部失效。
- **任意配对颜色**：只允许 `X` + `on-X`；`outline` 做分隔线、`outline-variant` 做输入框边界都是错的。
- **桶导入**：`import '@m3e/web/all'` 会打包全部组件；按模块导入。
- **用 Vue 的 `#slot` 语法给自定义元素传插槽**：不会投影，且多数情况下不报错。
- **裸 `v-model` 直接落在 `m3e-*` 上**：静默不更新；用包装层。
- **给 `@m3e/web` 组件写 `border-radius`/内部选择器**：改用 `--m3e-<component>-shape` 等公开 CSS 变量。
- **只写 `border-radius` / 用 `50%` 做扁条药丸**：前者仍是 G1，后者曲率不对称；所有圆角走 G2 + `9999px`。
- **`clip-path` 与 `box-shadow` 同元素 / 任意间距 / 给 `m3e-*` 加 margin 或固定 height**：阴影被裁；用 4dp 子集与父级 `gap`。
- **假设 SSR 有内容**：自定义元素需要浏览器升级；Nuxt 用 client-only。
- **只做手机布局**：600dp+ 用侧栏/抽屉，1200dp+ 限制阅读宽度（840–1040px）。

## MD3 合规审查

以 `/material-3-vue audit` 或“审查 MD3 合规性”触发。目标可以是 URL、源码目录或运行中的应用。

1. 识别目标（URL / 源码 / 运行应用），确认走 `--md-sys-*` 还是 `--v-theme-*`。
2. 按 10 类评分 0–10：色彩、字体、形状/G2、层阶、组件、布局、导航、动效、无障碍、主题。
3. 报告：总分 + 分类表 + 关键问题 0–3（带 `file:line`）+ 警告 4–6 + 通过项 7–10 + 修复清单。

评分表细节、grep 命令、G2 与布局抽查、状态阈值见 `references/audit-checklist.md`。

## Resources

- `references/vue-integration.md` —— **接入前必读**：Vite/Nuxt/SSR、props/attrs/events/slots、声明式 v-model 包装、故障排查与验收清单。
- `references/component-catalog.md` —— 组件逐个的 Vue 模板、插槽、属性、事件；含 Vuetify 对应组件。
- `references/color-system.md` —— 48 个色彩角色、色调映射、亮/暗基准值、动态配色与图片取色。
- `references/typography-and-shape.md` —— 15+15 排版角色、形状与层阶、动效令牌、`m3e-shape` 形状名。
- `references/theming-and-dynamic-color.md` —— `m3e-theme` 全部属性、运行时切换、作用域主题、Vuetify 主题桥接。
- `references/navigation-patterns.md` —— bar/rail/drawer/tabs/app-bar 的准确结构与断点切换。
- `references/layout-and-responsive.md` —— 窗口尺寸类别、三种标准布局、`m3e-split-pane`/`m3e-drawer-container`、折叠屏与容器查询。
- `references/layout-rules.md` —— **布局强制项**：4dp 子集、组件内部尺寸、组间间距、触达、反模式与审查清单。
- `references/corner-g2.md` —— **G2 强制项**：`corner-shape` 三层策略、Shadow DOM 注入、描边闸门、SVG/Canvas、验收。
- `references/audit-checklist.md` —— MD3 审查评分表、grep 命令、G2/布局抽查与阈值。
- `assets/vue/use-m3e-model.js` —— 声明式 `v-model` 包装工厂与常用导出。
- `assets/g2/g2-shadow.js` / `g2-corners.mjs` —— Shadow DOM 注入与 G2 几何/闸门计算。
- `references/vuetify-alternative.md` —— Vuetify 4 完整接入、组件映射、表格/日期选择器、与 @m3e/web 的差异清单。
