import { rectPoints, clipPathOf } from './g2-corners.mjs';

/**
 * Element Plus 是 light DOM，不需要 Shadow DOM 注入。
 * 只有不支持 corner-shape 的引擎才用本文件；clip-path 会裁掉元素自身的
 * border / box-shadow，所以只给显式加了 .ds-g2-surface 的元素用。
 */
const observers = new WeakMap();

export function applyG2Clip(el, { p = 4, eps = 0.5 } = {}) {
  if (!el) return;
  if (el.closest('[data-g2-exempt]')) return;
  const { width: w, height: h } = el.getBoundingClientRect();
  if (!w || !h) return;
  const raw = getComputedStyle(el).borderTopLeftRadius;
  const radius = Math.min(
    raw.endsWith('%') ? (parseFloat(raw) / 100) * Math.min(w, h) : parseFloat(raw) || 0,
    w / 2,
    h / 2,
  );
  if (!radius) return;
  el.style.setProperty('--g2-clip', clipPathOf(rectPoints(w, h, radius, p, eps)));
  if (!observers.has(el)) {
    const observer = new ResizeObserver(() => applyG2Clip(el, { p, eps }));
    observer.observe(el);
    observers.set(el, observer);
  }
}

export function applyG2ClipAll(selector = '.ds-g2-surface', options) {
  for (const el of document.querySelectorAll(selector)) {
    el.classList.add('ds-g2-surface');
    applyG2Clip(el, options);
  }
}
