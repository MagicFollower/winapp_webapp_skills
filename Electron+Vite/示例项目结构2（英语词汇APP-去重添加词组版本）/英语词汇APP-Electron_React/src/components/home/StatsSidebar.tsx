import StatCard from './StatCard';
import type { StudyStats } from '../../../electron/api-contract';

interface Props {
  stats: StudyStats;
  selectedDate: string;
  dailyGoal: number;
  onDailyGoal: (n: number) => void;
  onSelectToday: () => void;
}

export default function StatsSidebar({ stats, selectedDate, dailyGoal, onDailyGoal, onSelectToday }: Props) {
  const percent = Math.min(100, Math.round((stats.todayCount / dailyGoal) * 100));
  return (
    <div className="stat-side">
      <StatCard
        label={selectedDate === stats.today ? '今日' : `所选日期 ${selectedDate}`}
        value={stats.todayCount}
        unit="词已学习"
        accent="primary"
        onClick={onSelectToday}
      />
      <StatCard label="连续打卡" value={stats.streakDays} unit="天" accent="fire" />
      <StatCard label="累计" value={stats.totalLearned} unit="词" />

      <div className="panel" style={{ padding: 14 }}>
        <div className="row" style={{ justifyContent: 'space-between' }}>
          <span className="stat-label">今日目标</span>
          <span className="stat-label">
            {stats.todayCount}/{dailyGoal}
          </span>
        </div>
        <div className="progress" style={{ marginTop: 8 }}>
          <div style={{ width: `${percent}%` }} />
        </div>
        <div className="row" style={{ marginTop: 10, justifyContent: 'space-between' }}>
          <span className="stat-label">目标词数</span>
          <input
            type="number"
            min={1}
            max={200}
            value={dailyGoal}
            onChange={(e) => onDailyGoal(Number(e.target.value))}
            style={{ width: 72, background: 'var(--bg-inset)', border: '1px solid var(--border)', borderRadius: 8, padding: '3px 6px' }}
          />
        </div>
      </div>

      <div className="panel" style={{ padding: 14 }}>
        <div className="stat-label">已掌握 / 已学习</div>
        <div className="stat-value" style={{ fontSize: '1.25rem' }}>
          {stats.totalMastered} / {stats.totalLearned}
        </div>
        <div className="panel-hint" style={{ marginTop: 6 }}>
          全库唯一单词 {stats.uniqueWords.toLocaleString('en-US')} 个
        </div>
      </div>
    </div>
  );
}
