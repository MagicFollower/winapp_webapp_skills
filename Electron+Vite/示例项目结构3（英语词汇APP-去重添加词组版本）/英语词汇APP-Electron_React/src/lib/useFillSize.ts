import { useLayoutEffect, useState } from 'react';

export interface FillSize {
  width: number;
  height: number;
  // 元素在视口里的顶端，以及同列中排在它后面的兄弟总高——要"跟随窗体动态变化"就得用这两个量，
  // 因为容器本身被 min-height 夹住时，容器高度已经不反映可用视口空间了。
  top: number;
  below: number;
  // 视口高度也进 state：只比 width/height/top/below 的话，纯改窗体高度可能一个量都不变，
  // 观察器就白跑一趟、卡片拿到的还是旧视口尺寸。
  vh: number;
  // callback ref：元素可能是条件渲染出来的，用普通 ref 时 effect 只跑一次就再也挂不上观察器。
  ref: (el: HTMLDivElement | null) => void;
}

// 有些子组件（react-window 的行容器、FlipCard）只吃显式像素尺寸，不认 flex 高度，
// 所以"自适应 app 高度"必须落在测量上：量容器真实盒尺寸，再喂给它们。
export function useFillSize(minHeight = 120, minWidth = 200): FillSize {
  const [node, setNode] = useState<HTMLDivElement | null>(null);
  const [size, setSize] = useState({ width: minWidth, height: minHeight, top: 0, below: 0, vh: 0 });

  useLayoutEffect(() => {
    if (!node) return;
    const measure = () => {
      const rect = node.getBoundingClientRect();
      const parent = node.parentElement;
      let below = 0;
      if (parent) {
        const kids = Array.from(parent.children);
        const gap = parseFloat(getComputedStyle(parent).rowGap || '0') || 0;
        for (let i = kids.indexOf(node) + 1; i < kids.length; i += 1) {
          below += (kids[i] as HTMLElement).getBoundingClientRect().height + gap;
        }
      }
      const next = {
        width: Math.max(minWidth, Math.floor(rect.width)),
        height: Math.max(minHeight, Math.floor(rect.height)),
        top: Math.round(rect.top),
        below: Math.round(below),
        vh: window.innerHeight
      };
      setSize((prev) =>
        prev.width === next.width && prev.height === next.height && prev.top === next.top && prev.below === next.below && prev.vh === next.vh
          ? prev
          : next
      );
    };
    measure();
    const ro = new ResizeObserver(measure);
    ro.observe(node);
    window.addEventListener('resize', measure);
    return () => {
      ro.disconnect();
      window.removeEventListener('resize', measure);
    };
  }, [node, minHeight, minWidth]);

  return { width: size.width, height: size.height, top: size.top, below: size.below, vh: size.vh, ref: setNode };
}
