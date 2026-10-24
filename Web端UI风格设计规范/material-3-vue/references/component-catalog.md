# MD3 组件目录（Vue 3 / @m3e/web 2.10.2）

本目录按 `@m3e/web` 2.10.2 的**真实模块、标签、插槽、属性、事件**编写，示例是 Vue 3 模板。React 版的 `M3e*` 组件名与属性**不能照搬**：`@m3e/react` 只是同一套自定义元素的 React 包装，Vue 直接写 `m3e-*` 标签。本目录出现的 `M3eSelect` 等是**本技能的 Vue 声明式包装**（`assets/vue/use-m3e-model.js`），不是 `@m3e/react` 的组件。

**通用约束：**

- 每个示例涉及的模块都要在 `main.js` 或 `.client` 插件中 `import '@m3e/web/<module>'`。
- 具名插槽用真实元素 + `slot="名称"`；文本插槽要包 `<span>`。
- 布尔属性：静态写 `checked`，动态写 `:checked="x"`，不要写 `checked="false"`。
- 事件按连字符原样绑定：`@change`、`@view-change`、`@opened`。
- 弹层用 trigger + `for="id"`，不用受控 `open` 也可以（`open` 仍可直接绑）。
- Vuetify 列只给等价组件名；完整写法见 `vuetify-alternative.md`。
- **声明式绑定**：单值/布尔控件用 `assets/vue/use-m3e-model.js` 的包装组件 + `v-model`。示例中的 `M3eSelect`、`M3eSwitch`、`M3eCheckbox`、`M3eButton`、`M3eIconButton`、`M3eDrawerContainer`、`M3eSliderThumb`、`M3eDateInput`、`M3eDatepicker`、`M3eTimepicker` 都来自该文件；只读集合（segmented / filter-chip-set / selection-list / tabs / nav-bar / tree）保留子项 `selected` + 事件读取。

---

## 操作

### 按钮 `m3e-button`

| 项 | 内容 |
|---|---|
| 变体 | `variant` = `text`（默认）/ `filled` / `tonal` / `elevated` / `outlined` |
| 尺寸 | `size` = `extra-small` / `small`（默认）/ `medium` / `large` / `extra-large` |
| 形状 | `shape` = `rounded`（默认）/ `square` |
| 其他 | `toggle` + `selected`；`href`、`target`、`download`、`rel`；`type`、`name`、`value`；`disabled`、`disabled-interactive` |
| 插槽 | 默认（文字）、`icon`、`trailing-icon`、`selected`、`selected-icon` |
| 事件 | `click`；切换时 `beforeinput` / `input` / `change` |

```vue
<m3e-button variant="filled">
  <m3e-icon slot="icon" name="add" />
  新建
</m3e-button>

<m3e-button variant="outlined" href="/docs">
  文档
  <m3e-icon slot="trailing-icon" name="open_in_new" />
</m3e-button>

<M3eButton toggle v-model="liked">
  <m3e-icon slot="icon" name="favorite_border" />
  <m3e-icon slot="selected-icon" name="favorite" />
  收藏
</M3eButton>
```

尺寸演示：`size="extra-small|small|medium|large|extra-large"`。纯图标按钮请改用 `m3e-icon-button` 并提供 `aria-label`。

**Vuetify**：`v-btn`（`variant="flat"` + `color="primary"` ≈ filled；`tonal` / `outlined` / `text` / `elevated` 同名）。

### 按钮组 `m3e-button-group`

| 项 | 内容 |
|---|---|
| 属性 | `variant` = `standard`（默认）/ `connected`；`size`；`multi` |
| 插槽 | 默认（按钮） |

```vue
<m3e-button-group variant="connected">
  <m3e-button variant="outlined" toggle>日</m3e-button>
  <m3e-button variant="outlined" toggle selected>周</m3e-button>
  <m3e-button variant="outlined" toggle>月</m3e-button>
</m3e-button-group>
```

**Vuetify**：`v-btn-group` / `v-btn-toggle`。

### FAB `m3e-fab`

| 项 | 内容 |
|---|---|
| 变体 | `variant` = `primary-container`（默认）/ `primary` / `secondary` / `secondary-container` / `tertiary` / `tertiary-container` / `surface` |
| 尺寸 | `size` = `small` / `medium`（默认）/ `large` |
| 其他 | `extended`、`lowered`、`disabled`、`href` |
| 插槽 | 默认（图标）、`label`（扩展 FAB）、`close-icon` |

```vue
<m3e-fab aria-label="新建">
  <m3e-icon name="add" />
</m3e-fab>

<m3e-fab extended size="large" variant="tertiary">
  <m3e-icon name="edit" />
  <span slot="label">写消息</span>
</m3e-fab>
```

**Vuetify**：`v-fab`（`v-speed-dial` 用于 FAB 菜单）。

### FAB 菜单 `m3e-fab-menu`

```vue
<m3e-fab variant="primary" size="large" aria-label="更多操作">
  <m3e-fab-menu-trigger for="fab-menu">
    <m3e-icon name="edit" />
  </m3e-fab-menu-trigger>
</m3e-fab>

<m3e-fab-menu id="fab-menu" variant="secondary">
  <m3e-fab-menu-item><m3e-icon slot="icon" name="draw" />画一笔</m3e-fab-menu-item>
  <m3e-fab-menu-item><m3e-icon slot="icon" name="photo_camera" />拍照</m3e-fab-menu-item>
</m3e-fab-menu>
```

`m3e-fab-menu` 只有 `variant`（primary/secondary/tertiary）与 `toggle`/`beforetoggle` 事件；`m3e-fab-menu-item` 支持 `disabled`、`href` 等链接属性。**Vuetify**：`v-speed-dial`。

### 图标按钮 `m3e-icon-button`

| 项 | 内容 |
|---|---|
| 变体 | `standard`（默认）/ `filled` / `tonal` / `outlined` / `elevated` |
| 尺寸 | extra-small / small / medium / large / extra-large |
| 宽度 | `width` = `default` / `narrow` / `wide` |
| 其他 | `toggle`、`selected`、`href`、`disabled` |
| 插槽 | 默认（图标）、`selected`（选中图标） |

