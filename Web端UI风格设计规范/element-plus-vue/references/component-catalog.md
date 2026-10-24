# 组件目录（Element Plus 2.14.7）

按“什么时候用、怎么写、关键 API”组织。模板默认已全量注册；按需导入时组件名在脚本里是 `El*`，模板里是 `el-*`。

约定：所有示例的间距都应来自 `--space-*`；示例里写 `class="ds-*"` 的地方直接复用 `../assets/styles/design-system.css`。

## 1. 基础

### Button `el-button`

```vue
<el-button>默认</el-button>
<el-button type="primary">主要</el-button>
<el-button type="primary" plain>次要</el-button>
<el-button type="danger" plain>危险</el-button>
<el-button text>文字</el-button>
<el-button link type="primary">链接</el-button>
<el-button type="primary" :loading="saving" :icon="Check">保存</el-button>
<el-button circle :icon="Plus" aria-label="新增" data-g2-exempt />
```

- `type`：`primary / success / warning / danger / info`；`plain` 降饱和；`text` 无边框；`link` 像链接。
- `native-type` 默认 `button`；表单提交必须显式 `native-type="submit"`。
- 同一操作区只有一个 `primary`；最多一个 `danger`。
- 纯图标按钮必须 `circle` + `aria-label` + `data-g2-exempt`。

### Button Group `el-button-group`

```vue
<el-button-group>
  <el-button :icon="Back" aria-label="后退" />
  <el-button :icon="Right" aria-label="前进" />
</el-button-group>
```

### Icon `el-icon`

```vue
<script setup>
import { Search, Plus } from '@element-plus/icons-vue';
</script>

<template>
  <el-icon :size="16" color="var(--el-text-color-secondary)"><Search /></el-icon>
  <el-button :icon="Plus">新增</el-button>
</template>
```

- 图标组件来自 `@element-plus/icons-vue`，不要手写 SVG 路径。
- 图标与文字间距 4–8；图标尺寸 14/16/20。
- 装饰性图标加 `aria-hidden="true"`；可点击图标按钮必须有 `aria-label`。

### Link `el-link`

```vue
<el-link type="primary" href="/orders" :underline="false">查看订单</el-link>
<el-link type="danger" :icon="Delete" @click="remove">删除</el-link>
```

### Text `el-text`

```vue
<el-text size="small" type="info">更新于 2026-10-24</el-text>
<el-text truncated style="max-width: 240px">很长的单行文本…</el-text>
<el-text tag="p" line-clamp="2">最多两行，超出省略。</el-text>
```

### Space `el-space`

```vue
<el-space :size="8" wrap>
  <el-tag>标签一</el-tag>
  <el-tag>标签二</el-tag>
</el-space>
```

页面级排版优先 `.ds-row` / `.ds-stack`；`el-space` 适合局部行内元素。

### Divider `el-divider`

```vue
<el-divider />
<el-divider content-position="left">高级设置</el-divider>
```

### Row / Col `el-row` / `el-col`

```vue
<el-row :gutter="16">
  <el-col :xs="24" :sm="12" :md="8"><el-card>指标 A</el-card></el-col>
  <el-col :xs="24" :sm="12" :md="8"><el-card>指标 B</el-card></el-col>
  <el-col :xs="24" :sm="24" :md="8"><el-card>指标 C</el-card></el-col>
</el-row>
```

- 24 栅格；`gutter` 只允许 8/16/24。
- 只用于行内分栏，不用于页面骨架。

### Container `el-container` 系

```vue
<el-container>
  <el-aside width="240px"><AppMenu /></el-aside>
  <el-container>
    <el-header height="64px"><AppHeader /></el-header>
    <el-main><router-view /></el-main>
  </el-container>
</el-container>
```

应用外壳的完整做法见 `navigation-and-layout.md`。

### Scrollbar `el-scrollbar`

```vue
<el-scrollbar height="320px">
  <div class="ds-stack">…</div>
</el-scrollbar>
```

- 固定高度必须给 `height`；内容高度用 `max-height`。
- 原生滚动更符合平台习惯时用 `native`。

### Affix `el-affix`

