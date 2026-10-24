# 主题与暗色模式

Element Plus 的主题有两层：**编译期 SCSS 变量**与**运行时 CSS 变量**。运行时层只覆盖变量，不重新生成派生色；所以“换主题”不是改一个 `--el-color-primary` 就结束。

## 1. 运行时主题（推荐）

### 只换主色

`applyPalette(primary, { mode })` 会一次写全 7 个变量；`applyTheme(primary, dark)` 额外切换 `html.dark`。

```js
import { applyTheme } from './styles/theme.js';

applyTheme('#6750A4', false); // 亮色
applyTheme('#6750A4', true);  // 暗色
```

生成规则：

| 变量 | 亮色（与白混合） | 暗色（与背景混合） |
|---|---|---|
| `--el-color-primary` | 原色 | 原色 |
| `-light-3/5/7/8/9` | 逐渐变浅（用于 hover、选中、浅底） | 逐渐变深（用于暗色底、hover 底） |
| `-dark-2` | 与黑混合 20% → 更深 | 与白混合 20% → **更亮** |

### 暗色下选文本色

暗色里 `primary-light-N` 是**更暗**的色，不能当链接色。需要高对比的主色文本时用 `--el-color-primary-dark-2`：

| 色 | 对 `#141414` 的对比度 |
|---|---|
| `#6750A4`（primary） | 2.86:1 |
| `#4e3e79`（light-3） | 2.01:1 |
| `#8573b6`（dark-2） | **4.50:1** |

即：暗色下主色文字用 `dark-2`，链接、图标、强调数字都适用；`light-N` 只用于背景层。

### 暗色开关

```js
import { ref, watch } from 'vue';
import { applyTheme } from './styles/theme.js';

const brand = ref('#6750A4');
const isDark = ref(document.documentElement.classList.contains('dark'));

watch([brand, isDark], ([color, dark]) => {
  applyTheme(color, dark);
  localStorage.setItem('theme', dark ? 'dark' : 'light');
  localStorage.setItem('brand', color);
}, { immediate: true });
```

**顺序必须是**：先切 `html.dark`（`applyTheme` 内部先 `toggle`），再按暗色模式重算 7 个变量。反过来会留下亮色推导的浅色档，暗色下出现亮蓝底。

### 首屏防闪白

在 `index.html` 的 `<head>` 里、任何样式之前执行：

```html
<script>
  (() => {
    const saved = localStorage.getItem('theme');
    const dark = saved ? saved === 'dark' : matchMedia('(prefers-color-scheme: dark)').matches;
    document.documentElement.classList.toggle('dark', dark);
  })();
</script>
```

应用启动后再按 `html.dark` 的状态调用一次 `applyTheme`，保证派生色与 class 一致。使用 `@vueuse/core` 时可直接 `useDark()`，但**仍要**在主题变化时重算调色板。

### 品牌色持久化与系统偏好

- 未手动选择时跟随 `prefers-color-scheme`；用户手动切换后写 `localStorage`，不再自动跟随。
- 提供“跟随系统”第三态时，监听 `matchMedia('(prefers-color-scheme: dark)')` 的 `change`。
- 主题切换要立即生效，不要要求刷新；切换按钮的 `aria-pressed` 反映当前状态。

## 2. 编译期主题（可选）

如果不想在运行时写内联变量，或要在首屏就有正确派生色，用 SCSS 一次生成：

```scss
// src/styles/element/index.scss
@forward 'element-plus/theme-chalk/src/common/var.scss' with (
  $colors: (
    'primary': ('base': #6750a4),
  ),
  $border-radius: (
    'base': 8px,
    'small': 4px,
    'round': 9999px,
    'circle': 50%,
  )
);

@use 'element-plus/theme-chalk/src/index.scss' as *;
```

```js
// vite.config.js
export default defineConfig({
  css: {
    preprocessorOptions: {
      scss: { additionalData: `@use "@/styles/element/index.scss" as *;` },
    },
  },
});
```

按需导入时 resolver 要指定 `importStyle: 'scss'`：

```js
ElementPlusResolver({ importStyle: 'scss' })
```

注意：

- SCSS 方案是**构建期**固定的；运行时再换品牌色仍要写 CSS 变量。
- 只适合“一套品牌色 + 亮/暗”的产品；多租户动态主色用运行时方案。
- 本设计层的运行时覆盖（圆角、间距）仍然生效；两者不冲突，但不要在 SCSS 里重复定义同一变量。

## 3. 组件级变量

Element Plus 的组件变量声明在组件选择器上，例如：

```css
.el-card {
  --el-card-padding: 16px;
  --el-card-border-radius: 12px;
}
```

**写在 `:root` 无效**，因为组件内部读取的是自身作用域的变量。要覆盖必须命中组件选择器，或在组件外层加类：

```css
.report-card .el-card__body { padding: 24px; }
```

