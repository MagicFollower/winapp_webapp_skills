import ShinyText from '../ui/ShinyText';

interface Props {
  value: string;
  onChange: (v: string) => void;
  phraseMode: boolean;
  onTogglePhrase: () => void;
  onSubmit: () => void;
}

export default function SearchBar({ value, onChange, phraseMode, onTogglePhrase, onSubmit }: Props) {
  return (
    <form
      className="searchbar"
      onSubmit={(e) => {
        e.preventDefault();
        onSubmit();
      }}
    >
      <input
        value={value}
        placeholder={phraseMode ? '搜索词组或例句，例如 give up' : '搜索单词或释义，例如 abandon / 放弃'}
        onChange={(e) => onChange(e.target.value)}
        aria-label="搜索关键词"
      />
      {!value ? (
        <span className="placeholder-hint">
          <ShinyText text="支持单词、释义、词组三类检索" color="var(--text-faint)" shineColor="var(--accent)" speed={3} />
        </span>
      ) : null}
      <button type="button" className="chip chip-toggle" aria-pressed={phraseMode} onClick={onTogglePhrase}>
        {phraseMode ? '词组检索' : '单词检索'}
      </button>
      <button type="submit" className="btn btn-primary">
        查询
      </button>
    </form>
  );
}
