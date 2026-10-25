import { useCallback, useEffect, useState, type CSSProperties } from 'react';
import BlurText from '../ui/BlurText';
import GradientText from '../ui/GradientText';
import FlipCard from '../ui/FlipCard';
import FuseButton from '../ui/FuseButton';
import HoldButton from '../ui/HoldButton';
import ModuleChips from '../common/ModuleChips';
import HeatmapPanel from '../home/HeatmapPanel';
import { useAppStore } from '../../lib/store';
import { todayLocalDate } from '../../lib/dates';
import { useFillSize } from '../../lib/useFillSize';
import type { HeatmapDay, ModuleRef, StudyWord } from '../../../electron/api-contract';

// FlipCard 默认 hoverScale=1.03，作用在 transform 上：按容器算满就必定溢出，所以留出走形的余量。
const HOVER_SCALE = 1.03;
// 卡片高度的上下限。下限要小于"矮窗口还能整张卡在视口里"，否则矮窗口下卡片底部被视口截断。
const CARD_MIN_H = 300;
const CARD_MAX_H = 620;
// 三卡行：左右侧卡是**正方形**并钉在行的两端（左上角/右上角），主卡居中，三张卡顶边齐平。
// 列宽低于 SIDE_BREAKPOINT 时收起侧卡，主卡回到整列宽。
const SIDE_MIN = 190;
const SIDE_MAX = 320;
const SIDE_RATIO = 0.22;
const ROW_GAP = 18;
const SIDE_BREAKPOINT = 900;
// 整行离工具条的间距：三张卡一起下移，顶边仍齐平（不是把某一张拉高/压低去迁就别人）。
const ROW_DROP = 48;

function SideCard({ label, data, size, onClick }: { label: string; data?: StudyWord; size: number; onClick: () => void }) {
  if (!data) return null;
  return (
    <button
      type="button"
      className="side-card"
      data-side-card={label}
      style={{ width: size, height: size } as CSSProperties}
      onClick={onClick}
      aria-label={`${label}：${data.word}`}
    >
      <span className="side-label">{label}</span>
      <span className="side-word">{data.word}</span>
      <ModuleChips modules={data.modules} />
    </button>
  );
}