```vue
<m3e-icon-button aria-label="设置">
  <m3e-icon name="settings" />
</m3e-icon-button>

<M3eIconButton toggle v-model="pinned" aria-label="置顶">
  <m3e-icon name="push_pin" />
</M3eIconButton>
```

**Vuetify**：`v-btn icon`。

### 分段按钮 `m3e-segmented-button`

| 项 | 内容 |
|---|---|
| 属性 | `multi`（多选）；`disabled`；`hide-selection-indicator`；`name` |
| 子项 | `m3e-button-segment`：`checked`、`value`、`disabled`，默认插槽 + `icon` 插槽 |
| 事件 | `change`（选中变化） |

```vue
<m3e-segmented-button @change="size = $event.target.value">
  <m3e-button-segment value="s" checked>Small</m3e-button-segment>
  <m3e-button-segment value="m">Medium</m3e-button-segment>
  <m3e-button-segment value="l">Large</m3e-button-segment>
</m3e-segmented-button>
```

单选由组件内部管理；`.value` 是只读 getter，初始态写在子项 `checked` 上，Vue 侧监听 `change` 回写状态。**Vuetify**：`v-btn-toggle`。

### 分裂按钮 `m3e-split-button`

```vue
<m3e-split-button variant="tonal">
  <m3e-button variant="tonal" slot="leading-button">
    <m3e-icon slot="icon" name="save" />保存
  </m3e-button>
  <m3e-icon-button slot="trailing-button" aria-label="更多保存方式">
    <m3e-icon name="keyboard_arrow_down" />
    <m3e-menu-trigger for="save-menu" />
  </m3e-icon-button>
</m3e-split-button>

<m3e-menu id="save-menu">
  <m3e-menu-item>另存为草稿</m3e-menu-item>
  <m3e-menu-item>导出 PDF</m3e-menu-item>
</m3e-menu>
```

`variant` = elevated / filled（默认）/ tonal / outlined；`size` 同按钮。**Vuetify**：无直接对应，用 `v-btn-group` 组合。

---

## 通信

### 徽章 `m3e-badge`

| 项 | 内容 |
|---|---|
| 属性 | `for`（目标元素 id）；`size` = small / medium（默认）/ large |
| 属性（property） | `position` = above-after / above-before / below-before / below-after / before / after / above / below |
| 插槽 | 默认（数字或文本） |

```vue
<m3e-icon-button id="notify" aria-label="通知">
  <m3e-icon name="notifications" />
</m3e-icon-button>
<m3e-badge for="notify" size="small">3</m3e-badge>
```

`m3e-badge` 是**独立元素**，用 `for` 附着到目标，不是包住目标。**Vuetify**：`v-badge`（`content` / `dot` 属性）。

### 线性进度 `m3e-linear-progress-indicator`

| 项 | 内容 |
|---|---|
| 属性 | `value`、`max`、`mode` = determinate（默认）/ indeterminate / buffer / query、`buffer-value`、`variant` = flat / wavy |

```vue
<m3e-linear-progress-indicator :value="60" :max="100" />
<m3e-linear-progress-indicator mode="indeterminate" />
```

### 环形进度 `m3e-circular-progress-indicator`

```vue
<m3e-circular-progress-indicator :value="75" />
<m3e-circular-progress-indicator indeterminate />
```

**Vuetify**：`v-progress-linear` / `v-progress-circular`。

### 加载指示器 `m3e-loading-indicator`

```vue
<m3e-loading-indicator />
<m3e-loading-indicator variant="contained" />
```

`variant` = uncontained（默认）/ contained；`pull-progress`（下拉刷新，1 = 一圈）为 property。**Vuetify**：无直接对应，用 `v-progress-circular`。

### Snackbar `m3e-snackbar`

| 项 | 内容 |
|---|---|
| 属性 | `open`、`action`（动作文字）、`duration`（默认 3000ms）、`dismissible`、`close-label` |
| 插槽 | 默认（消息）、`close-icon` |
| 事件 | `beforetoggle` / `toggle` |

```vue
<script setup>
import { ref } from 'vue';
const open = ref(false);
function undo() { /* ... */ open.value = false; }
</script>

<template>
  <m3e-button @click="open = true">删除</m3e-button>
  <m3e-snackbar
    :open="open"
    action="撤销"
    :duration="5000"
    dismissible
    @toggle="open = $event.newState === 'open'"
  >
    已删除 1 项
  </m3e-snackbar>
</template>
```

动作按钮的点击通过 Snackbar 的 `action` 渲染；如需自定义内容/动作，监听组件内部的 `click` 并判断目标不理想，建议直接用 `m3e-snackbar` + 外层受控逻辑，或改用一个轻量自绘条。**Vuetify**：`v-snackbar`。

### Tooltip `m3e-tooltip`

| 项 | 内容 |
|---|---|
| 属性 | `for`（目标 id）、`position` = above / below / before / after、`show-delay`、`hide-delay`、`disabled` |
| 插槽 | 默认（提示文本） |

```vue
<m3e-icon-button id="del" aria-label="删除">
  <m3e-icon name="delete" />
</m3e-icon-button>
<m3e-tooltip for="del" position="above">删除该项目</m3e-tooltip>
```

### 富 Tooltip `m3e-rich-tooltip`

```vue
<m3e-icon-button id="storage" aria-label="存储信息">
  <m3e-icon name="cloud" />
</m3e-icon-button>
<m3e-rich-tooltip for="storage" position="below-after">
  <span slot="subhead">存储空间不足</span>
  当前已用 9.2 GB，请清理后再上传。
  <div slot="actions">
    <m3e-button variant="text">
      <m3e-rich-tooltip-action>管理存储</m3e-rich-tooltip-action>
    </m3e-button>
  </div>
</m3e-rich-tooltip>
```

`position` 8 种方向（above/below/before/after 及 `-after`/`-before` 组合）。**Vuetify**：`v-tooltip`（富内容用 `v-overlay` 或 `v-menu`）。

### 图标 `m3e-icon`

| 项 | 内容 |
|---|---|
| 属性 | `name`（Material Symbols 名）、`variant` = outlined / rounded / sharp、`filled`、`grade` = low/medium/high、`weight` = 100–700、`optical-size` = 20–48 |

