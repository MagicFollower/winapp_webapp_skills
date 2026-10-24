# MD3 导航模式（Vue 3）

选择与实现 MD3 导航组件。示例的标签、插槽、事件均按 `@m3e/web` 2.10.2 编写；Vuetify 对应见每节末尾。

## 决策树

```
主要目标页面数量
├── 2 个相关视图                 → Tabs（primary）
├── 3–5 个
│   ├── 紧凑 <600px             → Nav Bar（底部）
│   ├── 中等 600–839px          → Nav Rail（侧边）
│   └── 展开 840px+             → Drawer（持久侧边）或 Rail
├── 6+ 个
│   ├── 紧凑                    → Drawer（over/modal）
│   ├── 中等                    → Drawer（side）或 Rail + 溢出菜单
│   └── 展开+                   → Drawer（side）
└── 层级结构（分区/子页）        → Nav Menu（可嵌套）
```

| 组件 | 目标数 | 窗口尺寸 | 持久性 | 位置 |
|---|---|---|---|---|
| Nav Bar | 3–5 | 紧凑 | 持久 | 底部 |
| Nav Rail | 3–7 | 中等 | 持久 | 起始侧 |
| Drawer | 不限 | 中等+ | 持久或覆盖 | 起始/结束侧 |
| Tabs | 2+ 相关视图 | 任意 | 持久 | 内容顶部 |
| Toolbar | 上下文操作 | 任意 | 持久 | 内容区 |

## 底部导航栏

**用途**：紧凑屏幕的 3–5 个主目标。始终显示标签，选中项使用 `selected-icon`（通常填充图标）。

`m3e-nav-bar` 没有 `value` 属性；条目也没有 `value`。选中态由 `m3e-nav-item` 的 `selected` 决定，`change` 后从 `bar.items` 与 `bar.selected` 反查索引：

```vue
<script setup>
import { ref } from 'vue';
const tab = ref(0);
function onNavChange(e) {
  tab.value = e.target.items.indexOf(e.target.selected);
}
</script>

<template>
  <m3e-nav-bar mode="compact" @change="onNavChange">
    <m3e-nav-item :selected="tab === 0">
      <m3e-icon slot="icon" name="home" />
      <m3e-icon slot="selected-icon" name="home" filled />
      首页
    </m3e-nav-item>
    <m3e-nav-item :selected="tab === 1">
      <m3e-icon slot="icon" name="explore" />
      <m3e-icon slot="selected-icon" name="explore" filled />
      发现
    </m3e-nav-item>
    <m3e-nav-item :selected="tab === 2">
      <m3e-icon slot="icon" name="person" />
      <m3e-icon slot="selected-icon" name="person" filled />
      我的
    </m3e-nav-item>
  </m3e-nav-bar>
</template>
```

- `mode` = compact / expanded / auto。
- 不要少于 3 个或多于 5 个目标。
- 需要 URL 导航时给 `m3e-nav-item` 加 `href`；它会渲染为链接并保留键盘语义。

**Vuetify**：`v-bottom-navigation`。

## 导航侧栏

**用途**：中等屏幕 3–7 个目标；顶部可放 FAB；`mode="auto"` 随容器宽度在 compact/expanded 间切换，`m3e-nav-rail-toggle` 提供手动展开按钮。

```vue
<m3e-nav-rail id="rail" mode="auto" aria-label="主导航">
  <m3e-icon-button toggle aria-label="展开/收起">
    <m3e-icon name="menu" />
    <m3e-icon slot="selected" name="menu_open" />
    <m3e-nav-rail-toggle for="rail" />
  </m3e-icon-button>

  <m3e-fab size="small" variant="tertiary" aria-label="新建">
    <m3e-icon name="edit" />
  </m3e-fab>

  <m3e-nav-item selected>
    <m3e-icon slot="icon" name="home" />
    <m3e-icon slot="selected-icon" name="home" filled />
    首页
  </m3e-nav-item>
  <m3e-nav-item>
    <m3e-icon slot="icon" name="search" />搜索
  </m3e-nav-item>
</m3e-nav-rail>
```

- 项目顶部对齐，FAB 在最上方。
- 紧凑宽度 80px 左右；展开时显示标签。
- 选中项使用 `secondary-container` 指示器（组件内部处理）。

**Vuetify**：`v-navigation-drawer` + `rail` 属性。

## 导航抽屉

**用途**：目标多、层级深、展开屏幕。`m3e-drawer-container` 同时管理主内容与起始/结束抽屉。

