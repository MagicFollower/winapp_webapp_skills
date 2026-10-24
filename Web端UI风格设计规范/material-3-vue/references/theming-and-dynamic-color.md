# MD3 主题与动态配色（Vue 3）

`@m3e/web` 用 `m3e-theme` 统一管理动态色、明暗、对比度、密度与动效；Vuetify 4 用 `createVuetify` 的主题系统。两套令牌不要混用。

## m3e-theme（主路线）

### 属性

| 属性 | 取值 | 默认 | 说明 |
|---|---|---|---|
| `color` | 十六进制种子色 | `#6750A4` | 生成整套配色 |
| `scheme` | `light` / `dark` / `auto` | `auto` | `auto` 跟随 `prefers-color-scheme` |
| `contrast` | `standard` / `medium` / `high` | `standard` | 对比度级别 |
| `variant` | 见下表 | `tonal-spot` | 配色生成算法 |
| `motion` | `expressive` / `standard` | `expressive` | 弹簧或标准动效 |
| `density` | `0` / `-1` / `-2` | `0` | 密度缩放 |
| `strong-focus` | 布尔 | `false` | 更强的焦点环 |

`variant`：`monochrome`、`neutral`、`tonal-spot`、`vibrant`、`expressive`、`fidelity`、`content`、`rainbow`、`fruit-salad`。

```vue
<script setup>
import { ref } from 'vue';
const seed = ref('#006A6A');
const scheme = ref('auto');
const contrast = ref('standard');
</script>

<template>
  <m3e-theme
    :color="seed"
    :scheme="scheme"
    :contrast="contrast"
    variant="tonal-spot"
    motion="expressive"
    density="0"
    strong-focus
  >
    <m3e-button variant="filled">主题化按钮</m3e-button>
  </m3e-theme>
</template>
```

`m3e-theme` 直接放在 `<body>` 下时，还会把 body 的背景/文字色设为 `--md-sys-color-background` / `on-background`，并同步滚动条颜色。

### 运行时切换

改绑定的 ref 即可（属性被 Vue patch）：

```vue
<script setup>
import { ref, watchEffect } from 'vue';
const seed = ref(localStorage.getItem('seed') || '#6750A4');
const scheme = ref(localStorage.getItem('scheme') || 'auto');

watchEffect(() => {
  localStorage.setItem('seed', seed.value);
  localStorage.setItem('scheme', scheme.value);
});
</script>
```

也可以持有元素引用直接改 property：

```js
const themeEl = ref(null);
themeEl.value.scheme = 'dark';
themeEl.value.contrast = 'high';
```

**不要两种方式同时用**。ref 驱动 + 手动改 property，下一次重渲染会把手动值覆盖回去。

### 跟随系统

`scheme="auto"` 已内建媒体查询监听。若要在 UI 上显示当前实际明暗，读取计算值：

```js
const isDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
```

或给 `m3e-theme` 的 `change` 事件加监听（事件在主题变化时触发）。

### 作用域主题

```vue
<m3e-theme color="#6750A4" scheme="auto">
  <section>正常区域</section>

  <m3e-theme color="#B3261E" scheme="light" variant="vibrant">
    <section>危险操作区域</section>
  </m3e-theme>
</m3e-theme>
```

也可以只覆盖需要的语义令牌：

```css
.promo {
  --md-sys-color-primary: #B69DF8;
  --md-sys-color-on-primary: #21005D;
  --md-sys-color-primary-container: #3F2D7A;
  --md-sys-color-on-primary-container: #EADDFF;
}
```

只覆盖部分令牌时，务必成对覆盖 `X` 与 `on-X`，否则对比度可能不达标。

### 主题预览

```vue
<m3e-theme-icon color="#004F4F" scheme="light" variant="tonal-spot" />
```

### 密度

`density="-1"` / `"-2"` 缩小组件内边距与高度，适合桌面密集界面；不要用它替代响应式布局，触达目标仍应保持 48dp。

## 动态配色

### 用种子色

`color` 接受任意 `#RRGGBB`。运行时改色会重算所有 `--md-sys-color-*`：

```vue
<input type="color" v-model="seed" />
<m3e-theme :color="seed" scheme="auto">…</m3e-theme>
```

### 从图片取色

```vue
<script setup>
import { ref } from 'vue';
import { getColorFromImage } from '@m3e/web/theme';

const seed = ref('#6750A4');
const file = ref(null);

async function onFile(e) {
  const url = URL.createObjectURL(e.target.files[0]);
  const img = new Image();
  img.src = url;
  await img.decode();
  seed.value = await getColorFromImage(img);
  URL.revokeObjectURL(url);
}
</script>

<template>
  <input type="file" accept="image/*" @change="onFile" />
  <m3e-theme :color="seed" scheme="auto">…</m3e-theme>
</template>
```

跨域图片需要 `crossOrigin="anonymous"` 且服务器允许 CORS；`getColorFromImage` 需要图片已完整加载。

### 手动生成（静态 CSS / SSR 首屏）

用 `@material/material-color-utilities` 的 `SchemeContent` + `Hct`，把结果写成 `--md-sys-color-*`。完整函数见 `color-system.md`。

**二选一**：`m3e-theme` 会在子树内计算并覆盖同一批令牌；如果外层又用 JS 写 `:root`，两套值会打架。需要静态首屏时，先输出静态值，客户端再挂 `m3e-theme` 并保持相同种子色。

## 对比度

```vue
<m3e-theme :color="seed" :contrast="'high'">…</m3e-theme>
```