```vue
<el-affix :offset="16" target=".ds-page">
  <el-button type="primary">保存</el-button>
</el-affix>
```

### Backtop `el-backtop`

```vue
<el-backtop :right="24" :bottom="24" />
```

## 2. 表单输入

### Input `el-input`

```vue
<el-input v-model="form.name" placeholder="请输入名称" clearable />
<el-input v-model="form.email" type="email" :prefix-icon="Message" />
<el-input v-model="form.code" maxlength="6" show-word-limit />
<el-input v-model="form.note" type="textarea" :rows="4" autosize />
<el-input v-model="form.password" type="password" show-password />
```

- 插槽：`#prefix` / `#suffix` / `#prepend` / `#append`。
- 数字输入用 `el-input-number`，不要用 `el-input type="number"`。
- 受控输入不要手写 `@input`；错误提示放 `el-form-item`。
- 搜索框配 `clearable` + 回车提交；不要用 `v-model.lazy` 代替显式查询按钮。

### Input Number `el-input-number`

```vue
<el-input-number v-model="form.quantity" :min="1" :max="999" :step="1" controls-position="right" />
```

`v-model` 清空时为 `null`，校验规则要允许 `null` 或加 `required`。

### Select `el-select`

```vue
<el-select v-model="form.plan" placeholder="选择方案" clearable style="width: 200px">
  <el-option label="Free" value="free" />
  <el-option label="Pro" value="pro" disabled />
</el-select>

<el-select v-model="form.tags" multiple collapse-tags collapse-tags-tooltip filterable>
  <el-option v-for="t in tags" :key="t.value" :label="t.label" :value="t.value" />
</el-select>
```

- `el-option` 的值是 `value`，显示是 `label`；不要只写 `label` 当值。
- 多选 `v-model` 必须是数组；`multiple-limit` 限制数量。
- 大数据量（>1000）用 `el-select-v2`：

```vue
<el-select-v2 v-model="form.userId" :options="userOptions" filterable :height="240" />
```

`options` 形如 `[{ label, value }]`；可用 `props` 改名。

### Autocomplete `el-autocomplete`

```vue
<el-autocomplete
  v-model="form.keyword"
  :fetch-suggestions="querySearch"
  :trigger-on-focus="false"
  clearable
  @select="({ value }) => (form.keyword = value)"
/>
```

`fetch-suggestions(queryString, cb)` 必须调用 `cb(list)`；异步请求注意竞态。

### Checkbox / Radio

```vue
<el-checkbox v-model="form.agree">同意条款</el-checkbox>
<el-checkbox v-model="form.all" :indeterminate="form.some" @change="toggleAll">全选</el-checkbox>

<el-checkbox-group v-model="form.tags">
  <el-checkbox value="a" label="标签 A" />
  <el-checkbox value="b" label="标签 B" />
</el-checkbox-group>

<el-radio-group v-model="form.size">
  <el-radio value="s">小</el-radio>
  <el-radio value="m">中</el-radio>
  <el-radio-button value="l">大</el-radio-button>
</el-radio-group>
```

- **值用 `value`**；`label` 从 2.6 起只表示显示文本（旧版本里 `label` 兼作值，迁移时必查）。
- 组内 `v-model` 是单值（radio）或数组（checkbox）。
- 竖排时用 `.ds-stack`；横排间隔 16。

### Switch `el-switch`

```vue
<el-switch v-model="form.enabled" />
<el-switch v-model="form.status" active-value="on" inactive-value="off" inline-prompt active-text="开" inactive-text="关" />
```

- 开关必须立即生效；需要“保存”才生效的场景改用 checkbox 或分段控件。
- 异步切换用 `before-change` 返回 Promise，失败时开关自动回弹。

### Slider `el-slider`

```vue
<el-slider v-model="form.volume" :min="0" :max="100" :step="5" show-stops />
<el-slider v-model="form.range" range :min="0" :max="100" />
<el-slider v-model="form.price" :min="0" :max="1000" show-input />
```

### Date / Time