```vue
<script setup>
import { ref } from 'vue';
import { M3eDrawerContainer } from './use-m3e-model.js';
const open = ref(true);
</script>

<template>
  <M3eDrawerContainer
    id="shell"
    v-model="open"
    start-mode="auto"
    start-divider
  >
    <m3e-nav-menu slot="start" aria-label="主导航">
      <m3e-nav-menu-item selected>
        <m3e-icon slot="icon" name="inbox" />
        <span slot="label">收件箱</span>
        <span slot="badge">24</span>
      </m3e-nav-menu-item>
      <m3e-nav-menu-item>
        <m3e-icon slot="icon" name="send" />
        <span slot="label">已发送</span>
      </m3e-nav-menu-item>
      <m3e-nav-menu-item open>
        <m3e-icon slot="icon" name="settings" />
        <span slot="label">设置</span>
        <m3e-nav-menu-item><span slot="label">外观</span></m3e-nav-menu-item>
        <m3e-nav-menu-item><span slot="label">通知</span></m3e-nav-menu-item>
      </m3e-nav-menu-item>
    </m3e-nav-menu>

    <main>
      <m3e-app-bar size="small">
        <m3e-icon-button slot="leading" aria-label="菜单">
          <m3e-icon name="menu" />
          <m3e-drawer-toggle for="shell" />
        </m3e-icon-button>
        <span slot="title">收件箱</span>
        <m3e-icon-button slot="trailing" aria-label="搜索">
          <m3e-icon name="search" />
        </m3e-icon-button>
      </m3e-app-bar>
      <slot />
    </main>
  </M3eDrawerContainer>
</template>
```

- `start-mode` / `end-mode`：`side`（并排）、`over`（覆盖 + 遮罩）、`push`（推开内容）、`auto`（按宽度切换）。
- 抽屉不能 `v-model`；用 `:start` + `@change`，或 ref 上直接赋 `.start`。
- 结束侧辅助面板用 `slot="end"` + `:end` / `@change`。
- 抽屉内优先用 `m3e-nav-menu`（层级）而不是普通列表。

**Vuetify**：`v-navigation-drawer`（`temporary` ≈ over，`permanent` ≈ side）。

## 顶部应用栏

| size | 高度 | 标题行为 |
|---|---|---|
| `small` | 64px | 单行，起始对齐 |
| `medium` | 112px | 标题在底部，滚动时收缩为 small |
| `large` | 152px | 更大的标题区，滚动时收缩 |

```vue
<m3e-app-bar size="medium" for="scroll-root" centered>
  <m3e-icon-button slot="leading" aria-label="返回">
    <m3e-icon name="arrow_back" />
  </m3e-icon-button>
  <span slot="title">页面标题</span>
  <span slot="subtitle">补充说明</span>
  <m3e-icon-button slot="trailing" aria-label="更多">
    <m3e-icon name="more_vert" />
  </m3e-icon-button>
</m3e-app-bar>
```

- 插槽：`leading`、`title`、`subtitle`、`trailing`；旧名 `leading-icon` / `trailing-icon` 仍可用但已标记 deprecated。
- `for` 指向滚动容器；组件据此处理滚动收缩/抬升。不传时监听窗口。
- `centered` 只影响标题对齐，不改变高度。
- 应用栏默认不投影阴影；滚动状态由 `for` 驱动，不要手工加 `elevated`（该属性不存在）。

**Vuetify**：`v-app-bar`。

## 标签页

**用途**：同一层级内的相关视图切换；`variant="primary"` 用于页面级，`secondary` 用于子分区。

```vue
<script setup>
import { ref } from 'vue';
const current = ref('panel-video');
function onTabChange(e) {
  current.value = e.target.querySelector('m3e-tab[selected]')?.getAttribute('for') ?? current.value;
}
</script>

<template>
  <m3e-tabs variant="primary" stretch @change="onTabChange">
    <m3e-tab selected for="panel-video">
      <m3e-icon slot="icon" name="videocam" />视频
    </m3e-tab>
    <m3e-tab for="panel-photo">
      <m3e-icon slot="icon" name="photo" />照片
    </m3e-tab>
    <m3e-tab for="panel-audio">
      <m3e-icon slot="icon" name="music_note" />音频
    </m3e-tab>

    <m3e-tab-panel id="panel-video">视频内容</m3e-tab-panel>
    <m3e-tab-panel id="panel-photo">照片内容</m3e-tab-panel>
    <m3e-tab-panel id="panel-audio">音频内容</m3e-tab-panel>
  </m3e-tabs>
</template>
```

- 面板用 `for` ↔ `id` 绑定；组件自己管理显示与 aria。
- `stretch` 让标签铺满宽度；`header-position="after"` 把标签放到底部（少见）。
- 不要在外层再写 `v-if` 切面板，会破坏组件的 aria 关联。

