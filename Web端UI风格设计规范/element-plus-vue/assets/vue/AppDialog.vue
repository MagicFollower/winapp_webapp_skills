<script setup>
import { computed } from 'vue';
import { ElDialog } from 'element-plus';

defineOptions({ inheritAttrs: false });

const props = defineProps({
  modelValue: { type: Boolean, default: false },
  width: { type: [String, Number], default: '520px' },
  closeOnClickModal: { type: Boolean, default: false },
  appendToBody: { type: Boolean, default: true },
});

const emit = defineEmits(['update:modelValue']);

const visible = computed({
  get: () => props.modelValue,
  set: (value) => emit('update:modelValue', value),
});
</script>

<template>
  <ElDialog
    v-bind="$attrs"
    v-model="visible"
    :width="width"
    :close-on-click-modal="closeOnClickModal"
    :append-to-body="appendToBody"
  >
    <template v-for="(_, name) in $slots" #[name]="slotProps">
      <slot :name="name" v-bind="slotProps ?? {}" />
    </template>
  </ElDialog>
</template>
