# MD3 排版、形状、层阶与动效（Vue 3）

色彩之外的视觉令牌。所有值来自 MD3 规范；令牌名严格按 `@m3e/web` 2.10.2 实际读取的 CSS 变量书写。

## 排版

### 15 个基线角色

| 角色 | 字重 | 字号 | 行高 | 字距 | 典型用途 |
|---|---|---|---|---|---|
| display-large | 400 | 57px / 3.5625rem | 64px / 4rem | 0.25px | Hero 数字、超大标题 |
| display-medium | 400 | 45px / 2.8125rem | 52px / 3.25rem | 0 | 大数字 |
| display-small | 400 | 36px / 2.25rem | 44px / 2.75rem | 0 | 大标题 |
| headline-large | 400 | 32px / 2rem | 40px / 2.5rem | 0 | 页面主标题 |
| headline-medium | 400 | 28px / 1.75rem | 36px / 2.25rem | 0 | 区块标题 |
| headline-small | 400 | 24px / 1.5rem | 32px / 2rem | 0 | 卡片组标题 |
| title-large | 400 | 22px / 1.375rem | 28px / 1.75rem | 0 | App Bar 标题 |
| title-medium | 500 | 16px / 1rem | 24px / 1.5rem | 0.15px | 卡片标题 |
| title-small | 500 | 14px / 0.875rem | 20px / 1.25rem | 0.1px | 小标题、Tab |
| body-large | 400 | 16px / 1rem | 24px / 1.5rem | 0.5px | 正文 |
| body-medium | 400 | 14px / 0.875rem | 20px / 1.25rem | 0.25px | 次要正文 |
| body-small | 400 | 12px / 0.75rem | 16px / 1rem | 0.4px | 辅助说明 |
| label-large | 500 | 14px / 0.875rem | 20px / 1.25rem | 0.1px | 按钮、Chip |
| label-medium | 500 | 12px / 0.75rem | 16px / 1rem | 0.5px | 导航标签 |
| label-small | 500 | 11px / 0.6875rem | 16px / 1rem | 0.5px | Badge |

> `@m3e/web` 的默认 display-large 字距是 `0.015625rem`（0.25px），不是负数；以令牌默认值为准。

### 15 个强调角色

与基线一一对应，字重更高，用于选中态、主操作、需要突出的标题。令牌名在角色前加 `emphasized-`：

```
--md-sys-typescale-emphasized-title-large-font-weight
--md-sys-typescale-emphasized-body-large-font-size
```

### 真实令牌名

每个角色只有 4 个轴向令牌：

```css
/* display-large 的真实令牌（2.10.2） */
--md-sys-typescale-display-large-font-size: 3.5625rem;
--md-sys-typescale-display-large-font-weight: 400;
--md-sys-typescale-display-large-line-height: 4rem;
--md-sys-typescale-display-large-tracking: 0.015625rem;
```

- **没有** `--md-sys-typescale-*-font`、`-size`、`-weight` 这些简写；写成 `font-size: var(--md-sys-typescale-body-large-size)` 会得到空值。
- **没有**逐角色的 `font-family` 令牌。字体族在全局设置：

```css
body {
  font-family: 'Roboto Flex', Roboto, 'Noto Sans SC', sans-serif;
}
```

### 在 Vue 中应用

首选组件（自动带语义层级与正确角色）：

```vue
<script setup>
import '@m3e/web/heading';
</script>

<template>
  <m3e-heading variant="display" size="large" level="1">大标题</m3e-heading>
  <m3e-heading variant="headline" size="medium" level="2">区块标题</m3e-heading>
  <m3e-heading variant="title" size="medium" level="3">卡片标题</m3e-heading>
  <m3e-heading variant="label" size="large" emphasized>强调标签</m3e-heading>
</template>
```

- `variant`：`display` / `headline` / `title` / `label`
- `size`：`small` / `medium` / `large`
- `level`：1–6，控制渲染的 HTML 标题级别（无障碍结构），与视觉大小解耦
- `emphasized`：使用强调字重

自定义文本用工具类或直接组合令牌：

```css
.md3-title-medium {
  font-size: var(--md-sys-typescale-title-medium-font-size);
  font-weight: var(--md-sys-typescale-title-medium-font-weight);
  line-height: var(--md-sys-typescale-title-medium-line-height);
  letter-spacing: var(--md-sys-typescale-title-medium-tracking);
}
```

不要写 `font: var(--md-sys-typescale-...)` 简写；令牌不是一个完整的 `font` 值。

### 组件角色映射

| 组件区域 | 角色 |
|---|---|
| 按钮文字 | label-large |
| Card 标题 / 正文 | title-medium / body-medium |
| 顶部 App Bar 标题 | title-large |
| 导航标签 | label-medium |
| Dialog 标题 / 正文 | headline-small / body-medium |
| Chip 标签 | label-large |
| 输入框输入 / 标签 | body-large / body-small（浮动） |
| List 主文本 / 辅助文本 | body-large / body-medium |
| Snackbar | body-medium |
| Tooltip | body-small |
| Tab | title-small |
| Badge 数字 | label-small |

