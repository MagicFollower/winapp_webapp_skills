# Vuetify 4 备选路线

当项目要求**原生 Vue 组件、完整数据表格生态、Nuxt 一等支持**时用 Vuetify 4；要 M3E Expressive 的弹簧/形状变换与 `--md-sys-*` 令牌时用 `@m3e/web`。两者不要在同一主题域混用。

- 版本基线：Vuetify **4.3.0**（2026-10 当前最新），peer `vue ^3.5.0`。
- Vuetify 4 是活跃主线；Vuetify 3 自 2026-07 进入维护（LTS 到 2027-07）。
- MD3 蓝图：`vuetify/blueprints` 的 `md3`。

## 安装与接入

```bash
pnpm add vuetify@4.3.0 @mdi/font
```

```js
// vite.config.js
import { defineConfig } from 'vite';
import vue from '@vitejs/plugin-vue';
import vuetify from 'vite-plugin-vuetify';

export default defineConfig({
  plugins: [vue(), vuetify({ autoImport: true })],
});
```

`vite-plugin-vuetify` 负责按需自动导入组件与样式；不用它也可以，但需要手动导入 `vuetify/styles` 与组件。

```js
// plugins/vuetify.js
import 'vuetify/styles';
import '@mdi/font/css/materialdesignicons.css';
import { createVuetify } from 'vuetify';
import { md3 } from 'vuetify/blueprints';

export default createVuetify({
  blueprint: md3,
  theme: {
    defaultTheme: 'system',
    themes: {
      light: {
        dark: false,
        colors: {
          background: '#FEF7FF',
          surface: '#FEF7FF',
          'surface-bright': '#FEF7FF',
          'surface-light': '#F3EDF7',
          'surface-variant': '#E7E0EC',
          'on-surface-variant': '#49454F',
          primary: '#6750A4',
          'primary-darken-1': '#4F378B',
          secondary: '#625B71',
          tertiary: '#7D5260',
          error: '#B3261E',
        },
      },
      dark: {
        dark: true,
        colors: {
          background: '#141218',
          surface: '#141218',
          'surface-bright': '#3B383E',
          'surface-light': '#211F26',
          'surface-variant': '#49454F',
          'on-surface-variant': '#CAC4D0',
          primary: '#D0BCFF',
          'primary-darken-1': '#B69DF8',
          secondary: '#CCC2DC',
          tertiary: '#EFB8C8',
          error: '#F2B8B5',
        },
      },
    },
  },
});
```

```js
// main.js
import { createApp } from 'vue';
import vuetify from './plugins/vuetify';
import App from './App.vue';

createApp(App).use(vuetify).mount('#app');
```

`md3` 蓝图设置的默认值（来自 4.3.0 源码，不是猜测）：

| 组件 | 默认 |
|---|---|
| `VBtn` | `color: primary`, `rounded: xl` |
| `VCard` | `rounded: lg` |
| `VBtnGroup` | `rounded: xl`，内部按钮取消圆角 |
| `VTextField` / `VTextarea` / `VSelect` / `VAutocomplete` / `VNumberInput` / `VFileInput` | `variant: outlined` |
| `VCheckbox` | `color: secondary`, `inset: true` |
| `VChip` | `rounded: sm` |
| `VProgressLinear` | `rounded`, `stopIndicator`, `variant: split` |
| `VProgressCircular` | `rounded`, `variant: split` |
| `VDatePicker` | `rounded: xl`, `elevation: 1`, `divided: true` |
| `VTabs` | `color: primary`, `sliderVariant: primary` |
| `VAppBar` | `flat: true` |
| `VBottomSheet` | `rounded-t-xl overflow-hidden` |

## 令牌对照

Vuetify **不暴露** `--md-sys-color-*`。它的主题色是 RGB 三元组：

```css
--v-theme-primary: 103 80 164;          /* 对应 #6750A4 */
background: rgb(var(--v-theme-primary));
color: rgb(var(--v-theme-on-primary));
```

| MD3 语义 | Vuetify 主题键 | 说明 |
|---|---|---|
| `surface` | `surface` | 组件背景 |
| `background` | `background` | 应用背景 |
| `on-surface` | `on-surface` | 自动生成或显式配置 |
| `primary` / `secondary` / `tertiary` / `error` | 同名 | 强调色 |
| `surface-container` | 无内建键 | 可用 `surface-light` 近似，或自定义键 |
| `surface-container-low/high/...` | 无 | 组件不会自动使用 |
| `outline` / `outline-variant` | 无 | Vuetify 用 `border-color` 变量与透明度 |
| `inverse-surface` | 无 | Snackbar 用 `theme` 反色实现 |

需要 MD3 语义名时，在主题 `colors` 里加自定义键（如 `surface-container-high`），但**只有你自己写 CSS 时能用**，Vuetify 组件不会自动套用。

**hex → RGB 三元组**：

