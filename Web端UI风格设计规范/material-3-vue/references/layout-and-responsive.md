# MD3 布局与响应式（Vue 3 / Web）

MD3 的窗口尺寸类别、标准布局与在 Vue + `@m3e/web` 中的落地。布局级响应式用 CSS；组件级交互（抽屉、拖拽、导航）交给组件。

## 窗口尺寸类别

| 类别 | 宽度 | 典型设备 | 列数 |
|---|---|---|---|
| 紧凑 | <600px | 手机竖屏 | 4 |
| 中等 | 600–839px | 平板竖屏、折叠屏展开 | 8 |
| 展开 | 840–1199px | 平板横屏、小桌面 | 12 |
| 大 | 1200–1599px | 桌面 | 12 |
| 超大 | 1600px+ | 超宽屏 | 12 |

Web 上 1dp ≈ 1px（标准密度），断点直接写 CSS 像素：

```css
/* 移动优先：基础样式 = 紧凑 */
@media (min-width: 600px) { /* 中等 */ }
@media (min-width: 840px) { /* 展开 */ }
@media (min-width: 1200px) { /* 大 */ }
@media (min-width: 1600px) { /* 超大 */ }
```

## 边距与间距

| 窗口 | 屏幕边距 | 列间距 |
|---|---|---|
| 紧凑 | 16px | 8px |
| 中等 | 24px | 16px |
| 展开 | 24px | 16px |
| 大 | 24px | 24px |
| 超大 | 24px | 24px |

组件内边距用 4/8/12/16/24px，组件间距用 8/12/16/24px，区块间距用 24/32/48px。都取 4 的倍数。

```css
:root {
  --space-xxs: 4px;
  --space-xs: 8px;
  --space-sm: 16px;
  --space-md: 24px;
  --space-lg: 32px;
  --space-xl: 48px;
}
```

## 三种标准布局

### 信息流

单列 → 2 列 → 3 列 → 4 列：

```vue
<template>
  <div class="md3-feed">
    <m3e-card v-for="item in items" :key="item.id" variant="outlined">
      <div slot="content">{{ item.title }}</div>
    </m3e-card>
  </div>
</template>

<style scoped>
.md3-feed {
  display: grid;
  grid-template-columns: 1fr;
  gap: var(--space-xs);
  padding: var(--space-sm);
}
@media (min-width: 600px) {
  .md3-feed { grid-template-columns: repeat(2, 1fr); gap: var(--space-sm); padding: var(--space-md); }
}
@media (min-width: 840px) { .md3-feed { grid-template-columns: repeat(3, 1fr); } }
@media (min-width: 1200px) { .md3-feed { grid-template-columns: repeat(4, 1fr); gap: var(--space-md); } }
</style>
```

### 列表-详情

紧凑：列表与详情二选一；中等+：并排。可拖拽分隔用 `m3e-split-pane`。

```vue
<script setup>
import { ref } from 'vue';
const selected = ref(null);
</script>

<template>
  <div class="md3-list-detail" :class="{ 'detail-active': selected }">
    <aside class="list">
      <m3e-action-list>
        <m3e-list-action
          v-for="msg in messages"
          :key="msg.id"
          @click="selected = msg"
        >
          <span slot="supporting-text">{{ msg.preview }}</span>
          {{ msg.title }}
        </m3e-list-action>
      </m3e-action-list>
    </aside>

    <main class="detail">
      <template v-if="selected">
        <m3e-icon-button aria-label="返回" class="back" @click="selected = null">
          <m3e-icon name="arrow_back" />
        </m3e-icon-button>
        <h2>{{ selected.title }}</h2>
        <p>{{ selected.body }}</p>
      </template>
      <p v-else>选择左侧项目查看详情</p>
    </main>
  </div>
</template>

<style scoped>
.md3-list-detail { display: flex; flex-direction: column; min-height: 100%; }
.list { background: var(--md-sys-color-surface-container); border-radius: var(--md-sys-shape-corner-value-large); overflow: auto; }
.detail { flex: 1; padding: var(--space-md); }

@media (max-width: 599px) {
  .detail { display: none; }
  .md3-list-detail.detail-active .list { display: none; }
  .md3-list-detail.detail-active .detail { display: block; }
}
@media (min-width: 600px) {
  .md3-list-detail { flex-direction: row; gap: var(--space-md); padding: var(--space-md); }
  .list { width: 360px; flex-shrink: 0; }
  .back { display: none; }
}
@media (min-width: 840px) { .list { width: 400px; } }
</style>
```

