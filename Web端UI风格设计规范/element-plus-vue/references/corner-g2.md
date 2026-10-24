# G2 圆角（强制）

**规则：本项目所有非零圆角必须是 G2（曲率连续）。`border-radius` 只决定半径，不决定曲率；只写 `border-radius` 得到的是 G1（圆弧拼接，接缝处曲率从 0 跳到 1/r）。**

G2 的实现方式是同时声明：

```css
.card {
  border-radius: 12px;
  corner-shape: squircle; /* = superellipse(2)，指数 p = 4 */
}
```

Element Plus 是**轻 DOM（light DOM）**，没有 Shadow DOM，所以一条全局通配规则就能覆盖所有组件及其伪元素；不需要向 shadow root 注入。

## 1. 三层策略

`../assets/styles/design-system.css` 已实现：

```css
:root { --g2-shape: squircle; }

/* 第一层：能力探测；corner-shape 不继承，必须逐个元素命中 */
@supports (corner-shape: squircle) {
  *, *::before, *::after { corner-shape: var(--g2-shape, squircle); }
}

/* 第二层：显式例外；必须登记并附理由 */
[data-g2-exempt], [data-g2-exempt] * { corner-shape: round; }

/* 第三层：不支持时的兜底；只对显式 opt-in 的元素生效 */
@supports not (corner-shape: squircle) {
  .ds-g2-surface { clip-path: var(--g2-clip); }
}
```

- **不要在 `:root` 上单独写 `corner-shape`**：该属性不继承，写了也不生效。
- **不要按 UA 猜支持**：用 `CSS.supports('corner-shape', 'squircle')`，或依赖上面的 `@supports`。
- 全局通配只改角形，不改半径；半径仍由各组件变量控制。

## 2. 半径档位（与 Element Plus 对齐）

| 令牌 | 半径 | Element Plus 变量 | 组件 |
|---|---|---|---|
| `--radius-xs` | 4px | `--el-border-radius-small` | Tag、Pagination 页码 |
| `--radius-sm` | 8px | `--el-border-radius-base` | Button、Input、Select、Popover、Message、Notification |
| `--radius-md` | 12px | `--el-card-border-radius` | Card |
| `--radius-lg` | 16px | `--el-dialog-border-radius`、`--el-messagebox-border-radius` | Dialog、MessageBox |
| `--radius-xl` | 24px | — | 大面板、展示卡 |
| `--radius-pill` | 9999px | `--el-border-radius-round` | 药丸 Button、Switch、round Tag |
| 50% | — | `--el-border-radius-circle` | **仅正圆**（见下） |

### 50% 的适用范围

`border-radius: 50%` 只允许用于**长宽相等的正圆**：Avatar、圆形图标按钮、Radio 圆点、Slider 按钮、Loading 圆环。

- 扁条（宽 > 高）写 `50%` 会得到椭圆，四角曲率不一致；用 `9999px`。
- Element Plus 内部的正圆（`.el-avatar--circle`、`.el-button.is-circle`、`.el-backtop`、`.el-badge__content.is-dot`、`.el-radio__inner`、`.el-slider__button`、`.el-switch__action`、`.el-timeline-item__node`、`.el-tour-indicator`、`.el-skeleton__circle` 等）已在 `design-system.css` 自动豁免，不需要逐个加属性。
- 自建的正圆元素要显式登记例外，否则 `squircle` 会把圆压成超椭圆：

```vue
<el-avatar :size="40" :src="user.avatar" />
<el-button circle :icon="Plus" aria-label="新增" />

<!-- 自建圆形元素 -->
<div class="status-dot" data-g2-exempt />
```

`data-g2-exempt` 会同时作用于后代，所以一个例外容器内的所有圆角都退回 G1（`round`）。**例外只能用于几何上必须是正圆的元素，不能用来偷懒跳过 G2。**

## 3. 曲率闸门

G2 角形的峰值曲率 `κpeak = 1/(j₁·r)`。squircle 的 `j₁ ≈ 0.3964`，因此：

- `r = 8` 时 `κpeak ≈ 0.3153`，曲率半径约 3.17px；
- 描边、`inset box-shadow`、`outline-offset` 的厚度必须小于局部曲率半径，否则描边会在角上起节、发胖、或出现缺口。

安全条件：`内部厚度 ≤ (1/κpeak) / 1.2`（留 20% 余量）。

| 半径 r | κpeak | 1/κpeak | 安全内部厚度（/1.2） |
|---|---|---|---|
| 4px | 0.630672 | 1.586px | **1.32px** |
| 8px | 0.315336 | 3.171px | **2.64px** |
| 12px | 0.210224 | 4.757px | **3.96px** |
| 16px | 0.157668 | 6.342px | **5.29px** |
| 24px | 0.105112 | 9.514px | **7.93px** |
| 32px | 0.078834 | 12.685px | **10.57px** |

实际结论：

| 描边 / 内阴影 | 最小半径 |
|---|---|
| 1px | 4px（全档安全） |
| 2px | 8px |
| 3px | 12px |
| 4px | 16px |
| 6px | 24px |

- Element Plus 默认描边 1px，因此在 4px 档也安全。
- 自定义 `box-shadow: inset 0 0 0 2px`、`outline: 2px` 时，必须先确认半径 ≥ 8。
- 外阴影不受此闸门限制；但 `clip-path` 兜底会裁掉外阴影（见第 5 节）。

## 4. 嵌套圆角

内层半径必须满足：

```
内层半径 = 外层半径 − 内边距
内层半径 ≥ 4
```

例：Card `r=12`、内边距 16 → 内部元素理想半径 0（贴边），实际用 4 或直接不设圆角。Dialog `r=16`、内边距 24 → 内部块不设圆角；如果内部有次级卡片，用 8。