```vue
<m3e-icon name="home" />
<m3e-icon name="favorite" filled />
<m3e-icon name="settings" variant="rounded" :weight="600" />
```

必须加载 Material Symbols 字体（见 `vue-integration.md`）。**Vuetify**：`v-icon`（默认 mdi，也可配 Material Symbols 图标集）。

### 头像 `m3e-avatar`

```vue
<m3e-avatar>AB</m3e-avatar>
<m3e-avatar><m3e-icon name="person" /></m3e-avatar>
<m3e-avatar><img src="/avatar.jpg" alt="张三" /></m3e-avatar>
```

尺寸/形状用组件 CSS 变量或外层容器控制；库未暴露 `size` 属性（以 `.d.ts` 为准）。**Vuetify**：`v-avatar`。

### 骨架屏 `m3e-skeleton`

```vue
<m3e-skeleton :loaded="loaded" animation="wave" shape="rounded">
  <m3e-card variant="outlined">
    <div slot="content">真实内容</div>
  </m3e-card>
</m3e-skeleton>
```

`shape` = circular / rounded / square / auto；`animation` = pulse / wave / none；`loaded` 后显示真实内容。**Vuetify**：`v-skeleton-loader`。

---

## 容器

### 卡片 `m3e-card`

| 项 | 内容 |
|---|---|
| 变体 | `variant` = filled（默认）/ outlined / elevated |
| 布局 | `orientation` = vertical（默认）/ horizontal；`inline`；`actionable` |
| 插槽 | 默认（无内边距）、`header`、`content`（有内边距）、`actions`、`footer` |

```vue
<m3e-card variant="outlined" actionable>
  <img src="/cover.jpg" alt="" class="cover" />
  <div slot="content">
    <m3e-heading variant="title" size="medium" level="3">项目更新</m3e-heading>
    <p>新的设计系统已可评审。</p>
  </div>
  <div slot="actions">
    <m3e-button variant="text">稍后</m3e-button>
    <m3e-button variant="tonal">查看</m3e-button>
  </div>
</m3e-card>

<style scoped>
.cover { width: 100%; aspect-ratio: 16 / 9; object-fit: cover; display: block; }
</style>
```

`actionable` 让卡片响应悬停/按压并派发 `click`；不要把可点击卡片再包一层按钮。**Vuetify**：`v-card`。

### 对话框 `m3e-dialog`

| 项 | 内容 |
|---|---|
| 属性 | `open`、`alert`、`dismissible`、`disable-close`、`no-focus-trap`、`close-label`；`returnValue`（property） |
| 插槽 | 默认（内容）、`header`、`actions`、`close-icon` |
| 事件 | `opening` / `opened` / `closing` / `closed` / `cancel` |
| 触发器 | `m3e-dialog-trigger for="对话框id"` |
| 动作 | `m3e-dialog-action return-value="ok"` |

```vue
<m3e-button variant="filled">
  <m3e-dialog-trigger for="confirm">删除</m3e-dialog-trigger>
</m3e-button>

<m3e-dialog id="confirm" dismissible @closed="onClosed">
  <span slot="header">确认删除？</span>
  删除后无法恢复。
  <div slot="actions" end>
    <m3e-button variant="text">
      <m3e-dialog-action return-value="cancel">取消</m3e-dialog-action>
    </m3e-button>
    <m3e-button variant="filled" autofocus>
      <m3e-dialog-action return-value="ok">删除</m3e-dialog-action>
    </m3e-button>
  </div>
</m3e-dialog>
```

`@closed` 里读 `$event.target.returnValue`。**Vuetify**：`v-dialog`。

### 底部表单 `m3e-bottom-sheet`

| 项 | 内容 |
|---|---|
| 属性 | `open`、`modal`、`handle`、`handle-label`、`hideable`、`detents`（空格分隔，如 `fit half full`）、`detent`（索引）、`hide-friction`、`overshoot-limit` |
| 插槽 | 默认（内容）、`header` |
| 事件 | `opening` / `opened` / `closing` / `closed` / `cancel` |
| 触发器 | `m3e-bottom-sheet-trigger for="id"` |

```vue
<m3e-button>
  <m3e-bottom-sheet-trigger for="sheet">打开</m3e-bottom-sheet-trigger>
</m3e-button>

<m3e-bottom-sheet id="sheet" modal handle hideable detents="fit half full">
  <span slot="header">分享到</span>
  <m3e-action-list>
    <m3e-list-action>
      <m3e-bottom-sheet-action>备忘录</m3e-bottom-sheet-action>
      <span slot="supporting-text">保存为笔记</span>
    </m3e-list-action>
  </m3e-action-list>
</m3e-bottom-sheet>
```

**Vuetify**：`v-bottom-sheet`。

### 抽屉容器 `m3e-drawer-container`

| 项 | 内容 |
|---|---|
| 插槽 | 默认（主内容）、`start`（起始抽屉）、`end`（结束抽屉） |
| 属性 | `start` / `end`（是否打开）；`start-mode` / `end-mode` = side（默认）/ over / push / auto；`start-divider` / `end-divider` |
| 事件 | `change`（抽屉开合） |
| 触发器 | `m3e-drawer-toggle for="容器id"` |

```vue
<script setup>
import { ref } from 'vue';
const open = ref(true);
</script>

<template>
  <M3eDrawerContainer id="shell" v-model="open" start-mode="auto">
    <nav slot="start">
      <m3e-nav-menu>…</m3e-nav-menu>
    </nav>
    <main>
      <m3e-icon-button aria-label="菜单">
        <m3e-icon name="menu" />
        <m3e-drawer-toggle for="shell" />
      </m3e-icon-button>
      <slot />
    </main>
  </M3eDrawerContainer>
</template>
```

`auto` 会按容器宽度在 side/over 之间切换；需要固定行为时显式写 `side` / `over` / `push`。**Vuetify**：`v-navigation-drawer`。

### 展开面板 `m3e-accordion` / `m3e-expansion-panel`

