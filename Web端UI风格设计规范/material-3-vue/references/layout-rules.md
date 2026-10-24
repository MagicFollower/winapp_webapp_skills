# 布局约束（组件内部与组件之间）

本文件约束**作者负责的布局**：页面容器、组件外间距、栅格、触达区域、对齐。库组件自身的内部尺寸由 `@m3e/web` 负责，不要用 `margin` / 固定 `height` 去“修”。

数值来源：`@m3e/web@2.10.2` 源码（`MeasurementToken.ts`、`ButtonSizeToken.ts`、`ListItemElement.ts`、`AppBarSizeToken.ts`、`DialogElement.ts` 等）与 MD3 8dp 网格。库版本变化时以源码为准。

## 1. 间距令牌

`@m3e/web` 自己用 `--md-sys-measurement-space<unit>`，公式是 `8 × unit/100` px：

| 令牌 | px | 令牌 | px | 令牌 | px |
|---|---|---|---|---|---|
| `space50` | 4 | `space100` | 8 | `space150` | 12 |
| `space200` | 16 | `space250` | 20 | `space300` | 24 |
| `space400` | 32 | `space500` | 40 | `space600` | 48 |
| `space700` | 56 | `space800` | 64 | `space900` | 72 |

作者层只使用 **4dp 网格子集**，在 `:root` 建别名（带 fallback，令牌未定义也不会塌）：

```css
:root {
  --space-1: var(--md-sys-measurement-space50, 4px);
  --space-2: var(--md-sys-measurement-space100, 8px);
  --space-3: var(--md-sys-measurement-space150, 12px);
  --space-4: var(--md-sys-measurement-space200, 16px);
  --space-6: var(--md-sys-measurement-space300, 24px);
  --space-8: var(--md-sys-measurement-space400, 32px);
  --space-12: var(--md-sys-measurement-space600, 48px);
  --space-16: var(--md-sys-measurement-space800, 64px);
}
```

**允许值**：`0 / 4 / 8 / 12 / 16 / 24 / 32 / 48 / 64`。
**禁止值**：`5 / 10 / 13 / 18 / 20 / 22 / 28 / 30 …`（库内部的 2dp 子网格是组件实现细节，作者层不要模仿）。

## 2. 间距层级

| 距离 | 用在哪 | 例 |
|---|---|---|
| 4px | 图标与文字、控件内部紧邻元素 | `m3e-icon` + 标签 |
| 8px | 同一组内的兄弟控件；按钮组；Chip 之间 | 取消 / 提交、筛选片 |
| 12px | 紧凑列表中的字段间距（表单统一用 16） | 设置项 |
| 16px | 组件之间、表单字段之间、卡片内边距、卡片网格（紧凑） | 两个 Card、两个输入框 |
| 24px | 组与组、区块之间、页面内边距（≥600px）、对话框内边距、卡片网格（展开） | 表单区与操作区 |
| 32–48px | 页面大区块之间 | 标题区与内容区 |
| 64px | 页面顶部/底部留白、空状态 | 首屏 |

判断规则：

1. **同一组的间距 < 组间间距 < 区块间间距**。三者相等就没有层级。
2. 相邻组件用父容器的 `gap`，不要给子组件写 `margin`。
3. 一组控件内最多用一个间距值；不要 8 和 12 混用。
4. 卡片网格的 gap 在紧凑用 16、≥840px 用 24；不要用 10/20。
5. 列表项之间默认 0；需要分组时用 32px 或分隔线，不要给每个 `m3e-list-item` 加 margin。

## 3. 库组件默认尺寸（不要覆盖）

| 组件 | 尺寸 |
|---|---|
| `m3e-button` / `m3e-icon-button` | `size`：xs 32 / **small 40（默认）** / medium 56 / large 96 / xl 136 |
| `m3e-fab` | small 56 / **medium 80（默认）** / large 96 |
| `m3e-chip` | 视觉高 32；触达区域 48 |
| `m3e-list-item` | 单行 56 / 双行 72 / 三行 88 |
| 列表内容 | 左右内边距 16；leading/trailing 与文本间距 16；单行上下 8；三行上下 12 |
| `m3e-form-field` | 最小高 56 |
| `m3e-app-bar` | small 64 / medium 112 / large 120 |
| `m3e-nav-bar` | 高 64 |
| `m3e-tabs` | 文字 48；图标 + 文字 64 |
| `m3e-card` | `content` 插槽内边距 16 |
| `m3e-dialog` | 最大宽/高 560；内容内边距 24；actions 间距 8 |
| `m3e-nav-rail` | 左右内边距 20 |
| `m3e-snackbar` | 左右内边距 16 |

规则：

- 不要给 `m3e-*` 写 `height` / `min-height`；要改尺寸用组件的 `size` 属性。
- 不要给 `m3e-*` 写 `margin`；用父级 `gap`。
- `m3e-card` 的 `slot="content"` 已有 16px 内边距，不要再包一层 `padding: 16px`。
- `m3e-form-field` 已包含标签、hint/error 的垂直节奏；不要给内部的 `input` 加 margin。
- 密度 (`m3e-theme density`) 会改内部高度，不要再手动补偿。

## 4. 页面骨架

```css
.page {
  padding: var(--space-4);
  max-width: 1040px;
  margin-inline: auto;
  display: grid;
  gap: var(--space-6);
}

@media (min-width: 600px) {
  .page { padding: var(--space-6); }
}

.reading { max-width: 840px; }        /* 长文本阅读宽度 */
.stack { display: grid; gap: var(--space-4); }
.row { display: flex; align-items: center; gap: var(--space-2); }
```

