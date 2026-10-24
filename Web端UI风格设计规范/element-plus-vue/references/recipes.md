# 常用页面配方

所有配方默认已加载 Element Plus、`design-system.css` 与 `use-crud.js`。代码只展示结构与状态流，请求函数 `api.*` 由项目实现。

## 1. 管理列表（查询 + 表格 + 分页 + 弹窗编辑）

```vue
<script setup>
import { onMounted, reactive, ref, watch } from 'vue';
import { ElMessageBox } from 'element-plus';
import { Plus } from '@element-plus/icons-vue';
import AppDialog from '@/components/AppDialog.vue';
import { toast } from '@/composables/use-toast.js';
import { useDialogForm, useTableQuery } from '@/composables/use-crud.js';

const { rows, loading, error, currentPage, pageSize, total, load } = useTableQuery({ pageSize: 20 });
const { visible, mode, form, open, close } = useDialogForm({ id: null, name: '', status: 'active' });
const formRef = ref();
const filters = reactive({ keyword: '', status: '' });
const selected = ref([]);

const fetchPage = (params) => api.orders.list(params);

function query() {
  if (currentPage.value !== 1) currentPage.value = 1;
  else load(fetchPage, filters);
}

watch([currentPage, pageSize], () => load(fetchPage, filters));
onMounted(() => load(fetchPage, filters));

function openCreate() {
  open();
}

function openEdit(row) {
  open(row);
}

async function submit() {
  const valid = await formRef.value.validate().catch(() => false);
  if (!valid) return;
  if (mode.value === 'edit') await api.orders.update(form.id, form);
  else await api.orders.create(form);
  toast.info('已保存');
  close();
  load(fetchPage, filters);
}

async function remove(row) {
  try {
    await ElMessageBox.confirm(`删除「${row.name}」后不可恢复，确定继续？`, '删除确认', {
      type: 'warning',
      confirmButtonText: '删除',
      cancelButtonText: '取消',
    });
  } catch {
    return; // 用户取消
  }
  await api.orders.remove(row.id);
  toast.info('已删除');
  load(fetchPage, filters);
}

async function batchRemove() {
  try {
    await ElMessageBox.confirm(`将删除选中的 ${selected.value.length} 项，确定继续？`, '批量删除', {
      type: 'warning',
      confirmButtonText: '删除',
      cancelButtonText: '取消',
    });
  } catch {
    return;
  }
  await api.orders.removeMany(selected.value.map((r) => r.id));
  selected.value = [];
  toast.info('已删除');
  load(fetchPage, filters);
}
</script>

<template>
  <div class="ds-page">
    <header class="page-head">
      <div>
        <h1 class="ds-h1">订单</h1>
        <p class="ds-caption">共 {{ total }} 条</p>
      </div>
      <el-button type="primary" :icon="Plus" @click="openCreate">新建订单</el-button>
    </header>

    <el-card shadow="never">
      <div class="ds-data-grid">
        <div class="toolbar">
          <div class="ds-row">
            <el-input v-model="filters.keyword" placeholder="搜索订单号 / 客户" clearable style="width: 240px" @keyup.enter="query" />
            <el-select v-model="filters.status" placeholder="状态" clearable style="width: 140px">
              <el-option label="启用" value="active" />
              <el-option label="停用" value="inactive" />
            </el-select>
            <el-button type="primary" @click="query">查询</el-button>
            <el-button @click="Object.assign(filters, { keyword: '', status: '' }); query()">重置</el-button>
          </div>
          <el-button v-if="selected.length" type="danger" plain @click="batchRemove">删除 {{ selected.length }} 项</el-button>
        </div>

        <el-alert v-if="error" :title="error.message ?? '加载失败，请重试'" type="error" show-icon :closable="false" />

        <el-table v-loading="loading" :data="rows" row-key="id" stripe @selection-change="selected = $event">
          <el-table-column type="selection" width="48" />
          <el-table-column prop="name" label="名称" min-width="180" show-overflow-tooltip />
          <el-table-column prop="status" label="状态" width="100">
            <template #default="{ row }">
              <el-tag :type="row.status === 'active' ? 'success' : 'info'" effect="plain">
                {{ row.status === 'active' ? '启用' : '停用' }}
              </el-tag>
            </template>
          </el-table-column>
          <el-table-column label="操作" width="140" fixed="right">
            <template #default="{ row }">
              <el-button link type="primary" @click="openEdit(row)">编辑</el-button>
              <el-button link type="danger" @click="remove(row)">删除</el-button>
            </template>
          </el-table-column>
          <template #empty><el-empty description="没有符合条件的订单" /></template>
        </el-table>

        <el-pagination v-model:current-page="currentPage" v-model:page-size="pageSize" :total="total"
          :page-sizes="[10, 20, 50, 100]" layout="total, sizes, prev, pager, next, jumper"
          background class="ds-data-grid__pager" />
      </div>
    </el-card>

    <AppDialog v-model="visible" :title="mode === 'edit' ? '编辑订单' : '新建订单'" width="560px" destroy-on-close>
      <el-form ref="formRef" :model="form" label-width="96px">
        <el-form-item label="名称" prop="name" :rules="[{ required: true, message: '请填写名称', trigger: 'blur' }]">
          <el-input v-model="form.name" maxlength="64" show-word-limit />
        </el-form-item>
        <el-form-item label="状态" prop="status" :rules="[{ required: true, message: '请选择状态', trigger: 'change' }]">
          <el-radio-group v-model="form.status">
            <el-radio value="active">启用</el-radio>
            <el-radio value="inactive">停用</el-radio>
          </el-radio-group>
        </el-form-item>
      </el-form>
      <template #footer>
        <div class="ds-actions">
          <el-button @click="close">取消</el-button>
          <el-button type="primary" @click="submit">保存</el-button>
        </div>
      </template>
    </AppDialog>
  </div>
</template>

<style scoped>
.page-head { display: flex; align-items: flex-start; justify-content: space-between; gap: var(--space-4); }
.toolbar { display: flex; align-items: center; justify-content: space-between; gap: var(--space-4); }
</style>
```