可拖拽版本：

```vue
<script setup>
import { ref } from 'vue';
import { M3eSplitPane } from './use-m3e-model.js';
const width = ref(35);
</script>

<template>
  <M3eSplitPane orientation="horizontal" v-model="width" :min="20" :max="60" label="调整列表宽度">
    <aside slot="start"><!-- 列表 --></aside>
    <main slot="end"><!-- 详情 --></main>
  </M3eSplitPane>
</template>
```

`m3e-split-pane` 的 `value` 是起始面板百分比（0–100），不是 0–1；`detents` 是空格分隔的百分比（如 `25 50 75`），键盘可访问性内建。用包装层的 `v-model`，拖动后 `width` 会同步。

### 辅助面板

主内容 + 补充信息。紧凑时堆叠，中等+ 并排：

```vue
<template>
  <div class="md3-supporting">
    <main class="primary">…</main>
    <aside class="secondary">
      <m3e-card variant="filled">
        <div slot="content">属性 / 评论 / 相关信息</div>
      </m3e-card>
    </aside>
  </div>
</template>

<style scoped>
.md3-supporting { display: flex; flex-direction: column; gap: var(--space-sm); padding: var(--space-sm); }
@media (min-width: 600px) {
  .md3-supporting { flex-direction: row; gap: var(--space-md); padding: var(--space-md); }
  .primary { flex: 2; }
  .secondary { flex: 1; }
}
</style>
```

辅助面板需要可滑出时，用 `m3e-drawer-container` 的 `slot="end"`，不要自己写 `transform` 动画。

## 组件化的布局交互

| 场景 | 方案 | 不要 |
|---|---|---|
| 可拖拽双面板 | `m3e-split-pane` | 手写 pointermove |
| 侧边抽屉/属性面板 | `m3e-drawer-container` + `slot="end"` | 手写遮罩 + translate |
| 可滚动文档表面 | `m3e-content-pane` | 再套一层 `overflow:auto` |
| 溢出横向卡片 | `m3e-slide-group` | 手写滚动按钮 |
| 导航切换 | `m3e-nav-bar` / `m3e-nav-rail` / `m3e-nav-menu` | 自绘 tablist |
| 信息流/网格 | CSS Grid | 引入布局库 |

**原则**：网格、flex、断点媒体查询留在 CSS；拖拽、滑动、焦点管理交给组件。

## 大屏与阅读宽度

- 正文阅读宽度限制在 **840–1040px**；超过约 80 个字符一行会难扫视。
- 多余宽度用于多面板，而不是把单列拉宽。
- 大屏保留 48dp 触达目标，同时补悬停态与键盘快捷键。
- 对话框在中等+ 居中、最大宽度约 560px；不要全屏。

```css
.md3-content {
  width: 100%;
  max-width: 1040px;
  margin-inline: auto;
  padding: 16px;
}
@media (min-width: 600px) { .md3-content { padding: 24px; } }
```

## 折叠屏 / 双屏

CSS 视口分段 API 可以在铰链处切分布局：

```css
@media (horizontal-viewport-segments: 2) {
  .md3-list-detail { flex-direction: row; }
  .list { width: env(viewport-segment-width 0 0); }
  .detail { flex: 1; }
}

@media (vertical-viewport-segments: 2) {
  .md3-media { display: flex; flex-direction: column; }
  .video { height: env(viewport-segment-height 0 0); }
}
```

