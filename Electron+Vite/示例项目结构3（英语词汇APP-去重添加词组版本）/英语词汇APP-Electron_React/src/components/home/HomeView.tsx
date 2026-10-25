import { useCallback, useEffect, useState } from 'react';
import { format, startOfMonth } from 'date-fns';
import BlurText from '../ui/BlurText';
import HeatmapPanel from './HeatmapPanel';
import ModuleChips from '../common/ModuleChips';
import StatsSidebar from './StatsSidebar';
import { useAppStore } from '../../lib/store';
import type { HeatmapDay, StudyStats, StudyWord } from '../../../electron/api-contract';

export default function HomeView({ colorScheme }: { colorScheme: 'light' | 'dark' }) {
  const selectedDate = useAppStore((s) => s.selectedDate);
  const setSelectedDate = useAppStore((s) => s.setSelectedDate);
  const dailyGoal = useAppStore((s) => s.dailyGoal);
  const setDailyGoal = useAppStore((s) => s.setDailyGoal);
  const refreshKey = useAppStore((s) => s.refreshKey);
  const bump = useAppStore((s) => s.bump);
  const setView = useAppStore((s) => s.setView);
  const setDetailWordId = useAppStore((s) => s.setDetailWordId);

  const [heat, setHeat] = useState<HeatmapDay[]>([]);
  const [stats, setStats] = useState<StudyStats | null>(null);
  const [dayWords, setDayWords] = useState<StudyWord[]>([]);

  const load = useCallback(async () => {
    const [h, s] = await Promise.all([window.api.getHeatmap({ months: 6 }), window.api.getStudyStats()]);
    setHeat(h);
    setStats(s);
  }, []);

  useEffect(() => {
    void load();
  }, [load, refreshKey]);

  useEffect(() => {
    void window.api.getWordsByDate(selectedDate).then(setDayWords);
  }, [selectedDate, refreshKey]);

  const percent = stats && stats.monthWords > 0 ? Math.round((stats.monthMastered / stats.monthWords) * 100) : 0;

  return (
    <div className="stack">
      <div>
        <BlurText text="我的学习主页" delay={40} className="panel-title" animateBy="words" />
        <p className="panel-hint" style={{ marginTop: -6 }}>
          数据口径：按去重后的唯一单词统计，{format(startOfMonth(new Date()), 'yyyy-MM')} 起算本月
        </p>
      </div>

      <div className="home-grid" data-grid-probe>
        <div className="stack">
          <HeatmapPanel data={heat} selectedDate={selectedDate} colorScheme={colorScheme} onSelect={setSelectedDate} grow />

          {stats ? (
            <div className="panel">
              <h3 className="panel-title">本月概览</h3>
              <p className="panel-hint" style={{ marginTop: -4 }}>
                学习 {stats.monthDays} 天 · 累计 {stats.monthWords} 词 · 掌握 {stats.monthMastered} 词
              </p>
              <div className="progress">
                <div style={{ width: `${percent}%` }} />
              </div>
              <div className="row" style={{ justifyContent: 'space-between', marginTop: 6 }}>
                <span className="stat-label">本月掌握率</span>
                <span className="stat-label">{percent}%</span>
              </div>
            </div>
          ) : null}
        </div>

        {stats ? (
          <StatsSidebar
            stats={stats}
            selectedDate={selectedDate}
            dailyGoal={dailyGoal}
            onDailyGoal={setDailyGoal}
            onSelectToday={() => setSelectedDate(stats.today)}
          />
        ) : null}
      </div>

      <div className="panel">
        <div className="row" style={{ justifyContent: 'space-between' }}>
          <h3 className="panel-title" style={{ margin: 0 }}>
            {selectedDate} 学习的单词
          </h3>
          <button type="button" className="btn" onClick={() => void window.api.resampleDailyWords(selectedDate).then(() => bump())}>
            重新抽样这一天
          </button>
        </div>
        {dayWords.length === 0 ? (
          <p className="panel-hint" style={{ marginTop: 10 }}>
            这一天还没有学习记录。去「学习」页抽样今日单词，或在查询页打开某个单词。
          </p>
        ) : (
          <div className="word-cards">
            {dayWords.map((w, i) => (
              <button
                key={w.wordId}
                type="button"
                className="word-card"
                data-word-card
                onClick={() => {
                  setDetailWordId(w.wordId);
                  setView('search');
                }}
              >
                <span className="word-card-no">{i + 1}</span>
                <span className="word-card-word">{w.word}</span>
                <span className="word-card-meaning">{w.meaning}</span>
                <ModuleChips modules={w.modules} />
              </button>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
