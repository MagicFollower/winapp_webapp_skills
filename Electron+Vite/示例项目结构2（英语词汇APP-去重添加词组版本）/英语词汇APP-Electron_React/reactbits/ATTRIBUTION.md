# ReactBits 取用记录

## 来源

- 仓库：`DavidHDev/react-bits`（GitHub API 实测 `stargazers_count` 48,524，`default_branch` main）
- 变体：`src/ts-default/<分类>/<组件名>/<组件名>.tsx`（+ 同名 `.css`）——这是 ReactBits 官方的 TypeScript + 独立 CSS 版
- 引用固定方式：按 `main` 分支的 `raw.githubusercontent.com` 单文件 GET 取回（详见 `manifest.json` 的 `source.hosts`）
- 取用日期：2026-10-05
- 用法：ReactBits 本身按 copy-in（把组件源码复制进工程）方式分发，本工程把原文固化在 `reactbits/raw/`，由 `bin/fetch-reactbits.mjs` 做确定性转换后写入 `src/components/ui/`；原文不被改写，改动只发生在转换与调用侧。

## 授权状态（待你确认后再对外发布）

实测两条，都不支持"随便商用"的结论：

1. `https://raw.githubusercontent.com/DavidHDev/react-bits/main/LICENSE` 返回 **404**——仓库根没有 LICENSE 文件。
2. `https://api.github.com/repos/DavidHDev/react-bits` 的 `license` 字段是
   `{"key":"other","name":"Other","spdx_id":"NOASSERTION"}`——GitHub 识别不出 SPDX 许可证。

所以**在把带这些组件的包对外分发之前**，需要人工去 reactbits.dev / 仓库 README 确认代码使用条款，
并把结论记在这里。本地开发、内部试用不受影响。文档 `doc/运行与构建（T0Level）.md` 的"换组件"小节同步引用本节。

## 落位清单

16 个组件、29 个文件（`.tsx` 与 `.css`），逐条哈希见 `lock.json`；组件名与分类见 `manifest.json`：

- Components：Dock、SpotlightCard、AnimatedList、Carousel、TiltedCard
- TextAnimations：BlurText、CountUp、SplitText、GradientText、ShinyText
- Micro：FlipCard、FuseButton、HoldButton
- Backgrounds：Aurora、Waves、DotGrid

对应运行时依赖（全部只在渲染层被 Vite 打进 `dist/`）：`motion`、`gsap`、`@gsap/react`、
`react-icons`、`@hugeicons/react`、`@hugeicons/core-free-icons`、`ogl`。
