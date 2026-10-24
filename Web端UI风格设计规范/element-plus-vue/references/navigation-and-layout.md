# 导航与应用外壳

Element Plus 提供菜单、标签页、面包屑、下拉等零件，但不提供应用外壳。本文给出可直接落地的一套外壳与页面骨架，断点与 `layout-rules.md` 保持一致。

## 1. 断点与导航形态

| 宽度 | 导航 | 内容边距 | 侧栏宽 |
|---|---|---|---|
| < 600 | 顶部栏 + 抽屉菜单 | 16 | 抽屉 240（`size="240px"`） |
| 600–839 | 顶部栏 + 抽屉菜单 | 24 | 抽屉 240 |
| 840–1199 | 常驻侧栏（可折叠） | 24 | 240 / 折叠 64 |
| ≥ 1200 | 常驻侧栏 + 可选二级栏 | 24 | 240 + 200 |

- 只切布局，不切字号。
- 840 以下不要把 240px 侧栏硬塞进视口；改抽屉。
- 顶部栏高度 64；触屏页可 56，但主操作热区仍 ≥48。

## 2. 标准外壳

```vue
<!-- layouts/AppShell.vue -->
<script setup>
import { computed, ref } from 'vue';
import { useRoute } from 'vue-router';
import { Fold, Expand, Menu as MenuIcon, Plus } from '@element-plus/icons-vue';

const route = useRoute();
const collapsed = ref(false);
const mobileOpen = ref(false);

const activeMenu = computed(() => route.path);
</script>

<template>
  <div class="shell" :class="{ 'is-collapsed': collapsed }">
    <aside class="shell__side" :class="{ 'is-collapsed': collapsed }">
      <div class="shell__brand">
        <span class="shell__logo">A</span>
        <span v-show="!collapsed" class="ds-h3">Acme</span>
      </div>

      <el-menu :default-active="activeMenu" :collapse="collapsed" router class="shell__menu">
        <el-menu-item index="/dashboard">
          <el-icon><MenuIcon /></el-icon>
          <template #title>概览</template>
        </el-menu-item>
        <el-sub-menu index="/orders">
          <template #title>
            <el-icon><MenuIcon /></el-icon>
            <span>订单</span>
          </template>
          <el-menu-item index="/orders/list">订单列表</el-menu-item>
          <el-menu-item index="/orders/refund">退款</el-menu-item>
        </el-sub-menu>
      </el-menu>

      <div class="shell__side-foot">
        <el-button text :icon="collapsed ? Expand : Fold" :aria-label="collapsed ? '展开侧栏' : '收起侧栏'"
          @click="collapsed = !collapsed" />
      </div>
    </aside>

    <div class="shell__main">
      <header class="shell__header">
        <el-button class="shell__burger" text :icon="MenuIcon" aria-label="打开菜单" @click="mobileOpen = true" />
        <el-breadcrumb separator="/">
          <el-breadcrumb-item v-for="item in route.matched" :key="item.path" :to="item.path">
            {{ item.meta.title }}
          </el-breadcrumb-item>
        </el-breadcrumb>
        <div class="shell__header-actions">
          <el-button type="primary" :icon="Plus">新建</el-button>
          <UserMenu />
        </div>
      </header>

      <main class="shell__content">
        <router-view />
      </main>
    </div>

    <el-drawer v-model="mobileOpen" direction="ltr" size="240px" :with-header="false">
      <el-menu :default-active="activeMenu" router @select="mobileOpen = false">
        <el-menu-item index="/dashboard">概览</el-menu-item>
        <el-sub-menu index="/orders">
          <template #title>订单</template>
          <el-menu-item index="/orders/list">订单列表</el-menu-item>
          <el-menu-item index="/orders/refund">退款</el-menu-item>
        </el-sub-menu>
      </el-menu>
    </el-drawer>
  </div>
</template>

<style scoped>
.shell {
  --side-w: 240px;
  display: grid;
  grid-template-columns: var(--side-w) minmax(0, 1fr);
  min-height: 100vh;
  background: var(--el-bg-color-page);
}
.shell.is-collapsed { --side-w: 64px; }
.shell__side {
  display: flex;
  flex-direction: column;
  width: var(--side-w);
  border-right: 1px solid var(--el-border-color-light);
  background: var(--el-bg-color);
  position: sticky;
  top: 0;
  height: 100vh;
}
.shell__brand {
  display: flex; align-items: center; gap: var(--space-2);
  height: 64px; padding: 0 var(--space-4);
  border-bottom: 1px solid var(--el-border-color-lighter);
}
.shell__logo {
  display: grid; place-items: center;
  width: 32px; height: 32px;
  background: var(--el-color-primary); color: #fff;
  border-radius: var(--radius-sm);
}
.shell__menu { border-right: 0; flex: 1; }
.shell__side-foot { padding: var(--space-2); border-top: 1px solid var(--el-border-color-lighter); }

.shell__main { display: flex; flex-direction: column; min-width: 0; }
.shell__header {
  display: flex; align-items: center; gap: var(--space-4);
  height: 64px; padding: 0 var(--space-6);
  background: var(--el-bg-color);
  border-bottom: 1px solid var(--el-border-color-light);
  position: sticky; top: 0; z-index: var(--el-index-top);
}
.shell__header-actions { margin-inline-start: auto; display: flex; align-items: center; gap: var(--space-2); }
.shell__content { flex: 1; min-width: 0; }
.shell__burger { display: none; }

@media (max-width: 839px) {
  .shell { grid-template-columns: minmax(0, 1fr); }
  .shell__side { display: none; }
  .shell__burger { display: inline-flex; }
  .shell__header { padding: 0 var(--space-4); }
}
</style>
```