要点：

- 查询时把页码重置为 1，否则会停在越界页。
- 删除先 `confirm`，取消时 `catch` 直接返回；不要用 `if (await confirm())` 的假值判断（取消会 reject）。
- 保存后重新拉当前页；新增后若想回到第一页，显式 `currentPage.value = 1`。
- 批量操作在选中后才出现，避免工具栏常驻两个危险按钮。
- 弹窗用 `AppDialog`（默认禁止点击遮罩关闭）；`destroy-on-close` 配合 `useDialogForm` 的重置，避免残留校验态。
- 工具栏、表格、分页同属 `.ds-data-grid`，分页 `ds-data-grid__pager` 固定在右下角。

## 2. 设置表单（分组 + 脏检查 + 离开确认）

```vue
<script setup>
import { computed, reactive, ref } from 'vue';
import { onBeforeRouteLeave } from 'vue-router';
import { toast } from '@/composables/use-toast.js';

const formRef = ref();
const saved = ref(null);
const form = reactive({
  siteName: 'Acme',
  supportEmail: '',
  notify: { email: true, sms: false },
  retentionDays: 30,
});

const dirty = computed(() => saved.value && JSON.stringify(form) !== JSON.stringify(saved.value));
saved.value = JSON.parse(JSON.stringify(form));

const rules = {
  siteName: [{ required: true, message: '请填写站点名称', trigger: 'blur' }],
  supportEmail: [{ type: 'email', message: '邮箱格式不正确', trigger: 'blur' }],
};

async function save() {
  const valid = await formRef.value.validate().catch(() => false);
  if (!valid) return;
  await api.settings.save(form);
  saved.value = JSON.parse(JSON.stringify(form));
  toast.info('设置已保存');
}

onBeforeRouteLeave(() => {
  if (!dirty.value) return true;
  return window.confirm('有未保存的修改，确定离开？');
});
</script>

<template>
  <div class="ds-page ds-page--reading">
    <h1 class="ds-h1">设置</h1>

    <el-form ref="formRef" :model="form" :rules="rules" label-width="120px">
      <div class="ds-stack">
        <el-card shadow="never">
          <template #header><div class="ds-h3">基础信息</div></template>
          <el-form-item label="站点名称" prop="siteName">
            <el-input v-model="form.siteName" />
          </el-form-item>
          <el-form-item label="支持邮箱" prop="supportEmail">
            <el-input v-model="form.supportEmail" placeholder="support@example.com" />
          </el-form-item>
        </el-card>

        <el-card shadow="never">
          <template #header><div class="ds-h3">通知</div></template>
          <el-form-item label="邮件通知"><el-switch v-model="form.notify.email" /></el-form-item>
          <el-form-item label="短信通知"><el-switch v-model="form.notify.sms" /></el-form-item>
        </el-card>

        <el-card shadow="never">
          <template #header><div class="ds-h3">数据保留</div></template>
          <el-form-item label="保留天数">
            <el-input-number v-model="form.retentionDays" :min="7" :max="365" />
          </el-form-item>
        </el-card>
      </div>
    </el-form>

    <div class="ds-actions">
      <el-button @click="formRef.resetFields()">重置</el-button>
      <el-button type="primary" :disabled="!dirty" @click="save">保存</el-button>
    </div>
  </div>
</template>
```

