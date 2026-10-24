# Vue 3 声明式写法（Element Plus）

本文件规定业务代码的写法边界：**模板只声明状态与绑定，不命令 DOM**。Element Plus 是原生 Vue 组件，所有表单控件都支持 `v-model`；不需要像 Web Components 那样写包装层。

## 1. 安装与注册

```bash
pnpm add element-plus@2.14.7 @element-plus/icons-vue@2.3.2
```

### 全量导入（推荐用于本项目模板）

```js
// main.js
import { createApp } from 'vue';
import ElementPlus from 'element-plus';
import zhCn from 'element-plus/es/locale/lang/zh-cn';
import 'element-plus/dist/index.css';
import 'element-plus/theme-chalk/dark/css-vars.css';
import './styles/design-system.css';

import App from './App.vue';

createApp(App).use(ElementPlus, { locale: zhCn }).mount('#app');
```

全量导入的代价是包体；换来的是所有组件与指令（`v-loading`、`v-infinite-scroll`）零配置可用。设计规范项目优先可预测性。

### 按需自动导入

```js
// vite.config.js
import { defineConfig } from 'vite';
import vue from '@vitejs/plugin-vue';
import AutoImport from 'unplugin-auto-import/vite';
import Components from 'unplugin-vue-components/vite';
import { ElementPlusResolver } from 'unplugin-vue-components/resolvers';

export default defineConfig({
  plugins: [
    vue(),
    AutoImport({
      resolvers: [ElementPlusResolver()],
      imports: ['vue', 'vue-router'],
      dts: 'src/auto-imports.d.ts',
    }),
    Components({
      resolvers: [ElementPlusResolver()],
      dts: 'src/components.d.ts',
    }),
  ],
});
```

按需模式下，`ElMessage` / `ElMessageBox` / `ElNotification` / `ElLoading` 的**样式**不会自动引入，需在 `main.js` 显式导入，且**必须排在 `design-system.css` 之前**：

```js
import 'element-plus/es/components/message/style/css';
import 'element-plus/es/components/message-box/style/css';
import 'element-plus/es/components/notification/style/css';
import 'element-plus/es/components/loading/style/css';
// 然后才是主题与设计层
import 'element-plus/theme-chalk/dark/css-vars.css';
import './styles/design-system.css';
```

组件样式文件会重新声明 `:root` 上的 `--el-*` 默认值；如果它排在 `design-system.css` 之后，会把主色、圆角、动效时长重置回默认。`use-toast.js` 本身不导入样式，避免在业务模块中“晚到”的样式覆盖设计层。图标不会自动注册，按需导入：

```js
import { Plus, Search, Delete } from '@element-plus/icons-vue';
```

### Nuxt

```ts
// nuxt.config.ts
export default defineNuxtConfig({
  modules: ['@element-plus/nuxt'],
  elementPlus: { importStyle: 'scss', locale: 'zh-cn' },
});
```

Nuxt 模块自动处理 SSR 与按需导入；`ElMessage` 等命令式 API 会注入。主题切换须放在 `onMounted` 或 client 插件里，避免服务端没有 `document`。

## 2. 先用 `v-model`，不要读 DOM

**硬规则**：业务模板不出现 `$event.target.value`、`document.querySelector`、`ref.value.innerHTML` 来驱动状态。事件只用于“动作”（提交、删除、关闭），值同步一律 `v-model`。

```vue
<!-- ✅ 声明式 -->
<el-input v-model="form.name" maxlength="32" show-word-limit />
<el-select v-model="form.plan" placeholder="选择方案">
  <el-option label="Free" value="free" />
  <el-option label="Pro" value="pro" />
</el-select>
<el-switch v-model="form.enabled" />
<el-slider v-model="form.volume" :min="0" :max="100" />

<!-- ❌ 命令式 -->
<el-input :model-value="form.name" @input="(e) => (form.name = e.target.value)" />
```

## 3. `v-model` 速查表

### 单值绑定