| 项 | 内容 |
|---|---|
| Accordion | `multi`（允许多个同时展开） |
| Panel | `open`、`disabled`、`hide-toggle`、`toggle-direction`（vertical/horizontal）、`toggle-position`（before/after） |
| 插槽 | `header`、默认（内容）、`actions`、`toggle-icon` |
| 事件 | `opening` / `opened` / `closing` / `closed` |

```vue
<m3e-accordion multi>
  <m3e-expansion-panel>
    <span slot="header">运输信息</span>
    预计 3–5 个工作日送达。
  </m3e-expansion-panel>
  <m3e-expansion-panel open>
    <span slot="header">退换政策</span>
    签收后 7 天内可无理由退换。
  </m3e-expansion-panel>
</m3e-accordion>
```

**Vuetify**：`v-expansion-panels` / `v-expansion-panel`。

### 分割面板 `m3e-split-pane`

| 项 | 内容 |
|---|---|
| 插槽 | `start`、`end` |
| 属性 | `orientation` = horizontal（默认）/ vertical / auto；`value`（0–100，起始面板占比）、`min`、`max`、`step`、`detents`（空格分隔）、`wrap-detents`、`label` |
| 事件 | `beforeinput` / `input` / `change` |

```vue
<m3e-split-pane orientation="horizontal" :value="40" :min="20" :max="70" detents="30 50 70">
  <aside slot="start"><slot name="list" /></aside>
  <main slot="end"><slot name="detail" /></main>
</m3e-split-pane>
```

**Vuetify**：无直接对应，用 `v-row`/`v-col` 或自绘。

### 内容面板 `m3e-content-pane`

```vue
<m3e-content-pane>
  <p>可垂直滚动的文档内容。</p>
</m3e-content-pane>
```

带形状与内边距的滚动表面；不要在里面再套一个 `overflow: auto` 的容器。

### 轮播 / 滑动组 `m3e-slide-group`

```vue
<m3e-slide-group threshold="48">
  <m3e-card variant="outlined">卡片 1</m3e-card>
  <m3e-card variant="outlined">卡片 2</m3e-card>
  <m3e-card variant="outlined">卡片 3</m3e-card>
</m3e-slide-group>
```

`threshold` 是出现翻页按钮的溢出像素；`vertical`、`disabled`、`previous-page-label`、`next-page-label`。**Vuetify**：`v-slide-group`。

### 提示卡 `m3e-callout`

```vue
<m3e-callout variant="error">
  <m3e-icon slot="icon" name="error" />
  <span slot="label">需要处理</span>
  请先修正表单错误再提交。
</m3e-callout>
```

`variant` = secondary（默认）/ primary / tertiary / error / surface；`shape` = rounded / square。**Vuetify**：`v-alert`。

---

## 输入

### 表单字段 `m3e-form-field`

`@m3e/web` 2.10.2 **没有** `m3e-text-field`。文本输入由 `m3e-form-field` 容器 + **原生 `input` / `textarea`** 组成；`m3e-select`、`m3e-date-input`、`m3e-input-chip-set` 也可以作为它的控件。

| 项 | 内容 |
|---|---|
| 属性 | `variant` = outlined（默认）/ filled；`float-label` = auto（默认）/ always；`error`；`hide-required-marker`；`hide-subscript` = auto / always / never |
| 插槽 | 默认（控件）、`label`、`prefix`、`prefix-text`、`suffix`、`suffix-text`、`hint`、`error` |
| 原生校验 | 控件的 `required` / `pattern` / `minlength` 等直接生效；错误文本放 `error` 插槽 |

```vue
<script setup>
import { ref } from 'vue';
const email = ref('');
const valid = ref(true);
</script>

<template>
  <m3e-form-field variant="outlined" :error="!valid">
    <label slot="label" for="email">邮箱</label>
    <m3e-icon slot="prefix" name="mail" />
    <input
      id="email"
      v-model="email"
      type="email"
      required
      @input="valid = $event.target.checkValidity()"
    />
    <m3e-icon-button slot="suffix" aria-label="清空" @click="email = ''">
      <m3e-icon name="close" />
    </m3e-icon-button>
    <span slot="hint">我们不会公开你的邮箱</span>
    <span slot="error">请输入有效的邮箱地址</span>
  </m3e-form-field>
</template>
```

- `variant="filled"` 为填充式；`float-label="always"` 让标签常驻顶部。
- `error` 为真时显示 `error` 插槽并给控件 `aria-invalid`；也可以不手控，让原生校验触发。
- **`v-model` 放在 input 上**，不要放在 `m3e-form-field`。

多行文本：

```vue
<m3e-form-field variant="outlined">
  <label slot="label" for="msg">留言</label>
  <textarea id="msg" rows="4" maxlength="500"></textarea>
  <m3e-textarea-autosize for="msg" :min-rows="4" :max-rows="10" />
  <span slot="hint">最多 500 字</span>
</m3e-form-field>
```

`m3e-textarea-autosize` 用 `for` 指向 textarea，`min-rows` / `max-rows` 控制高度。**Vuetify**：`v-text-field` / `v-textarea`。

### 复选框 `m3e-checkbox`

| 项 | 内容 |
|---|---|
| 属性 | `checked`、`indeterminate`、`disabled`、`required`、`name`、`value` |
| 事件 | `change` / `input`；`invalid` |

```vue
<label>
  <M3eCheckbox v-model="agree" required />
  接受条款
</label>

<m3e-checkbox indeterminate aria-label="部分选中" />
```

**Vuetify**：`v-checkbox`。

### 单选组 `m3e-radio-group`

```vue
<m3e-radio-group name="size" @change="size = $event.target.value">
  <label><m3e-radio value="s" :checked="size === 's'" /> Small</label>
  <label><m3e-radio value="m" :checked="size === 'm'" /> Medium</label>
  <label><m3e-radio value="l" :checked="size === 'l'" /> Large</label>
</m3e-radio-group>
```

`m3e-radio-group` 负责分组、键盘导航与表单校验；`m3e-radio` 只提供 `checked` / `value` / `disabled` / `name` / `required`，**没有 label 插槽**，用原生 `<label>` 包住。组的 `.value` 是只读 getter，不能绑 `v-model`：选中态用每个 radio 的 `:checked` 声明，`change` 只负责回写状态。**Vuetify**：`v-radio-group` + `v-radio`。