要点：

- `saved` 用深拷贝快照；`dirty` 用结构化比较，不要逐字段手写。
- 保存按钮在无修改时禁用，避免无意义请求。
- 离开确认用路由守卫；浏览器 `window.confirm` 只作为最小实现，产品内可换 `ElMessageBox.confirm`。
- 每个分组一张 Card，Card 之间 16；页面区块间距 24。

## 3. 登录页

```vue
<script setup>
import { reactive, ref } from 'vue';
import { useRouter } from 'vue-router';
import { toast } from '@/composables/use-toast.js';

const router = useRouter();
const formRef = ref();
const loading = ref(false);
const form = reactive({ email: '', password: '', remember: true });

const rules = {
  email: [
    { required: true, message: '请输入邮箱', trigger: 'blur' },
    { type: 'email', message: '邮箱格式不正确', trigger: 'blur' },
  ],
  password: [{ required: true, min: 8, message: '密码至少 8 位', trigger: 'blur' }],
};

async function login() {
  const valid = await formRef.value.validate().catch(() => false);
  if (!valid) return;
  loading.value = true;
  try {
    await api.auth.login(form);
    router.replace('/dashboard');
  } catch (e) {
    toast.error(e.message ?? '登录失败');
  } finally {
    loading.value = false;
  }
}
</script>

<template>
  <div class="auth">
    <div class="auth__panel">
      <div>
        <h1 class="ds-display">登录 Acme</h1>
        <p class="ds-body ds-muted">使用工作邮箱继续</p>
      </div>

      <el-form ref="formRef" :model="form" :rules="rules" label-position="top" @submit.prevent="login">
        <el-form-item label="邮箱" prop="email">
          <el-input v-model="form.email" type="email" size="large" autocomplete="username" />
        </el-form-item>
        <el-form-item label="密码" prop="password">
          <el-input v-model="form.password" type="password" size="large" show-password autocomplete="current-password" />
        </el-form-item>
        <div class="auth__row">
          <el-checkbox v-model="form.remember">记住我</el-checkbox>
          <el-link type="primary" :underline="false" href="/reset">忘记密码</el-link>
        </div>
        <el-button type="primary" size="large" native-type="submit" :loading="loading" class="auth__submit">登录</el-button>
      </el-form>
    </div>
  </div>
</template>

<style scoped>
.auth { min-height: 100vh; display: grid; place-items: center; padding: var(--space-4); background: var(--el-bg-color-page); }
.auth__panel { width: 100%; max-width: 400px; display: grid; gap: var(--space-6); padding: var(--space-8); background: var(--el-bg-color); border-radius: var(--radius-lg); }
.auth__row { display: flex; align-items: center; justify-content: space-between; }
.auth__submit { width: 100%; margin-top: var(--space-4); }
</style>
```

要点：登录页用 `size="large"`（40px），并配合 `.ds-touch` 可到 48；不要用 32px 控件做触屏主操作。

## 4. 仪表盘指标

```vue
<script setup>
import { onMounted, ref } from 'vue';

const loading = ref(true);
const metrics = ref([]);
const trend = ref([]);

onMounted(async () => {
  try {
    const data = await api.dashboard.summary();
    metrics.value = data.metrics;
    trend.value = data.trend;
  } finally {
    loading.value = false;
  }
});
</script>

<template>
  <div class="ds-page ds-page--wide">
    <div class="metric-grid">
      <el-card v-for="m in metrics" :key="m.key" shadow="never">
        <el-skeleton :loading="loading" animated :rows="2">
          <el-statistic :title="m.label" :value="m.value" :precision="m.precision ?? 0">
            <template #prefix v-if="m.prefix">{{ m.prefix }}</template>
            <template #suffix v-if="m.suffix">{{ m.suffix }}</template>
          </el-statistic>
        </el-skeleton>
      </el-card>
    </div>

    <el-card shadow="never">
      <template #header><div class="ds-h3">趋势</div></template>
      <el-skeleton :loading="loading" animated :rows="8">
        <TrendChart :data="trend" />
      </el-skeleton>
    </el-card>
  </div>
</template>

<style scoped>
.metric-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(200px, 1fr)); gap: var(--space-4); }
</style>
```

要点：

- 指标卡 `minmax(200px, 1fr)`；数字用 `tabular-nums`。
- 加载用 `el-skeleton` 保持布局高度，避免跳动。
- 图表容器必须有明确高度（≥320），否则空数据时塌陷。