### 字体族

- 品牌字体用于 display/headline，正文字体用于 title/body/label；两者默认都是 Roboto。
- Roboto Flex 是可变字体（wght 100–1000、wdth 25–151、opsz 8–144），按需自托管或走 Google Fonts。
- 中文项目补 `'Noto Sans SC'` 等 CJK 回退，但不要把 CJK 字体放在 Roboto 前面，否则拉丁字母会被替换。

```css
@font-face {
  font-family: 'Roboto Flex';
  src: url('/fonts/RobotoFlex.woff2') format('woff2');
  font-weight: 100 1000;
  font-stretch: 25% 151%;
}
```

**单位**：Android 的 sp 在 Web 上按 1sp = 1px（根字号 16px）换算，例如 14sp = 0.875rem。

## 形状

### 圆角值

组件读取 `--md-sys-shape-corner-value-*`：

| 令牌 | 值 | 默认组件 |
|---|---|---|
| `none` | 0 | — |
| `extra-small` | 4px | Snackbar |
| `small` | 8px | 输入框、菜单、Chip |
| `medium` | 12px | Card |
| `large` | 16px | FAB、导航抽屉 |
| `large-increased` | 20px | Expressive |
| `extra-large` | 28px | Dialog、Bottom Sheet |
| `extra-large-increased` | 32px | Expressive |
| `extra-extra-large` | 48px | Expressive |
| `full` | 9999px | Button、Badge、Slider |

另有方向性令牌供局部圆角使用：`--md-sys-shape-corner-large-top`、`-large-start`、`-large-end`、`-extra-large-top`、`-extra-small-top`。

### 组件形状

| 组件 | 形状 |
|---|---|
| Button（全部变体） | full |
| FAB / Extended FAB | large |
| Icon Button | full |
| Chip | small |
| Card | medium |
| Dialog | extra-large |
| Text field | small（顶部） |
| Menu | small |
| Navigation drawer | large（末端） |
| Bottom sheet | extra-large（顶部） |
| Snackbar | extra-small |
| Badge / Slider handle / Switch track | full |
| Tabs indicator | full（顶部） |
| Search bar | full |

不要给库组件写 `border-radius`；用公开变量：

```css
m3e-card { --m3e-card-shape: var(--md-sys-shape-corner-value-large); }
m3e-dialog { --m3e-dialog-shape: var(--md-sys-shape-corner-value-extra-large); }
```

具体变量名以组件 `.d.ts` 的 `@cssprop` 为准；不确定时先用主题级值令牌。

### G2（强制）

上面的圆角令牌只决定**半径**，默认形状仍是圆弧（G1）。本项目要求所有圆角为 **G2 曲率连续**：

```css
:root { --g2-shape: squircle; } /* squircle = superellipse(2) = p4 */

@supports (corner-shape: squircle) {
  *, *::before, *::after { corner-shape: var(--g2-shape, squircle); }
}
```

- `corner-shape` 不继承，必须逐元素声明；Shadow DOM 用 `assets/g2/g2-shadow.js` 注入。
- 半径与描边要过闸门：squircle 的最大描边约为 `0.3964 × r ÷ 1.2`（r=8 → 2.64px，r=12 → 3.96px）。
- 完整规则、`clip-path` 兜底、SVG/Canvas 与验收见 `references/corner-g2.md`。

### 抽象形状（Expressive）

`m3e-shape` 提供 Material Shape 库的 35 个形状，公开属性只有 `name`：

```vue
<m3e-shape name="4-leaf-clover" class="deco">…</m3e-shape>
<m3e-shape name="burst">…</m3e-shape>
```

形状名：`4-leaf-clover`、`4-sided-cookie`、`6-sided-cookie`、`7-sided-cookie`、`8-leaf-clover`、`9-sided-cookie`、`12-sided-cookie`、`arch`、`arrow`、`boom`、`bun`、`burst`、`circle`、`diamond`、`fan`、`flower`、`gem`、`ghost-ish`、`heart`、`hexagon`、`oval`、`pentagon`、`pill`、`pixel-circle`、`pixel-triangle`、`puffy`、`puffy-diamond`、`semicircle`、`slanted`、`soft-boom`、`soft-burst`、`square`、`sunny`、`triangle`、`very-sunny`。

> 2.10.2 的 `m3e-shape` **没有** `morphTo`、`on` 之类的属性；不要照搬其他库的 API。形状装饰用于强调，不要拿它承载正文。

## 层阶

| Level | DP | 色调表面 | 静置组件 |
|---|---|---|---|
| 0 | 0 | surface | 扁平 App Bar、filled/tonal/outlined Button、Card、Chip、List、Tab |
| 1 | 1 | surface-container-low | Elevated Button/Card/Chip、模态抽屉 |
| 2 | 3 | surface-container | Menu、Nav Bar、滚动后的 App Bar、Toolbar |
| 3 | 6 | surface-container-high | FAB、Dialog、Search、日期/时间选择器 |
| 4 | 8 | surface-container-highest | 悬停/聚焦提升 |
| 5 | 12 | surface-container-highest | 悬停/聚焦提升（少见） |

