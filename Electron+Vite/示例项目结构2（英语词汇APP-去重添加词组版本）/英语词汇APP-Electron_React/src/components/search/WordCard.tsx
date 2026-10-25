import type { CSSProperties } from 'react';
import ModuleChips from '../common/ModuleChips';
import type { PhraseSearchResult, WordSearchResult } from '../../../electron/api-contract';

export const WORD_ROW_HEIGHT = 74;
export const PHRASE_ROW_HEIGHT = 92;

interface Injected {
  index: number;
  style: CSSProperties;
  ariaAttributes: { 'aria-posinset': number; 'aria-setsize': number; role: 'listitem' };
}

export interface WordRowData {
  items: WordSearchResult[];
  selectedId: number | null;
  onSelect: (id: number) => void;
}

export interface PhraseRowData {
  items: PhraseSearchResult[];
  onSelect: (wordId: number) => void;
}

export function WordCardRow({ index, style, ariaAttributes, items, selectedId, onSelect }: Injected & WordRowData) {
  const row = items[index];
  if (!row) return null;
  return (
    <div
      style={style}
      {...ariaAttributes}
      className="word-row"
      data-selected={selectedId === row.id}
      onClick={() => onSelect(row.id)}
      role="button"
      tabIndex={0}
      onKeyDown={(e) => {
        if (e.key === 'Enter') onSelect(row.id);
      }}
    >
      <div className="word-row-head">
        <span className="word" dangerouslySetInnerHTML={{ __html: row.wordHtml }} />
        <ModuleChips modules={row.modules} max={4} />
        <span className="spacer" />
        <span className="stat-label">{row.matchedFields.join(' / ')}</span>
      </div>
      <span className="word-meaning" dangerouslySetInnerHTML={{ __html: row.meaningHtml }} />
    </div>
  );
}

export function PhraseRow({ index, style, ariaAttributes, items, onSelect }: Injected & PhraseRowData) {
  const row = items[index];
  if (!row) return null;
  return (
    <div
      style={style}
      {...ariaAttributes}
      className="word-row"
      role="button"
      tabIndex={0}
      onClick={() => onSelect(row.wordId)}
      onKeyDown={(e) => {
        if (e.key === 'Enter') onSelect(row.wordId);
      }}
    >
      <div className="word-row-head">
        <span className="word" dangerouslySetInnerHTML={{ __html: row.phraseHtml }} />
        <span className="chip" data-module={row.module.code}>
          {row.module.name}
        </span>
        <span className="spacer" />
        <span className="stat-label">{row.word}</span>
      </div>
      <span className="word-meaning" dangerouslySetInnerHTML={{ __html: row.exampleHtml }} />
      <span className="word-meaning" dangerouslySetInnerHTML={{ __html: row.exampleTranslationHtml }} />
    </div>
  );
}
