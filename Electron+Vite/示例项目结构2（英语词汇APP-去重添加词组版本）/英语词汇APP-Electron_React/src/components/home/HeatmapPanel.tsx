import { cloneElement, useLayoutEffect, useRef, useState } from 'react';
import { ActivityCalendar, type Activity } from 'react-activity-calendar';
import { eachDayOfInterval, format, subMonths } from 'date-fns';
import SpotlightCard from '../ui/SpotlightCard';
import type { HeatmapDay } from '../../../electron/api-contract';

interface Props {
  data: HeatmapDay[];
  selectedDate: string;
  colorScheme: 'light' | 'dark';
  onSelect: (date: string) => void;
  minBlockSize?: number;
  maxBlockSize?: number;
  grow?: boolean;
}

const BLOCK_MARGIN = 4;
// 首估时给星期标签列与滚动条留的余量；估多了只是格子偏小，估少了靠收缩循环兜住。
const LABEL_ALLOWANCE = 64;
// 收缩硬下限：宁可格子小于 minBlockSize，也不能让末列溢出到点不着。
const MIN_BLOCK = 6;
const MAX_CORRECTIONS = 8;

const LEGEND: Array<{ level: number; label: string }> = [
  { level: 0, label: '0' },
  { level: 1, label: '1-10' },
  { level: 2, label: '11-20' },
  { level: 3, label: '21-50' },
  { level: 4, label: '51+' }
];

export default function HeatmapPanel({
  data,
  selectedDate,
  colorScheme,
  onSelect,
  minBlockSize = 12,
  maxBlockSize = 26,
  grow = false
}: Props) {
  const byDate = new Map(data.map((d) => [d.date, d]));
  // 网格右端停在今天：今日之后的格子不画（补齐整周会让未来日期出现在可点的残列里）。
  const end = new Date();
  const start = subMonths(end, 6);
  const activities: Activity[] = eachDayOfInterval({ start, end }).map((day) => {
    const key = format(day, 'yyyy-MM-dd');
    const hit = byDate.get(key);
    return { date: key, count: hit?.count ?? 0, level: hit?.level ?? 0 };
  });
  const weeks = Math.ceil(activities.length / 7);

  // 按容器宽度反推格子边长。组件自己的 SVG 宽度公式对不上 weeks*(block+margin)（实测 806 vs 预测 810），
  // 所以不能纯算：先按余量估一挡，渲染后量真实宽度，超出就收缩，直到不溢出为止。
  const wrapRef = useRef<HTMLDivElement>(null);
  const [blockSize, setBlockSize] = useState(minBlockSize);
  const corrections = useRef(0);

  const initialFit = (el: HTMLElement) => {
    const pitch = Math.floor((el.clientWidth - LABEL_ALLOWANCE) / weeks);
    return Math.min(maxBlockSize, Math.max(MIN_BLOCK, pitch - BLOCK_MARGIN));
  };

  useLayoutEffect(() => {
    const el = wrapRef.current;
    if (!el) return;
    corrections.current = 0;
    setBlockSize(initialFit(el));
    const ro = new ResizeObserver(() => {
      corrections.current = 0;
      setBlockSize(initialFit(el));
    });
    ro.observe(el);
    return () => ro.disconnect();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [weeks, minBlockSize, maxBlockSize]);

  useLayoutEffect(() => {
    const el = wrapRef.current;
    const svg = el ? el.querySelector('svg') : null;
    if (!el || !svg || corrections.current > MAX_CORRECTIONS) return;
    const excess = svg.getBoundingClientRect().width - (el.clientWidth - 2);
    if (excess > 0) {
      corrections.current += 1;
      setBlockSize((b) => Math.max(MIN_BLOCK, b - Math.ceil(excess / weeks) - 1));
    }
  });

  const theme = {
    light: ['#ebedf0', '#c6e48b', '#7bc96f', '#239a3b', '#196127'],
    dark: ['#1f2937', '#0e4429', '#006d32', '#26a641', '#39d353']
  };

  const todayIso = format(new Date(), 'yyyy-MM-dd');
  // 补齐整周会让未来几天出现在网格里；它们不可选，否则点上去会被当成"那天学过"写进记录。
  const pick = (date: string) => {
    if (date > todayIso) return;
    onSelect(date);
  };

  return (
    <SpotlightCard className={grow ? "panel panel-grow" : "panel"} spotlightColor="rgba(47, 111, 237, 0.16)">
      <div className="row" style={{ justifyContent: 'space-between', alignItems: 'baseline' }}>
        <h2 className="panel-title" style={{ margin: 0 }}>
          学习热力图
        </h2>
        <span className="panel-hint" style={{ margin: 0 }}>
          {format(start, 'yyyy-MM-dd')} → {todayIso} · {weeks} 周 · 格子 {blockSize}px
        </span>
      </div>

      <div style={{ marginTop: 14, overflowX: 'auto', padding: '0 10px 10px 0' }} data-heatmap ref={wrapRef}>
        <ActivityCalendar
          data={activities}
          colorScheme={colorScheme}
          theme={theme}
          blockSize={blockSize}
          blockMargin={BLOCK_MARGIN}
          blockRadius={3}
          fontSize={12}
          maxLevel={4}
          showColorLegend={false}
          showWeekdayLabels={['mon', 'wed', 'fri']}
          labels={{
            // v3 要求 weekdays 必须给满 7 个标签，具体显示哪几天由 showWeekdayLabels 选。
            weekdays: ['周日', '周一', '周二', '周三', '周四', '周五', '周六'],
            months: ['1月', '2月', '3月', '4月', '5月', '6月', '7月', '8月', '9月', '10月', '11月', '12月'],
            totalCount: '{{count}} 词已学习'
          }}
          tooltips={{
            activity: {
              text: (activity) => {
                const hit = byDate.get(activity.date);
                return hit && hit.count > 0
                  ? `${activity.date} · 学习 ${hit.count} 词 · 掌握 ${hit.mastered} 词`
                  : `${activity.date} · 未学习`;
              }
            }
          }}
          renderBlock={(block, activity) => {
            const future = activity.date > todayIso;
            const styled = cloneElement(block, {
              onClick: () => pick(activity.date),
              style: {
                // 未来格不吃指针：既不选也不聚焦，否则焦点环会单独留在被点的那格上、与选中框脱节。
                pointerEvents: future ? 'none' : 'auto',
                cursor: future ? 'default' : 'pointer'
              }
            });
            if (activity.date !== selectedDate) return styled;
            // 选中框画在格子内侧：SVG 描边压着几何边线，外半条要靠视口外溢才能看见，
            // 而库自己的 svg 样式优先级更高，结果就是右/下两边被裁（像素取色实测无蓝色）。
            const x = Number(block.props.x ?? 0);
            const y = Number(block.props.y ?? 0);
            const w = Number(block.props.width ?? 0);
            const h = Number(block.props.height ?? 0);
            return (
              <g>
                {styled}
                <rect
                  x={x + 1.5}
                  y={y + 1.5}
                  width={Math.max(w - 3, 1)}
                  height={Math.max(h - 3, 1)}
                  rx={Number(block.props.rx ?? 3)}
                  fill="none"
                  stroke="var(--accent)"
                  strokeWidth={2}
                  pointerEvents="none"
                />
              </g>
            );
          }}
        />
      </div>

      <div className="legend">
        <span>学习量</span>
        {LEGEND.map((l) => (
          <span key={l.level} className="row" style={{ gap: 4 }}>
            <i style={{ background: `var(--heat-${l.level})` }} />
            {l.label}
          </span>
        ))}
      </div>
    </SpotlightCard>
  );
}