| 组件 | 绑定写法 | 模型类型 / 备注 |
|---|---|---|
| `el-input` | `v-model` | string；`type="textarea"` 同 |
| `el-input-number` | `v-model` | number，清空时为 `null` |
| `el-select` | `v-model` | 单选为单值，`multiple` 为数组 |
| `el-select-v2` | `v-model` | 大数据量下拉，同上 |
| `el-cascader` | `v-model` | 单选为数组路径；`props.multiple` 为二维数组 |
| `el-date-picker` | `v-model` | `Date` 或 string（配 `value-format`） |
| `el-time-picker` | `v-model` | `Date` 或 string（配 `value-format`） |
| `el-time-select` | `v-model` | `"HH:mm"` 字符串 |
| `el-checkbox` | `v-model` | boolean（`indeterminate` 单独绑定） |
| `el-checkbox-group` | `v-model` | 数组 |
| `el-radio` | `v-model` | 与 `value` 比较 |
| `el-radio-group` | `v-model` | 单值 |
| `el-switch` | `v-model` | boolean 或 `active-value`/`inactive-value` 指定值 |
| `el-slider` | `v-model` | number；`range` 为 `[min, max]` |
| `el-rate` | `v-model` | number |
| `el-color-picker` | `v-model` | string（`null` 表示空） |
| `el-transfer` | `v-model` | 右侧 key 数组 |
| `el-tree-select` | `v-model` | 选中节点的 value |
| `el-mention` | `v-model` | string |
| `el-segmented` | `v-model` | 选中项 value |
| `el-calendar` | `v-model` | `Date` |
| `el-tabs` | `v-model` | 当前 `el-tab-pane` 的 `name` |
| `el-collapse` | `v-model` | 展开项 name 数组；`accordion` 时为单值 |
| `el-dialog` | `v-model` | 是否显示（boolean） |
| `el-drawer` | `v-model` | 是否显示 |
| `el-popover` | `v-model:visible` | 手动控制时用；默认由 `trigger` 管理 |
| `el-tour` | `v-model` | 是否显示引导 |
| `el-upload` | `v-model:file-list` | 文件列表数组 |
| `el-pagination` | `v-model:current-page` `v-model:page-size` | **两个分别绑定** |

### 没有 `v-model` 的组件

| 组件 | 正确做法 |
|---|---|
| `el-table` | `:data` + `@selection-change` / `@sort-change` / `@row-click`；不要把 `v-model` 写在表格上 |
| `el-tree` | `:data` + `:default-checked-keys` + 组件 ref 的 `getCheckedKeys()` / `setCheckedKeys()`；勾选状态不是 `v-model` |
| `el-carousel` | `:initial-index` + `@change`；切页用 ref 的 `setActiveItem()` / `prev()` / `next()` |
| `el-menu` | `:default-active`（通常绑定 `route.path`）+ `router`；不要写 `v-model` |
| `el-steps` | `:active` 单向 |
| `el-splitter` | 分栏面板支持 `v-model:size`；整体拖拽结果用 `@resize` 读取 |
| `el-affix` / `el-backtop` | 纯行为组件，无模型 |

## 4. 自定义组件的 `defineModel`

Vue 3.4+ 用 `defineModel` 封装业务控件，调用方仍是标准 `v-model`：

```vue
<!-- components/MoneyInput.vue -->
<script setup>
const amount = defineModel({ type: Number, default: 0 });

const display = computed({
  get: () => (amount.value / 100).toFixed(2),
  set: (v) => { amount.value = Math.round(Number(v || 0) * 100); },
});
</script>

<template>
  <el-input v-model="display" inputmode="decimal">
    <template #prepend>¥</template>
  </el-input>
</template>
```

```vue
<MoneyInput v-model="form.amount" />
```

- 不要 `props.modelValue` + 手动 `emit('update:modelValue')`，除非需要兼容 Vue < 3.4。
- 不要在子组件里直接改 prop；`defineModel` 返回的是可写 ref。
- 多个模型用 `defineModel('start')` / `defineModel('end')`，调用方 `v-model:start`。

## 5. 表单

```vue
<script setup>
import { reactive, ref } from 'vue';

const formRef = ref();
const form = reactive({
  name: '',
  plan: 'pro',
  notify: true,
  tags: [],
});

const rules = {
  name: [
    { required: true, message: '请填写名称', trigger: 'blur' },
    { min: 2, max: 32, message: '长度 2–32', trigger: 'blur' },
  ],
  plan: [{ required: true, message: '请选择方案', trigger: 'change' }],
};

async function submit() {
  const valid = await formRef.value.validate().catch(() => false);
  if (!valid) return;
  await save(form);
}
</script>

<template>
  <el-form
    ref="formRef"
    :model="form"
    :rules="rules"
    label-width="96px"
    class="ds-form"
    @submit.prevent="submit"
  >
    <el-form-item label="名称" prop="name">
      <el-input v-model="form.name" />
    </el-form-item>

    <el-form-item label="方案" prop="plan">
      <el-select v-model="form.plan">
        <el-option label="Free" value="free" />
        <el-option label="Pro" value="pro" />
      </el-select>
    </el-form-item>

    <el-form-item label="通知">
      <el-switch v-model="form.notify" />
    </el-form-item>

    <div class="ds-actions">
      <el-button @click="formRef.resetFields()">重置</el-button>
      <el-button type="primary" native-type="submit">保存</el-button>
    </div>
  </el-form>
</template>
```

