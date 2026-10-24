# 审查清单与评分

触发方式：`/element-plus-vue audit` 或“审查这套 Element Plus 界面”。目标是 URL、源码目录或运行中的应用。

## 1. 审查顺序

1. **确认栈**：`package.json` 有 `vue@3` 与 `element-plus@2.x`；确认是全量导入还是按需导入，是否加载 `design-system.css`。
2. **确认基线**：`main.js` 导入顺序是否为 `index.css → dark/css-vars.css → design-system.css`。
3. **静态扫描**：间距、圆角、颜色、`v-model`、组件 API。
4. **运行态测量**：页面、Card、Form、Dialog、Table、弹层。
5. **交互验证**：表单提交、分页、弹窗、暗色切换、键盘。
6. **出报告**：总分 + 分类表 + 阻断项 + 警告 + 通过项 + 修复清单。

没有运行态证据时，报告必须注明“未验证”，不能只凭源码打满分。

## 2. 评分表（10 类，每类 0–10）

| 类别 | 10 分标准 | 主要证据 |
|---|---|---|
| 1. 令牌与主题 | 无硬编码色/字号；主色 7 个派生齐全；暗色可切 | `theme.js`、computed style |
| 2. 布局与间距 | 全部落在 9 档；页面/Card/Form/Dialog/Table 归一 | 运行态 padding/margin |
| 3. G2 圆角 | 非零圆角 `cornerShape` 覆盖；正圆例外登记 | `getComputedStyle().cornerShape` |
| 4. 排版 | 六级刻度；正文 14/22；无 13/15px 段落 | 源码 + computed |
| 5. 组件与交互 | `v-model` 名称正确；顶部提示三态统一；Modal 遮罩禁用；分页在 Grid 右下角 | 源码 + 交互 |
| 6. 表单与数据 | 校验、错误落点、三态、空态、分页 | 交互 |
| 7. 导航与外壳 | 断点正确；当前项；移动端可达 | 375 / 840 / 1280 三档 |
| 8. 无障碍 | 对比度、标签、热区、键盘、焦点 | 计算 + 键盘 |
| 9. 暗色 | 无闪白；派生色重算；提示/弹层/表格/表单全通过 | 切换实测 |
| 10. 视觉与动效 | 卡片优先；路由/面板/列表过渡统一；reduced-motion 生效 | 源码 + 运行态 |

总分 = 10 类平均 × 10。**低于 70 分不可交付**；任一阻断项存在也不可交付（见第 6 节）。

## 3. 静态扫描

### 间距

只扫业务源码（`src/`），不扫 `node_modules`。Element Plus 组件内部未暴露的 1–3px 实现值不计入；判定对象是业务类、布局容器与设计层覆盖。

```bash
# 非 4 倍数的 padding / margin / gap（人工扫输出里的 10/18/20/14）
grep -rnE "(padding|margin|gap)(-[a-z]+)?: *[0-9]+px" src --include=*.vue --include=*.css

# 固定高度（容易裁内容）
grep -rnE "height: *[0-9]+px" src --include=*.vue --include=*.css

# 栅格 gutter 非 8/16/24
grep -rnE "gutter=\"[0-9]+\"" src --include=*.vue
```

判定：出现 1–2 处可警告；≥5 处或出现在页面骨架/表单/卡片上，布局类 ≤5 分。

### 圆角与 G2

```bash
# 裸 border-radius（未用令牌）
grep -rn "border-radius" src --include=*.vue --include=*.css | grep -v "var(--radius" | grep -v "var(--el-"

# 扁条 50%
grep -rnE "border-radius: *50%" src --include=*.vue --include=*.css

# 例外登记
grep -rn "data-g2-exempt" src/

# 是否误在 :root 写 corner-shape
grep -rn "corner-shape" src/ --include=*.css
```

判定：

- 非零圆角没有 `cornerShape` 且无例外 → 每个组件扣分；
- 扁条 `50%` → 阻断；
- 正圆无 `data-g2-exempt` → 警告；
- `:root { corner-shape }` → 视为无效方案，G2 类 ≤3 分。

### 主题与硬编码

```bash
# hex / rgb 硬编码
grep -rnE "#[0-9a-fA-F]{3,8}\b|rgb\(" src --include=*.vue --include=*.css

# 只改主色未改派生
grep -rn "el-color-primary" src/ | grep -v "light-\|dark-2"

# 字号硬编码
grep -rnE "font-size: *[0-9]+px" src --include=*.vue --include=*.css
```

允许的 hex：`#fff` / `#ffffff` 在彩色底上写文字（且对比度达标），以及 `theme.js` 内部。其余应走令牌。

