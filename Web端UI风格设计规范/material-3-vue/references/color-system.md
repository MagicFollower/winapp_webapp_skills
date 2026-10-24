# MD3 色彩系统（Vue 3）

MD3 的色彩角色、色调映射、动态配色与在 `@m3e/web` / Vuetify 4 中的落地方式。所有 Web 令牌都是 CSS 自定义属性，可跨 Shadow DOM 继承。

## 色彩角色（48 个）

`@m3e/web` 实际读取的 `--md-sys-color-*` 令牌如下（来自 2.10.2 发布包，非推测）：

| 组 | 令牌 | 用途 |
|---|---|---|
| 强调 | `primary` | 高强调填充、文本、图标 |
| | `on-primary` | primary 上的文本/图标 |
| | `primary-container` | 突出容器填充（FAB、选中态） |
| | `on-primary-container` | primary-container 上的文本/图标 |
| | `secondary` / `on-secondary` | 次级强调 |
| | `secondary-container` / `on-secondary-container` | 退让型组件（色调按钮、筛选片） |
| | `tertiary` / `on-tertiary` | 对比强调 |
| | `tertiary-container` / `on-tertiary-container` | 补充容器 |
| 错误 | `error` / `on-error` | 错误状态，不随动态配色改变 |
| | `error-container` / `on-error-container` | 错误容器 |
| 表面 | `surface` | 默认背景 |
| | `on-surface` | 表面上的文本/图标 |
| | `on-surface-variant` | 低强调文本/图标 |
| | `surface-container-lowest` | 最低强调容器 |
| | `surface-container-low` | 低强调容器 |
| | `surface-container` | 默认容器（导航区） |
| | `surface-container-high` | 高强调容器 |
| | `surface-container-highest` | 最高强调容器 |
| | `surface-dim` / `surface-bright` | 在明暗主题中保持相对亮度 |
| | `surface-variant` | 替代表面（较少用） |
| 反色 | `inverse-surface` / `inverse-on-surface` / `inverse-primary` | Snackbar、对比元素 |
| 轮廓 | `outline` | 重要边界（输入框，3:1） |
| | `outline-variant` | 装饰边界（分隔线、卡片描边） |
| 根 | `background` / `on-background` | 文档根背景 |
| 其他 | `shadow` / `scrim` | 阴影色、遮罩色 |
| 固定 | `primary-fixed` / `primary-fixed-dim` / `on-primary-fixed` / `on-primary-fixed-variant` | 不随明暗翻转 |
| | `secondary-fixed` / `secondary-fixed-dim` / `on-secondary-fixed` / `on-secondary-fixed-variant` | 同上 |
| | `tertiary-fixed` / `tertiary-fixed-dim` / `on-tertiary-fixed` / `on-tertiary-fixed-variant` | 同上 |

**固定色注意**：固定色在亮/暗主题中不变，容易造成对比度问题；不要用它做正文或关键操作，除非同时锁定主题。

## 配对规则

只允许以下配对，否则动态配色和高对比度模式下的对比度保证会失效：

| 填充/容器 | 文本/图标 |
|---|---|
| `primary` | `on-primary` |
| `primary-container` | `on-primary-container` |
| `secondary` | `on-secondary` |
| `secondary-container` | `on-secondary-container` |
| `tertiary` | `on-tertiary` |
| `tertiary-container` | `on-tertiary-container` |
| `error` | `on-error` |
| `error-container` | `on-error-container` |
| `surface` | `on-surface` 或 `on-surface-variant` |
| `surface-container-*` | `on-surface` 或 `on-surface-variant` |
| `inverse-surface` | `inverse-on-surface` 或 `inverse-primary` |

- 分隔线：`outline-variant`。
- 输入框、选中框等需要 3:1 的边界：`outline`。
- 不要用 `primary` 做正文色，也不要把 `on-primary` 放到 `surface` 上。

## 色调映射

MD3 用种子色生成 5 条色调色板（Primary、Secondary、Tertiary、Neutral、Neutral-Variant），角色映射到 0–100 的色调：

| 角色 | 亮色 | 暗色 |
|---|---|---|
| primary | P40 | P80 |
| on-primary | P100 | P20 |
| primary-container | P90 | P30 |
| on-primary-container | P10 | P90 |
| surface | N98 | N6 |
| on-surface | N10 | N90 |
| surface-container-lowest | N100 | N4 |
| surface-container-low | N96 | N10 |
| surface-container | N94 | N12 |
| surface-container-high | N92 | N17 |
| surface-container-highest | N90 | N22 |
| outline | NV50 | NV60 |
| outline-variant | NV80 | NV30 |

`P` = Primary，`N` = Neutral，`NV` = Neutral-Variant，数字为色调值。

## 基准配色（未启用动态色）

### 亮色