单组件覆盖优先用官方变量；变量不存在时才用后代选择器。用 `:deep()` 穿透 scoped 样式：

```vue
<style scoped>
.report :deep(.el-card__body) { padding: var(--space-6); }
</style>
```

不要覆盖组件内部结构（`.el-input__inner` 的 padding、`.el-table__body` 的布局），库不承诺这些结构稳定。

## 4. ConfigProvider

```vue
<script setup>
import zhCn from 'element-plus/es/locale/lang/zh-cn';
</script>

<template>
  <el-config-provider :locale="zhCn" :size="'default'" :z-index="3000" :message="{ max: 3 }">
    <App />
  </el-config-provider>
</template>
```

| 配置 | 作用 | 注意 |
|---|---|---|
| `locale` | 分页、日期、上传等文案 | 全量导入时也可在 `app.use` 里设 |
| `size` | 全局控件尺寸 | 局部仍可用 `size` 覆盖 |
| `z-index` | 弹层起始层级 | 与页面固定头（1000）留出差距 |
| `message` | 消息最大同屏数 | `max: 3` 防止刷屏 |
| `namespace` | CSS 类前缀 | **不要改**：本设计层的选择器全部以 `el-` 开头 |

`namespace` 一旦改为 `ep`，类名变 `.ep-*`、变量变 `--ep-*`，`design-system.css` 全部失效。设计规范项目保持默认。

## 5. 对比度与可访问性

实测对比度（WCAG 公式，白底 / `#141414` 暗底）：

| 前景 | 白底 | 暗底 |
|---|---|---|
| `#303133`（text primary） | 13.02:1 | 1.41:1 |
| `#606266`（text regular） | 6.11:1 | 3.01:1 |
| `#909399`（text secondary） | 3.08:1 | 5.98:1 |
| `#a8abb2`（placeholder） | 2.30:1 | 8.01:1 |
| `#409EFF`（默认 primary） | 2.78:1 | 6.63:1 |
| `#337ecc`（默认 primary dark-2） | 4.20:1 | 4.38:1 |
| `#6750A4`（示例品牌色） | 6.44:1 | 2.86:1 |
| `#524083`（示例 dark-2） | 8.68:1 | 2.12:1 |

判定：

- **AA 正文**要求 4.5:1；**大字号**（≥24px，或 ≥18.66px 粗体）3:1；**UI 边界/图标** 3:1。
- 默认主色 `#409EFF` 配白字只有 2.78:1，**不满足 AA 正文**。若项目要求 AA，用更深的品牌色（`#6750A4` 配白字 6.44:1）或只把主色用于非文本图形，文字用 `text regular`。
- `secondary` / `placeholder` 不承载正文（见 `design-tokens.md`）。
- 暗色下 `#6750A4` 作正文只有 2.86:1；用 `dark-2`（`#8573b6`，4.50:1）。
- 表格斑马纹、禁用态不降低文字对比度；禁用文本允许低于 AA，但必须仍有“禁用”语义（`disabled` 属性 + 光标）。
- 顶部提示 `toast.info/warn/error` 用 `--el-color-<type>-light-9/-light-7` 作底色/边框、`--el-text-color-primary` 作文字；暗色下自动翻转，不需要额外覆盖。

## 6. 主题作用域

需要局部主题（例如预览卡片）时，用容器变量覆盖：

```vue
<div class="preview" :style="palette('#6750A4', 'light')">
  <el-button type="primary">预览</el-button>
</div>
```

`palette()` 返回变量对象，Vue 的 `:style` 会写到该容器；Element Plus 组件在这些变量下渲染。注意：

- 这只影响**变量读取**，不影响已编译的组件样式；
- 弹层（Dialog、Select 下拉）teleport 到 body 后**不会**继承容器变量；需要局部主题时用 `append-to-body="false"` 或接受全局主题；
- 暗色 class 仍要在 `html` 上，容器级无法切换 `html.dark`。

## 7. 常见问题

| 现象 | 原因 | 修正 |
|---|---|---|
| 改了主色，悬停还是蓝色 | 只写 `--el-color-primary` | 用 `theme.js` 写全 7 个 |
| 切暗色后按钮是亮蓝底 | 先算色后切 class，或未重算 | `applyTheme(color, true)` |
| 刷新瞬间白屏 | class 在 JS 挂载后才加 | `index.html` 内联脚本预设 |
| `:root` 改 Card 内边距无效 | 组件变量在组件作用域 | 命中 `.el-card` |
| 局部主题的 Dialog 不生效 | teleport 到 body | `append-to-body="false"` 或全局主题 |
| 暗色下链接看不清 | 用了 `light-3` | 用 `dark-2` |
| 改 namespace 后样式全乱 | 选择器前缀变了 | 保持 `el` |
| 消息弹窗层级被页头盖住 | 页面 `z-index: 9999` | 页头 ≤1000，弹层由管理器分配 |
