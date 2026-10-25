export interface ModuleRef {
  id: number;
  code: string;
  name: string;
}

export interface Phrase {
  id: number;
  phrase: string;
  usage: string;
  example: string;
  exampleTranslation: string;
}

export interface ModulePhrases {
  module: ModuleRef;
  phrases: Phrase[];
}

export interface WordSearchResult {
  id: number;
  word: string;
  phonetic: string | null;
  meaning: string;
  wordHtml: string;
  meaningHtml: string;
  matchedFields: Array<'word' | 'meaning'>;
  modules: ModuleRef[];
}

export interface PhraseSearchResult {
  phraseId: number;
  wordId: number;
  word: string;
  meaning: string;
  module: ModuleRef;
  phraseHtml: string;
  usage: string;
  exampleHtml: string;
  exampleTranslationHtml: string;
}

export interface WordDetail {
  id: number;
  word: string;
  phonetic: string | null;
  meaning: string;
  modules: ModuleRef[];
  groups: ModulePhrases[];
}

export interface StudyWord {
  wordId: number;
  word: string;
  meaning: string;
  status: string;
  rating: number | null;
  reviewedAt: string | null;
  modules: ModuleRef[];
  groups: ModulePhrases[];
}

export interface HeatmapDay {
  date: string;
  count: number;
  mastered: number;
  level: number;
}

export interface StudyStats {
  today: string;
  todayCount: number;
  todayMastered: number;
  streakDays: number;
  totalLearned: number;
  totalMastered: number;
  monthDays: number;
  monthWords: number;
  monthMastered: number;
  uniqueWords: number;
}

export type ThemeMode = 'system' | 'light' | 'dark';

export interface AppSettings {
  themeMode: ThemeMode;
}

export interface WordListPage {
  items: WordSearchResult[];
  total: number;
}

export interface SearchOptions {
  modules?: string[];
  firstLetter?: string;
  limit?: number;
  offset?: number;
}

export interface SampleOptions {
  modules?: string[];
  count?: number;
}

export interface Api {
  ping(): Promise<'pong'>;
  getModules(): Promise<ModuleRef[]>;
  searchWords(query: string, opts?: SearchOptions): Promise<WordSearchResult[]>;
  searchPhrases(query: string, opts?: SearchOptions): Promise<PhraseSearchResult[]>;
  getWordDetail(wordId: number): Promise<WordDetail | null>;
  browseWords(opts: { module?: string; firstLetter?: string; limit: number; offset: number }): Promise<WordListPage>;
  getDailyWords(date: string, opts?: SampleOptions): Promise<StudyWord[]>;
  resampleDailyWords(date: string, opts?: SampleOptions): Promise<StudyWord[]>;
  updateStudyRecord(wordId: number, rating: number, date: string): Promise<void>;
  getWordsByDate(date: string): Promise<StudyWord[]>;
  getHeatmap(opts: { months: number }): Promise<HeatmapDay[]>;
  getStudyStats(): Promise<StudyStats>;
  getDbInfo(): Promise<{ dataVersion: string; words: number; phrases: number; importedInMs: number | null }>;
  getSettings(): Promise<AppSettings>;
  setThemeMode(mode: ThemeMode): Promise<AppSettings>;
}