### 开关 `m3e-switch`

```vue
<label>
  <M3eSwitch v-model="dark" icons="both" />
  深色模式
</label>
```

`icons` = none（默认）/ selected / both；没有 `selected` 属性，状态属性是 `checked`。**Vuetify**：`v-switch`。

### 滑块 `m3e-slider`

```vue
<m3e-slider :min="0" :max="100" :step="5" discrete labelled>
  <M3eSliderThumb v-model="volume" />
</m3e-slider>

<!-- 范围：两个 thumb -->
<m3e-slider :min="0" :max="100" labelled>
  <M3eSliderThumb v-model="range[0]" />
  <M3eSliderThumb v-model="range[1]" />
</m3e-slider>
```

`m3e-slider` 自身没有 `value`；值在 `m3e-slider-thumb` 上。`size` = extra-small（默认）/ small / medium / large / extra-large；`orientation` = horizontal / vertical。**Vuetify**：`v-slider` / `v-range-slider`。

### 选择器 `m3e-select`

| 项 | 内容 |
|---|---|
| 属性 | `value`（`string \| string[] \| null`；attribute 形式多选用逗号分隔）、`multi`、`required`、`disabled`、`name`、`hide-selection-indicator` |
| 子项 | `m3e-option`（`value`、`selected`、`disabled`）、`m3e-optgroup` |
| 事件 | `change` / `input` / `toggle` |

```vue
<m3e-form-field variant="outlined">
  <label slot="label" for="plan">方案</label>
  <M3eSelect id="plan" v-model="plan">
    <m3e-option value="free">Free</m3e-option>
    <m3e-option value="pro">Pro</m3e-option>
    <m3e-option value="team">Team</m3e-option>
  </M3eSelect>
</m3e-form-field>

<M3eSelect multi v-model="tags">
  <m3e-option value="vue">Vue</m3e-option>
  <m3e-option value="ts">TypeScript</m3e-option>
</M3eSelect>
```

多选的取值口径（`SelectElement.ts` 实测）：property 与事件里的 `value` 在选中 0 项时为 `null`、1 项时为字符串、多项时为**数组**；只有 HTML attribute 形式才是逗号分隔字符串。包装层的 `v-model` 直接拿到这个值，不要 `split(',')`。**Vuetify**：`v-select`。

### 自动完成 `m3e-autocomplete`

`for` 指向已经存在的原生 input；选项来自子元素，过滤由组件完成：

```vue
<m3e-form-field variant="outlined">
  <label slot="label" for="fruit">水果</label>
  <input id="fruit" v-model="fruit" />
</m3e-form-field>

<m3e-autocomplete for="fruit" auto-activate @query="onQuery" @change="onPick">
  <m3e-option value="apple">Apple</m3e-option>
  <m3e-option value="orange">Orange</m3e-option>
  <m3e-option value="banana">Banana</m3e-option>
</m3e-autocomplete>
```

- `filter` = contains（默认）/ starts-with / ends-with / none，也可传函数（property）。
- 事件：`query`（输入/聚焦变化）、`change`（提交值变化）、`toggle`（菜单开合）。
- 异步加载时用 `loading` + `loading-label`、`no-data-label`。
**Vuetify**：`v-autocomplete`。

### 芯片 `m3e-chip` 系

| 标签 | 用途 | 独立属性 |
|---|---|---|
| `m3e-chip` | 基础只读/可点击芯片 | `variant` outlined/elevated |
| `m3e-assist-chip` | 建议动作 | 链接属性 |
| `m3e-filter-chip` | 过滤，可选中 | `selected` |
| `m3e-input-chip` | 输入令牌 | `removable`、`remove-label` |
| `m3e-suggestion-chip` | 搜索建议 | 链接属性 |
| 对应 set | 容器 | `m3e-filter-chip-set multi`；`m3e-input-chip-set` 配 `input slot="input"` |

```vue
<m3e-chip-set role="group" aria-label="快捷操作">
  <m3e-assist-chip>
    <m3e-icon slot="icon" name="edit" />编辑
  </m3e-assist-chip>
  <m3e-assist-chip>
    <m3e-icon slot="icon" name="delete" />删除
  </m3e-assist-chip>
</m3e-chip-set>

<m3e-filter-chip-set multi @change="filters = $event.target.value">
  <m3e-filter-chip value="veg" selected>素食</m3e-filter-chip>
  <m3e-filter-chip value="vegan">纯素</m3e-filter-chip>
  <m3e-filter-chip value="gf">无麸质</m3e-filter-chip>
</m3e-filter-chip-set>

<m3e-form-field variant="outlined">
  <label slot="label" for="kw">关键词</label>
  <m3e-input-chip-set aria-label="关键词">
    <input id="kw" slot="input" placeholder="输入后回车" />
  </m3e-input-chip-set>
</m3e-form-field>
```

输入芯片的自定义输入框必须放在 `slot="input"`。`m3e-filter-chip-set` 的 `.value` 是**只读** getter（0 项 `null` / 1 项字符串 / 多项数组），初始选中态写在各 `m3e-filter-chip` 的 `selected` 上。**Vuetify**：`v-chip` / `v-chip-group` / `v-combobox`（输入芯片）。

### 日期选择器 `m3e-datepicker`

| 项 | 内容 |
|---|---|
| 属性 | `variant` = docked（默认）/ modal / auto；`range`；`date`（Date，property）；`min-date` / `max-date`；`start-view` = month/year/multi-year；`clearable`；`label` |
| 事件 | `change`；`beforetoggle` / `toggle` |
| 触发器 | `m3e-datepicker-toggle for="id"` |

```vue
<script setup>
import { ref } from 'vue';
const date = ref(new Date(2026, 9, 24));
</script>

<template>
  <m3e-form-field variant="outlined">
    <label slot="label" for="date-field">日期</label>
    <input id="date-field" :value="date?.toLocaleDateString('zh-CN')" readonly />
    <m3e-icon-button slot="suffix" aria-label="选择日期">
      <m3e-icon name="calendar_today" />
      <m3e-datepicker-toggle for="date-picker" />
    </m3e-icon-button>
  </m3e-form-field>

  <M3eDatepicker
    id="date-picker"
    variant="modal"
    v-model="date"
    :min-date="new Date(2026, 0, 1)"
    :max-date="new Date(2026, 11, 31)"
  />
</template>
```