**Vuetify**：`v-tabs` + `v-window` / `v-tabs-window`。

## 面包屑

```vue
<m3e-breadcrumb>
  <span slot="separator">/</span>
  <m3e-breadcrumb-item href="/">首页</m3e-breadcrumb-item>
  <m3e-breadcrumb-item href="/reports">报表</m3e-breadcrumb-item>
  <m3e-breadcrumb-item current="page">年度</m3e-breadcrumb-item>
</m3e-breadcrumb>
```

最后一项用 `current="page"`；`wrap` 控制换行。**Vuetify**：`v-breadcrumbs`。

## 工具栏与上下文操作

```vue
<m3e-toolbar variant="vibrant" shape="rounded">
  <m3e-icon-button aria-label="撤销"><m3e-icon name="undo" /></m3e-icon-button>
  <m3e-icon-button aria-label="重做"><m3e-icon name="redo" /></m3e-icon-button>
  <m3e-divider vertical inset />
  <m3e-icon-button variant="filled" aria-label="添加"><m3e-icon name="add" /></m3e-icon-button>
</m3e-toolbar>
```

工具栏用于当前页面的上下文操作，不承担主导航。**Vuetify**：`v-toolbar`。

## 响应式切换

MD3 的导航随窗口尺寸类别切换，不是把同一套导航拉宽：

```
<600px   → Nav Bar（底部）
600–839  → Nav Rail（侧边，紧凑/展开）
840px+   → Drawer（side）或 Navigation Rail（项目少时）
```

**推荐做法**：用 CSS 断点控制三个导航组件的显示，Vue 只保留一份路由/选中状态。

```vue
<script setup>
import { ref } from 'vue';
const tab = ref(0);
</script>

<template>
  <div class="app-shell">
    <m3e-nav-rail class="nav-rail" mode="compact">…</m3e-nav-rail>
    <m3e-drawer-container class="nav-drawer" :start="true" start-mode="side">…</m3e-drawer-container>
    <m3e-nav-bar class="nav-bar" @change="tab = $event.target.items.indexOf($event.target.selected)">
      …
    </m3e-nav-bar>
  </div>
</template>

<style scoped>
.app-shell { display: flex; min-height: 100vh; }
.nav-rail, .nav-drawer { display: none; }

@media (max-width: 599px) {
  .app-shell { flex-direction: column; }
  .nav-bar { display: block; order: 1; }
}
@media (min-width: 600px) and (max-width: 839px) {
  .nav-rail { display: block; }
}
@media (min-width: 840px) {
  .nav-drawer { display: flex; }
}
</style>
```

**避免**：用 `v-if` 在三个组件间重建 DOM（会丢焦点、丢滚动位置、重复触发动画）。用 CSS 显示/隐藏，或只在 JS 里切换 `mode`。

**Vuetify**：`v-navigation-drawer` + `mobile-breakpoint`、`v-bottom-navigation`，或 `useDisplay()` 的组合式断点。

## 导航无障碍

- 每个导航容器有 `aria-label` 区分用途（“主导航”“页脚导航”）。
- 图标按钮必须有 `aria-label`；导航项文字已提供可读名，不要再加重复的 aria-label。
- 当前页面：`m3e-nav-item selected` / `m3e-tab selected` / `m3e-breadcrumb-item current` 会自动映射 `aria-current`/选中语义。
- 键盘：Tab 进入导航，方向键在条目间移动（组件内建 roving tabindex）；不要拦截方向键。
- 触达目标 ≥ 48×48dp；底部导航项由组件保证。
- 抽屉/对话框打开时焦点被困在内部；关闭后回到触发按钮（`m3e-drawer-toggle` / `m3e-dialog-trigger` 内建）。

## 常见错误

| 错误 | 后果 | 修正 |
|---|---|---|
| 用 `m3e-nav-bar` 承载 7 个目标 | 拥挤、标签截断 | 改用 drawer 或 rail + 溢出菜单 |
| 给 `m3e-nav-item` 写 `value` 属性 | 属性不存在，选中逻辑失效 | 用 `selected` + `items.indexOf(selected)` |
| 给 `m3e-drawer-container` 写 `v-model` | 静默不更新 | `:start` + `@change` |
| 手动 `v-if` 切导航组件 | 丢失焦点/滚动/动画 | CSS 断点 + 单份状态 |
| 用 `m3e-app-bar` 代替页面标题层级 | 屏幕阅读器缺少 heading | 正文里仍用 `m3e-heading level="1..6"` |
| 给 `m3e-tab-panel` 手写 `hidden` | 与组件 aria 状态冲突 | 交给 `for`/`id` 关联 |
