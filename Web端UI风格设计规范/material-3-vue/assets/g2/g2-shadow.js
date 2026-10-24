/**
 * 把 G2 角形注入 light DOM 与 @m3e/web 的 Shadow DOM。
 * corner-shape 不继承（MDN: Inherited: no），只写 :root 不会影响任何盒子，
 * 所以 light DOM 需要一条全局 `*` 规则，每个 shadow root 再各追加一条。
 * 先行条件：document 上已设置 `--g2-shape`（如 squircle）。
 */
const TEXT = ':host, *, *::before, *::after { corner-shape: var(--g2-shape, squircle); }';
const LIGHT_TEXT = '*, *::before, *::after { corner-shape: var(--g2-shape, squircle); }';

let sheet;
let lightSheet;
const patched = new WeakSet();

function patchDocument() {
  if (!document.querySelector('style[data-g2-light]') && !document.adoptedStyleSheets) {
    const style = document.createElement('style');
    style.dataset.g2Light = '';
    style.textContent = LIGHT_TEXT;
    document.head.append(style);
    return;
  }
  try {
    lightSheet ??= new CSSStyleSheet();
    if (!lightSheet.cssRules.length) lightSheet.replaceSync(LIGHT_TEXT);
    if (!document.adoptedStyleSheets.includes(lightSheet)) {
      document.adoptedStyleSheets = [...document.adoptedStyleSheets, lightSheet];
    }
  } catch {
    if (!document.querySelector('style[data-g2-light]')) {
      const style = document.createElement('style');
      style.dataset.g2Light = '';
      style.textContent = LIGHT_TEXT;
      document.head.append(style);
    }
  }
}

function apply(el) {
  const root = el.shadowRoot;
  if (!root || patched.has(root)) return;

  try {
    if (!('adoptedStyleSheets' in root)) throw new Error('adoptedStyleSheets unsupported');
    sheet ??= new CSSStyleSheet();
    if (!sheet.cssRules.length) sheet.replaceSync(TEXT);
    if (!root.adoptedStyleSheets.includes(sheet)) {
      root.adoptedStyleSheets = [...root.adoptedStyleSheets, sheet];
    }
  } catch {
    if (!root.querySelector('style[data-g2]')) {
      const style = document.createElement('style');
      style.dataset.g2 = '';
      style.textContent = TEXT;
      root.append(style);
    }
  }

  patched.add(root);
  root.querySelectorAll('*').forEach(walk);
}

function walk(el) {
  if (typeof el.updateComplete?.then === 'function') {
    el.updateComplete.then(() => apply(el));
  } else {
    apply(el);
  }
}

/** 对已挂载的树做一次全量注入（在 app.mount 之后调用）。 */
export function patchG2(root = document) {
  patchDocument();
  if (root.nodeType === 1) walk(root);
  root.querySelectorAll('*').forEach(walk);
}

/** 监听后续新增节点；组件内部再嵌套 shadow root 时由 apply() 递归处理。 */
export function observeG2() {
  patchDocument();
  const observer = new MutationObserver((records) => {
    for (const record of records) {
      for (const node of record.addedNodes) {
        if (node.nodeType === 1) patchG2(node);
      }
    }
  });
  observer.observe(document.documentElement, { childList: true, subtree: true });
  return observer;
}