```vue
<el-date-picker v-model="form.date" type="date" value-format="YYYY-MM-DD" placeholder="选择日期" />
<el-date-picker v-model="form.range" type="daterange" value-format="YYYY-MM-DD"
  start-placeholder="开始" end-placeholder="结束" />
<el-date-picker v-model="form.month" type="month" value-format="YYYY-MM" />
<el-time-picker v-model="form.time" value-format="HH:mm:ss" placeholder="选择时间" />
<el-time-select v-model="form.slot" start="08:00" step="00:30" end="18:00" />
```

- 表单提交字符串时**必须** `value-format`；否则拿到 `Date` 对象。
- 范围选择 `v-model` 是数组；清空为 `null`。
- 日期面板是 teleport 到 body 的 popper，宽度按内容；不要给它设固定宽。

### Cascader `el-cascader`

```vue
<el-cascader
  v-model="form.region"
  :options="regions"
  :props="{ value: 'id', label: 'name', children: 'children', emitPath: false }"
  clearable
  filterable
/>
```

`emitPath: false` 时 `v-model` 是叶子值；默认是完整路径数组。

### Color Picker `el-color-picker`

```vue
<el-color-picker v-model="form.brand" show-alpha :predefine="['#409EFF', '#67C23A', '#E6A23C']" />
```

### Transfer `el-transfer`

```vue
<el-transfer v-model="form.selected" :data="allUsers" :titles="['可选', '已选']"
  :props="{ key: 'id', label: 'name' }" filterable />
```

### Rate `el-rate`

```vue
<el-rate v-model="form.score" :max="5" allow-half show-text />
```

### Mention `el-mention`

```vue
<el-mention v-model="form.comment" :options="members" prefix="@" :split="' '" />
```

### Upload `el-upload`

```vue
<el-upload v-model:file-list="files" action="/api/upload" :limit="5" :on-exceed="onExceed">
  <el-button :icon="Upload">上传</el-button>
  <template #tip><div class="ds-caption">单个文件不超过 10MB</div></template>
</el-upload>
```

拖拽：`:drag="true"` + `el-upload-dragger`；图片墙：`list-type="picture-card"`；自定义请求：`:http-request`。

### Form `el-form` / `el-form-item`

见 `vue-patterns.md` 第 5 节。补充：

- `label-width` 桌面 96px；`label-position="top"` 用于窄屏或长标签。
- `status-icon` 在输入框后显示校验图标；不要同时再写自定义错误图标。
- `scroll-to-error` 在长表单提交失败时滚动到首个错误。
- 局部校验 `formRef.validateField('email')`；重置 `resetFields()`；清除校验 `clearValidate()`。

### TreeSelect `el-tree-select`

```vue
<el-tree-select v-model="form.deptId" :data="deptTree" check-strictly
  :props="{ label: 'name', value: 'id', children: 'children' }" filterable clearable />
```

`check-strictly` 关闭父子联动；需要多选加 `multiple` + `show-checkbox`。

## 3. 数据展示

### Table `el-table`

```vue
<el-table :data="rows" row-key="id" stripe border height="520"
  @selection-change="onSelect" @sort-change="onSort">
  <el-table-column type="selection" width="48" />
  <el-table-column type="index" label="#" width="56" />
  <el-table-column prop="name" label="名称" min-width="160" show-overflow-tooltip />
  <el-table-column prop="amount" label="金额" width="120" align="right" sortable="custom">
    <template #default="{ row }">{{ money(row.amount) }}</template>
  </el-table-column>
  <el-table-column label="状态" width="100">
    <template #default="{ row }">
      <el-tag :type="row.active ? 'success' : 'info'">{{ row.active ? '启用' : '停用' }}</el-tag>
    </template>
  </el-table-column>
  <el-table-column label="操作" width="140" fixed="right">
    <template #default="{ row }">
      <el-button link type="primary" @click="edit(row)">编辑</el-button>
    </template>
  </el-table-column>
  <template #empty><el-empty description="暂无数据" /></template>
</el-table>
```

- `:data` 单向；`row-key` 用于展开/选择/树形。
- 树形数据：`row-key` + `:tree-props="{ children: 'children' }"`；数据必须是嵌套结构，**不要**给带 `children` 的扁平列表直接开树形（会重复行）。
- 展开行：`type="expand"` + `#default` 插槽。
- 合并单元格：`span-method`。
- 大数据量用 `el-table-v2`（虚拟滚动，必须给数值 `:width` `:height`）：