**不要内外同半径**：内角会凸出，视觉上像“圆角套圆角”错位。

## 5. 不支持 `corner-shape` 时的兜底

### 默认策略：保持 G1

Element Plus 的圆角都很小（4–16px），在低版本浏览器里退化成 G1 是**可接受降级**。不要为了 G2 引入会裁掉阴影的 `clip-path`。

### 显式 opt-in：`clip-path`

只有满足以下全部条件才用 `../assets/g2/g2-light.js`：

1. 元素没有外阴影、没有外描边、没有需要溢出显示的子元素；
2. 元素尺寸稳定（或已用 `ResizeObserver` 重算）；
3. 已加 `.ds-g2-surface` 类。

```js
import { applyG2ClipAll } from '@/assets/g2/g2-light.js';

// DOM 更新后执行；内部自带 ResizeObserver，尺寸变化会重算
applyG2ClipAll('.ds-g2-surface');
```

```css
.ds-g2-surface { background: var(--el-color-primary); }
/* design-system.css 里的 @supports not 规则会自动加 clip-path */
```

**限制**：

- `clip-path` 会裁掉自身 `box-shadow`、`border` 的外半部分；阴影要么放到父容器，要么用 `filter: drop-shadow()`（也会被裁，需放在裁剪元素之外）。
- `clip-path` 不能被 `corner-shape` 覆盖；同时存在时以裁剪路径为准。
- Dialog / Drawer / Popover / Message 一律**不要**走 clip-path：它们的视觉依赖阴影，裁剪后层次消失。

### SVG / Canvas 导出

需要把 G2 角形画到 SVG 或 Canvas 时，用 `../assets/g2/g2-corners.mjs`，不要手拼贝塞尔：

```js
import { rectPoints, rectPath, superCorner, segPerCorner } from './g2-corners.mjs';

// SVG：把点串成 polygon / path
const pts = rectPoints(240, 160, 16, 4, 0.5);
const d = `M ${pts.map(([x, y]) => `${x},${y}`).join(' L ')} Z`;

// 四角平端三次贝塞尔（P1=P2=盒角），几何上接近 p≈5.19，适合粗略导出
const approx = rectPath(240, 160, 16);

// Canvas：按 segPerCorner 采样，保证误差 ≤ 0.5px
const c = superCorner(16, 4);
const seg = segPerCorner(c, 0.5);
```

- `rectPath` 的端点曲率为 0，不是严格的 squircle；要求曲率连续时用 `rectPoints`。
- Canvas 采样步数按 `segPerCorner(corner, eps)` 计算，`eps` 是最大弦高误差（px）。
- 导出后验证：角部轮廓点到理论曲线的偏差 ≤ 0.5px。

## 6. 验证

### 浏览器内

```js
// 1) 能力
CSS.supports('corner-shape', 'squircle'); // 应为 true

// 2) 实际生效
getComputedStyle(document.querySelector('.el-card')).cornerShape; // "squircle"

// 3) 半径未被 G2 改掉
getComputedStyle(document.querySelector('.el-card')).borderTopLeftRadius; // "12px"
```

### 自动审查

```bash
# 所有非零半径是否都有 corner-shape 覆盖？（全局通配已覆盖，检查是否被例外误伤）
grep -rn "data-g2-exempt" src/

# 是否有人手写 border-radius 绕过组件变量
grep -rn "border-radius" src/ --include=*.vue --include=*.css | grep -v "var(--radius" | grep -v "var(--el-"

# 是否用 50% 做扁条
grep -rn "border-radius:\s*50%" src/ --include=*.vue --include=*.css

# 是否在 :root 单独写 corner-shape（无效）
grep -rn "corner-shape" src/ --include=*.css
```

### 人工抽查

1. Button / Input / Select 的角是否比同半径的 G1 更“方”一点点（squircle 特征）。
2. 1px 描边在 4px 角上是否均匀，无发胖或缺口。
3. 正圆元素（Avatar、圆形按钮）是否仍是正圆（有 `data-g2-exempt`）。
4. Dialog / Popover 的阴影是否完整（没有误用 clip-path）。
5. 缩放 200% 后角部是否仍平滑（无折线感）。

## 7. 常见错误

| 错误 | 后果 | 正确做法 |
|---|---|---|
| 只写 `border-radius` | 仍是 G1 | 加 `corner-shape`（全局规则已加） |
| 在 `:root` 写 `corner-shape` | 不继承，无效 | 通配或逐元素 |
| 给所有元素加 `clip-path` | 阴影/描边被裁 | 只给 `.ds-g2-surface` opt-in |
| 用 `50%` 做扁条 | 椭圆曲率不一致 | `9999px` |
| 正圆不登记例外 | 圆被压成超椭圆 | `data-g2-exempt` |
| 2px 描边配 4px 半径 | 角部起节 | 升到 8px 或减到 1px |
| 内外同半径 | 内角凸出 | 内 = 外 − padding |
| 给 `corner-shape` 加 transition | 形状插值不存在/跳变 | 只过渡半径或背景 |

## 8. 审查判定

以下任一项出现即 G2 不合格：

- 存在非零 `border-radius` 且计算样式 `cornerShape` 不是 `squircle` / `superellipse(...)`，且未登记例外；
- 扁条使用 `50%`；
- 正圆未登记 `data-g2-exempt`；
- Dialog / Drawer / Popover 使用 `clip-path` 兜底导致阴影缺失；
- 描边厚度超过第 3 节闸门表；
- 在 `:root` 单独声明 `corner-shape` 作为“全局方案”。