范围选择用 `range`，值在 `rangeStart` / `rangeEnd`。`m3e-date-input` 是键盘分段输入，可单独使用或与 form-field 组合：

```vue
<m3e-form-field variant="outlined">
  <label slot="label" for="d">日期</label>
  <M3eDateInput id="d" type="date" v-model="date" />
  <span slot="hint">MM/DD/YYYY</span>
</m3e-form-field>
```

**Vuetify**：`v-date-picker` / `v-date-input`。

### 时间选择器 `m3e-timepicker`

| 项 | 内容 |
|---|---|
| 属性 | `variant` = docked（默认）/ modal / auto；`mode` = dial（默认）/ input；`format` = 12（默认）/ 24 / auto；`orientation`；`show-seconds`；`min-time` / `max-time`；`date`（Date，property） |
| 事件 | `change`；`beforetoggle` / `toggle` |
| 触发器 | `m3e-timepicker-toggle for="id"` |

```vue
<m3e-button variant="outlined">
  <m3e-icon slot="icon" name="schedule" />
  选择时间
  <m3e-timepicker-toggle for="time-picker" />
</m3e-button>

<M3eTimepicker
  id="time-picker"
  variant="modal"
  format="24"
  v-model="time"
/>
```

`m3e-timepicker` 的 `date` 是 `Date | null`；不要用 `"14:30"` 字符串。**Vuetify**：`v-time-picker`。

### 搜索 `m3e-search-bar` / `m3e-search-view`

```vue
<m3e-search-bar clearable @clear="keyword = ''" @input="keyword = $event.target.value">
  <m3e-icon slot="leading" name="search" />
  <input slot="input" v-model="keyword" placeholder="搜索商品…" />
</m3e-search-bar>

<m3e-search-view mode="docked" contained :open="expanded"
  @query="runSearch" @toggle="expanded = $event.newState === 'open'">
  <input slot="input" v-model="keyword" placeholder="搜索…" />
  <m3e-list>
    <m3e-list-item v-for="item in results" :key="item.id">{{ item.title }}</m3e-list-item>
  </m3e-list>
</m3e-search-view>
```

`m3e-search-view` 的 `mode` = fullscreen / docked / auto；`contained` 给持久填充容器。事件：`query` / `clear` / `toggle`。**Vuetify**：`v-text-field` + `v-autocomplete` 或自定义搜索面板。

---

## 导航

### 顶部应用栏 `m3e-app-bar`

| 项 | 内容 |
|---|---|
| 属性 | `size` = small（默认）/ medium / large；`centered`；`for`（滚动监听的容器 id） |
| 插槽 | `leading`（别名 `leading-icon`）、`title`、`subtitle`、`trailing`（别名 `trailing-icon`） |

```vue
<m3e-app-bar size="medium" for="scroll-root">
  <m3e-icon-button slot="leading" aria-label="返回">
    <m3e-icon name="arrow_back" />
  </m3e-icon-button>
  <span slot="title">徒步路线 Top 10</span>
  <span slot="subtitle">发现热门路线</span>
  <m3e-icon-button slot="trailing" variant="tonal" aria-label="收藏">
    <m3e-icon name="bookmark" filled />
  </m3e-icon-button>
</m3e-app-bar>
```

`size="medium|large"` 在 `for` 指定的滚动容器滚动时收缩；不传 `for` 时监听窗口。**Vuetify**：`v-app-bar`。

### 底部导航栏 `m3e-nav-bar`

| 项 | 内容 |
|---|---|
| 属性 | `mode` = compact（默认）/ expanded / auto |
| 子项 | `m3e-nav-item`：`selected`、`disabled`、`href`、`orientation`；插槽 `icon`、`selected-icon`、默认（标签） |
| 事件 | `change` |

```vue
<script setup>
import { ref } from 'vue';
const tab = ref(0);
function onNavChange(e) {
  const bar = e.target;
  tab.value = bar.items.indexOf(bar.selected);
}
</script>

<m3e-nav-bar @change="onNavChange">
  <m3e-nav-item :selected="tab === 0">
    <m3e-icon slot="icon" name="home" />
    <m3e-icon slot="selected-icon" name="home" filled />
    首页
  </m3e-nav-item>
  <m3e-nav-item :selected="tab === 1">
    <m3e-icon slot="icon" name="search" />搜索
  </m3e-nav-item>
  <m3e-nav-item :selected="tab === 2">
    <m3e-icon slot="icon" name="person" />我的
  </m3e-nav-item>
</m3e-nav-bar>
```

3–5 个目标最合适；始终显示标签。**Vuetify**：`v-bottom-navigation`。

### 导航侧栏 `m3e-nav-rail`

```vue
<m3e-nav-rail id="rail" mode="auto">
  <m3e-icon-button toggle>
    <m3e-icon name="menu" />
    <m3e-icon slot="selected" name="menu_open" />
    <m3e-nav-rail-toggle for="rail" />
  </m3e-icon-button>

  <m3e-fab size="small" variant="tertiary" aria-label="新建">
    <m3e-icon name="edit" />
  </m3e-fab>

  <m3e-nav-item selected>
    <m3e-icon slot="icon" name="home" />首页
  </m3e-nav-item>
  <m3e-nav-item>
    <m3e-icon slot="icon" name="search" />搜索
  </m3e-nav-item>
</m3e-nav-rail>
```

`mode` = compact / expanded / auto；展开切换由 `m3e-nav-rail-toggle` 完成。**Vuetify**：`v-navigation-drawer rail`。

### 导航菜单 `m3e-nav-menu`

| 项 | 内容 |
|---|---|
| 子项 | `m3e-nav-menu-item`（`selected`、`open`、`disabled`；插槽 `icon`、`label`、`badge`、`selected-icon`、默认子项） |
| 分组 | `m3e-nav-menu-item-group` + `label` 插槽 |