```vue
<el-table-v2 :columns="columns" :data="rows" :width="960" :height="560" :row-height="40" fixed />
```

`columns` 用 `{ key, dataKey, title, width, cellRenderer }`；`cellRenderer` 返回 VNode（`h()` 或 JSX）。

### Pagination `el-pagination`

```vue
<el-pagination v-model:current-page="page" v-model:page-size="size" :total="total"
  :page-sizes="[10, 20, 50]" layout="total, sizes, prev, pager, next, jumper"
  background class="ds-data-grid__pager" />
```

- 两个 `v-model` 缺一不可。
- **默认放在数据 Grid 的右下角**：外层 `.ds-data-grid`，分页加 `ds-data-grid__pager`（`justify-self: end`）；不要 `margin-left: auto`。
- `layout` 顺序即显示顺序；`->` 表示右推。
- 总数为 0 时隐藏用 `hide-on-single-page`（谨慎：会让布局跳动）。

### Tag `el-tag`

```vue
<el-tag>默认</el-tag>
<el-tag type="success" effect="plain">已完成</el-tag>
<el-tag type="warning" round closable @close="remove">可关闭</el-tag>
```

### Badge `el-badge`

```vue
<el-badge :value="12" :max="99"><el-button>消息</el-button></el-badge>
<el-badge is-dot type="danger"><el-avatar :size="32" /></el-badge>
```

### Avatar `el-avatar`

```vue
<el-avatar :size="40" :src="user.avatar" />
<el-avatar :size="40">{{ user.name[0] }}</el-avatar>
```

Element Plus 的圆形头像已自动豁免 G2；自建圆形元素加 `data-g2-exempt`（见 `corner-g2.md`）。

### Progress `el-progress`

```vue
<el-progress :percentage="progress" :stroke-width="8" />
<el-progress type="circle" :percentage="progress" :width="96" />
<el-progress :percentage="progress" status="success" />
```

### Tree `el-tree`

```vue
<script setup>
const treeRef = ref();
const initialChecked = ref([1, 2]); // 仅初始值
const checked = ref([]);            // 当前勾选结果
function save() {
  checked.value = treeRef.value.getCheckedKeys();
}
</script>

<template>
  <el-tree ref="treeRef" :data="tree" node-key="id" show-checkbox default-expand-all
    :props="{ label: 'name', children: 'children' }"
    :default-checked-keys="initialChecked" @check="save" />
</template>
```

- 勾选态不是 `v-model`；用 ref 方法读写。
- 父子联动默认开启；`check-strictly` 关闭。
- 过滤用 `filter-node-method` + `treeRef.value.filter(keyword)`。

### Descriptions `el-descriptions`

```vue
<el-descriptions :column="2" border title="订单信息">
  <el-descriptions-item label="订单号">SO-20261024-001</el-descriptions-item>
  <el-descriptions-item label="金额">¥ 1,280.00</el-descriptions-item>
  <el-descriptions-item label="备注" :span="2">—</el-descriptions-item>
</el-descriptions>
```

### Statistic `el-statistic`

```vue
<el-statistic title="本月收入" :value="128000" :precision="2">
  <template #prefix>¥</template>
</el-statistic>
```

### Empty / Result / Skeleton

```vue
<el-empty description="暂无订单">
  <el-button type="primary">新建订单</el-button>
</el-empty>

<el-result icon="success" title="提交成功" sub-title="我们会在 1 个工作日内处理">
  <template #extra><el-button type="primary">返回列表</el-button></template>
</el-result>

<el-skeleton :loading="loading" animated :rows="5">
  <template #default><OrderList :rows="rows" /></template>
</el-skeleton>
```

### Image `el-image`

```vue
<el-image :src="url" fit="cover" :preview-src-list="[url]" preview-teleported style="width: 160px; height: 120px" />
```

`preview-teleported` 防止被父级 `overflow: hidden` 裁切。

