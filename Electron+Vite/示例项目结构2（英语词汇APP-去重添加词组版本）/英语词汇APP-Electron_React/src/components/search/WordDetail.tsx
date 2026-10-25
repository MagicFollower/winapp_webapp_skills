import { useEffect, useState } from 'react';
import SplitText from '../ui/SplitText';
import ModuleChips from '../common/ModuleChips';
import type { WordDetail as WordDetailType } from '../../../electron/api-contract';

interface Props {
  detail: WordDetailType;
  onAddToToday: (wordId: number) => void;
}

export default function WordDetail({ detail, onAddToToday }: Props) {
  const [activePhraseId, setActivePhraseId] = useState<number | null>(null);

  useEffect(() => {
    setActivePhraseId(detail.groups[0]?.phrases[0]?.id ?? null);
  }, [detail.id, detail.groups]);

  const active = detail.groups.flatMap((g) => g.phrases).find((p) => p.id === activePhraseId) ?? null;

  return (
    <div className="panel detail-panel">
      {/* key 是必需的：SplitText 首次动画完成后 animationCompletedRef 会永久挡住重播（SplitText.tsx:65），
          换词时 cleanup 已把字元 revert 成 opacity:0，标题就再也不出现。整块重挂载才能让闸门归零。 */}
      <SplitText key={detail.id} text={detail.word} className="detail-word" splitType="chars" duration={0.5} delay={0.04} tag="h2" />
      <p className="detail-meaning">{detail.meaning}</p>
      <div className="row" style={{ justifyContent: 'space-between' }}>
        <ModuleChips modules={detail.modules} />
        <button type="button" className="btn" onClick={() => onAddToToday(detail.id)}>
          加入今天的学习
        </button>
      </div>

      <div className="detail-body" data-detail-body>
        {detail.groups.map((group) => (
          <div key={group.module.id}>
            <div className="module-group-head">
              <span className="chip" data-module={group.module.code}>
                {group.module.name}
              </span>
              <span className="panel-hint" style={{ margin: 0 }}>
                {group.phrases.length} 个词组
              </span>
            </div>
            <div className="phrase-items">
              {group.phrases.map((p) => (
                <button
                  key={p.id}
                  type="button"
                  data-phrase-item
                  className={p.id === activePhraseId ? 'phrase-item is-active' : 'phrase-item'}
                  onClick={() => setActivePhraseId(p.id)}
                >
                  <span className="phrase-item-text">{p.phrase}</span>
                  {p.usage ? <span className="phrase-item-usage">｜ {p.usage}</span> : null}
                </button>
              ))}
            </div>
          </div>
        ))}
      </div>

      {active ? (
        <div className="phrase-block" style={{ marginTop: 12 }}>
          <div className="phrase">{active.phrase}</div>
          {active.usage ? <div className="usage">{active.usage}</div> : null}
          {active.example ? <div className="example">{active.example}</div> : null}
          {active.exampleTranslation ? <div className="example-translation">{active.exampleTranslation}</div> : null}
        </div>
      ) : null}
    </div>
  );
}