```js
function toTriplet(hex) {
  const n = parseInt(hex.slice(1), 16);
  return `${(n >> 16) & 255} ${(n >> 8) & 255} ${n & 255}`;
}
```

### G2 圆角与间距（必做）

Vuetify 的圆角走 light DOM，`corner-shape` 全局规则同样生效：

```css
:root { --g2-shape: squircle; }

@supports (corner-shape: squircle) {
  *, *::before, *::after { corner-shape: var(--g2-shape, squircle); }
}
```

- `rounded` / `rounded-pill` 只改半径，不改曲率；没有 `corner-shape` 就仍是 G1。
- 布局用同一套 4dp 子集（0/4/8/12/16/24/32/48/64）；Vuetify 的 `ma-*` / `pa-*` 里 `1=4px`、`2=8px`、`3=12px`、`4=16px`、`6=24px`。完整约束见 `layout-rules.md`。

## 应用外壳

```vue
<script setup>
import { ref } from 'vue';
const drawer = ref(true);
</script>

<template>
  <v-app>
    <v-app-bar flat>
      <v-app-bar-nav-icon @click="drawer = !drawer" />
      <v-app-bar-title>页面标题</v-app-bar-title>
      <v-btn icon="mdi-magnify" aria-label="搜索" />
      <v-btn icon="mdi-dots-vertical" aria-label="更多" />
    </v-app-bar>

    <v-navigation-drawer v-model="drawer" :rail="false">
      <v-list nav>
        <v-list-item prepend-icon="mdi-home" title="首页" active />
        <v-list-item prepend-icon="mdi-magnify" title="搜索" />
        <v-list-subheader>设置</v-list-subheader>
        <v-list-group value="appearance">
          <template #activator="{ props }">
            <v-list-item v-bind="props" prepend-icon="mdi-palette" title="外观" />
          </template>
          <v-list-item title="主题" />
          <v-list-item title="字体" />
        </v-list-group>
      </v-list>
    </v-navigation-drawer>

    <v-main>
      <v-container class="pa-4 pa-md-6" style="max-width: 1040px">
        <slot />
      </v-container>
    </v-main>

    <v-bottom-navigation v-if="$vuetify.display.mobile" grow>
      <v-btn value="home"><v-icon>mdi-home</v-icon>首页</v-btn>
      <v-btn value="search"><v-icon>mdi-magnify</v-icon>搜索</v-btn>
      <v-btn value="me"><v-icon>mdi-account</v-icon>我的</v-btn>
    </v-bottom-navigation>
  </v-app>
</template>
```

- `v-main` 自动处理 App Bar 与 Drawer 的偏移。
- `$vuetify.display.mobile` 来自 `useDisplay()`；也可以用 `mobile-breakpoint` 控制抽屉。
- Vuetify 的断点是 `xs<600 / sm 600–959 / md 960–1279 / lg 1280–1919 / xl 1920+`，与 MD3 的 600/840/1200/1600 不完全一致；按 MD3 设计时用自定义媒体查询。

## MD3 视觉映射

| MD3 | Vuetify 写法 |
|---|---|
| Filled Button | `<v-btn variant="flat" color="primary">` |
| Tonal Button | `<v-btn variant="tonal" color="primary">`（或用 `color="secondary"`） |
| Outlined Button | `<v-btn variant="outlined">` |
| Text Button | `<v-btn variant="text">` |
| Elevated Button | `<v-btn variant="elevated">` |
| Button 尺寸 | `size="x-small|small|default|large|x-large"` |
| Filled Tonal Card | `<v-card variant="tonal">` |
| Outlined Card | `<v-card variant="outlined">` |
| Elevated Card | `<v-card variant="elevated">` |
| FAB | `<v-btn icon="mdi-plus" color="primary" size="large" rounded="lg" />`（Vuetify 也有 `v-fab`，按项目版本核对 API） |
| Segmented | `<v-btn-toggle>` |
| Nav Bar | `<v-bottom-navigation>` |
| Nav Rail | `<v-navigation-drawer rail>` |
| Dialog | `<v-dialog max-width="560">` |
| Bottom Sheet | `<v-bottom-sheet>` |
| Snackbar | `<v-snackbar>` |
| Chip | `<v-chip>` / `<v-chip-group>` |
| Search Bar | `<v-text-field prepend-inner-icon="mdi-magnify" rounded="pill" variant="filled">` |
| Skeleton | `<v-skeleton-loader type="card">` |

MD3 的 `border-radius` 令牌 → `rounded="sm|md|lg|xl"`；Vuetify 4 允许任意值（`rounded="12"`）。

## 表单