规则：

- `el-form` 必须有 `:model`，`el-form-item` 的 `prop` 必须是 `model` 上的**路径**；否则校验不生效。
- `trigger` 选 `blur`（文本/数字）或 `change`（选择/开关/日期）。
- 字段级错误用 `el-form-item` 的 `error` 或校验规则；**不要用 `ElMessage` 顶替字段错误**。
- `resetFields()` 只重置有 `prop` 且绑定了 `model` 的字段；数组/对象字段要深拷贝初始值。
- 提交按钮用 `native-type="submit"` + 表单 `@submit.prevent`，让回车可用。
- 动态字段用 `v-for` 生成，`prop` 用 `` `items.${index}.name` ``。
- 跨字段校验用自定义 validator；不要在模板里写 `:disabled="!form.a || !form.b"` 代替业务校验（禁用不等于校验）。

## 6. 表格 + 分页（声明式 CRUD）

```vue
<script setup>
import { useTableQuery } from './composables/use-crud.js';

const { rows, loading, currentPage, pageSize, total, load } = useTableQuery({ pageSize: 20 });

const filters = reactive({ keyword: '', status: '' });

function fetchPage(params) {
  return api.listOrders(params); // 返回 { rows, total }
}

watch([currentPage, pageSize], () => load(fetchPage, filters), { immediate: true });
</script>

<template>
  <el-card shadow="never">
    <div class="ds-data-grid">
      <div class="ds-row">
        <el-input v-model="filters.keyword" placeholder="搜索订单号" clearable style="width: 240px" />
        <el-select v-model="filters.status" placeholder="状态" clearable style="width: 140px">
          <el-option label="待付款" value="pending" />
          <el-option label="已完成" value="done" />
        </el-select>
        <el-button type="primary" @click="load(fetchPage, filters)">查询</el-button>
      </div>

    <el-table v-loading="loading" :data="rows" row-key="id" stripe>
      <el-table-column prop="id" label="订单号" min-width="160" />
      <el-table-column prop="customer" label="客户" min-width="140" />
      <el-table-column prop="amount" label="金额" width="120" align="right">
        <template #default="{ row }">{{ formatMoney(row.amount) }}</template>
      </el-table-column>
      <el-table-column label="操作" width="140" fixed="right">
        <template #default="{ row }">
          <el-button link type="primary" @click="openEdit(row)">编辑</el-button>
          <el-button link type="danger" @click="remove(row)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-pagination
      v-model:current-page="currentPage"
      v-model:page-size="pageSize"
      :total="total"
      :page-sizes="[10, 20, 50, 100]"
      layout="total, sizes, prev, pager, next, jumper"
      background
      class="ds-data-grid__pager"
    />
    </div>
  </el-card>
</template>
```

- `el-table` 的数据流是 `:data` 单向；行内修改要显式写回 `rows`（或用可编辑单元格的 `@change`）。
- 列宽：文本列 `min-width`，数字/操作列 `width`；操作列 `fixed="right"`。
- 选择列：`<el-table-column type="selection" width="48" />` + `@selection-change="(rows) => (selected = rows)"`。
- 排序：`sortable="custom"` + `@sort-change="({ prop, order }) => load(fetchPage, { ...filters, prop, order })"`；本地排序用 `sortable`。
- 分页必须用 `v-model:current-page` 与 `v-model:page-size` 两个绑定；只写 `:current-page` 不会回写。
- 分页是 `.ds-data-grid` 的最后一个网格项，固定在右下角；不要用 `margin-left: auto`。
- `loading` 用 `v-loading` 指令，不要手写骨架屏盖住表格。

## 7. 弹窗表单