### Card `el-card`

```vue
<el-card shadow="never">
  <template #header><div class="ds-h3">订单概览</div></template>
  <p class="ds-body">内容</p>
  <template #footer><div class="ds-actions"><el-button link>详情</el-button></div></template>
</el-card>
```

- 默认内边距 16（已归一化）；不要再包 padding。
- 静置 `shadow="never"` + 描边；hover 浮起用 `shadow="hover"`。
- **卡片优先**：页面一级区块、表单、表格、图表都用卡片分组；扁平只用于卡片内部工具条。嵌套最多一层。
- 非 `el-card` 容器用 `.ds-card`（同一背景/边框/圆角）；强调态 `.ds-card--raised`。

### Carousel `el-carousel`

```vue
<el-carousel :initial-index="0" height="240px" @change="onChange">
  <el-carousel-item v-for="img in images" :key="img.id">
    <img :src="img.src" :alt="img.alt" style="width: 100%; height: 100%; object-fit: cover" />
  </el-carousel-item>
</el-carousel>
```

没有 `v-model`；需要程序化切页用 ref 的 `setActiveItem()`。

### Collapse `el-collapse`

```vue
<el-collapse v-model="openPanels">
  <el-collapse-item title="基础信息" name="basic">…</el-collapse-item>
  <el-collapse-item title="高级设置" name="advanced">…</el-collapse-item>
</el-collapse>
```

### Timeline `el-timeline`

```vue
<el-timeline>
  <el-timeline-item v-for="e in events" :key="e.id" :timestamp="e.at" :type="e.type">
    {{ e.text }}
  </el-timeline-item>
</el-timeline>
```

### Calendar `el-calendar`

```vue
<el-calendar v-model="date">
  <template #date-cell="{ data }">
    <div :class="{ 'is-busy': isBusy(data.day) }">{{ data.day.split('-')[2] }}</div>
  </template>
</el-calendar>
```

## 4. 导航

### Menu `el-menu`

```vue
<script setup>
import { useRoute } from 'vue-router';
const route = useRoute();
</script>

<template>
  <el-menu :default-active="route.path" router unique-opened>
    <el-menu-item index="/dashboard"><el-icon><HomeFilled /></el-icon><span>概览</span></el-menu-item>
    <el-sub-menu index="/orders">
      <template #title><el-icon><List /></el-icon><span>订单</span></template>
      <el-menu-item index="/orders/list">订单列表</el-menu-item>
      <el-menu-item index="/orders/refund">退款</el-menu-item>
    </el-sub-menu>
  </el-menu>
</template>
```

- `default-active` 绑定当前路由；`router` 模式按下标跳转。
- 折叠用 `collapse`；折叠后必须保留 tooltip 或图标语义。

### Tabs `el-tabs`

```vue
<el-tabs v-model="activeTab" @tab-change="loadTab">
  <el-tab-pane label="全部" name="all" />
  <el-tab-pane label="待付款" name="pending" />
  <el-tab-pane label="已完成" name="done" lazy />
</el-tabs>
```

面板内容直接写在 `el-tab-pane` 内；`lazy` 延迟渲染。

### Breadcrumb `el-breadcrumb`

```vue
<el-breadcrumb separator="/">
  <el-breadcrumb-item :to="{ path: '/' }">首页</el-breadcrumb-item>
  <el-breadcrumb-item :to="{ path: '/orders' }">订单</el-breadcrumb-item>
  <el-breadcrumb-item>详情</el-breadcrumb-item>
</el-breadcrumb>
```

### Dropdown `el-dropdown`

```vue
<el-dropdown trigger="click" @command="onCommand">
  <el-button>更多<el-icon class="el-icon--right"><ArrowDown /></el-icon></el-button>
  <template #dropdown>
    <el-dropdown-menu>
      <el-dropdown-item command="export">导出</el-dropdown-item>
      <el-dropdown-item command="archive">归档</el-dropdown-item>
      <el-dropdown-item command="delete" divided>删除</el-dropdown-item>
    </el-dropdown-menu>
  </template>
</el-dropdown>
```

### PageHeader `el-page-header`