```vue
<script setup>
import { ref } from 'vue';
const email = ref('');
const plan = ref('pro');
const agree = ref(false);
const valid = ref(true);
</script>

<template>
  <v-form @submit.prevent="save">
    <v-text-field
      v-model="email"
      label="邮箱"
      type="email"
      variant="outlined"
      :rules="[v => !!v || '请输入邮箱', v => /.+@.+\..+/.test(v) || '格式不正确']"
      persistent-hint
      hint="不会公开"
    />

    <v-select
      v-model="plan"
      label="方案"
      variant="outlined"
      :items="[
        { title: 'Free', value: 'free' },
        { title: 'Pro', value: 'pro' },
        { title: 'Team', value: 'team' },
      ]"
    />

    <v-checkbox v-model="agree" label="接受条款" :rules="[v => v || '必须接受']" />

    <div class="d-flex justify-end ga-2">
      <v-btn variant="text" type="reset">取消</v-btn>
      <v-btn variant="flat" color="primary" type="submit">提交</v-btn>
    </div>
  </v-form>
</template>
```

`variant="outlined"` 是 md3 蓝图给文本类输入的默认值；不用蓝图时显式写。

## 数据表格（Vuetify 的优势）

```vue
<script setup>
import { ref } from 'vue';
const headers = [
  { title: '项目', key: 'name' },
  { title: '负责人', key: 'owner' },
  { title: '状态', key: 'status' },
  { title: '', key: 'actions', sortable: false },
];
const items = ref([
  { name: '设计系统', owner: '张三', status: '进行中' },
  { name: '官网改版', owner: '李四', status: '已完成' },
]);
</script>

<template>
  <v-data-table :headers="headers" :items="items" item-value="name">
    <template #item.actions="{ item }">
      <v-btn icon="mdi-pencil" variant="text" size="small" :aria-label="`编辑 ${item.name}`" />
    </template>
  </v-data-table>
</template>
```

`@m3e/web` 没有数据表格；需要表格、虚拟滚动、排序/分页一体化时，这是选 Vuetify 的最强理由。若只缺表格，也可以 `@m3e/web` + TanStack Table 自绘表头/行。

## 日期与时间

```vue
<script setup>
import { ref } from 'vue';
const date = ref(new Date());
const time = ref('14:30');
</script>

<template>
  <v-date-picker v-model="date" />
  <v-time-picker v-model="time" format="24hr" />
  <v-date-input v-model="date" label="日期" variant="outlined" />
</template>
```

Vuetify 4 的 `VDatePicker` 已按 MD3 调整（圆角、范围选择、键盘方向键）；`VDateInput` 是分段输入。

## 迁移与共存

**从 Vuetify 3 升到 4：**

1. 换 `blueprint: md3`（若还没用）。
2. 检查 `variant`：Vuetify 4 的 `flat` 仍是 filled 的写法；`text`/`outlined`/`tonal`/`elevated` 不变。
3. 断点与网格有调整；重跑响应式验收（600/840/1200/1600）。
4. 主题色若写死在 Sass 变量里，迁到 `createVuetify({ theme })` 的运行时主题。

**与 `@m3e/web` 共存：**

- 不要在同一主题域混用；把 Vuetify 区域包在独立布局里，显式给它 `theme`，并避免 `--md-sys-*` 与 `--v-theme-*` 互相期待。
- 两个库的圆角/排版/密度比例不同，视觉上会出现“两套 MD3”。要么统一，要么在文档里明确分区。

**不从 `@m3e/web` 迁移的判断标准：**

| 需求 | 留 `@m3e/web` | 换 Vuetify |
|---|---|---|
| Expressive 弹簧/形状变换 | ✅ | ❌ |
| 与 React 版像素级一致 | ✅ | ❌ |
| 数据表格/虚拟滚动/树表 | ❌ | ✅ |
| 原生 Vue 组件与生态 | 一般 | ✅ |
| 包体积与 tree-shaking | ✅ | 取决于插件配置 |
| Nuxt 集成 | 需 client-only | ✅ 一等支持 |

## 审查清单（Vuetify 项目）

- [ ] 主题令牌只走 `--v-theme-*`；没有混用 `--md-sys-*`
- [ ] 所有非零圆角有 `corner-shape`；没有把 `rounded` 当作 G2
- [ ] 间距落在 4dp 子集；卡片/表单/触达符合 `layout-rules.md`
- [ ] 没有用 `--md-sys-color-*` 驱动 Vuetify 组件

- [ ] `package.json` 是 `vuetify@^4.3.0`，不是 3.x。
- [ ] `createVuetify({ blueprint: md3 })`。
- [ ] 主题色在 `theme.themes` 里，不散落硬编码。
- [ ] 自定义 CSS 用 `rgb(var(--v-theme-*))`，没有把 `--md-sys-*` 当成 Vuetify 令牌。
- [ ] `v-main` 包住内容，App Bar/Drawer 的偏移由框架处理。
- [ ] 断点按 MD3 的 600/840/1200/1600 复核，不只依赖 `md`/`lg`。
- [ ] 大屏内容最大宽度 840–1040px。
- [ ] 表单用 `variant="outlined"` 或与设计一致的变体；错误提示用 `rules`/`error-messages`。
- [ ] 图标按钮有 `aria-label`。
- [ ] 不把 Vuetify 与 `@m3e/web` 混在同一主题域。