```css
:root {
  --md-sys-color-primary: #6750A4;
  --md-sys-color-on-primary: #FFFFFF;
  --md-sys-color-primary-container: #EADDFF;
  --md-sys-color-on-primary-container: #21005D;
  --md-sys-color-secondary: #625B71;
  --md-sys-color-on-secondary: #FFFFFF;
  --md-sys-color-secondary-container: #E8DEF8;
  --md-sys-color-on-secondary-container: #1D192B;
  --md-sys-color-tertiary: #7D5260;
  --md-sys-color-on-tertiary: #FFFFFF;
  --md-sys-color-tertiary-container: #FFD8E4;
  --md-sys-color-on-tertiary-container: #31111D;
  --md-sys-color-error: #B3261E;
  --md-sys-color-on-error: #FFFFFF;
  --md-sys-color-error-container: #F9DEDC;
  --md-sys-color-on-error-container: #410E0B;
  --md-sys-color-background: #FEF7FF;
  --md-sys-color-on-background: #1D1B20;
  --md-sys-color-surface: #FEF7FF;
  --md-sys-color-on-surface: #1D1B20;
  --md-sys-color-on-surface-variant: #49454F;
  --md-sys-color-surface-variant: #E7E0EC;
  --md-sys-color-surface-container-lowest: #FFFFFF;
  --md-sys-color-surface-container-low: #F7F2FA;
  --md-sys-color-surface-container: #F3EDF7;
  --md-sys-color-surface-container-high: #ECE6F0;
  --md-sys-color-surface-container-highest: #E6E0E9;
  --md-sys-color-surface-dim: #DED8E1;
  --md-sys-color-surface-bright: #FEF7FF;
  --md-sys-color-outline: #79747E;
  --md-sys-color-outline-variant: #CAC4D0;
  --md-sys-color-inverse-surface: #322F35;
  --md-sys-color-inverse-on-surface: #F5EFF7;
  --md-sys-color-inverse-primary: #D0BCFF;
  --md-sys-color-shadow: #000000;
  --md-sys-color-scrim: #000000;
}
```

### 暗色

```css
[data-theme='dark'] {
  --md-sys-color-primary: #D0BCFF;
  --md-sys-color-on-primary: #381E72;
  --md-sys-color-primary-container: #4F378B;
  --md-sys-color-on-primary-container: #EADDFF;
  --md-sys-color-secondary: #CCC2DC;
  --md-sys-color-on-secondary: #332D41;
  --md-sys-color-secondary-container: #4A4458;
  --md-sys-color-on-secondary-container: #E8DEF8;
  --md-sys-color-tertiary: #EFB8C8;
  --md-sys-color-on-tertiary: #492532;
  --md-sys-color-tertiary-container: #633B48;
  --md-sys-color-on-tertiary-container: #FFD8E4;
  --md-sys-color-error: #F2B8B5;
  --md-sys-color-on-error: #601410;
  --md-sys-color-error-container: #8C1D18;
  --md-sys-color-on-error-container: #F9DEDC;
  --md-sys-color-background: #141218;
  --md-sys-color-on-background: #E6E0E9;
  --md-sys-color-surface: #141218;
  --md-sys-color-on-surface: #E6E0E9;
  --md-sys-color-on-surface-variant: #CAC4D0;
  --md-sys-color-surface-variant: #49454F;
  --md-sys-color-surface-container-lowest: #0F0D13;
  --md-sys-color-surface-container-low: #1D1B20;
  --md-sys-color-surface-container: #211F26;
  --md-sys-color-surface-container-high: #2B2930;
  --md-sys-color-surface-container-highest: #36343B;
  --md-sys-color-surface-dim: #141218;
  --md-sys-color-surface-bright: #3B383E;
  --md-sys-color-outline: #938F99;
  --md-sys-color-outline-variant: #49454F;
  --md-sys-color-inverse-surface: #E6E0E9;
  --md-sys-color-inverse-on-surface: #322F35;
  --md-sys-color-inverse-primary: #6750A4;
  --md-sys-color-shadow: #000000;
  --md-sys-color-scrim: #000000;
}
```

## 动态配色：用 m3e-theme

`m3e-theme` 从 `color` 生成整套亮/暗配色，并处理对比度与弹簧动效：

```vue
<script setup>
import { ref, computed } from 'vue';
import '@m3e/web/theme';
import '@m3e/web/button';

const seed = ref('#006A6A');
const scheme = ref('auto');      // light | dark | auto
const contrast = ref('standard'); // standard | medium | high
const variant = ref('tonal-spot'); // 见下表
</script>

<template>
  <m3e-theme :color="seed" :scheme="scheme" :contrast="contrast" :variant="variant" motion="expressive">
    <m3e-button variant="filled">跟随主题</m3e-button>
  </m3e-theme>
</template>
```

`variant` 取值：`monochrome`、`neutral`、`tonal-spot`（默认）、`vibrant`、`expressive`、`fidelity`、`content`、`rainbow`、`fruit-salad`。切换 `seed` 会实时重算；`scheme="auto"` 跟随 `prefers-color-scheme`。

