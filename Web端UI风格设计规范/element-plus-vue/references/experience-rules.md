# 体验基线（强制）

这五条约束决定“看起来是不是同一个产品”，与令牌、G2 同级执行。对应资产：

- `../assets/vue/use-toast.js` —— 顶部提示三态封装。
- `../assets/vue/AppDialog.vue` —— 默认禁止遮罩关闭的 Modal 包装。
- `../assets/styles/design-system.css` —— 卡片表面、数据 Grid、提示样式、过渡类与动效令牌。

## 1. 顶部提示：只用 `toast.info / warn / error`

### 入口

```js
import { toast } from '@/composables/use-toast.js';

toast.info('已保存');
toast.warn('额度将在 3 天后到期');
toast.error('保存失败，请检查网络后重试');
```

`useToast()` 返回同一个对象，适合在组件里注入：

```js
const { info, warn, error } = useToast();
```

### 三态定义

| 方法 | `type` | 默认时长 | 适用 |
|---|---|---|---|
| `info` | `info` | 3000ms | 成功、完成、一般通知 |
| `warn` / `warning` | `warning` | 4000ms | 将满、将过期、非阻断风险 |
| `error` | `error` | 5000ms | 失败、不可继续、数据异常 |

共同默认：顶部居中、`offset: 24px`、可关闭、`grouping: true`（同类合并）。**不要**在调用处覆盖颜色或位置；只允许覆盖 `duration` 与 `onClose`。

### 配色（主题自适应）

样式在 `design-system.css`，全部读 Element Plus 变量，亮/暗自动跟随：

| 态 | 背景 | 边框 | 文本 |
|---|---|---|---|
| info | `--el-color-info-light-9` | `--el-color-info-light-7` | `--el-text-color-primary` |
| warn | `--el-color-warning-light-9` | `--el-color-warning-light-7` | `--el-text-color-primary` |
| error | `--el-color-error-light-9` | `--el-color-error-light-7` | `--el-text-color-primary` |

- 图标颜色由 `type` 驱动。
- 圆角 8、内边距 12/16、最小宽 280、最大宽 `min(480px, 100vw - 32px)`。
- 暗色下 `light-9` 自动变成深色底，文本仍是 `text-primary`，不需要写 `html.dark` 分支。
- 不要给提示条加 3px 左描边：r=8 时安全内部厚度只有 2.64px，会起节；用 1px 边框。

### 使用规则

1. **成功/失败/提醒都走 toast**；不要在页面里再放一条“保存成功”的静态横幅。
2. 字段校验错误**不要**用 toast，落在 `el-form-item` 的错误位。
3. 需要用户确认的破坏操作**不要**用 toast，用 `ElMessageBox.confirm` 或 `el-popconfirm`。
4. 同一动作只弹一条；连续失败用 `grouping` 合并，不要刷屏。
5. 文案写“发生了什么 + 下一步”，例如“保存失败，请重试”，不要只写“错误”。
6. 页面级不可恢复错误用 `el-result`，toast 只承担一次性反馈。

```js
// ✅
toast.warn('本月额度已用 90%，超出后将暂停同步');

// ❌ 直接调用、颜色不受控
ElMessage({ message: '额度不足', type: 'warning' });
```

## 2. 分页与数据 Grid 绑定，固定右下角

分页不是表格的“附加元素”，而是数据 Grid 的最后一个网格项。使用 `.ds-data-grid`：

```vue
<el-card shadow="never">
  <div class="ds-data-grid">
    <div class="ds-row">
      <el-input v-model="filters.keyword" placeholder="搜索" clearable style="width: 240px" />
      <el-button type="primary" @click="query">查询</el-button>
    </div>

    <el-table v-loading="loading" :data="rows" row-key="id">…</el-table>

    <el-pagination
      v-model:current-page="page"
      v-model:page-size="size"
      :total="total"
      :page-sizes="[10, 20, 50]"
      layout="total, sizes, prev, pager, next, jumper"
      background
      class="ds-data-grid__pager"
    />
  </div>
</el-card>
```

```css
.ds-data-grid {
  display: grid;
  grid-template-columns: minmax(0, 1fr);
  gap: var(--space-4);
  min-width: 0;
}
.ds-data-grid__pager {
  justify-self: end;
  align-self: end;
}
```

规则：

- 工具栏、表格、分页在**同一个** Grid 容器内，gap 16；不要分三个独立块再手写 margin。
- 分页 `justify-self: end` 落在右下角；**不要用** `margin-left: auto` 或 `float: right`。
- 分页宽度随内容；窄屏允许 `layout="prev, pager, next"`，通过容器查询或 `useMediaQuery` 切换，不要横向溢出。
- 总数为 0 时要么隐藏分页，要么保留显示；同一产品保持一致。
- 查询条件变化时页码回到 1；`page-size` 变化时也回到 1。
- 表格横向滚动只发生在表格容器内，分页始终可见。