```vue
<script setup>
import AppDialog from '@/components/AppDialog.vue';
import { toast } from '@/composables/use-toast.js';
import { useDialogForm } from './composables/use-crud.js';

// 必须解构：模板只自动解包顶层 ref
const { visible, mode, form, open, close } = useDialogForm({ name: '', plan: 'pro' });
const formRef = ref();

function openEdit(row) {
  open(row); // mode = 'edit'，字段浅合并
}

async function submit() {
  const valid = await formRef.value.validate().catch(() => false);
  if (!valid) return;
  await (mode.value === 'edit' ? update(form) : create(form));
  toast.info('已保存');
  close();
}
</script>

<template>
  <AppDialog v-model="visible" :title="mode === 'edit' ? '编辑' : '新建'" width="520px" destroy-on-close>
    <el-form ref="formRef" :model="form" label-width="96px">
      <el-form-item label="名称" prop="name" :rules="[{ required: true, message: '必填' }]">
        <el-input v-model="form.name" />
      </el-form-item>
    </el-form>
    <template #footer>
      <div class="ds-actions">
        <el-button @click="close">取消</el-button>
        <el-button type="primary" @click="submit">保存</el-button>
      </div>
    </template>
  </AppDialog>
</template>
```

`AppDialog` 默认 `close-on-click-modal=false`；业务弹窗不要直接写 `<el-dialog>` 依赖默认值（Element Plus 默认可点遮罩关闭）。只读弹窗要放开时显式 `:close-on-click-modal="true"`。

**最常见的错误**：把组合式函数返回的对象整体绑给 `v-model`：

```js
const dialog = useDialogForm({ ... });
```

```vue
<!-- ❌ dialog.visible 是 ref 对象，不是 boolean -->
<el-dialog v-model="dialog.visible">
```

模板只自动解包**顶层** ref；`dialog.visible` 是嵌套属性，拿到的是 `Ref` 实例。要么解构（`const { visible } = useDialogForm()`），要么 `reactive({ ...toRefs(dialog) })`。

其他规则：

- Dialog 默认懒渲染；需要测量尺寸或聚焦时等 `@opened`，不要 `nextTick` 后直接量。
- `destroy-on-close` 用于重置内部表单；否则关闭后表单状态保留。
- 宽度：桌面 480–640；窄屏 `width="92%"` 或 `fullscreen`。
- 关闭前确认用 `before-close`，不要监听 `@close` 再反向改 `visible`（会闪烁）。
- Drawer 与 Dialog 的绑定方式相同，`v-model` + `direction` + `size`。

## 8. 上传

```vue
<script setup>
import { ref } from 'vue';
import { toast } from '@/composables/use-toast.js';

const fileList = ref([]);

function beforeUpload(file) {
  const ok = file.size / 1024 / 1024 < 10;
  if (!ok) toast.error('单个文件不超过 10MB');
  return ok;
}
</script>

<template>
  <el-upload
    v-model:file-list="fileList"
    action="/api/upload"
    :before-upload="beforeUpload"
    :limit="5"
    accept="image/*"
    list-type="picture-card"
    :on-exceed="() => toast.warn('最多 5 个文件')"
  >
    <el-icon><Plus /></el-icon>
  </el-upload>
</template>
```

- 文件列表用 `v-model:file-list`，不要自己维护 `fileList` 再手动同步。
- 自定义请求用 `:http-request`；上传头用 `:headers`；额外字段用 `:data`。
- `auto-upload=false` 时提交前调 `uploadRef.value.submit()`。
- 失败重试用 `:on-error` 更新 `fileList[i].status`，不要重新 push 一条。

## 9. 反馈 API

顶部一次性反馈统一走 `toast`（`../assets/vue/use-toast.js`）：

```js
import { toast } from '@/composables/use-toast.js';
import { ElMessageBox, ElNotification } from 'element-plus';

toast.info('已保存');
toast.warn('额度将在 3 天后到期');
toast.error('保存失败，请重试');

await ElMessageBox.confirm('删除后不可恢复，确定继续？', '删除确认', {
  type: 'warning',
  confirmButtonText: '删除',
  cancelButtonText: '取消',
  confirmButtonClass: 'el-button--danger',
});

// 跨页面的异步结果才用 Notification，并带 title
ElNotification({ title: '导出完成', message: '文件已发送到你的邮箱', type: 'success', position: 'bottom-right' });
```