- 铰链/折痕是物理分隔线，不要把按钮、输入框、关键信息放在其上。
- 桌面模式（水平半开）：上半放视频/图片/主内容，下半放控件/文字。
- 书本模式（垂直半开）：左列表、右详情，天然对应列表-详情布局。
- 不要在 JS 里猜折叠姿态；能靠 CSS 媒体查询就用 CSS。

## 容器查询

组件级响应式用容器查询，不依赖视口：

```css
.card-host { container-type: inline-size; container-name: card; }

@container card (min-width: 400px) {
  .md3-card-content { display: flex; flex-direction: row; gap: 16px; }
}
@container card (max-width: 399px) {
  .md3-card-content { display: flex; flex-direction: column; }
}
```

注意：`m3e-card` 是 Shadow DOM 组件，容器查询应作用在**你控制的宿主/插槽内容**上，不要假设能查询组件内部。

## 自适应组件行为

| 组件 | 紧凑 | 中等 | 展开+ |
|---|---|---|---|
| 导航 | 底部 Nav Bar | Nav Rail | Drawer / Rail |
| App Bar | small | small / medium | small / medium |
| Dialog | 全屏或近全屏 | 居中，max 560px | 居中，max 560px |
| Bottom Sheet | 全高/多档 | 部分高度 | 侧边面板（改用 drawer） |
| Search | 全屏 search view | 持久 search bar | 持久 search bar |
| 卡片 | 单列 | 2–3 列 | 3–4 列 |
| 面板 | 单面板 | 可选第二面板 | 2–3 面板 |

## 完整外壳

```vue
<template>
  <m3e-theme color="#6750A4" scheme="auto" motion="expressive">
    <m3e-drawer-container id="shell" :start="true" start-mode="auto">
      <m3e-nav-menu slot="start"><!-- 导航 --></m3e-nav-menu>

      <main class="md3-main">
        <m3e-app-bar size="small" for="scroll-root">
          <m3e-icon-button slot="leading" aria-label="菜单">
            <m3e-icon name="menu" />
            <m3e-drawer-toggle for="shell" />
          </m3e-icon-button>
          <span slot="title">页面标题</span>
        </m3e-app-bar>

        <div id="scroll-root" class="md3-scroll">
          <div class="md3-content"><slot /></div>
        </div>
      </main>
    </m3e-drawer-container>
  </m3e-theme>
</template>

<style scoped>
.md3-main { display: flex; flex-direction: column; height: 100vh; }
.md3-scroll { flex: 1; overflow-y: auto; }
.md3-content { max-width: 1040px; margin-inline: auto; padding: 16px; box-sizing: border-box; }
@media (min-width: 600px) { .md3-content { padding: 24px; } }
</style>
```

## Vuetify 4 对照

| MD3 布局 | Vuetify 4 |
|---|---|
| 12 列网格 | `v-row` / `v-col`（`cols`、`sm`、`md`、`lg`、`xl`） |
| 断点 | `useDisplay()`（`mobile`、`smAndUp`、`mdAndUp`…） |
| 屏幕边距 | `v-container`（`fluid`、`class="px-4 px-md-6"`） |
| 侧栏 | `v-navigation-drawer` + `mobile-breakpoint` |
| 底部导航 | `v-bottom-navigation` |
| 可拖拽双面板 | 无，用 `v-row`/`v-col` 或自绘 |
| 最大阅读宽度 | `max-width: 1040px; margin-inline: auto` |

Vuetify 的断点是 `xs <600`、`sm 600–959`、`md 960–1279`、`lg 1280–1919`、`xl 1920+`，与 MD3 的 600/840/1200/1600 **不完全一致**。按 MD3 设计时，不要直接套 `md`/`lg` 名称，用自定义媒体查询或 `useDisplay().width` 判断。
