import { ElMessage } from 'element-plus';

const base = {
  offset: 24,
  showClose: true,
  grouping: true,
  customClass: 'ds-toast',
};

const variants = {
  info: { type: 'info', duration: 3000 },
  warn: { type: 'warning', duration: 4000 },
  error: { type: 'error', duration: 5000 },
};

function show(kind, message, options = {}) {
  const variant = variants[kind] ?? variants.info;
  return ElMessage({
    ...base,
    ...variant,
    ...options,
    message,
    type: variant.type,
    customClass: ['ds-toast', `ds-toast--${kind}`, options.customClass]
      .filter(Boolean)
      .join(' '),
  });
}

/** 顶部提示条：全项目统一入口，不要直接调用 ElMessage。 */
export const toast = {
  info: (message, options) => show('info', message, options),
  warn: (message, options) => show('warn', message, options),
  warning: (message, options) => show('warn', message, options),
  error: (message, options) => show('error', message, options),
  closeAll: () => ElMessage.closeAll(),
};

export function useToast() {
  return toast;
}
