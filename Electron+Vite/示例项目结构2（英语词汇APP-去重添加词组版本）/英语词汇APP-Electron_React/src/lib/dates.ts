// 渲染侧的「今天」必须与主进程 electron/study.ts:5 的 todayLocalDate 同口径：本地日期，不是 UTC。
// 之前 store 与 SearchView 用 toISOString().slice(0,10)，东八区 00:00–07:59 会差一天
// （学习页把 UTC 日期当默认值，与 StudyView 的本地"今天"比不相等 → 走只读分支，当天永远不抽样）。
export function todayLocalDate(now = new Date()): string {
  const y = now.getFullYear();
  const m = String(now.getMonth() + 1).padStart(2, '0');
  const d = String(now.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}
