import type { ModuleRef } from '../../../electron/api-contract';

const LETTERS = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ'.split('');

interface Props {
  modules: ModuleRef[];
  selected: string[];
  onToggle: (code: string) => void;
  firstLetter: string | null;
  onLetter: (letter: string | null) => void;
}

export default function ModuleFilter({ modules, selected, onToggle, firstLetter, onLetter }: Props) {
  return (
    <div className="stack" style={{ gap: 10 }}>
      <div className="row">
        <span className="stat-label">模块</span>
        {modules.map((m) => (
          <button
            key={m.code}
            type="button"
            className="chip chip-toggle"
            data-module={m.code}
            aria-pressed={selected.includes(m.code)}
            onClick={() => onToggle(m.code)}
          >
            {m.name}
          </button>
        ))}
        {selected.length > 0 ? (
          <button type="button" className="btn" onClick={() => selected.forEach(onToggle)}>
            清除模块
          </button>
        ) : null}
      </div>
      <div className="letter-strip">
        <button type="button" aria-pressed={firstLetter === null} onClick={() => onLetter(null)}>
          全部
        </button>
        {LETTERS.map((l) => (
          <button key={l} type="button" aria-pressed={firstLetter === l} onClick={() => onLetter(firstLetter === l ? null : l)}>
            {l}
          </button>
        ))}
      </div>
    </div>
  );
}
