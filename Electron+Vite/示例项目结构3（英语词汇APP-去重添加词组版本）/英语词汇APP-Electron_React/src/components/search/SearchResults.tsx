import { List } from 'react-window';
import DotGrid from '../ui/DotGrid';
import SpotlightCard from '../ui/SpotlightCard';
import { PHRASE_ROW_HEIGHT, PhraseRow, WORD_ROW_HEIGHT, WordCardRow } from './WordCard';
import { useFillSize } from '../../lib/useFillSize';
import type { PhraseSearchResult, WordSearchResult } from '../../../electron/api-contract';

interface Props {
  mode: 'word' | 'phrase';
  words: WordSearchResult[];
  phrases: PhraseSearchResult[];
  selectedId: number | null;
  loading: boolean;
  onSelectWord: (id: number) => void;
}

export default function SearchResults({ mode, words, phrases, selectedId, loading, onSelectWord }: Props) {
  const rows = mode === 'word' ? words.length : phrases.length;
  const fill = useFillSize(220, 320);
  const height = Math.max(220, fill.height);

  if (!loading && rows === 0) {
    return (
      <div className="empty-state">
        <DotGrid gap={20} dotSize={5} baseColor="var(--border-strong)" activeColor="var(--accent)" proximity={70} />
        <p className="empty-text">没有命中结果。换个关键词，或清除模块过滤。</p>
      </div>
    );
  }

  return (
    <SpotlightCard className="panel panel-grow" spotlightColor="rgba(47, 111, 237, 0.14)">
      <div className="row" style={{ justifyContent: 'space-between' }}>
        <h3 className="panel-title" style={{ margin: 0 }}>
          {mode === 'word' ? '单词结果' : '词组结果'}
        </h3>
        <span className="panel-hint" style={{ margin: 0 }}>
          {loading ? '查询中…' : `${rows} 条（虚拟列表，只渲染可视区）`}
        </span>
      </div>
      <div className="list-scroller" style={{ marginTop: 10 }} ref={fill.ref}>
        {mode === 'word' ? (
          <List
            rowComponent={WordCardRow}
            rowCount={words.length}
            rowHeight={WORD_ROW_HEIGHT}
            rowProps={{ items: words, selectedId, onSelect: onSelectWord }}
            overscanCount={5}
            style={{ height }}
          />
        ) : (
          <List
            rowComponent={PhraseRow}
            rowCount={phrases.length}
            rowHeight={PHRASE_ROW_HEIGHT}
            rowProps={{ items: phrases, onSelect: onSelectWord }}
            overscanCount={5}
            style={{ height }}
          />
        )}
      </div>
    </SpotlightCard>
  );
}