要点：

- `el-menu` 的 `default-active` 绑定 `route.path`，`router` 模式下 `index` 就是路由。
- 侧栏 `position: sticky; height: 100vh`，内容区独立滚动；不要给整个 `body` 设 `overflow: hidden`。
- 折叠只改宽度与 `collapse`，不换导航结构。
- 移动端复用同一份菜单数据（示例重复了结构；实际项目抽成 `MenuTree.vue`，两处共用）。
- 顶部栏 `z-index: 1000`（`--el-index-top`），低于所有弹层（2000 起）。

## 3. 顶部导航替代

只有 3–5 个一级入口、没有二级时，用顶部导航：

```vue
<el-menu mode="horizontal" :default-active="route.path" router :ellipsis="false">
  <el-menu-item index="/dashboard">概览</el-menu-item>
  <el-menu-item index="/orders">订单</el-menu-item>
  <el-menu-item index="/settings">设置</el-menu-item>
</el-menu>
```

- `mode="horizontal"` 高度 40，外层再包 64 高的 header。
- 超过 5 项改用侧栏；不要用 `:ellipsis` 把入口收进“更多”。
- 顶部导航 + 页面内 `el-tabs` 是两层导航，最多两层；三层会失去方向感。

## 4. 面包屑

由 `route.matched` 自动生成：

```vue
<el-breadcrumb separator="/">
  <el-breadcrumb-item v-for="(item, i) in route.matched" :key="item.path"
    :to="i < route.matched.length - 1 ? item.path : undefined">
    {{ item.meta.title ?? item.name }}
  </el-breadcrumb-item>
</el-breadcrumb>
```

- 最后一项不可点击。
- 每级都要有 `meta.title`；缺失时显示路由名，不要显示原始 path。
- 移动端只显示最后两级（`v-if` 控制），完整路径仍可通过返回按钮。

## 5. 标签页导航

`el-tabs` 用于**同一实体的视图切换**（详情/日志/权限），不用于一级导航：

```vue
<el-tabs v-model="tab">
  <el-tab-pane label="基本信息" name="basic" />
  <el-tab-pane label="操作日志" name="logs" lazy />
</el-tabs>
```

- 标签 3–7 个；超过 7 个改用侧栏或下拉。
- 每个标签的内容独立加载；切换时不要整页刷新。
- 与路由同步时，`tab` 用 `computed({ get: () => route.query.tab, set: (v) => router.replace({ query: { ...route.query, tab: v } }) })`，不要 `watch` 双向写。
- 不要在 tab 内再套 tab。

## 6. 页面骨架

### 列表页

```vue
<div class="ds-page">
  <header class="page-head">
    <div>
      <h1 class="ds-h1">订单</h1>
      <p class="ds-caption">共 {{ total }} 条</p>
    </div>
    <el-button type="primary" :icon="Plus">新建订单</el-button>
  </header>

  <el-card shadow="never" class="page-card">
    <div class="ds-row page-toolbar">…筛选…</div>
    <el-table :data="rows" />
    <div class="page-pager"><el-pagination … /></div>
  </el-card>
</div>
```

```css
.page-head { display: flex; align-items: flex-start; justify-content: space-between; gap: var(--space-4); }
.page-card :deep(.el-card__body) { display: grid; gap: var(--space-4); }
.page-toolbar { justify-content: space-between; }
.page-pager { display: flex; justify-content: flex-end; }
```

工具栏、表格、分页都在同一张 Card 内，间距 16；页面级区块间距 24。

### 详情页

```vue
<div class="ds-page ds-page--reading">
  <el-page-header content="订单详情" @back="router.back()" />

  <el-card shadow="never">
    <el-descriptions :column="2" border>…</el-descriptions>
  </el-card>

  <el-tabs v-model="tab">
    <el-tab-pane label="商品" name="items" />
    <el-tab-pane label="日志" name="logs" lazy />
  </el-tabs>
</div>
```

详情页限宽 840；长文本再窄到 720（用 `.ds-reading`）。

### 仪表盘

```vue
<div class="ds-page ds-page--wide">
  <div class="metric-grid">
    <el-card v-for="m in metrics" :key="m.key" shadow="never">
      <el-statistic :title="m.label" :value="m.value" :precision="m.precision" />
    </el-card>
  </div>

  <div class="chart-grid">
    <el-card shadow="never">趋势</el-card>
    <el-card shadow="never">分布</el-card>
  </div>
</div>
```

