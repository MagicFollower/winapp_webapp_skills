import { ipcMain } from 'electron';
import type { DbInfo } from './db';
import type { QueryService } from './queries';
import type { Settings } from './settings';
import type { StudyService } from './study';
import type { SampleOptions, SearchOptions, ThemeMode } from './api-contract';

export interface Services {
  queries: QueryService;
  study: StudyService;
  settings: Settings;
  info: DbInfo;
  log: (line: string) => void;
}

export function registerIpc(s: Services): void {
  const h = (channel: string, fn: (...args: any[]) => unknown) => {
    ipcMain.handle(channel, (_event, ...args) => fn(...args));
  };

  h('word:ping', () => 'pong');
  h('word:modules', () => s.queries.getModules());
  h('word:searchWords', (query: string, opts?: SearchOptions) => {
    const t = Date.now();
    const rows = s.queries.searchWords(query, opts);
    s.log(`searchWords q=${JSON.stringify(query)} opts=${JSON.stringify(opts ?? {})} rows=${rows.length} ms=${Date.now() - t}`);
    return rows;
  });
  h('word:searchPhrases', (query: string, opts?: SearchOptions) => {
    const t = Date.now();
    const rows = s.queries.searchPhrases(query, opts);
    s.log(`searchPhrases q=${JSON.stringify(query)} rows=${rows.length} ms=${Date.now() - t}`);
    return rows;
  });
  h('word:detail', (wordId: number) => s.queries.getWordDetail(Number(wordId)));
  h('word:browse', (opts: { module?: string; firstLetter?: string; limit?: number; offset?: number }) =>
    s.queries.browseWords({
      module: opts.module,
      firstLetter: opts.firstLetter,
      limit: opts.limit ?? 60,
      offset: opts.offset ?? 0
    })
  );
  h('word:daily', (date: string, opts?: SampleOptions) => s.study.ensureDailySample(date, opts));
  h('word:resample', (date: string, opts?: SampleOptions) => s.study.resampleDailyWords(date, opts));
  h('word:rate', (wordId: number, rating: number, date: string) => s.study.updateStudyRecord(Number(wordId), Number(rating), date));
  h('word:wordsByDate', (date: string) => s.study.getWordsByDate(date));
  h('word:heatmap', (opts: { months?: number }) => s.study.getHeatmap(opts?.months ?? 6));
  h('word:stats', () => s.study.getStats());
  h('word:dbInfo', () => s.info);
  h('word:getSettings', () => s.settings.snapshot());
  h('word:setThemeMode', (mode: ThemeMode) => s.settings.setThemeMode(mode));
}