### Vue 绑定

```bash
# 分页是否两个 v-model
grep -rn "el-pagination" src -A6 | grep -E "v-model:current-page|v-model:page-size"

# 上传
grep -rn "v-model:file-list" src

# 表格误用 v-model
grep -rnE "<el-table[^>]*v-model" src

# 树误用 v-model
grep -rnE "<el-tree[^>]*v-model" src

# 弹窗整体绑定组合式对象（应解构）
grep -rnE "v-model=\"[a-zA-Z]+\\.visible\"" src
```

### 交互与视觉

```bash
# 业务层绕过 toast 封装
grep -rnE "ElMessage[.(]" src --include=*.vue --include=*.js --include=*.ts

# 提示三态 class
grep -rn "ds-toast--" src

# Modal 是否统一包装 / 是否显式禁用遮罩
grep -rnE "<el-dialog|AppDialog" src
grep -rn "close-on-click-modal" src

# 分页是否与数据 Grid 绑定
grep -rn "ds-data-grid" src
grep -rn "margin-left: *auto" src

# 卡片优先
grep -rnE "<el-card|<div class=\"[^\"]*ds-card" src
grep -rn "ds-card" src

# 过渡与禁用
grep -rnE "transition: *all|transition-property: *all" src
grep -rn "ds-page\|ds-fade\|ds-slide-up\|ds-scale-fade\|ds-list" src
grep -rn "prefers-reduced-motion" src
```

### 无障碍

```bash
# 纯图标按钮缺 aria-label
grep -rnE "<el-button[^>]*(circle|:icon)" src | grep -v "aria-label"

# 图片缺 alt
grep -rn "<img" src --include=*.vue | grep -v "alt="
```

## 4. 运行态测量

在浏览器控制台执行，或在 Playwright 中跑同样选择器。

### 页面与间距

```js
const cs = (sel) => getComputedStyle(document.querySelector(sel));

// 页面边距
cs('.ds-page').padding; // 375: 16px；>=600: 24px

// Card
cs('.el-card__body').padding;        // 16px
cs('.el-card').borderTopLeftRadius;  // 12px

// Form
cs('.el-form-item').marginBottom;    // 16px（最后一项 0）

// Dialog（先打开）
cs('.el-dialog').padding;            // 24px（或 .el-dialog__body 的父级 padding）
cs('.el-dialog').borderTopLeftRadius; // 16px

// Table
cs('.el-table .cell').padding;       // 0px 16px
```

### 横向溢出

```js
document.documentElement.scrollWidth <= window.innerWidth; // 375 宽应为 true
```

表格自身滚动不算页面溢出；检查 `.el-table__body-wrapper` 的 `scrollWidth > clientWidth` 是否发生在预期容器内。

### G2 覆盖

```js
const sels = ['.el-button', '.el-input__wrapper', '.el-card', '.el-dialog', '.el-message', '.el-notification', '.el-select__wrapper'];
sels.map((s) => {
  const el = document.querySelector(s);
  return el ? [s, getComputedStyle(el).cornerShape, getComputedStyle(el).borderTopLeftRadius] : [s, 'missing'];
});
```

判定：所有非零半径元素的 `cornerShape` 为 `squircle`（或 `superellipse(...)`）；正圆元素为 `round`（Element Plus 内部正圆已自动豁免，自建正圆需有 `data-g2-exempt`）。

### 顶部提示 / Modal / 分页 / 动效

```js
// 1) 提示三态：触发后检查 class 与背景
document.querySelector('.el-message.ds-toast--warn')?.className;
getComputedStyle(document.querySelector('.ds-toast--warn')).backgroundColor;

// 2) Modal 遮罩：点击后仍存在
const overlay = document.querySelector('.el-overlay');
overlay?.click();
!!document.querySelector('.el-dialog');

// 3) 分页右下角：右边缘/底边缘与 Grid 对齐（±1px）
const grid = document.querySelector('.ds-data-grid');
const pager = grid?.querySelector('.ds-data-grid__pager');
const g = grid?.getBoundingClientRect();
const p = pager?.getBoundingClientRect();
Math.abs(g.right - p.right) <= 1 && Math.abs(g.bottom - p.bottom) <= 1;

// 4) 卡片表面
getComputedStyle(document.querySelector('.el-card')).backgroundColor;
getComputedStyle(document.querySelector('.el-card')).borderTopLeftRadius; // 12px

// 5) 过渡是否存在且只动 opacity/transform
getComputedStyle(document.querySelector('.ds-page-enter-active') ?? document.body).transitionProperty;
```