```vue
<el-page-header title="返回" content="订单详情" @back="router.back()" />
```

### Steps `el-steps`

```vue
<el-steps :active="step" finish-status="success" align-center>
  <el-step title="填写信息" />
  <el-step title="确认" />
  <el-step title="完成" />
</el-steps>
```

### Anchor `el-anchor`

```vue
<el-anchor :offset="80" direction="vertical">
  <el-anchor-link href="#basic" title="基础信息" />
  <el-anchor-link href="#advanced" title="高级设置" />
</el-anchor>
```

### Segmented `el-segmented`

```vue
<el-segmented v-model="view" :options="['列表', '看板']" block />
```

## 5. 反馈

### Alert `el-alert`

```vue
<el-alert title="额度将在 3 天后到期" type="warning" show-icon :closable="false" />
<el-alert title="保存失败" type="error" description="请检查网络后重试" show-icon @close="dismiss" />
```

### 顶部提示 `toast`（封装 `ElMessage`）

```js
import { toast } from '@/composables/use-toast.js';

toast.info('已保存');
toast.warn('额度将在 3 天后到期');
toast.error('保存失败，请重试');
```

- 统一入口，三态样式为 `ds-toast--info / --warn / --error`，颜色走主题变量，亮/暗自动适配。
- 不要直接调用 `ElMessage`；字段错误与破坏操作确认也不要用 toast。
- 封装实现见 `../assets/vue/use-toast.js`，样式见 `../assets/styles/design-system.css`。

### MessageBox `ElMessageBox`

```js
await ElMessageBox.confirm('删除后不可恢复，确定继续？', '删除确认', {
  type: 'warning',
  confirmButtonText: '删除',
  cancelButtonText: '取消',
});
```

### Notification `ElNotification`

```js
ElNotification({ title: '导出完成', message: '已发送到邮箱', type: 'success', position: 'bottom-right' });
```

### Popconfirm `el-popconfirm`

```vue
<el-popconfirm title="确定删除？" confirm-button-text="删除" cancel-button-text="取消" @confirm="remove">
  <template #reference><el-button link type="danger">删除</el-button></template>
</el-popconfirm>
```

### Tooltip `el-tooltip`

```vue
<el-tooltip content="刷新" placement="top" :show-after="300">
  <el-button circle :icon="Refresh" aria-label="刷新" data-g2-exempt />
</el-tooltip>
```

### Popover `el-popover`

```vue
<el-popover placement="bottom-start" :width="280" trigger="click">
  <template #reference><el-button>筛选</el-button></template>
  <div class="ds-stack">…</div>
</el-popover>
```

### Dialog `AppDialog`（封装 `el-dialog`）

```vue
<script setup>
import AppDialog from '@/components/AppDialog.vue';
</script>

<template>
  <AppDialog v-model="visible" title="新建订单" width="520px" destroy-on-close>
    <OrderForm v-model="form" />
    <template #footer>
      <div class="ds-actions">
        <el-button @click="visible = false">取消</el-button>
        <el-button type="primary" @click="submit">保存</el-button>
      </div>
    </template>
  </AppDialog>
</template>
```

- **默认 `close-on-click-modal=false`**：点击遮罩不关闭；只读弹窗需显式传 `:close-on-click-modal="true"`。
- `append-to-body` 默认开启，避免被父级 `overflow: hidden` 裁切；宽度桌面 480–640，窄屏 92%。
- 原始 `el-dialog` 默认可点遮罩关闭；直接使用时必须显式 `:close-on-click-modal="false"`。
- 长表单不居中（顶部对齐更稳定）；测量/聚焦等 `@opened`；有未保存内容用 `before-close` 确认。
- 实现见 `../assets/vue/AppDialog.vue`。

### Drawer `el-drawer`

```vue
<el-drawer v-model="visible" title="筛选" direction="rtl" size="420px" :with-header="true">
  <FilterForm @apply="apply" />
</el-drawer>
```

`size` 支持 px 与百分比；窄屏用 `direction="btt"` + `size="80%"`。

### Loading `v-loading`

```vue
<div v-loading="loading" element-loading-text="加载中…">…</div>
```