```vue
<m3e-nav-menu>
  <m3e-nav-menu-item-group>
    <m3e-heading slot="label" variant="label" size="large">邮件</m3e-heading>
    <m3e-nav-menu-item selected>
      <m3e-icon slot="icon" name="inbox" />
      <span slot="label">收件箱</span>
      <span slot="badge">24</span>
    </m3e-nav-menu-item>
    <m3e-nav-menu-item>
      <m3e-icon slot="icon" name="send" />
      <span slot="label">已发送</span>
    </m3e-nav-menu-item>
  </m3e-nav-menu-item-group>

  <m3e-nav-menu-item open>
    <m3e-icon slot="icon" name="settings" />
    <span slot="label">设置</span>
    <m3e-nav-menu-item><span slot="label">外观</span></m3e-nav-menu-item>
    <m3e-nav-menu-item><span slot="label">通知</span></m3e-nav-menu-item>
  </m3e-nav-menu-item>
</m3e-nav-menu>
```

嵌套菜单项直接放在父项内部；`open` 控制展开。**Vuetify**：`v-list nav` + `v-list-group`。

### 标签页 `m3e-tabs`

| 项 | 内容 |
|---|---|
| 属性 | `variant` = secondary（默认）/ primary；`stretch`；`header-position` = before / after；`disable-swipe`；`disable-pagination` |
| 子项 | `m3e-tab`：`selected`、`for`、`disabled`、`icon` 插槽；`m3e-tab-panel`：`id` |
| 事件 | `change` |

```vue
<script setup>
import { ref } from 'vue';
const panel = ref('panel-video');
function onTabChange(e) {
  panel.value = e.target.querySelector('m3e-tab[selected]')?.getAttribute('for') ?? panel.value;
}
</script>

<template>
<m3e-tabs variant="primary" @change="onTabChange">
  <m3e-tab selected for="panel-video">
    <m3e-icon slot="icon" name="videocam" />视频
  </m3e-tab>
  <m3e-tab for="panel-photo">
    <m3e-icon slot="icon" name="photo" />照片
  </m3e-tab>

  <m3e-tab-panel id="panel-video">视频内容</m3e-tab-panel>
  <m3e-tab-panel id="panel-photo">照片内容</m3e-tab-panel>
</m3e-tabs>
</template>
```

面板靠 `for` ↔ `id` 关联；不要同时用 `v-if` 手动切面板。**Vuetify**：`v-tabs` + `v-window`。

### 面包屑 `m3e-breadcrumb`

```vue
<m3e-breadcrumb>
  <m3e-breadcrumb-item href="/">首页</m3e-breadcrumb-item>
  <m3e-breadcrumb-item href="/reports">报表</m3e-breadcrumb-item>
  <m3e-breadcrumb-item current="page">年度</m3e-breadcrumb-item>
</m3e-breadcrumb>
```

`current` = page / step / location / date / time / true；自定义分隔符用 `<span slot="separator">/</span>`。**Vuetify**：`v-breadcrumbs`。

### 工具栏 `m3e-toolbar`

```vue
<m3e-toolbar variant="vibrant" shape="rounded">
  <m3e-icon-button aria-label="撤销"><m3e-icon name="undo" /></m3e-icon-button>
  <m3e-icon-button aria-label="重做"><m3e-icon name="redo" /></m3e-icon-button>
  <m3e-divider vertical inset />
  <m3e-icon-button variant="filled" aria-label="添加"><m3e-icon name="add" /></m3e-icon-button>
</m3e-toolbar>
```

`variant` = standard（默认）/ vibrant；`shape` = square（默认）/ rounded；`elevated`、`vertical`。**Vuetify**：`v-toolbar`。

---

## 数据展示

### 列表 `m3e-list`

| 项 | 内容 |
|---|---|
| 容器 | `m3e-list`（`variant` = standard/segmented）、`m3e-action-list`、`m3e-selection-list`（`multi`） |
| 条目 | `m3e-list-item`（静态）、`m3e-list-action`（点击/链接）、`m3e-list-option`（可选中）、`m3e-expandable-list-item` |
| 插槽 | 默认（主文本）、`leading`、`overline`、`supporting-text`、`trailing` |
| 事件 | 可点击项 `click`；可选中列表 `change` |

```vue
<m3e-list>
  <m3e-list-item>
    <m3e-avatar slot="leading">AB</m3e-avatar>
    <span slot="overline">设计</span>
    张三
    <span slot="supporting-text">高级设计师</span>
    <span slot="trailing">3 分钟前</span>
  </m3e-list-item>

  <m3e-list-action @click="openSettings">
    <m3e-icon slot="leading" name="settings" />
    设置
    <m3e-icon slot="trailing" name="chevron_right" />
  </m3e-list-action>
</m3e-list>

<m3e-selection-list multi @change="selected = $event.target.value">
  <m3e-list-option value="a">选项 A</m3e-list-option>
  <m3e-list-option value="b">选项 B</m3e-list-option>
</m3e-selection-list>
```

`m3e-selection-list` 自己就是列表容器，不要再套在 `m3e-list` 里。它的 `.value` 同样只读（0 项 `null` / 1 项字符串 / 多项数组），初始选中态写在各 `m3e-list-option` 的 `selected` 上。

`m3e-list-item` 本身不可点击；可点击必须用 `m3e-list-action`。分隔线用 `<m3e-divider />`。**Vuetify**：`v-list` / `v-list-item`。

### 分页器 `m3e-paginator`

```vue
<m3e-paginator
  :length="300"
  :page-size="25"
  :page-index="page"
  page-sizes="10,25,50,100"
  show-first-last-buttons
  @page="page = $event.detail.pageIndex"
/>
```

`length` 是**条目总数**，不是页数；`page-index` 从 0 开始。事件 `page` 的 `detail` 含 `pageIndex` / `pageSize` / `length`。**Vuetify**：`v-pagination`。

### 步骤器 `m3e-stepper`