MD3 用**色调**而不是阴影表达层阶。只在下列情况用阴影：

- 浮在图片/繁忙内容上（FAB、Menu）；
- 色调差不足以区分重叠元素；
- 平台约定需要（部分桌面外壳）。

需要阴影时用 MD3 的 5 级：

```css
--md3-shadow-1: 0 1px 2px rgba(0,0,0,.3), 0 1px 3px 1px rgba(0,0,0,.15);
--md3-shadow-2: 0 1px 2px rgba(0,0,0,.3), 0 2px 6px 2px rgba(0,0,0,.15);
--md3-shadow-3: 0 4px 8px 3px rgba(0,0,0,.15), 0 1px 3px rgba(0,0,0,.3);
--md3-shadow-4: 0 6px 10px 4px rgba(0,0,0,.15), 0 2px 3px rgba(0,0,0,.3);
--md3-shadow-5: 0 8px 12px 6px rgba(0,0,0,.15), 0 4px 4px rgba(0,0,0,.3);
```

`@m3e/web` 内部还读取 `--md-sys-elevation-level0..5`；不要手动改这些值，除非确认目标组件只用它。

## 动效

### 弹簧（Expressive）

```vue
<m3e-theme motion="expressive">…</m3e-theme>
```

`motion` 取 `expressive`（默认）或 `standard`。弹簧动画没有固定时长，由库的 motion scheme 控制；不要在子组件上再写 `transition-duration` 覆盖。

`@m3e/web` 还读取这些弹簧令牌（一般不必手改）：

```
--md-sys-motion-spring-fast-spatial
--md-sys-motion-spring-default-spatial
--md-sys-motion-spring-slow-spatial
--md-sys-motion-spring-fast-effects
--md-sys-motion-spring-default-effects
--md-sys-motion-spring-slow-effects
```

### 缓动与持续时间

自定义 CSS 过渡用同一套令牌：

| 令牌 | 值 | 用途 |
|---|---|---|
| `--md-sys-motion-easing-emphasized` | `cubic-bezier(0.2, 0, 0, 1)` | 留在屏幕上的过渡 |
| `--md-sys-motion-easing-emphasized-decelerate` | `cubic-bezier(0.05, 0.7, 0.1, 1)` | 进入 |
| `--md-sys-motion-easing-emphasized-accelerate` | `cubic-bezier(0.3, 0, 0.8, 0.15)` | 退出 |
| `--md-sys-motion-easing-standard` | `cubic-bezier(0.2, 0, 0, 1)` | 功能性过渡 |
| `--md-sys-motion-easing-standard-decelerate` | `cubic-bezier(0, 0, 0, 1)` | 功能性进入 |
| `--md-sys-motion-easing-standard-accelerate` | `cubic-bezier(0.3, 0, 1, 1)` | 功能性退出 |
| `--md-sys-motion-duration-short-1..4` | 50 / 100 / 150 / 200ms | 微交互 |
| `--md-sys-motion-duration-medium-1..4` | 250 / 300 / 350 / 400ms | 标准过渡 |
| `--md-sys-motion-duration-long-1..4` | 450 / 500 / 550 / 600ms | 大范围过渡 |
| `--md-sys-motion-duration-extra-long-1..4` | 700 / 800 / 900 / 1000ms | 页面过渡 |

推荐搭配：留在屏幕上 emphasized/500ms；进入 emphasized-decelerate/400ms；永久退出 emphasized-accelerate/200ms；临时退出 emphasized/300ms；小型功能过渡 standard/300ms。

```vue
<Transition name="md3-dialog">
  <m3e-dialog v-if="open" open>…</m3e-dialog>
</Transition>
```

```css
.md3-dialog-enter-active {
  transition: opacity var(--md-sys-motion-duration-medium-4) var(--md-sys-motion-easing-emphasized-decelerate),
              transform var(--md-sys-motion-duration-medium-4) var(--md-sys-motion-easing-emphasized-decelerate);
}
.md3-dialog-enter-from { opacity: 0; transform: scale(.8); }
.md3-dialog-leave-active {
  transition: opacity var(--md-sys-motion-duration-short-4) var(--md-sys-motion-easing-emphasized-accelerate);
}
.md3-dialog-leave-to { opacity: 0; }
```

> 组件自身的进入/退出已经内置动效；只有在用 `v-if` 包裹或需要自定义外壳时才加 Vue `<Transition>`，否则会叠加两层动画。

## Vuetify 4 对照

- 排版类：`text-display-large`、`text-headline-medium`、`text-title-small`、`text-body-large`、`text-label-large`。
- 圆角类/属性：`rounded="sm|md|lg|xl"`，v4 允许任意 px 值。
- 主题字体：`--v-font-body`、`--v-font-heading`。
- 动效：Vuetify 有内置过渡（`v-dialog-transition` 等），但不提供 MD3 弹簧物理令牌；需要弹簧时用 CSS 近似或改用 `@m3e/web`。
