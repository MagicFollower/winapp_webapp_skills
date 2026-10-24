# G2 圆角（强制规范）

本项目的所有圆角必须是 **G2 曲率连续**。`border-radius` 单独使用永远是 G1：圆弧在直边接缝处曲率从 0 跳到 `1/r`，把半径调大只是稀释跳变，不会变成 G2。G2 必须换角形族（`corner-shape` 或平端贝塞尔）。

## 1. 三条硬事实

1. `corner-shape` 的 `superellipse(v)` 中 **p = 2^v**，不是 p=v。
   - `round` = `superellipse(1)` = p2（圆弧，G1）
   - `squircle` = `superellipse(2)` = p4（G2 默认档）
   - `superellipse(1.4)` ≈ p2.64（温和档）
   - v<0 ⇒ p<1 ⇒ 内凹（scoop / notch），不要当“更方”用。
2. `corner-shape` **不继承**（MDN：Inherited: no），初始值 `round`；它作用于元素自身的盒子，并联动 `border`、`outline`、`box-shadow`、背景。
   - 只写 `:root { corner-shape: … }` 对任何元素都无效。
   - 必须写在每个带圆角的元素上；Shadow DOM 内还要单独注入。
3. `border-radius` 与描边不是独立自由度。`1/κ峰值` 是这一档能承受的最大内偏移（描边、inset、outline-offset），超过就自交。

## 2. 令牌层

```css
:root {
  /* 角形族：squircle = superellipse(2) = p4 */
  --g2-shape: squircle;
  --g2-shape-soft: superellipse(1.4); /* p≈2.64，用于大半径容器 */

  /* 半径仍用 MD3 形状令牌，G2 只换曲率族，不换半径档 */
  --radius-card: var(--md-sys-shape-corner-value-medium);
  --radius-dialog: var(--md-sys-shape-corner-value-extra-large);
  --radius-pill: 9999px;
}
```

**禁止**：
- `border-radius: 50%` 做扁条药丸：百分比按各自轴解析成椭圆，两轴曲率不对称 `(a/b)³`（200×80 达到 15.63×）。药丸一律 `9999px`。
- 在 `border-radius: 0` 的元素上写 `corner-shape`：没有半径就没有角形可换。
- 用 `corner-shape: superellipse(-0.2)` 表示“更方”：得到的是内凹。

## 3. 全量生效（light DOM）

```css
@supports (corner-shape: squircle) {
  *,
  *::before,
  *::after {
    corner-shape: var(--g2-shape, squircle);
  }
}
```

- 用通配选择器是刻意的：`corner-shape` 不继承，逐个组件声明必然漏。
- 例外必须显式登记，不能靠漏写：

```css
[data-g2-exempt],
[data-g2-exempt] * {
  corner-shape: round; /* 例外：需在审查清单里写明理由 */
}
```

- 圆角值为 0 的元素不受影响；只有带 `border-radius` 的盒子才看得出差别。
- Vuetify 的 `rounded` 类与 `border-radius` 都走 light DOM，这条规则同样生效；不需要桥接 `--v-*` 令牌。

## 4. @m3e/web（Shadow DOM）注入

`corner-shape` 不继承，`:root` 的规则进不了组件的 shadow root。用现成脚本，在 `app.mount()` 之后调用：

```js
// main.js
import { createApp } from 'vue';
import '@m3e/web/theme';
import '@m3e/web/all';
import App from './App.vue';
import { patchG2, observeG2 } from './g2-shadow.js';

createApp(App).mount('#app');
patchG2();   // 注入 light DOM 全局规则 + 所有已挂载 shadow root
observeG2(); // 监听后续新增节点
```

脚本位于 `assets/g2/g2-shadow.js`，核心规则：

```css
:host,
*,
*::before,
*::after {
  corner-shape: var(--g2-shape, squircle);
}
```

实测（Chromium 150，2026-10-24）：`m3e-button` 内部 `border-radius: 9999px` 的容器读回 `corner-shape: squircle`；light DOM 盒子、组件 host 同样读回 `squircle`。把 `--g2-shape` 改成 `round` 后，light DOM 与 shadow 内部同时读回 `round`，改回 `squircle` 也同步恢复——**单变量回退有效**。

**边界**：不支持 `corner-shape` 的引擎，Shadow DOM 内部无法用 `clip-path` 逐元素兜底（拿不到内部元素清单），会保持 G1。此时要么接受降级并在验收中注明，要么把该交互面改成自有组件/自绘 SVG。

## 5. 描边闸门（必算）

`1/κ峰值 = c(p) · r`。`cornerGate(p, r, w)` 的判定是 `1/κ峰值 ≥ 1.2w`。

r 与最大内偏移（px）：

| p | 形状 | r=4 | r=8 | r=12 | r=16 | r=24 | r=28 |
|---|---|---|---|---|---|---|---|
| 2 | round（G1） | 4.000 | 8.000 | 12.000 | 16.000 | 24.000 | 28.000 |
| 3 | superellipse(1.585) | 2.245 | 4.490 | 6.735 | 8.980 | 13.470 | 15.714 |
| 4 | **squircle（默认）** | **1.586** | **3.171** | **4.757** | **6.343** | **9.514** | **11.100** |
| 5 | superellipse(2.322) | 1.231 | 2.462 | 3.694 | 4.925 | 7.387 | 8.618 |
| 5.19 | 平端贝塞尔 | 1.061 | 2.122 | 3.182 | 4.243 | 6.364 | 7.425 |

换算成“可安全使用的描边宽”（再除 1.2）：