## 3. Modal 默认禁止点击遮罩关闭

所有业务弹窗使用 `AppDialog`：

```vue
<script setup>
import AppDialog from '@/components/AppDialog.vue';
</script>

<template>
  <AppDialog v-model="visible" title="编辑订单" width="560px" @closed="onClosed">
    <el-form :model="form" label-width="96px">…</el-form>
    <template #footer>
      <div class="ds-actions">
        <el-button @click="visible = false">取消</el-button>
        <el-button type="primary" @click="submit">保存</el-button>
      </div>
    </template>
  </AppDialog>
</template>
```

`AppDialog` 的默认值：

| 属性 | 默认 | 说明 |
|---|---|---|
| `close-on-click-modal` | **false** | 点击遮罩不关闭 |
| `append-to-body` | true | 避免被父级 `overflow: hidden` 裁切 |
| `width` | `520px` | 表单弹窗 480–640 |

- **破坏性表单、未保存修改、支付/发布流程**：绝不允许遮罩关闭。
- 只读信息弹窗如果确实要支持遮罩关闭，显式传 `:close-on-click-modal="true"`，并在评审中说明理由。
- ESC 仍可关闭（`close-on-press-escape` 默认 true）；如有未保存内容，用 `before-close` 做二次确认。
- 直接写 `<el-dialog>` 时必须显式 `:close-on-click-modal="false"`；默认值不是 false。
- 关闭后焦点回到触发按钮；不要把 `@close` 里反向改 `v-model`（会抖动）。

## 4. 卡片优先，扁平只做例外

**页面一级区块、表单、表格、图表、统计块一律落在卡片表面上。** 扁平只用于卡片内部的工具栏、表头、分隔区域。

### 两种卡片

```vue
<!-- 首选：Element Plus 卡片 -->
<el-card shadow="never">
  <template #header><div class="ds-h3">订单概览</div></template>
  …
</el-card>

<!-- 非 el-card 容器：用同一表面令牌 -->
<section class="ds-card">
  <h2 class="ds-h3">同步设置</h2>
  …
</section>
```

`design-system.css` 的卡片表面：

| 令牌 | 值 |
|---|---|
| `--ds-card-bg` | `var(--el-bg-color)` |
| `--ds-card-border` | `var(--el-border-color-lighter)` |
| `--ds-card-radius` | `12px` |
| `--ds-card-padding` | `16px` |

静态卡片用 1px 边框、无阴影；`el-card` 的默认阴影已降到 `--el-box-shadow-lighter`。需要强调时用 `.ds-card--raised` 或 `shadow="hover"`，不要同时加边框和重阴影。

`design-system.css` 已把 `body` 背景设为 `--el-bg-color-page`（亮色 `#f2f3f5` / 暗色 `#0a0a0a`），卡片用 `--el-bg-color`；页面与卡片之间才有层级。若项目已有自己的页面底色，必须确保它与卡片底不同，否则“卡片优先”只剩圆角没有边界。

### 布局规则

| 关系 | 规则 |
|---|---|
| 卡片之间 | 16（紧凑）/ 24（展开），用父级 Grid gap |
| 卡片内边距 | 16；卡片标题与内容 12 |
| 卡片内分组 | 用 1px 分隔线或 16 间距，不再套卡片 |
| 页面区块 | 每块一张卡片；不要整页白底平铺 |
| 表格/表单/图表 | 放进卡片；卡片内部工具栏保持扁平 |
| 嵌套 | 最多一层；禁止“卡片套卡片套卡片” |
| 侧栏/顶栏 | 属于外壳，不做卡片 |
| 例外 | 全屏画布、地图、沉浸式编辑器、登录页整屏背景 |

```vue
<!-- ✅ 卡片分组 -->
<div class="ds-page">
  <el-card shadow="never">筛选 + 表格 + 分页</el-card>
  <el-card shadow="never">图表</el-card>
</div>

<!-- ❌ 大段扁平区块，没有边界 -->
<div class="ds-page">
  <div>筛选 + 表格</div>
  <div>图表</div>
</div>
```

## 5. 全项目统一动效

### 令牌

| 令牌 | 值 | 用途 |
|---|---|---|
| `--motion-duration-fast` | 160ms | 退出、hover、颜色 |
| `--motion-duration-base` | 220ms | 常规进入、页面切换 |
| `--motion-duration-slow` | 320ms | 大面板、复杂布局 |
| `--motion-ease-standard` | `cubic-bezier(0.2, 0, 0, 1)` | 标准进出 |
| `--motion-ease-emphasized` | `cubic-bezier(0.05, 0.7, 0.1, 1)` | 强调进入 |
| `--motion-ease-exit` | `cubic-bezier(0.3, 0, 0.8, 0.15)` | 退出 |

`design-system.css` 同时把 Element Plus 的 `--el-transition-duration` / `-fast` 映射到 220/160ms，组件弹层与自定义过渡节奏一致。