### 运行时切换

```js
const themeEl = document.querySelector('m3e-theme');
themeEl.scheme = 'dark';
themeEl.contrast = 'high';
themeEl.color = '#7D5260';
```

在 Vue 里优先改绑定的 ref（驱动 attribute/property），不要混用两套来源；混用后下次重渲染会被 Vue 覆盖。

### 只看主题预览

```vue
<m3e-theme-icon color="#004F4F" scheme="light" variant="tonal-spot" />
```

## 手动生成配色

需要把生成结果写进静态 CSS（例如邮件模板、SSR 首屏）时用 `@material/material-color-utilities`：

```bash
pnpm add @material/material-color-utilities
```

```js
import {
  argbFromHex, hexFromArgb, Hct, SchemeContent,
} from '@material/material-color-utilities';

function schemeTokens(seedHex, isDark = false, contrast = 0.0) {
  const scheme = new SchemeContent(Hct.fromInt(argbFromHex(seedHex)), isDark, contrast);
  const map = {
    primary: scheme.primary,
    'on-primary': scheme.onPrimary,
    'primary-container': scheme.primaryContainer,
    'on-primary-container': scheme.onPrimaryContainer,
    secondary: scheme.secondary,
    'on-secondary': scheme.onSecondary,
    'secondary-container': scheme.secondaryContainer,
    'on-secondary-container': scheme.onSecondaryContainer,
    tertiary: scheme.tertiary,
    'on-tertiary': scheme.onTertiary,
    'tertiary-container': scheme.tertiaryContainer,
    'on-tertiary-container': scheme.onTertiaryContainer,
    error: scheme.error,
    'on-error': scheme.onError,
    'error-container': scheme.errorContainer,
    'on-error-container': scheme.onErrorContainer,
    background: scheme.background,
    'on-background': scheme.onBackground,
    surface: scheme.surface,
    'on-surface': scheme.onSurface,
    'on-surface-variant': scheme.onSurfaceVariant,
    'surface-container-lowest': scheme.surfaceContainerLowest,
    'surface-container-low': scheme.surfaceContainerLow,
    'surface-container': scheme.surfaceContainer,
    'surface-container-high': scheme.surfaceContainerHigh,
    'surface-container-highest': scheme.surfaceContainerHighest,
    'surface-dim': scheme.surfaceDim,
    'surface-bright': scheme.surfaceBright,
    outline: scheme.outline,
    'outline-variant': scheme.outlineVariant,
    'inverse-surface': scheme.inverseSurface,
    'inverse-on-surface': scheme.inverseOnSurface,
    'inverse-primary': scheme.inversePrimary,
  };
  return Object.fromEntries(
    Object.entries(map).map(([k, v]) => [`--md-sys-color-${k}`, hexFromArgb(v)]),
  );
}

export function applyScheme(seedHex, isDark, contrast = 0) {
  const style = document.documentElement.style;
  for (const [k, v] of Object.entries(schemeTokens(seedHex, isDark, contrast))) {
    style.setProperty(k, v);
  }
}
```

**不要**同时用 `m3e-theme` 和 `applyScheme` 写同一批令牌：`m3e-theme` 会在自身子树内计算并覆盖，结果是两套值打架。二选一。

## 从图片取种子色

`@m3e/web/theme` 自带提取器，返回 `#RRGGBB`：

```js
import { getColorFromImage } from '@m3e/web/theme';

const img = document.querySelector('#cover');
img.crossOrigin = 'anonymous'; // 跨域图片需要
await img.decode();
const seed = await getColorFromImage(img);
seedRef.value = seed; // 交给 m3e-theme 的 :color
```

图片未加载完成时调用会得到错误结果；先 `decode()`/等 `load`。

## 对比度

`m3e-theme` 的 `contrast`：`standard`（默认）、`medium`、`high`。高对比度会拉开成对角色（如 `primary` 与 `on-primary`）的色调距离，但不改变色相。

用 `@material/material-color-utilities` 手动生成时，`SchemeContent` 的第三个参数是 0 / 0.5 / 1.0。

## 作用域主题

CSS 变量按祖先继承，`m3e-theme` 可以嵌套：

```vue
<m3e-theme color="#6750A4" scheme="auto">
  <section class="normal">…</section>

  <m3e-theme color="#B3261E" scheme="light">
    <section class="danger-zone">…</section>
  </m3e-theme>
</m3e-theme>
```

也可以只覆盖部分令牌做局部强调：

```css
.premium {
  --md-sys-color-primary: #B69DF8;
  --md-sys-color-on-primary: #21005D;
}
```

## 与 Vuetify 的区别

Vuetify 4 不使用 `--md-sys-*`。它的主题颜色是 RGB 三元组，组件里写成 `rgb(var(--v-theme-primary))`。两套色板不要混用；Vuetify 项目的令牌写法见 `vuetify-alternative.md`。
