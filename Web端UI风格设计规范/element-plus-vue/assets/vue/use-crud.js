import { computed, reactive, ref } from 'vue';

/** 分页状态：直接绑定 el-pagination 的 v-model:current-page / v-model:page-size。 */
export function usePagination({ page = 1, pageSize = 20 } = {}) {
  const currentPage = ref(page);
  const size = ref(pageSize);
  const total = ref(0);
  const offset = computed(() => (currentPage.value - 1) * size.value);

  function resetPage() {
    currentPage.value = 1;
  }

  return { currentPage, pageSize: size, total, offset, resetPage };
}

/** 弹窗表单：visible 直接给 el-dialog / el-drawer 的 v-model。 */
export function useDialogForm(initial = {}) {
  const visible = ref(false);
  const mode = ref('create');
  const form = reactive({ ...initial });

  function open(payload = null) {
    mode.value = payload ? 'edit' : 'create';
    Object.assign(form, initial, payload ?? {});
    visible.value = true;
  }

  function close() {
    visible.value = false;
  }

  return { visible, mode, form, open, close };
}

/**
 * 简单列表查询：load(fetcher, query) 约定 fetcher 返回 { rows | items, total }。
 * 复杂项目请换成 TanStack Query / Pinia，不要在这里堆业务。
 */
export function useTableQuery({ pageSize = 20 } = {}) {
  const loading = ref(false);
  const error = ref(null);
  const rows = ref([]);
  const pagination = usePagination({ pageSize });

  async function load(fetcher, query = {}) {
    loading.value = true;
    error.value = null;
    try {
      const result = await fetcher({
        ...query,
        page: pagination.currentPage.value,
        pageSize: pagination.pageSize.value,
      });
      rows.value = result.rows ?? result.items ?? [];
      pagination.total.value = result.total ?? rows.value.length;
    } catch (e) {
      error.value = e;
    } finally {
      loading.value = false;
    }
  }

  return { loading, error, rows, ...pagination, load };
}