| p=4 | r=4 | r=8 | r=12 | r=16 | r=24 | r=28 |
|---|---|---|---|---|---|---|
| 最大描边 | 1.32px | 2.64px | 3.96px | 5.29px | 7.93px | 9.25px |

结论：
- MD3 的 1px 描边在 r≥4 全部安全。
- 3px focus ring 在 r=8 时已用到上限的 95%，优先把半径提到 r≥12 或把 ring 降到 2px。
- 药丸按钮的实际半径是 `min(w,h)/2`（40px 高 → r=20），按 used 半径算，不要按 9999px 算。

```js
import { cornerGate, usedRadius } from '../assets/g2/g2-corners.mjs';

const box = usedRadius(240, 96, 24);       // { f: 1, used: 24, bitten: false }
cornerGate(4, box.used, 3);                 // { safeInner: 9.514, ok: true }

const pill = usedRadius(160, 40, 9999);     // { f: 1, used: 20, bitten: false }
cornerGate(4, pill.used, 2);                // safeInner 7.93 ⇒ ok
```

**用过半径**：`getComputedStyle().borderRadius` 返回声明值，f 缩放读不回来。短边不足时（100×40 写 24/30/60px）实际全部压成 20px，必须用 `usedRadius(w,h,r).used` 重算闸门。

## 6. clip-path 兜底层

仅在 `CSS.supports('corner-shape', 'squircle') === false` 时启用：

```css
@supports not (corner-shape: squircle) {
  .g2-surface {
    clip-path: var(--g2-clip);
  }
}
```

```js
import { rectPoints, clipPathOf } from '../assets/g2/g2-corners.mjs';

function applyG2Clip(el, p = 4, eps = 0.5) {
  const { width: w, height: h } = el.getBoundingClientRect();
  const r = Math.min(parseFloat(getComputedStyle(el).borderRadius) || 0, w / 2, h / 2);
  if (!r) return;
  el.style.setProperty('--g2-clip', clipPathOf(rectPoints(w, h, r, p, eps)));
  if (!applyG2Clip.obs) {
    applyG2Clip.obs = new ResizeObserver((entries) => {
      entries.forEach((entry) => applyG2Clip(entry.target, p, eps));
    });
  }
  applyG2Clip.obs.observe(el);
}
```

兜底的两条代价必须写进验收：
1. `clip-path` 会连元素自己的 `box-shadow` 一起裁掉 → 阴影移到不裁剪的父层。
2. `path()` 数字必须无单位，且是绝对坐标 → 尺寸变化必须重算（ResizeObserver）。

祖先若有 `overflow: hidden` + 圆弧 `border-radius`，会把后代的 G2 角削平；祖先也要同形状，或去掉裁剪。

## 7. SVG / Canvas

- SVG 整块圆角矩形用平端三次贝塞尔（两端 κ=0，直接 G2）：

```js
import { rectPath } from '../assets/g2/g2-corners.mjs';

path.setAttribute('d', rectPath(200, 120, 24));
// M 24,0 H 176 C 200,0 200,0 200,24 V 96 C 200,120 200,120 176,120 H 24
// C 0,120 0,120 0,96 V 24 C 0,0 0,0 24,0 Z
```

- Canvas 用 `rectPoints(S, S, S/2, 4, 0.5)` 顺时针点列；`ctx.roundRect()` 画的是圆弧（G1），不要用它冒充 G2。
- 段数用 `segPerCorner()` 算（ε=0.5px 可见阈值，0.05px 对齐阈值）；段数 ∝ √尺寸，不要手填固定值（96px / p5 / ε0.5 → 每角 12 段）。

## 8. 动画

- `corner-shape` 可动画，但首帧是 `round`（G1），G2 只在 v>1 之后成立。
- 宽度动画：定值半径需 `2r ≤ 最窄宽度`；药丸需 `最窄宽度 ≥ 高度`，否则咬合点曲率被钉成 `2/w`。
- 共享元素/指示器做形变时，优先同时动画 `border-radius` 与 `corner-shape`，不要只动半径。

## 9. 验收

```js
import {
  superCorner, j1, peakKappa, cornerGate, usedRadius, rectPath, segPerCorner,
} from '../assets/g2/g2-corners.mjs';

j1(superCorner(24, 2)).ratio;   // 1.0000  ← 对照组，不是 G2
j1(superCorner(24, 4)).ratio;   // 254.1365 ← ≥2，G2
peakKappa(superCorner(24, 4));  // 0.105112
cornerGate(4, 24, 3);           // safeInner 9.514，ok true
usedRadius(100, 40, 30).used;   // 20 ← 被短边压缩
segPerCorner(superCorner(48, 5), 0.5); // 12
rectPath(200, 120, 24);         // 见 §7 的 d
```

审查清单：

- [ ] 每个非零 `border-radius` 都在 `@supports (corner-shape: …)` 或全局 `*` 规则内配了 `corner-shape`
- [ ] 全站已跑 `patchG2()` + `observeG2()`，抽查组件内部元素读回 `corner-shape: squircle`
- [ ] 没有 `border-radius: 50%` 做扁条药丸
- [ ] 所有描边/inset/outline-offset 过 `cornerGate(...).ok`
- [ ] 短边过 `usedRadius(...).bitten === false`，或已按 used 半径重排
- [ ] `clip-path` 兜底元素没有 `box-shadow`
- [ ] 祖先链没有 `overflow: hidden + border-radius` 压住 G2 后代
- [ ] `[data-g2-exempt]` 名单为空，或每条都有批准理由

**已知边界**：Safari/Firefox 的支持情况以 `CSS.supports('corner-shape','squircle')` 现场探测为准，不要按 UA 猜；不支持时 light DOM 可按 §6 降级，Shadow DOM 内部保持 G1 并在验收中注明。
