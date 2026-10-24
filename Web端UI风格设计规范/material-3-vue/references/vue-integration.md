# Vue 3 接入 @m3e/web

`@m3e/web` 是自定义元素（Web Components），Vue 3 能像原生标签一样渲染它，但**属性、插槽、事件、表单绑定都走 HTML 语义，不走 Vue 组件语义**。本文件把两者的接缝讲清楚；先读它，再写业务组件。

## 为什么没有 @m3e/vue

- npm 上 `@m3e/react` 存在（2.10.2），`@m3e/vue` **404**。
- `@m3e/react` 的职责只是把 React 的 props/slots 映射到 `@m3e/web` 自定义元素；Vue 不需要这层映射就能消费自定义元素。
- 因此 Vue 项目直接用 `@m3e/web`，组件行为与 React 版同源；差别只在模板写法。

## 安装与注册

```bash
pnpm add @m3e/web@2.10.2
```

按模块导入，每个导入都会注册对应标签：

```js
// main.js —— 必须在 mount 之前执行
import { createApp } from 'vue';
import App from './App.vue';

import '@m3e/web/theme';          // m3e-theme / m3e-theme-icon
import '@m3e/web/button';         // m3e-button
import '@m3e/web/icon';           // m3e-icon
import '@m3e/web/icon-button';    // m3e-icon-button
import '@m3e/web/card';           // m3e-card
import '@m3e/web/form-field';     // m3e-form-field
import '@m3e/web/button-group';   // m3e-button-group
```

模块与标签不是一一对应：`@m3e/web/nav-bar` 注册 `m3e-nav-bar` 与 `m3e-nav-item`；`@m3e/web/tabs` 注册 `m3e-tabs`、`m3e-tab`、`m3e-tab-panel`；`@m3e/web/chips` 注册全部芯片。以 `references/component-catalog.md` 的标签为准。

**不要桶导入** `@m3e/web/all`，它会把全部组件打进包。

**图标字体**（`m3e-icon` 用 Material Symbols 连字）：

```html
<link rel="stylesheet"
  href="https://fonts.googleapis.com/css2?family=Material+Symbols+Outlined:opsz,wght,FILL,GRAD@20..48,100..700,0..1,-50..200" />
```

## 构建配置

### Vite

```js
// vite.config.js
import { defineConfig } from 'vite';
import vue from '@vitejs/plugin-vue';

export default defineConfig({
  plugins: [
    vue({
      template: {
        compilerOptions: {
          isCustomElement: (tag) => tag.startsWith('m3e-'),
        },
      },
    }),
  ],
});
```

### Nuxt 3/4

```ts
// nuxt.config.ts
export default defineNuxtConfig({
  vue: {
    compilerOptions: {
      isCustomElement: (tag: string) => tag.startsWith('m3e-'),
    },
  },
  vite: {
    ssr: { noExternal: ['@m3e/web'] },
  },
});
```

```ts
// plugins/m3e.client.ts
import '@m3e/web/theme';
import '@m3e/web/button';
import '@m3e/web/card';
```

```vue
<template>
  <ClientOnly>
    <m3e-theme color="#6750A4" scheme="auto">
      <m3e-button variant="filled">新建</m3e-button>
    </m3e-theme>
  </ClientOnly>
</template>
```

**为什么必须 client-only**：自定义元素在浏览器里才升级、才有 Shadow DOM 与样式；服务端渲染出来只是空标签，水合时还会闪。`@m3e/web` 不在 SSR 中产出有意义的 HTML。

**不配 `isCustomElement` 的后果**：Vue 把 `<m3e-button>` 当未注册组件，开发时警告 “Failed to resolve component”，生产构建可能直接丢掉标签，插槽内容也不会按预期投影。

## 属性与属性绑定

| 写法 | Vue 行为 | 对 Lit 元素的结果 |
|---|---|---|
| `variant="filled"` | 设置 attribute | 正确 |
| `:selected="true"` | 元素已升级 → 设 property；未升级 → `selected="true"` | 正确（属性存在即真） |
| `:disabled="false"` | 设 property `false` | 正确；**不要写 `disabled="false"`**（字符串非空 → 真） |
| `:detents="['60%','90%']"` | 设 property（数组） | 正确；写成 `detents="[...]"` 会得到字符串 |
| `:icon-size="28"` | kebab 名不是 property → 设 attribute，Lit 转换 | 正确 |
| `:iconSize="28"` | 命中 property | 正确 |