### 页面切换

```vue
<!-- App.vue -->
<RouterView v-slot="{ Component, route }">
  <Transition name="ds-page" mode="out-in" appear>
    <component :is="Component" :key="route.path" />
  </Transition>
</RouterView>
```

- `mode="out-in"` 避免两个页面同时占位。
- 只过渡 `opacity` 与 `transform`；不要过渡 `height`、`width`、`margin`。
- 页面根节点保持稳定结构（`.ds-page`），过渡才不会跳。

### 组件显示/隐藏

```vue
<Transition name="ds-fade">
  <el-alert v-if="showTip" title="…" />
</Transition>

<Transition name="ds-slide-up">
  <section v-if="open" class="ds-card">…</section>
</Transition>

<Transition name="ds-scale-fade">
  <div v-if="preview" class="preview">…</div>
</Transition>
```

| 类名 | 适用 |
|---|---|
| `ds-fade` | 提示、徽标、轻量内容 |
| `ds-slide-up` | 面板、设置区、底部操作区 |
| `ds-scale-fade` | 预览、浮层、局部卡片 |
| `ds-page` | 路由页面 |
| `ds-list` | `TransitionGroup` 列表增删 |

### 列表增删

```vue
<TransitionGroup name="ds-list" tag="div" class="ds-list ds-stack">
  <el-card v-for="item in items" :key="item.id" shadow="never">…</el-card>
</TransitionGroup>
```

`.ds-list-leave-active` 会绝对定位离场项，容器必须带 `.ds-list`（`position: relative`），否则会跳到页面左上角。

### 动效规则

1. 进入用 `emphasized`，退出用 `exit` 且更短（160ms）。
2. 位移不超过 8px；缩放不小于 0.96，避免“弹跳感”。
3. 同一屏只允许一种过渡语义；不要页面淡入 + 卡片滑入 + 列表缩放同时发生。
4. 不要给 `transition` 写 `all`。
5. Element Plus 的 Dialog/Drawer/Message 已自带过渡，不要再包一层 `Transition`。
6. 大量列表（>100 项）不做逐项动画；用骨架屏或整体淡入。
7. **`prefers-reduced-motion: reduce` 下所有过渡降到 1ms 且取消位移/缩放**（已内置全局规则）。

## 6. 验收清单

1. 顶部提示全部来自 `toast.info/warn/error`；全局搜索不到直接 `ElMessage(` 调用。
2. 三种提示的 class 分别为 `ds-toast--info / --warn / --error`，背景/边框随主题变化。
3. 数据页的工具栏、表格、分页在同一个 `.ds-data-grid` 内；分页右边缘与表格右边缘对齐。
4. 点击任意业务 Modal 遮罩，弹窗保持打开；ESC 可关闭（有未保存内容时先确认）。
5. 页面一级区块都有卡片边界；卡片内边距 16、卡片间 gap 16/24；没有三层嵌套卡片。
6. 路由切换、面板显隐、列表增删都有统一过渡；无 `transition: all`。
7. 模拟 `prefers-reduced-motion: reduce` 后，页面切换无位移、无长动画。
8. 控制台无 Vue 警告；`AppDialog` 的 `v-model` 正常回写。

## 7. 运行态取证

```js
// 提示三态
document.querySelector('.el-message.ds-toast--warn')?.className;
getComputedStyle(document.querySelector('.ds-toast--warn')).backgroundColor;

// 分页右下角
const grid = document.querySelector('.ds-data-grid');
const pager = grid.querySelector('.ds-data-grid__pager');
const g = grid.getBoundingClientRect();
const p = pager.getBoundingClientRect();
Math.abs(g.right - p.right) <= 1 && Math.abs(g.bottom - p.bottom) <= 1;

// 遮罩关闭（点击遮罩后仍存在）
document.querySelector('.el-overlay')?.click();
!!document.querySelector('.el-dialog');

// 动效
getComputedStyle(document.querySelector('.ds-page-enter-active') ?? document.body).transitionDuration;
```

## 8. 反模式

- **每个页面各自 `ElMessage({ type, duration, customClass })`**：三套颜色、三套时长。
- **把 `toast` 当确认框**：破坏操作必须显式确认。
- **分页用 `margin-left: auto`**：撤掉父级 flex 就失效；应作为 Grid 项右对齐。
- **分页放在表格外、另一个卡片里**：滚动后与表格脱节。
- **Dialog 默认允许点遮罩关闭**：未保存内容丢失；破坏性表单尤其危险。
- **整页扁平白底**：区块无边界，信息层级只能靠字号。
- **卡片套卡片**：每层都加边框、阴影，页面变脏。
- **页面切换无过渡 / `transition: all`**：生硬或抖动。
- **退出动画比进入还长**：操作显得迟钝。
- **忽略 reduced-motion**：对前庭敏感用户不友好。