## 5. 空态、加载态、错误态

```vue
<template>
  <el-card shadow="never">
    <el-skeleton v-if="loading" animated :rows="6" />

    <el-result v-else-if="error" icon="error" title="加载失败" :sub-title="error.message">
      <template #extra><el-button type="primary" @click="retry">重试</el-button></template>
    </el-result>

    <el-empty v-else-if="!rows.length" description="还没有数据">
      <el-button type="primary" @click="create">创建第一条</el-button>
    </el-empty>

    <OrderTable v-else :rows="rows" />
  </el-card>
</template>
```

三态互斥且顺序固定：加载 → 错误 → 空 → 内容。不要同时显示“空态 + 分页”。

## 6. 头像上传

```vue
<script setup>
import { ref } from 'vue';
import { toast } from '@/composables/use-toast.js';

const fileList = ref([]);
const avatar = ref('');

async function upload({ file }) {
  const form = new FormData();
  form.append('file', file);
  const { url } = await api.upload.avatar(form);
  avatar.value = url;
}

function beforeUpload(file) {
  const isImage = file.type.startsWith('image/');
  const small = file.size / 1024 / 1024 <= 2;
  if (!isImage) toast.error('只能上传图片');
  else if (!small) toast.error('图片不超过 2MB');
  return isImage && small;
}
</script>

<template>
  <el-upload :show-file-list="false" :http-request="upload" :before-upload="beforeUpload" accept="image/*">
    <el-avatar :size="72" :src="avatar" data-g2-exempt />
  </el-upload>
</template>
```

要点：

- 头像只接受图片、限制 2MB；失败用 `toast.error`。
- `:http-request` 完全接管请求，避免 `action` 写死。
- 上传成功后立刻回显；不要把本地 blob URL 当成最终地址。

## 7. 筛选条件同步到 URL

```js
import { reactive, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';

const route = useRoute();
const router = useRouter();
const filters = reactive({
  keyword: route.query.q ?? '',
  status: route.query.status ?? '',
  page: Number(route.query.page ?? 1),
});

watch(filters, () => {
  router.replace({ query: {
    ...(filters.keyword ? { q: filters.keyword } : {}),
    ...(filters.status ? { status: filters.status } : {}),
    ...(filters.page > 1 ? { page: filters.page } : {}),
  } });
}, { deep: true });
```

要点：

- 空值从 query 中删除，保持 URL 干净。
- 用 `replace` 而不是 `push`，避免每次输入都堆历史。
- 输入类筛选加 300ms 防抖，或只在中点“查询”时同步。

## 8. 详情页（描述 + 时间线）

```vue
<div class="ds-page ds-page--reading">
  <el-page-header content="订单 SO-20261024-001" @back="router.back()" />

  <el-card shadow="never">
    <el-descriptions :column="2" border>
      <el-descriptions-item label="客户">张三</el-descriptions-item>
      <el-descriptions-item label="金额">¥ 1,280.00</el-descriptions-item>
      <el-descriptions-item label="状态">
        <el-tag type="success" effect="plain">已完成</el-tag>
      </el-descriptions-item>
      <el-descriptions-item label="创建时间">2026-10-24 10:12</el-descriptions-item>
    </el-descriptions>
  </el-card>

  <el-card shadow="never">
    <template #header><div class="ds-h3">操作记录</div></template>
    <el-timeline>
      <el-timeline-item v-for="log in logs" :key="log.id" :timestamp="log.at" :type="log.type">
        {{ log.text }}
      </el-timeline-item>
    </el-timeline>
  </el-card>
</div>
```

## 9. 配方通用检查

1. 查询条件变化时页码回到 1。
2. 删除/危险操作必须二次确认；确认文案写清后果。
3. 保存成功后刷新数据源，而不是在本地拼一条假记录。
4. 所有异步动作有 loading；按钮在提交期间禁用或 loading。
5. 表单错误落在字段上，不用 toast 顶替。
6. 空态给出下一步动作，不只写“暂无数据”。
7. 列表与详情都支持浏览器返回；筛选条件尽量进 URL。
8. 触屏主操作 ≥48 高；桌面表格内操作可用 link 按钮。
9. 顶部反馈统一 `toast.info/warn/error`，不直接调用 `ElMessage`。
10. 工具栏、表格、分页同属 `.ds-data-grid`，分页固定在右下角。
11. Modal 用 `AppDialog`，默认点击遮罩不关闭。
12. 页面一级区块用卡片分组，卡片间用父级 gap；路由/面板切换有统一过渡。