**规则：**

1. 静态值用 kebab-case attribute，和组件文档一致（`:page-size`、`:auto-activate`）。
2. 动态布尔值用 `:` 绑定 property；不要拼字符串 `"false"`。
3. 数组/对象用 `:` 绑定；元素必须在 mount 前注册，否则会被序列化成 `[object Object]`。
4. 属性名大小写不敏感：HTML attribute 会被浏览器转小写，所以 camelCase 只适合绑定 property（`v-bind:iconSize`）。
5. 元素更新时，Vue 只 patch 变化的值；组件内部状态（如用户点开的菜单）不会被无关的父级重渲染覆盖。

## 具名插槽

Vue 的 `#slot` / `<template #slot>` 只对 **Vue 组件**生效。自定义元素靠浏览器原生 `<slot>` 投影：

```vue
<m3e-card>
  <div slot="header">标题区</div>
  <div slot="content">正文区</div>
  <div slot="actions"><m3e-button variant="text">详情</m3e-button></div>
</m3e-card>
```

- **必须用真实元素承载 `slot` 属性**；纯文本没有 `slot` 属性，所以文本插槽要包一层 `<span slot="title">`。
- 默认插槽直接放子元素，不需要 `slot`。
- 同一插槽放多个元素是允许的（例如 App Bar 的多个 `slot="trailing"` 按钮）。
- 若在自定义元素里写 `<template #header>`，Vue 会当成普通 `<template>`，浏览器不会投影；**不会有明显报错**。

## 事件

```vue
<m3e-tabs @change="onTabChange" />
<m3e-timepicker @view-change="onViewChange" />
<m3e-dialog @opened="onOpened" @closed="onClosed" />
```

- 事件名按组件文档原样写，连字符不要转 camelCase：`@view-change`、`@beforetoggle`。
- 事件是原生 `CustomEvent`/`Event`；`$event.target` 就是派发事件的自定义元素。
- `@click` 等原生事件照常工作；不要加 `.native`（Vue 3 已移除）。
- 需要阻止默认行为时，先确认组件是否支持 `preventDefault`（如 `beforeinput`），不要假设。

## 双向绑定（声明式）

裸 `v-model` 不能直接用在 `m3e-*` 上：自定义元素只暴露 `value` / `checked` 属性与 `change` / `input` 事件，没有 Vue 的 `modelValue` / `update:modelValue`。直接写会**静默失效**：界面动了、状态不动，且没有警告。

用 `assets/vue/use-m3e-model.js` 的工厂把属性/事件差异封进包装层，业务模板保持声明式：

```js
// src/m3e.js
import { defineM3eModel } from './use-m3e-model.js';

export const M3eSelect = defineM3eModel('m3e-select');
export const M3eSwitch = defineM3eModel('m3e-switch', {
  prop: 'checked', defaultValue: false,
});
export const M3eCheckbox = defineM3eModel('m3e-checkbox', {
  prop: 'checked', defaultValue: false,
});
export const M3eSliderThumb = defineM3eModel('m3e-slider-thumb', {
  prop: 'value', event: 'input', defaultValue: 0,
});
```

```vue
<M3eSelect id="plan" v-model="plan">
  <m3e-option value="free">Free</m3e-option>
  <m3e-option value="pro">Pro</m3e-option>
</M3eSelect>

<M3eSwitch v-model="dark" />

<m3e-slider labelled>
  <M3eSliderThumb v-model="volume" />
</m3e-slider>
```

工厂内做三件事：把 `modelValue` 写进元素的** property**、监听组件的原生事件、把 `el[prop]` 通过 `update:modelValue` 回写给 `v-model`。所以模板里不再出现 `$event.target.*`。

已导出的包装：

| 包装 | property | 事件 |
|---|---|---|
| `M3eSelect` | `value` | `change` |
| `M3eSwitch` / `M3eCheckbox` | `checked` | `change` |
| `M3eButton` / `M3eIconButton`（仅 `toggle`） | `selected` | `change` |
| `M3eDrawerContainer` | `start` | `change` |
| `M3eSliderThumb` | `value` | `input` |
| `M3eDateInput` | `value` | `change` |
| `M3eDatepicker` / `M3eTimepicker` | `date` | `change` |
| `M3eSplitPane` | `value` | `change` |