- `standard`：默认。
- `medium`：拉开配对色调。
- `high`：最大色调距离，用于视觉无障碍。

高对比度不改变色相，只改变成对角色之间的明度差。切换后实际生效的是 `--md-sys-color-*` 的计算值，业务样式只要用令牌就会自动跟随。

## Vuetify 4 主题

### 配置

```js
// plugins/vuetify.js
import 'vuetify/styles';
import { createVuetify } from 'vuetify';
import { md3 } from 'vuetify/blueprints';

const light = {
  dark: false,
  colors: {
    background: '#FEF7FF',
    surface: '#FEF7FF',
    'surface-bright': '#FEF7FF',
    'surface-light': '#F3EDF7',
    'surface-variant': '#E7E0EC',
    'on-surface-variant': '#49454F',
    primary: '#6750A4',
    secondary: '#625B71',
    tertiary: '#7D5260',
    error: '#B3261E',
  },
};

const dark = {
  dark: true,
  colors: {
    background: '#141218',
    surface: '#141218',
    'surface-bright': '#3B383E',
    'surface-light': '#211F26',
    'surface-variant': '#49454F',
    'on-surface-variant': '#CAC4D0',
    primary: '#D0BCFF',
    secondary: '#CCC2DC',
    tertiary: '#EFB8C8',
    error: '#F2B8B5',
  },
};

export default createVuetify({
  blueprint: md3,
  theme: { defaultTheme: 'system', themes: { light, dark } },
});
```

```js
// main.js
import { createApp } from 'vue';
import vuetify from './plugins/vuetify';
import App from './App.vue';

createApp(App).use(vuetify).mount('#app');
```

### 运行时切换

```vue
<script setup>
import { useTheme } from 'vuetify';
const theme = useTheme();
</script>

<template>
  <v-btn @click="theme.toggle()">切换明暗</v-btn>
  <v-btn @click="theme.change('dark')">深色</v-btn>
  <v-btn @click="theme.change('high-contrast')">高对比</v-btn>
</template>
```

`useTheme()` 提供 `name`、`current`、`global`、`themes`、`change()`、`toggle()`、`cycle()`。`v-theme-provider` 或组件的 `theme` 属性可以给子树换主题。

### 令牌格式

Vuetify 的 CSS 变量是 **RGB 三元组**：

```css
.my-box {
  background: rgb(var(--v-theme-primary));
  color: rgba(var(--v-theme-on-primary), 0.9);
}
```

不是十六进制，也不是 `--md-sys-color-*`。因此：

- 从 `--md-sys-*` 迁移到 Vuetify 时，需要把 hex 转成 `r g b`，或直接在 Vuetify 主题配置里写 hex（推荐）。
- 不要在 Vuetify 组件上设置 `--md-sys-color-primary` 期待它变色。
- 需要 MD3 语义角色（如 `surface-container-high`）时，在 Vuetify 主题里定义自定义颜色名，并在自己的 CSS 中使用；Vuetify 组件不会自动套用这些角色。

### 与 m3e-theme 共存

同一页面不要同时挂 `m3e-theme` 与 Vuetify 主题并期望一致：两套算法、两套令牌、两套圆角/排版默认值。要么全 Vuetify，要么全 `@m3e/web`；只在边界处（如嵌入一个第三方 Vuetify 表格）做局部隔离，并为该区域单独设定颜色。

## 主题状态管理示例

一个可持久化的组合式函数（主路线）：

```js
// composables/useM3eTheme.js
import { ref, watch } from 'vue';

const seed = ref(localStorage.getItem('m3e.seed') || '#6750A4');
const scheme = ref(localStorage.getItem('m3e.scheme') || 'auto');
const contrast = ref(localStorage.getItem('m3e.contrast') || 'standard');

watch([seed, scheme, contrast], () => {
  localStorage.setItem('m3e.seed', seed.value);
  localStorage.setItem('m3e.scheme', scheme.value);
  localStorage.setItem('m3e.contrast', contrast.value);
});

export function useM3eTheme() {
  return { seed, scheme, contrast };
}
```

```vue
<script setup>
import { useM3eTheme } from '@/composables/useM3eTheme';
const { seed, scheme, contrast } = useM3eTheme();
</script>

<template>
  <m3e-theme :color="seed" :scheme="scheme" :contrast="contrast" motion="expressive">
    <slot />
  </m3e-theme>
</template>
```

`localStorage` 在 SSR 中不存在；Nuxt 把这段放到 `.client` composable 或在 `onMounted` 后再读。SSR 首屏用固定种子色，避免水合不匹配。

## 主题排障

| 现象 | 原因 | 处理 |
|---|---|---|
| 改了 `:root` 但组件不变 | 组件在 `m3e-theme` 子树内，主题值优先 | 改 `m3e-theme` 的 `color`/`scheme` |
| 暗色只影响部分区域 | `m3e-theme` 只覆盖子树 | 把主题提到应用根，或给每个子树加主题 |
| 颜色对但文字看不清 | 只覆盖了 `primary` 没覆盖 `on-primary` | 成对覆盖 |
| Vuetify 组件不吃 `--md-sys-*` | 令牌体系不同 | 用 `--v-theme-*` 或在 Vuetify 主题配置里设色 |
| SSR 首屏闪一下 | 自定义元素客户端才升级 | `<ClientOnly>` + 固定首屏种子色 |
| 切换主题后焦点环丢失 | 手动改 property 与 ref 冲突 | 只保留一个来源 |