`prefers-reduced-motion`：在 DevTools Rendering 面板模拟 `reduce`，刷新后页面切换应无位移、动画 ≤1ms。

### 触达与热区

```js
[...document.querySelectorAll('button, .el-menu-item, a')].map((el) => {
  const r = el.getBoundingClientRect();
  return [el.textContent.trim().slice(0, 12), Math.round(r.width), Math.round(r.height)];
});
```

判定：桌面 ≥32×32；触屏主操作 ≥48 高；相邻热区间距 ≥8。

### 对比度

用第 5 节公式或浏览器 DevTools 的 contrast checker。重点抽：

- 主按钮文字 / 主色底；
- 正文 / 页面底；
- 辅助文字 / 卡片底；
- 暗色下链接 / 暗底；
- 禁用文字（允许低，但必须有禁用语义）。

## 5. 对比度公式

```js
const lum = (hex) => {
  const c = [1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16) / 255)
    .map((v) => (v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4));
  return 0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2];
};
const contrast = (a, b) => {
  const [x, y] = [lum(a), lum(b)].sort((p, q) => q - p);
  return (x + 0.05) / (y + 0.05);
};
```

阈值：正文 4.5:1；大字号（≥24px 或 ≥18.66px 粗体）3:1；UI 边界/图标 3:1。

## 6. 阻断项（任一存在即不可交付）

1. 页面在 375 宽出现横向滚动（表格容器内部滚动除外）。
2. 非零圆角没有 G2 覆盖且未登记例外；或扁条用 `50%`。
3. 主色只改 `--el-color-primary`，派生色仍是默认蓝，页面出现两套主色。
4. 暗色切换后出现白底/亮蓝底/不可读文字，或首屏明显闪白。
5. `el-pagination` 只绑 `:current-page`，点页码不回写。
6. `el-table` / `el-tree` 使用不存在的 `v-model`，导致状态不同步。
7. 表单校验不生效（缺 `:model` / `prop` 路径错），或错误只用 toast。
8. 删除等破坏操作无二次确认。
9. 控制台有 Element Plus 的 Vue 警告或运行时错误。
10. 正文对比度 < 4.5:1 且非大字号。
11. 业务层直接调用 `ElMessage`，顶部提示出现两套以上配色/时长。
12. 分页不在数据 Grid 的右下角，或与表格分属不同容器。
13. 业务 Modal 点击遮罩即关闭（未保存内容可能丢失）。
14. 页面一级区块大面积扁平、无卡片边界。
15. 路由/面板切换无过渡，或使用 `transition: all`；reduced-motion 未降级。

## 7. 常见警告（扣分但不阻断）

- 出现 1–2 处非 4 倍数间距。
- 卡片/弹窗用固定高度裁内容。
- 表格操作列超过 3 个按钮。
- 纯图标按钮缺 `aria-label`。
- 表单标签宽度不统一（96 / 120 混用）。
- 卡片嵌套两层但视觉仍可辨识。
- 少数 `v-if` 面板未加过渡（非主流程）。
- toast 文案只写“失败”未给下一步。
- 同一屏两个 `primary` 按钮。
- 移动端菜单不可达，只能靠手势返回。
- `transition: all`。
- `!important` 超过 3 处。
- 自定义组件直接用 `props.modelValue` 手动 emit（可接受，但应迁移 `defineModel`）。

## 8. 报告格式

```md
## Element Plus 风格审查

**总分：82 / 100**（可交付）

| 类别 | 分数 | 依据 |
|---|---|---|
| 令牌与主题 | 9 | 主色 7 派生齐全；1 处 hex |
| 布局与间距 | 8 | Card/Form/Dialog 达标；2 处 10px |
| G2 圆角 | 9 | 覆盖 12/12；Avatar 已登记 |
| … | … | … |

### 阻断项
- 无

### 关键问题（0–3）
1. `src/views/OrderList.vue:88` —— `padding: 10px`，非 4 倍数；改为 `var(--space-3)`。
2. …

### 警告（4–6）
- `src/components/UserMenu.vue:31` 纯图标按钮缺 `aria-label`。

### 通过项（7–10）
- 页面 375 / 840 / 1280 三档无横向溢出。
- Dialog 内边距 24、圆角 16、G2 生效。
- 暗色切换后卡片/表格/弹层背景正确。

### 未验证
- 真实后端分页（无接口）。
- Safari 的 `corner-shape` 降级（环境无 Safari）。

### 修复清单
1. …
```

要求：

- 每个结论附**证据**：`file:line`、computed 值、截图或命令输出。
- 未跑过的检查写进“未验证”，不要给分。
- 评分与阻断项冲突时，阻断项优先。