**只读集合不能设初值**：`m3e-segmented-button`、`m3e-filter-chip-set`、`m3e-selection-list`、`m3e-tabs`、`m3e-nav-bar`、`m3e-tree` 的 `.value` / `.selected` 是 getter。初始选中态写在子项的 `selected` / `checked` 上；变更用计算属性 + 事件读取，不要给这些元素绑 `v-model`。（`m3e-tabs` 虽有 `selectedIndex`，但初始写入会被子项挂载顺序覆盖，仍以子项 `selected` 为准。）

`m3e-radio-group` 同理：`.value` 只读，用每个 `m3e-radio` 的 `:checked="size === 'm'"` 声明选中态。

### 原生输入框

`m3e-form-field` 只是容器，**`v-model` 放在里面的原生 `input` / `textarea` 上**：

```vue
<m3e-form-field variant="outlined">
  <label slot="label" for="email">邮箱</label>
  <input id="email" v-model="email" type="email" />
  <span slot="hint">不会公开</span>
</m3e-form-field>
```

### 底层等价写法（不用包装时）

```vue
<m3e-select :value="plan" @change="plan = $event.target.value">…</m3e-select>
<m3e-switch :checked="dark" @change="dark = $event.target.checked" />
```

两种写法语义相同；团队内统一用包装层的 `v-model`，把 `$event.target` 收敛到 `use-m3e-model.js` 一处。

本机实测（Vue 3.5 + `@m3e/web` 2.10.2）：`M3eSelect`、`M3eSwitch`、`M3eCheckbox`、`M3eSliderThumb`、`M3eButton`（toggle）、`M3eDrawerContainer` 的初值写入与事件回写均正确。

## 命令式访问

需要读组件方法/属性时，用模板 ref：

```vue
<script setup>
import { ref, onMounted } from 'vue';
const snackbar = ref(null);
const shell = ref(null);

function notify() {
  snackbar.value.open = true;       // 直接改 property
}
function openDrawer() {
  shell.value.start = true;
}
</script>

<template>
  <m3e-snackbar ref="snackbar" duration="4000">已保存</m3e-snackbar>
  <m3e-drawer-container ref="shell" id="shell" :start="false">
    <nav slot="start">…</nav>
    <main>…</main>
  </m3e-drawer-container>
</template>
```

弹层类组件（Dialog、Menu、Drawer、Bottom Sheet、Datepicker、Timepicker）也可以用配套 trigger 声明式打开：`<m3e-dialog-trigger for="dlg">`、`<m3e-menu-trigger for="menu1">`、`<m3e-drawer-toggle for="shell">`、`<m3e-datepicker-toggle for="picker">`。`for` 指向目标的 `id`。

## 样式

- `--md-sys-*` 与组件 `--m3e-*` 变量可穿透 Shadow DOM，在 `m3e-theme`、`:root` 或任意祖先上设置。
- 不要用 `::v-deep` / `:deep()` 戳组件内部；库不承诺内部 class 名与 DOM 结构。
- 需要单个组件改形状/颜色时，用它的公开变量，例如：

```css
m3e-card { --m3e-card-shape: var(--md-sys-shape-corner-value-large); }
```

- G2 圆角需要在 light DOM 全局声明，并给 Shadow DOM 注入；`corner-shape` 不继承，只写 `:root` 无效：

```css
:root { --g2-shape: squircle; }

@supports (corner-shape: squircle) {
  *, *::before, *::after { corner-shape: var(--g2-shape, squircle); }
}
```

```js
import { patchG2, observeG2 } from './g2-shadow.js';
app.mount('#app');
patchG2();
observeG2();
```

- 全局字体不要用 typescale 的 `font-family`（不存在该令牌）；直接：

```css
body { font-family: 'Roboto Flex', Roboto, sans-serif; }
```

## 接入验收清单（已实测）

在 Vue 3.5 + Vite 7 + `@m3e/web` 2.10.2 下，用最小页面验证过以下行为；新项目接入后照此自检，任何一项不符先修接入：