```vue
<m3e-stepper linear>
  <m3e-step for="s1">填写姓名</m3e-step>
  <m3e-step for="s2">填写地址</m3e-step>
  <m3e-step for="s3">完成</m3e-step>

  <m3e-step-panel id="s1">
    <m3e-form-field variant="outlined">
      <label slot="label" for="name">姓名</label>
      <input id="name" required />
    </m3e-form-field>
    <div slot="actions">
      <m3e-button><m3e-stepper-next>下一步</m3e-stepper-next></m3e-button>
    </div>
  </m3e-step-panel>

  <m3e-step-panel id="s2">
    <div slot="actions">
      <m3e-button><m3e-stepper-previous>上一步</m3e-stepper-previous></m3e-button>
      <m3e-button><m3e-stepper-next>下一步</m3e-stepper-next></m3e-button>
    </div>
  </m3e-step-panel>

  <m3e-step-panel id="s3">
    完成
    <div slot="actions">
      <m3e-button><m3e-stepper-reset>重新开始</m3e-stepper-reset></m3e-button>
    </div>
  </m3e-step-panel>
</m3e-stepper>
```

`orientation` = horizontal/vertical/auto；`linear` 会校验前序步骤。**Vuetify**：`v-stepper`。

### 树 `m3e-tree`

```vue
<m3e-tree multi cascade @change="selected = $event.target.selected">
  <m3e-tree-item open>
    <span slot="label">入门</span>
    <m3e-tree-item><span slot="label">概览</span></m3e-tree-item>
    <m3e-tree-item><span slot="label">安装</span></m3e-tree-item>
  </m3e-tree-item>
  <m3e-tree-item>
    <span slot="label">组件</span>
    <m3e-tree-item><span slot="label">按钮</span></m3e-tree-item>
    <m3e-tree-item><span slot="label">卡片</span></m3e-tree-item>
  </m3e-tree-item>
</m3e-tree>
```

`multi` 多选、`cascade` 级联；嵌套结构直接写在父项里。**Vuetify**：`v-treeview`。

### 目录 `m3e-toc`

```vue
<div class="doc-layout">
  <m3e-toc for="article" :max-depth="3">
    <span slot="overline">目录</span>
    <span slot="title">文档</span>
  </m3e-toc>
  <article id="article">
    <h2>简介</h2>
    <h2>开始使用</h2>
    <h3>安装</h3>
  </article>
</div>
```

`for` 指向内容容器；`max-depth` 控制抓取的标题层级。**Vuetify**：无直接对应。

### 标题 `m3e-heading`

```vue
<m3e-heading variant="display" size="large" level="1">大标题</m3e-heading>
<m3e-heading variant="headline" size="medium" level="2">区块标题</m3e-heading>
<m3e-heading variant="label" size="large" emphasized>强调标签</m3e-heading>
```

`variant` = display/headline/title/label；`size` = small/medium/large；`level` = 1–6（语义层级）；`emphasized`。

### 其他

| 组件 | 关键点 |
|---|---|
| `m3e-calendar` | 单日历视图；`date`、`min-date`、`max-date`、`start-view`；事件 `change` |
| `m3e-option` / `m3e-optgroup` / `m3e-option-panel` | 供 select/autocomplete 使用；也可单独组成选项面板 |
| `m3e-breadcrumb-item` | `href`、`current`、`disabled` |
| `m3e-shape` | `name` 取 35 个抽象形状之一（见 typography-and-shape.md） |
| `m3e-theme-icon` | 主题预览色块：`color`、`scheme`、`variant` |

---

## Vuetify 4 对照总表

| @m3e/web | Vuetify 4 | 备注 |
|---|---|---|
| m3e-button | v-btn | filled ≈ `variant="flat" color="primary"` |
| m3e-button-group | v-btn-group / v-btn-toggle | — |
| m3e-fab | v-fab | — |
| m3e-fab-menu | v-speed-dial | — |
| m3e-icon-button | v-btn icon | — |
| m3e-split-button | 无 | 用 v-btn-group 组合 |
| m3e-segmented-button | v-btn-toggle | — |
| m3e-badge | v-badge | — |
| m3e-linear-progress-indicator | v-progress-linear | — |
| m3e-circular-progress-indicator | v-progress-circular | — |
| m3e-loading-indicator | 无 | 用 v-progress-circular |
| m3e-snackbar | v-snackbar | — |
| m3e-tooltip / rich-tooltip | v-tooltip | 富内容用 v-overlay |
| m3e-card | v-card | — |
| m3e-dialog | v-dialog | — |
| m3e-bottom-sheet | v-bottom-sheet | — |
| m3e-drawer-container | v-navigation-drawer | — |
| m3e-accordion / expansion-panel | v-expansion-panels | — |
| m3e-split-pane | 无 | 自绘或 v-row/v-col |
| m3e-content-pane | 无 | v-sheet + overflow |
| m3e-slide-group | v-slide-group | — |
| m3e-callout | v-alert | — |
| m3e-checkbox | v-checkbox | — |
| m3e-radio-group | v-radio-group | — |
| m3e-switch | v-switch | — |
| m3e-slider | v-slider / v-range-slider | — |
| m3e-form-field + input | v-text-field / v-textarea | Vuetify 是单一组件 |
| m3e-select | v-select | — |
| m3e-autocomplete | v-autocomplete | — |
| m3e-chip 系 | v-chip / v-chip-group | 输入芯片用 v-combobox |
| m3e-datepicker / date-input | v-date-picker / v-date-input | — |
| m3e-timepicker | v-time-picker | — |
| m3e-search-bar / view | 无直接对应 | v-text-field / v-autocomplete |
| m3e-app-bar | v-app-bar | — |
| m3e-nav-bar | v-bottom-navigation | — |
| m3e-nav-rail | v-navigation-drawer rail | — |
| m3e-nav-menu | v-list nav + v-list-group | — |
| m3e-tabs | v-tabs + v-window | — |
| m3e-breadcrumb | v-breadcrumbs | — |
| m3e-toolbar | v-toolbar | — |
| m3e-list | v-list | — |
| m3e-avatar | v-avatar | — |
| m3e-heading | text-* 类 | 无独立组件 |
| m3e-paginator | v-pagination | — |
| m3e-stepper | v-stepper | — |
| m3e-tree | v-treeview | — |
| m3e-toc | 无 | 自绘或插件 |
| m3e-icon | v-icon | 默认 mdi，可换 Material Symbols |
| m3e-shape | 无 | 自绘 SVG/CSS |
| m3e-theme | v-theme-provider | 令牌体系不同 |
