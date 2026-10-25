import { useCallback, useEffect, useState } from 'react';
import BlurText from '../ui/BlurText';
import SearchBar from './SearchBar';
import ModuleFilter from './ModuleFilter';
import SearchResults from './SearchResults';
import WordDetail from './WordDetail';
import { useAppStore } from '../../lib/store';
import { todayLocalDate } from '../../lib/dates';
import type { ModuleRef, PhraseSearchResult, WordDetail as WordDetailType, WordSearchResult } from '../../../electron/api-contract';

export default function SearchView() {
  const query = useAppStore((s) => s.query);
  const setQuery = useAppStore((s) => s.setQuery);
  const modules = useAppStore((s) => s.searchModules);
  const toggleModule = useAppStore((s) => s.toggleSearchModule);
  const phraseMode = useAppStore((s) => s.searchPhrases);
  const setPhraseMode = useAppStore((s) => s.setSearchPhrases);
  const detailWordId = useAppStore((s) => s.detailWordId);
  const setDetailWordId = useAppStore((s) => s.setDetailWordId);
  const bump = useAppStore((s) => s.bump);

  const [allModules, setAllModules] = useState<ModuleRef[]>([]);
  const [firstLetter, setFirstLetter] = useState<string | null>(null);
  const [words, setWords] = useState<WordSearchResult[]>([]);
  const [phrases, setPhrases] = useState<PhraseSearchResult[]>([]);
  const [detail, setDetail] = useState<WordDetailType | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    void window.api.getModules().then(setAllModules);
  }, []);

  const run = useCallback(async () => {
    setLoading(true);
    try {
      if (query.trim()) {
        if (phraseMode) {
          setPhrases(await window.api.searchPhrases(query, { modules, limit: 120 }));
          setWords([]);
        } else {
          setWords(await window.api.searchWords(query, { modules, limit: 200 }));
          setPhrases([]);
        }
      } else if (firstLetter) {
        const page = await window.api.browseWords({ module: modules[0], firstLetter, limit: 200, offset: 0 });
        setWords(page.items);
        setPhrases([]);
      } else {
        const page = await window.api.browseWords({ module: modules[0], limit: 120, offset: 0 });
        setWords(page.items);
        setPhrases([]);
      }
    } finally {
      setLoading(false);
    }
  }, [query, phraseMode, modules, firstLetter]);

  useEffect(() => {
    const t = window.setTimeout(() => void run(), 240);
    return () => window.clearTimeout(t);
  }, [run]);

  useEffect(() => {
    if (detailWordId === null) {
      setDetail(null);
      return;
    }
    void window.api.getWordDetail(detailWordId).then(setDetail);
  }, [detailWordId]);

  const addToToday = async (wordId: number) => {
    await window.api.updateStudyRecord(wordId, 2, todayLocalDate());
    bump();
  };

  return (
    <div className="stack">
      <div>
        <BlurText text="查词" delay={40} className="panel-title" animateBy="letters" />
        <p className="panel-hint" style={{ marginTop: -6 }}>
          全文检索走 SQLite FTS5；中文子串与零命中时自动回落 LIKE。命中片段用高亮标出。
        </p>
      </div>

      <SearchBar
        value={query}
        onChange={setQuery}
        phraseMode={phraseMode}
        onTogglePhrase={() => setPhraseMode(!phraseMode)}
        onSubmit={() => void run()}
      />

      <ModuleFilter
        modules={allModules}
        selected={modules}
        onToggle={toggleModule}
        firstLetter={firstLetter}
        onLetter={setFirstLetter}
      />

      <div className="search-grid">
        <SearchResults
          mode={query.trim() && phraseMode ? 'phrase' : 'word'}
          words={words}
          phrases={phrases}
          selectedId={detailWordId}
          loading={loading}
          onSelectWord={setDetailWordId}
        />
        {detail ? <WordDetail detail={detail} onAddToToday={(id) => void addToToday(id)} /> : (
          <div className="panel">
            <h3 className="panel-title">单词详情</h3>
            <p className="panel-hint">从左侧选一个单词，这里会显示它的模块标签与按模块分组的词组。</p>
          </div>
        )}
      </div>
    </div>
  );
}