| 检查 | 通过判据 |
|---|---|
| 元素已升级 | `customElements.get('m3e-button')` 为真；`m3e-button` 实例有 `.variant` 等 property |
| 静态布尔 | `<m3e-button disabled>` → `el.disabled === true` |
| 动态布尔 | `:disabled="false"` → `false`；`:disabled="true"` → `true` |
| 具名插槽 | `el.shadowRoot.querySelector('slot[name="header"]').assignedNodes()` 非空 |
| 主题令牌 | `getComputedStyle(themeEl).getPropertyValue('--md-sys-color-primary')` 有值（非空） |
| 事件 | `@change` / `@foo-bar` 均能触发，`$event.target` 是元素本身 |
| 原生输入 | form-field 内的 `<input v-model>` 能双向更新 |
| 声明式绑定 | `use-m3e-model.js` 的包装组件 + `v-model`：初值写入、事件回写均正确（select / switch / checkbox / slider-thumb / toggle button / drawer-container / date-input / datepicker / timepicker / split-pane 已实测） |
| 反例 | 直接 `<m3e-switch v-model="x">`：界面切换、`x` 不变、无警告 |

本次实测样本（仅作基线，不代表所有浏览器）：主题 `color="#006A6A"` 解析出 `--md-sys-color-primary: #1b6969`、`surface: #f6faf9`；`m3e-linear-progress-indicator :value="0.6"` 的 `value` 为 `0.6`。

## 故障排查

| 现象 | 先查 | 处理 |
|---|---|---|
| 控制台 “Failed to resolve component: m3e-*” | `vite.config` 的 `isCustomElement` | 按前缀放行；Nuxt 配 `vue.compilerOptions` |
| 组件是空白/无样式 | 模块是否导入；是否 SSR 首屏 | 在 mount 前 import；Nuxt 改 `.client` + `<ClientOnly>` |
| 布尔属性关不掉 | 是否写了 `disabled="false"` | 改成 `:disabled="false"` |
| 数组 prop 变成字符串 | 绑定写成普通 attribute / 元素未升级 | 用 `:` 绑定；确认 import 顺序 |
| 插槽内容不显示 | 是否用了 `<template #x>` | 改成真实元素 + `slot="x"` |
| `v-model` 不生效 | 是否直接落在 `m3e-*` 上 | 用 `use-m3e-model.js` 的包装组件；或改 `:值` + `@事件` |
| 事件不触发 | 事件名是否被 camelCase | 用文档里的连字符名 |
| 暗色不切换 | 是否只改了 `prefers-color-scheme` | 给 `m3e-theme` 绑 `scheme="dark"` 或改其 property |

## 数据基线与来源

本技能中的版本号、标签、属性、插槽、事件、令牌名不是凭记忆写的，按以下来源核对（2026-10-24）：

| 数据 | 来源 |
|---|---|
| `@m3e/web` 2.10.2、58 个模块、各组件 `.d.ts` 的 `@tag` / `@slot` / `@attr` / `@fires` / `@cssprop` | npm 包 `@m3e/web@2.10.2` 的 `dist/src/**/*.d.ts` 与包 README |
| 组件用法示例（trigger、badge `for`、stepper、tabs、drawer 等） | `github.com/matraic/m3e` 标签 `v2.10.2` 下 `packages/web/src/*/README.md` 与 `.d.ts` 的 `@example` |
| `--md-sys-*` 令牌清单与默认值 | `@m3e/web@2.10.2/dist/all.js`、`dist/theme.js` 中的 token 定义 |
| `@m3e/vue` 不存在 | npm registry `@m3e%2Fvue` 返回 404 |
| Vuetify 4.3.0、`md3` 蓝图默认值、105 个组件目录 | npm 包 `vuetify@4.3.0` 的 `lib/blueprints/md3.js` 与 `lib/components/` |
| Vue 自定义元素接入方式 | Vue 官方文档「Vue 与 Web Components」：`compilerOptions.isCustomElement` |
| 实测行为（布尔属性、插槽、kebab 事件、`v-model` 失效） | Vue 3.5 + Vite 7.3.7 + `@m3e/web` 2.10.2 最小页面，Chromium 实测（见上文验收表） |
