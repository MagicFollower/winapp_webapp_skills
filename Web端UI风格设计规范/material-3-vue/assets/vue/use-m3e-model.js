import { defineComponent, h } from 'vue';

const handlerName = (event) => `on${event[0].toUpperCase()}${event.slice(1)}`;

/**
 * 为 @m3e/web 自定义元素生成一个支持 v-model 的 Vue 包装组件。
 * 自定义元素只有 `value`/`checked` 属性 + `change`/`input` 事件，
 * 没有 Vue 的 `modelValue` / `update:modelValue`，所以裸用 v-model 会静默失效。
 */
export function defineM3eModel(tag, {
  prop = 'value',
  event = 'change',
  defaultValue = null,
  read = (el) => el[prop],
} = {}) {
  return defineComponent({
    name: `M3e${tag.replace(/^m3e-/, '').replace(/(^|-)([a-z])/g, (_, __, c) => c.toUpperCase())}`,
    inheritAttrs: false,
    props: {
      modelValue: { type: [String, Number, Boolean, Array, Object], default: defaultValue },
    },
    emits: ['update:modelValue'],
    setup(props, { attrs, emit, slots }) {
      return () => h(tag, {
        ...attrs,
        [prop]: props.modelValue,
        [handlerName(event)]: (e) => emit('update:modelValue', read(e.target)),
      }, slots.default ? slots.default() : undefined);
    },
  });
}

export const M3eSelect = defineM3eModel('m3e-select');
export const M3eSwitch = defineM3eModel('m3e-switch', { prop: 'checked', defaultValue: false });
export const M3eCheckbox = defineM3eModel('m3e-checkbox', { prop: 'checked', defaultValue: false });
export const M3eButton = defineM3eModel('m3e-button', { prop: 'selected', defaultValue: false });
export const M3eIconButton = defineM3eModel('m3e-icon-button', { prop: 'selected', defaultValue: false });
export const M3eDrawerContainer = defineM3eModel('m3e-drawer-container', { prop: 'start' });
export const M3eSliderThumb = defineM3eModel('m3e-slider-thumb', {
  prop: 'value', event: 'input', defaultValue: 0,
});
export const M3eDateInput = defineM3eModel('m3e-date-input');
export const M3eDatepicker = defineM3eModel('m3e-datepicker', { prop: 'date' });
export const M3eTimepicker = defineM3eModel('m3e-timepicker', { prop: 'date' });
export const M3eSplitPane = defineM3eModel('m3e-split-pane', { prop: 'value', defaultValue: 50 });
