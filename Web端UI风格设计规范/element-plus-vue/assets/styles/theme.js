const clamp = (n) => Math.max(0, Math.min(255, Math.round(n)));

function parseHex(hex) {
  let h = hex.replace('#', '').trim();
  if (h.length === 3) h = h.split('').map((c) => c + c).join('');
  if (!/^[0-9a-fA-F]{6}$/.test(h)) throw new Error(`invalid hex: ${hex}`);
  return [parseInt(h.slice(0, 2), 16), parseInt(h.slice(2, 4), 16), parseInt(h.slice(4, 6), 16)];
}

const toHex = ([r, g, b]) => `#${[r, g, b].map((n) => clamp(n).toString(16).padStart(2, '0')).join('')}`;

/** mix(a, b, weightA)：weightA 为 a 的占比，与 Element Plus SCSS 的混合方向一致。 */
function mix(a, b, weightA) {
  const A = parseHex(a), B = parseHex(b);
  return toHex(A.map((n, i) => n * weightA + B[i] * (1 - weightA)));
}

/**
 * 生成 Element Plus 的 9 个主色变量。
 * light：light-N 与白色混合，dark-2 与黑色混合。
 * dark ：light-N 与暗色底 #141414 混合，dark-2 与白色混合（与官方 dark/css-vars.css 同向）。
 * 默认 #409EFF 会复现官方值（个别通道可能有 ±1/255 的取整差）。
 */
export function palette(primary, mode = 'light') {
  const isDark = mode === 'dark';
  const surface = isDark ? '#141414' : '#ffffff';
  const shift = isDark ? '#ffffff' : '#000000';
  return {
    '--el-color-primary': primary,
    '--el-color-primary-light-3': mix(primary, surface, 0.7),
    '--el-color-primary-light-5': mix(primary, surface, 0.5),
    '--el-color-primary-light-7': mix(primary, surface, 0.3),
    '--el-color-primary-light-8': mix(primary, surface, 0.2),
    '--el-color-primary-light-9': mix(primary, surface, 0.1),
    '--el-color-primary-dark-2': mix(primary, shift, 0.8),
  };
}

/** 写入 :root 或指定元素；切换明暗时要用对应 mode 重写一次。 */
export function applyPalette(primary, { mode = 'light', root = document.documentElement } = {}) {
  const vars = palette(primary, mode);
  for (const [name, value] of Object.entries(vars)) root.style.setProperty(name, value);
  return vars;
}

/** 一步切换明暗：改 html.dark 并重算主色变体。 */
export function applyTheme(primary, dark = false, root = document.documentElement) {
  root.classList.toggle('dark', dark);
  return applyPalette(primary, { mode: dark ? 'dark' : 'light', root });
}