- 紧凑 16px、≥600px 24px 页边距；不要用 12/20 混着来。
- 阅读宽度 840–1040px；超过后正文不要继续拉满。
- 页面滚动容器只保留一个；不要 `main` 和内部卡片各滚一次。

## 5. 表单

```css
.form {
  display: grid;
  gap: var(--space-4);       /* 字段之间 16 */
  max-width: 560px;
}

.form__actions {
  display: flex;
  justify-content: flex-end;
  gap: var(--space-2);       /* 按钮之间 8 */
  margin-top: var(--space-2);
}
```

- 单列优先；两列只在 ≥840px 且字段有关联时使用，列间距 24。
- 标签在上、控件在下（`m3e-form-field` 默认）；不要让标签和输入框左右分栏。
- 错误文本紧跟控件（库已处理），不要把它做成 toast。
- 同一表单只保留一个主按钮（filled），其余用 text/outlined。
- 提交按钮在表单底部右侧；移动端可改为通栏，但仍保持 8px 与次要按钮的间距。

## 6. 卡片与网格

```css
.card-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: var(--space-4);
}

@media (min-width: 840px) {
  .card-grid { gap: var(--space-6); }
}
```

- 卡片内部：媒体放默认插槽（无内边距），标题/正文放 `slot="content"`（16px），操作放 `slot="actions"`；标题与正文 8px，正文与操作 16px。
- 卡片高度由内容决定；同一行卡片用 `align-items: stretch`，不要写死高度。
- 卡片本身不要再加外边距；间隙由网格 gap 负责。
- 点击整张卡片时用 `actionable`，不要在卡片里再放一个占满的按钮。

## 7. 对话框、抽屉、底部面板

- 对话框：最大 560px，内容 24px，标题与内容 16px，actions 右对齐、间距 8px；按钮不超过两个（最多一个 filled）。
- 对话框宽度不要用 `vw`；用库的 `max-width` 变量或 560px 上限。
- 抽屉：展开宽度按导航规范；内容左右 24px，顶部与 app bar 对齐。
- Bottom sheet：顶部圆角由库负责；内容左右 24px，底部留 24px 安全区。
- 弹层内的第一个可聚焦元素应有 `autofocus`；关闭按钮保持 48px 触达。

## 8. 触达与对齐

- 所有可点击元素的**触达区域** ≥ 48×48px；视觉可以是 40px，但不要缩小热区。
- 相邻可点击元素的触达区域之间至少 8px；不要只靠视觉间距。
- 文本左对齐；正文、段落、对话框内容不要居中。只有空状态标题和短标签可以居中。
- 数字列右对齐；表格单元格左右内边距 16、上下 8；表头与数据列同宽。
- 图标与文字基线对齐；图标尺寸 24（小尺寸控件可用 20），不要混用 18/22。
- 同一行的控件垂直居中对齐；不要让按钮比输入框高出一截（用同样的 40/56 档）。

## 9. 响应式

| 宽度 | 页边距 | 网格 | 导航 |
|---|---|---|---|
| <600 | 16 | 1 列 | 底部 nav bar（3–5 项） |
| 600–839 | 24 | 1–2 列 | nav rail（compact） |
| 840–1199 | 24 | 2–3 列；表单可两列 | 展开 drawer |
| 1200–1599 | 24 | 3–4 列 | drawer + 内容限宽 |
| 1600+ | 24 | 4+ 列 | drawer + 内容限宽 1040 |

- 断点切换只改布局，不要同时改字号与间距档位。
- 卡片网格用 `auto-fill` + `minmax`，不要写死列数。
- 容器查询用于卡片内部（`@container`），不要用视口断点控制卡片内文字大小。

## 10. 反模式

| 反模式 | 问题 | 改法 |
|---|---|---|
| `gap: 10px` / `padding: 13px` | 脱离 4dp 网格，视觉节奏散 | 用 8 / 12 / 16 |
| `m3e-button { margin-top: 8px }` | 组件外间距散落在各处 | 父容器 `gap` |
| `m3e-button { height: 36px }` | 破坏库的 size 与触达 | 用 `size="small"`（40） |
| 卡片 `content` 再包 `padding: 16px` | 双重内边距 32 | 直接用插槽 |
| 每个列表项 `margin-bottom: 8px` | 列表节奏断裂 | 列表内 0，分组用 32 |
| 表单字段间距 24 与 16 混用 | 组内层级混乱 | 统一 16，组间 24 |
| 正文居中、超长行宽 | 阅读困难 | 左对齐，840px 限宽 |
| 36px 高的自定义按钮 | 触达不足 | 视觉 40，热区 48 |
| 移动端与桌面同时改字号 | 断点行为不可预测 | 断点只改布局 |

## 11. 审查清单

- [ ] 所有 `gap` / `padding` / `margin` 落在 4dp 子集（0/4/8/12/16/24/32/48/64）
- [ ] 没有任何 `m3e-*` 选择器带 `margin`
- [ ] 没有任何 `m3e-*` 选择器带固定 `height` / `min-height`
- [ ] `m3e-card` 的 `content` 插槽没有二次 padding
- [ ] 表单字段间距统一 16，组间 24；操作区按钮间距 8
- [ ] 卡片网格 gap 16（紧凑）/ 24（≥840）
- [ ] 可点击元素触达 ≥48×48，相邻热区间距 ≥8
- [ ] 正文左对齐，阅读宽度 ≤1040px（长文 ≤840px）
- [ ] 页边距 16（<600）/ 24（≥600）
- [ ] 断点只改布局，不改字号与间距档位