export default function StudyView({ colorScheme }: { colorScheme: 'light' | 'dark' }) {
  const selectedDate = useAppStore((s) => s.selectedDate);
  const setSelectedDate = useAppStore((s) => s.setSelectedDate);
  const dailyGoal = useAppStore((s) => s.dailyGoal);
  const sampleModules = useAppStore((s) => s.sampleModules);
  const toggleSampleModule = useAppStore((s) => s.toggleSampleModule);
  const bump = useAppStore((s) => s.bump);
  const refreshKey = useAppStore((s) => s.refreshKey);

  const [modules, setModules] = useState<ModuleRef[]>([]);
  const [heat, setHeat] = useState<HeatmapDay[]>([]);
  const [words, setWords] = useState<StudyWord[]>([]);
  const [index, setIndex] = useState(0);
  const [flipped, setFlipped] = useState(false);
  // 只在首次进入时显示占位；之后再取数都保留旧内容，否则卡区被占位符替换会明显闪一下。
  const [booted, setBooted] = useState(false);
  const slot = useFillSize(260, 320);

  useEffect(() => {
    void window.api.getModules().then(setModules);
  }, []);

  // 热力图数据与"看哪天"无关：只在进入页面与评分/重抽后刷新，避免点一次日期就重绘 27 周 SVG。
  useEffect(() => {
    void window.api.getHeatmap({ months: 6 }).then(setHeat);
  }, [refreshKey]);

  const load = useCallback(async () => {
    // 只有"今天"允许自动抽样；其他日期一律只读。否则点一下日历就会给那天生成 20 个随机词并写库，
    // 热力图会凭空变绿（实测过：17 天 n=20 且全部未评分，还包含未来日期）。
    const today = todayLocalDate();
    const daily =
      selectedDate === today
        ? await window.api.getDailyWords(selectedDate, { modules: sampleModules, count: dailyGoal })
        : await window.api.getWordsByDate(selectedDate);
    setWords(daily);
    setIndex((i) => Math.min(i, Math.max(daily.length - 1, 0)));
    setFlipped(false);
    setBooted(true);
  }, [selectedDate, sampleModules, dailyGoal]);

  useEffect(() => {
    void load();
  }, [load]);

  const current = words[index];

  // 切词条交给方向键与按钮：卡片内部不再吃横向手势，翻转与滑动不会抢同一根指针。
  useEffect(() => {
    if (words.length === 0) return;
    const onKey = (e: KeyboardEvent) => {
      const tag = (e.target as HTMLElement | null)?.tagName;
      if (tag === 'INPUT' || tag === 'SELECT' || tag === 'TEXTAREA') return;
      if (e.key === 'ArrowRight') {
        setFlipped(false);
        setIndex((i) => (i + 1) % words.length);
      } else if (e.key === 'ArrowLeft') {
        setFlipped(false);
        setIndex((i) => (i - 1 + words.length) % words.length);
      } else if (e.key === ' ' || e.key === 'Enter') {
        e.preventDefault();
        setFlipped((f) => !f);
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [words.length]);

  const rate = useCallback(
    async (rating: number) => {
      if (!current) return;
      await window.api.updateStudyRecord(current.wordId, rating, selectedDate);
      await load();
      bump();
      setIndex((i) => (i + 1) % Math.max(words.length, 1));
    },
    [current, selectedDate, load, bump, words.length]
  );

  // 三卡行：中间是当前词，左右是序列里的前一个/后一个（循环）。点侧卡等于走一步，两侧随之刷新。
  const total = Math.max(words.length, 1);
  const prev = words.length > 1 ? words[(index - 1 + total) % total] : undefined;
  const next = words.length > 1 ? words[(index + 1) % total] : undefined;
  const showSides = Boolean(prev) && Boolean(next) && slot.width >= SIDE_BREAKPOINT;
  // 侧卡是正方形：边长按列宽比例给，夹在 [SIDE_MIN, SIDE_MAX]。
  const sideSize = Math.max(SIDE_MIN, Math.min(SIDE_MAX, Math.floor((slot.width - 2 * ROW_GAP - 16) * SIDE_RATIO)));
  const sideRoom = showSides ? 2 * sideSize + 2 * ROW_GAP : 0;
  const cardWidth = Math.max(280, Math.min((slot.width - 16 - sideRoom) / HOVER_SCALE, 720));

  // 卡片高度按视口算，不吃容器高度：容器被 min-height 夹住时卡片就再也不跟窗体联动
  // （实测窗口从 635px 拉到 1115px，卡片恒为 408px，矮窗口下底部直接被视口截断）。
  // 除 HOVER_SCALE 是给悬浮放大留余量；ROW_DROP 也要扣掉——行有 padding-top，卡的实际顶边比行的顶边低这些。
  const cardHeight =
    slot.top > 0
      ? Math.max(CARD_MIN_H, Math.min(CARD_MAX_H, (slot.vh - slot.top - ROW_DROP - slot.below - 8) / HOVER_SCALE))
      : CARD_MIN_H;

  return (
    <div className="stack study-stack">
      <div>
        <BlurText text="每日学习卡片" delay={40} className="panel-title" animateBy="words" />
        <p className="panel-hint" style={{ marginTop: -6 }}>
          抽样口径：从{sampleModules.length === 0 ? '全部模块' : sampleModules.join(' / ')}随机取 {dailyGoal} 个词，同一天不重复抽
        </p>
      </div>

      <div className="panel">
        <div className="row">
          <span className="stat-label">抽样范围</span>
          {modules.map((m) => (
            <button
              key={m.code}
              type="button"
              className="chip chip-toggle"
              data-module={m.code}
              aria-pressed={sampleModules.includes(m.code)}
              onClick={() => toggleSampleModule(m.code)}
            >
              {m.name}
            </button>
          ))}
          <span className="spacer" />
          <span className="stat-label">日期</span>
          <input
            type="date"
            value={selectedDate}
            onChange={(e) => e.target.value && setSelectedDate(e.target.value)}
            style={{ background: 'var(--bg-inset)', border: '1px solid var(--border)', borderRadius: 8, padding: '3px 8px' }}
          />
        </div>
      </div>

      <div className="flashcard-wrap">
        <div className="stack">
          {!booted ? (
            <div className="panel">正在准备今日词卡…</div>
          ) : current ? (
            <div className="card-slot" ref={slot.ref}>
              <div className={showSides ? 'card-row card-row-wide' : 'card-row'}>
              {showSides ? <SideCard label="上一个" data={prev} size={sideSize} onClick={() => setIndex((i) => (i - 1 + total) % total)} /> : null}
              <FlipCard
              key={current.wordId}
              className="main-card"
              flipped={flipped}
              onFlipChange={setFlipped}
              draggable={false}
              /* 背面禁点翻：列表区的 down 被我挡在 scroller 里，root 就收不到成套的按下-抬起；
                 而按下释义区时 root 会 setPointerCapture，真实设备的抬起被重定向回 root
                 （FlipCard.tsx:232 在 draggable=false 下 moved 永假，于是必翻面，:198/:233）。
                 按下落在动画期间也一样：flip() 会把 spring 停在半路（:181），背面斜着，
                 命中测试够不到滚动区，滚轮随之落空——这正是"刚翻过来偶尔还能点翻+滚不动"。
                 正面仍保留点卡片翻面，翻回正面走「看正面」或 ← →。 */
              flipOnClick={!flipped}
              hoverScale={HOVER_SCALE}
              width={cardWidth}
              height={cardHeight}
              radius={16}
              background={colorScheme === 'dark' ? '#171e27' : '#ffffff'}
              color={colorScheme === 'dark' ? '#e8eef6' : '#12202f'}
              front={
                <div className="card-face card-face-front">
                  <div className="card-index">
                    第 {index + 1} / {words.length} 张 · 点卡片翻面，← → 切词条
                  </div>
                  <h2 className="card-word">
                    <GradientText colors={['#2f6fed', '#2f9e8f', '#6b5bd2']} animationSpeed={6}>
                      {current.word}
                    </GradientText>
                  </h2>
                  <div className="row">
                    <ModuleChips modules={current.modules} />
                    <span className="spacer" />
                    <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}>
                      <i className="status-dot" data-status={current.status} />
                      {current.status === 'mastered' ? '已掌握' : current.status === 'review' ? '待复习' : '未评分'}
                    </span>
                  </div>
                </div>
              }
              back={
                <div className="card-face card-face-back">
                  <p className="card-meaning">{current.meaning}</p>
                  {/* 列表区不吃翻面：FlipCard 在 draggable=false 下任何一次按下-抬起都会 flip
                      （FlipCard.tsx:198 让 moved 永远为 false，:232 于是走 flip 分支），
                      所以在词组区里点一下、或拖选一段文字，卡片就翻回正面，滚轮随即落空。
                      只挡 down/up，不挡 move —— 背面的倾斜手感要留着。挡不住的那半交给 flipOnClick。 */}
                  <div
                    className="card-back-scroll"
                    data-card-back
                    onPointerDown={(e) => e.stopPropagation()}
                    onPointerUp={(e) => e.stopPropagation()}
                  >
                    {current.groups.length === 0 ? <p className="panel-hint">该词没有词组数据</p> : null}
                    {current.groups.map((group) => (
                      <section className="phrase-group" key={group.module.id}>
                        <div className="module-group-head">
                          <span className="chip" data-module={group.module.code}>
                            {group.module.name}
                          </span>
                          <span className="panel-hint" style={{ margin: 0 }}>
                            {group.phrases.length} 个词组
                          </span>
                        </div>
                        {group.phrases.map((p) => (
                          <div className="phrase-block" key={p.id}>
                            <div className="phrase">{p.phrase}</div>
                            {p.usage ? <div className="usage">{p.usage}</div> : null}
                            {p.example ? <div className="example">{p.example}</div> : null}
                            {p.exampleTranslation ? <div className="example-translation">{p.exampleTranslation}</div> : null}
                          </div>
                        ))}
                      </section>
                    ))}
                  </div>
                </div>
              }
              />
              {showSides ? <SideCard label="下一个" data={next} size={sideSize} onClick={() => setIndex((i) => (i + 1) % total)} /> : null}
              </div>
            </div>
          ) : (
            <div className="panel">
              <p className="panel-hint" style={{ margin: 0 }}>
                {selectedDate} 没有学习记录。点日历只会查看，不会自动抽样——要给这天生成词卡请显式点「抽样这一天」。
              </p>
              <div className="row" style={{ marginTop: 10 }}>
                <button
                  type="button"
                  className="btn btn-primary"
                  onClick={() => {
                    void window.api.resampleDailyWords(selectedDate, { modules: sampleModules, count: dailyGoal }).then((rows) => {
                      setWords(rows);
                      setIndex(0);
                      setFlipped(false);
                      bump();
                    });
                  }}
                >
                  抽样这一天
                </button>
              </div>
            </div>
          )}

          <div className="rating-row">
            <button type="button" className="btn" data-rating="1" onClick={() => void rate(1)} disabled={!current}>
              Hard
            </button>
            <button type="button" className="btn" data-rating="2" onClick={() => void rate(2)} disabled={!current}>
              Medium
            </button>
            <button type="button" className="btn" data-rating="3" onClick={() => void rate(3)} disabled={!current}>
              Easy
            </button>
            <span className="spacer" />
            <button type="button" className="btn" data-flip-btn onClick={() => setFlipped((f) => !f)} disabled={!current}>
              {flipped ? '看正面' : '看释义'}
            </button>
            <button type="button" className="btn" onClick={() => setIndex((i) => (i - 1 + words.length) % Math.max(words.length, 1))} disabled={words.length === 0}>
              上一张
            </button>
            <button type="button" className="btn" onClick={() => setIndex((i) => (i + 1) % Math.max(words.length, 1))} disabled={words.length === 0}>
              下一张
            </button>
          </div>

          <div className="row">
            <FuseButton
              label="标记已掌握"
              doneLabel="已掌握"
              undoLabel="撤销"
              undoWindow={2200}
              commitOn="press"
              settle="reset"
              onCommit={() => void rate(3)}
              onUndo={() => void rate(2)}
              disabled={!current}
            />
            <HoldButton
              doneLabel="已重新抽样"
              holdTime={900}
              disabled={!booted}
              onHold={() => {
                void window.api
                  .resampleDailyWords(selectedDate, { modules: sampleModules, count: dailyGoal })
                  .then((rows) => {
                    setWords(rows);
                    setIndex(0);
                    setFlipped(false);
                    bump();
                  });
              }}
            >
              长按重新抽样今日
            </HoldButton>
          </div>
        </div>

      </div>

      <div className="study-heat">
      <HeatmapPanel
        data={heat}
        selectedDate={selectedDate}
        colorScheme={colorScheme}
        onSelect={setSelectedDate}
        minBlockSize={16}
        maxBlockSize={30}
      />
      </div>
    </div>
  );
}