```css
.metric-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(200px, 1fr)); gap: var(--space-4); }
.chart-grid { display: grid; grid-template-columns: minmax(0, 2fr) minmax(0, 1fr); gap: var(--space-6); }
@media (max-width: 839px) { .chart-grid { grid-template-columns: minmax(0, 1fr); } }
```

指标卡最小 200；图表卡至少 320 高，避免塌陷。

### 设置页

```vue
<div class="ds-page ds-page--reading">
  <h1 class="ds-h1">设置</h1>
  <el-card shadow="never">
    <template #header><div class="ds-h3">通知</div></template>
    <el-form :model="form" label-width="120px">…</el-form>
  </el-card>
  <div class="ds-actions">
    <el-button>取消</el-button>
    <el-button type="primary" @click="save">保存</el-button>
  </div>
</div>
```

设置页用“分组 Card + 底部操作区”；不要把所有字段塞进一张超长表单。

### 登录页

```vue
<div class="auth">
  <div class="auth__panel">
    <h1 class="ds-display">登录 Acme</h1>
    <p class="ds-body ds-muted">使用工作邮箱继续</p>
    <el-form :model="form" label-position="top" @submit.prevent="login">…</el-form>
  </div>
</div>
```

```css
.auth { min-height: 100vh; display: grid; place-items: center; padding: var(--space-4); background: var(--el-bg-color-page); }
.auth__panel { width: 100%; max-width: 400px; display: grid; gap: var(--space-6); padding: var(--space-8); background: var(--el-bg-color); border-radius: var(--radius-lg); }
```

登录页不复用后台外壳；不显示侧栏与面包屑。

## 7. 列表–详情双栏

```css
.split {
  display: grid;
  grid-template-columns: 320px minmax(0, 1fr);
  gap: var(--space-4);
  align-items: start;
}
@media (max-width: 839px) {
  .split { grid-template-columns: minmax(0, 1fr); }
}
```

左列表用 `el-scrollbar` 固定高度，右侧详情独立滚动；窄屏改成“列表 → 详情”两级路由，不要上下堆叠超长页面。

需要可拖拽分栏时用 `el-splitter`：

```vue
<el-splitter style="height: calc(100vh - 64px)">
  <el-splitter-panel size="320px" min="240px" max="480px"><OrderList /></el-splitter-panel>
  <el-splitter-panel><OrderDetail /></el-splitter-panel>
</el-splitter>
```

## 8. 滚动与路由

- 内容区独立滚动时，路由切换后手动回到顶部：

```js
const contentRef = ref();
watch(() => route.fullPath, () => contentRef.value?.scrollTo({ top: 0 }));
```

- 窗口滚动时用 `scrollBehavior`：

```js
scrollBehavior(to, from, saved) {
  return saved ?? { top: 0 };
}
```

- 返回列表要恢复位置时，用 `savedPosition` 或 `keep-alive` + `scrollTop` 记录；不要依赖浏览器自动恢复。
- `keep-alive` 只包列表页，详情页不缓存：

```vue
<router-view v-slot="{ Component }">
  <keep-alive include="OrderList">
    <component :is="Component" />
  </keep-alive>
</router-view>
```

### 路由过渡

页面切换不能生硬替换。统一用 `ds-page`：

```vue
<RouterView v-slot="{ Component, route }">
  <Transition name="ds-page" mode="out-in" appear>
    <component :is="Component" :key="route.path" />
  </Transition>
</RouterView>
```

- `mode="out-in"`：旧页先退出，新页再进入，避免两页同时占位。
- 只动 `opacity` + 8px 位移；时长 220ms（退出更短）。
- 页面根节点用 `.ds-page` 保持稳定结构。
- `keep-alive` 与 `Transition` 组合时，`Transition` 包在 `keep-alive` 外层，避免缓存节点不触发过渡。
- `prefers-reduced-motion` 已由 `design-system.css` 全局降级。

## 9. 可访问性

- 侧栏用 `<nav aria-label="主导航">`；当前项 `aria-current="page"`（`el-menu` 的选中态已提供视觉，补语义）。
- 移动端抽屉打开后焦点进入抽屉，关闭后回到触发按钮。
- 顶部栏的图标按钮全部有 `aria-label`。
- 面包屑用 `<nav aria-label="面包屑">`。
- 不要用颜色单独表示当前项；`el-menu` 的选中态已有背景 + 文字色。

## 10. 反模式

- **侧栏固定 240 + `min-width: 1200`**：小屏直接横向滚动。
- **移动端把侧栏 `display: none` 就完事**：没有替代入口，无法导航。
- **面包屑写死文本**：路由变化后不同步。
- **一级导航用 `el-tabs`**：标签页不是导航，刷新后状态丢失。
- **顶部导航 + 侧栏 + tabs 三层并存**：认知负担过高。
- **`body { overflow: hidden }` 后忘记给内容区滚动**：页面无法滚动。
- **每个页面各写一套 header**：应抽到 layout。
- **用 `position: fixed` 做侧栏且不预留宽度**：内容被盖住。