- `toast.info/warn/error` 已内置 3/4/5 秒时长、顶部 24px、可关闭、同类合并；业务层不要直接调 `ElMessage`。
- 三种颜色来自 `ds-toast--info/--warn/--error` + Element Plus 主题变量，暗色自动适配。
- 字段错误用 `el-form-item`，破坏操作确认用 `ElMessageBox` / `el-popconfirm`，不要用 toast 代替。
- 命令式 API 返回 Promise；取消时 `ElMessageBox.confirm` 抛 `'cancel'` / `'close'`，用 `try/catch` 或 `.catch(() => {})`。
- 不要在 `ElMessageBox` 的 message 里拼未转义的 HTML；需要富文本用 `dangerouslyUseHTMLString` 前先消毒。
- 全局错误在 axios 拦截器里统一 `toast.error`，业务层不要重复弹。

## 10. 组合式函数

`../assets/vue/use-crud.js` 提供三个：

| 函数 | 返回 | 说明 |
|---|---|---|
| `usePagination({ page, pageSize })` | `currentPage, pageSize, total, offset, resetPage` | 分页状态与偏移量 |
| `useDialogForm(initial)` | `visible, mode, form, open, close` | 新建/编辑共用一个表单 |
| `useTableQuery({ pageSize })` | `loading, error, rows, currentPage, pageSize, total, load` | 加载、分页、错误收集 |

使用规则：

- **解构返回的 ref**；整体放进普通对象会失去模板自动解包（见第 7 节）。
- 需要保留响应式对象时用 `reactive({ ...toRefs(state) })` 或 `storeToRefs`。
- `useDialogForm` 的 `form` 是 `reactive`；`open(payload)` 会先重置到 `initial` 再浅合并 `payload`，嵌套对象需自行深拷贝。
- `load` 内的 fetcher 返回 `{ rows, total }` 或 `{ items, total }`；失败时写入 `error` 并保留旧数据，不抛出；页面用 `error` 渲染错误态。
- 组合式函数只做状态与流程，不发请求、不弹提示；请求和提示留在页面层。

## 11. 异步数据

```vue
<script setup>
import { onMounted, ref } from 'vue';

const loading = ref(false);
const items = ref([]);

onMounted(async () => {
  loading.value = true;
  try {
    items.value = await api.list();
  } finally {
    loading.value = false;
  }
});
</script>
```

- 首屏数据用 `onMounted` 或路由守卫；`<script setup>` 顶层 `await` 需要 `<Suspense>`，Element Plus 弹层在 Suspense 下可能延后挂载，非必要不用。
- 请求竞态用 `AbortController` 或请求序号丢弃旧响应；不要只靠 `loading`。
- 空态、错误态、加载态三态都要有；`el-table` 用 `#empty` 插槽，页面级用 `el-result`。

## 12. 常见错误

| 错误 | 表现 | 修正 |
|---|---|---|
| `el-radio` / `el-checkbox` 用 `label` 当值 | 值与显示错位（2.6+ `label` 只是文本） | 值用 `value` |
| `el-pagination` 只绑 `:current-page` | 点页码不回写 | `v-model:current-page` |
| `el-table` 写 `v-model` | 无效 | `:data` |
| `el-tree` 写 `v-model` | 无效 | `default-checked-keys` + ref |
| `el-date-picker` 期望字符串 | 拿到 `Date` | `value-format="YYYY-MM-DD"` |
| `useDialogForm()` 整体绑 `v-model` | 弹窗不显示 / 类型警告 | 解构 `visible` |
| `el-form` 缺 `:model` | 校验静默失效 | 补 `:model` |
| `prop` 写成 `"user.name"` 但 model 是 `form` | 校验不生效 | `prop` 相对 `model` 路径 |
| 直接改 `props` | Vue 警告，父状态不同步 | `defineModel` / emit |
| `reactive` 解构后使用 | 失去响应性 | `toRefs` / 直接用对象 |
| 在模板里 `@input="v = $event"` | 自定义元素/事件对象类型错 | 用 `v-model` |
| 命令式 API 未引样式（按需） | 消息无样式 | 显式引 `style/css` |
| 业务层直接调 `ElMessage` | 颜色/时长各写一套 | 统一 `toast.info/warn/error` |
| 分页用 `margin-left: auto` | 父级布局一变就失效 | `.ds-data-grid__pager` |
| 直接写 `<el-dialog>` | 点击遮罩误关闭 | `AppDialog` 或显式 `:close-on-click-modal="false"` |
| 整页扁平、无卡片 | 区块边界丢失 | `el-card shadow="never"` / `.ds-card` |
| 页面切换无过渡或 `transition: all` | 生硬 / 抖动 | `ds-page` 等过渡类 |