全屏：`ElLoading.service({ fullscreen: true, text: '处理中…' })`，记得 `close()`。

### Tour `el-tour`

```vue
<script setup>
const open = ref(false);
const steps = [
  { target: () => document.querySelector('#create'), title: '新建', description: '从这里创建第一条记录' },
  { target: () => document.querySelector('#filters'), title: '筛选', description: '按状态与时间筛选' },
];
</script>

<template>
  <el-tour v-model="open" :steps="steps" @finish="open = false" />
</template>
```

## 6. 其他

### Splitter `el-splitter`

```vue
<el-splitter style="height: 480px" @resize="onResize">
  <el-splitter-panel size="30%" min="240px"><SideList /></el-splitter-panel>
  <el-splitter-panel><DetailPane /></el-splitter-panel>
</el-splitter>
```

`layout="vertical"` 切换上下分栏；面板可用 `v-model:size` 双向绑定，整体拖拽结果用 `@resize` / `@resize-start` / `@resize-end` / `@collapse`。

### Watermark `el-watermark`

```vue
<el-watermark content="内部资料" :font="{ fontSize: 14 }" :gap="[100, 100]" :rotate="-20">
  <router-view />
</el-watermark>
```

### ConfigProvider `el-config-provider`

```vue
<el-config-provider :locale="zhCn" :z-index="3000" size="default">
  <App />
</el-config-provider>
```

- 用于统一语言、尺寸、弹层起始 z-index。
- 主题命名空间（`namespace`）一旦修改，所有自定义 CSS 选择器都要跟着改；设计规范项目不要启用。

### Infinite Scroll `v-infinite-scroll`

```vue
<ul v-infinite-scroll="loadMore" :infinite-scroll-disabled="loading" :infinite-scroll-distance="120">
  <li v-for="item in items" :key="item.id">{{ item.name }}</li>
</ul>
```

### 选择决策表

| 需求 | 用 |
|---|---|
| 2–5 个互斥选项且都需可见 | `el-radio-group` / `el-segmented` |
| 6–20 个互斥选项 | `el-select` |
| >1000 项 | `el-select-v2` |
| 多选且选项少 | `el-checkbox-group` |
| 多选且选项多 | `el-select multiple` |
| 是/否立即生效 | `el-switch` |
| 表单里保存后生效 | `el-checkbox` |
| 层级选择 | `el-cascader` / `el-tree-select` |
| 左右分配 | `el-transfer` |
| 短提示 | `toast.info/warn/error` / `el-tooltip` |
| 需确认的破坏操作 | `ElMessageBox.confirm` / `el-popconfirm` |
| 表单编辑 | `AppDialog`（短）/ `el-drawer`（长、需保留上下文） |
| 长列表 | `el-table-v2` / `v-infinite-scroll` |
| 全屏流程 | 独立路由页，不用 Dialog |

## 7. 组件使用硬规则

1. 不用 `el-input` 拼日期/时间/颜色/数字，改用对应控件。
2. 不给 `el-table` 写 `v-model`；不给 `el-tree` 写 `v-model`。
3. `AppDialog` / `el-drawer` 的 `footer` 用 `.ds-actions`，不要手写 margin。
4. 库内正圆自动豁免；自建正圆加 `data-g2-exempt`；其余圆角交给全局 G2 规则。
5. 组件的 `style` 只写宽度/布局，不写颜色、圆角、内边距（用令牌或类）。
6. 同一屏最多一个 `primary` 按钮、一个 `danger` 按钮。
7. teleport 组件（Select 下拉、Date 面板、Dialog）不要放在 `overflow: hidden` 且无 `append-to-body` 的容器里。
8. 组件尺寸混用只允许“主表单 large、表格内 small、其余 default”；不要三个尺寸并排。
9. 顶部反馈统一 `toast.info/warn/error`；不要直接调 `ElMessage`。
10. 分页与表格同属 `.ds-data-grid`，分页固定在右下角。
11. 业务弹窗用 `AppDialog`，不要依赖 `el-dialog` 的默认遮罩关闭行为。
12. 页面一级区块用卡片分组；静态卡片用边框不用重阴影。
