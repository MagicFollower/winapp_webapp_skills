# MD3 合规审查清单

以 `/material-3-vue audit` 或“审查 MD3 合规性”触发。目标可以是 URL、源码目录或运行中的应用。

## 1. 识别目标

- URL：用浏览器工具看渲染结果、控制台、计算样式。
- 源码：读 `package.json`、`*.vue`、CSS、`vite.config.*` / `nuxt.config.*`。
- 先确认路线：`@m3e/web`（`--md-sys-*`）还是 Vuetify（`--v-theme-*`），两者不要混用。

## 2. 评分表（0–10）

| 类别 | 检查内容 |
|---|---|
| 色彩令牌 | `--md-sys-color-*`；只按 `X` + `on-X` 配对；暗色完整；无硬编码 hex |
| 字体 | `m3e-heading` 或 typescale 令牌；角色与层级正确；无手拼字号 |
| 形状 / G2 | 半径用 `--md-sys-shape-corner-value-*` / `--m3e-*-shape`；**每个非零圆角都配 `corner-shape`**；Shadow DOM 已注入；描边过闸门；无 `50%` 药丸；无 `clip-path` + `box-shadow` 同元素 |
| 层阶 | `surface-container-*` 色调；悬停/聚焦升一级；没有用阴影硬造层阶 |
| 组件 | 主路线 `@m3e/web`；Vuetify 项目允许但需说明令牌体系；没有用非 MD3 组件冒充 |
| 布局 | 间距落在 4dp 子集；表单字段 16、组间 24；卡片内容不二次 padding；无 `m3e-*` 的 margin/固定高度；触达 ≥48；阅读宽度 ≤1040 |
| 导航 | 紧凑 bar / 中等 rail / 展开 drawer；3–5 个主项；断点只切布局 |
| 动效 | `m3e-theme motion`；CSS 回退用 `--md-sys-motion-*`；`corner-shape` 动画首帧不是 G2 |
| 无障碍 | 对比度 4.5:1（正文）/ 3:1（大字与边界）；ARIA；键盘；48dp 触达；焦点可见 |
| 主题 | `m3e-theme` 或 `:root` 令牌；作用域主题不泄漏；暗色不是简单反色 |

## 3. 报告结构

1. 总分与状态（通过 7+ / 警告 4–6 / 失败 0–3）。
2. 分类评分表。
3. 关键问题 0–3 条：`file:line` + 现象 + 影响 + 修法。
4. 警告 4–6 条。
5. 通过项 7–10 条。
6. 按影响力排序的修复清单。

评分标准：9–10 完全符合；7–8 基本符合有少量硬编码或漏项；4–6 部分符合；1–3 主要是非 MD3；0 不适用。

## 4. 快速检查

按项目实际路径调整；命中后逐条人工判断，grep 只是线索：

```bash
# 主路线 / Vuetify
grep -rn '@m3e/web' --include='*.js' --include='*.ts' --include='*.vue' .
grep -rn "from 'vuetify'" --include='*.js' --include='*.ts' --include='*.vue' .

# 硬编码颜色
grep -rnE '#[0-9a-fA-F]{3,8}' --include='*.css' --include='*.vue' .

# 错误接入形态
grep -rn 'v-model' --include='*.vue' .        # 再确认是否直接落在 m3e-* 上
grep -rn '<template #' --include='*.vue' .    # 自定义元素的具名插槽误用
grep -rn 'isCustomElement' --include='*.js' --include='*.ts' .

# G2
grep -rn 'border-radius' --include='*.css' --include='*.vue' .
grep -rn 'corner-shape' --include='*.css' --include='*.vue' .
grep -rnE 'border-radius\s*:\s*50%' --include='*.css' --include='*.vue' .
grep -rn 'patchG2' --include='*.js' --include='*.ts' --include='*.vue' .
grep -rn 'clip-path' --include='*.css' --include='*.vue' .

# 布局
grep -rnE 'm3e-[a-z-]+[^{]*\{[^}]*margin' --include='*.css' --include='*.vue' .
grep -rnE 'm3e-[a-z-]+[^{]*\{[^}]*height' --include='*.css' --include='*.vue' .
grep -rnE '(gap|padding|margin)[^;]*:\s*[0-9.]+px' --include='*.css' --include='*.vue' .
```

## 5. G2 抽查（浏览器内）

```js
// 1) 能力
CSS.supports('corner-shape', 'squircle');

// 2) light DOM：挑一个 r>0 的盒子
getComputedStyle(document.querySelector('.g2-surface')).cornerShape; // 'squircle'

// 3) @m3e/web：抽查内部元素
const root = document.querySelector('m3e-button')?.shadowRoot;
[...root.querySelectorAll('*')]
  .map((el) => [el, getComputedStyle(el)])
  .find(([, s]) => s.borderRadius !== '0px')
  ?.[1].cornerShape; // 'squircle'

// 4) 闸门（Node 或页面模块）
// cornerGate(4, usedRadius(w, h, r).used, borderWidth).ok === true
```

## 6. 布局抽查

- 取 3 个代表性页面（列表、表单、详情），量：页边距、字段间距、卡片 gap、按钮热区。
- 页边距应为 16（<600）/ 24（≥600）；表单字段 16；卡片 gap 16/24。
- 可点击元素用 `getBoundingClientRect()` 量热区，小于 48×48 记问题。
- 正文容器宽度 >1040 记问题；长文 >840 记警告。
- 任何 `m3e-*` 被覆盖 `height` 或 `margin` 记问题（改用 size 与父级 gap）。
