import { create } from 'zustand';
import type { ThemeMode } from '../../electron/api-contract';
import { todayLocalDate } from './dates';

export type View = 'home' | 'study' | 'search';

const LS_GOAL = 'word-app.dailyGoal';
const LS_SAMPLE_MODULES = 'word-app.sampleModules';

function readNumber(key: string, fallback: number): number {
  const raw = localStorage.getItem(key);
  const n = raw === null ? NaN : Number(raw);
  return Number.isFinite(n) ? n : fallback;
}

function readStringArray(key: string): string[] {
  try {
    const raw = localStorage.getItem(key);
    const parsed = raw ? (JSON.parse(raw) as unknown) : [];
    return Array.isArray(parsed) ? parsed.filter((x): x is string => typeof x === 'string') : [];
  } catch {
    return [];
  }
}

interface AppState {
  view: View;
  themeMode: ThemeMode;
  selectedDate: string;
  query: string;
  searchModules: string[];
  searchPhrases: boolean;
  detailWordId: number | null;
  dailyGoal: number;
  sampleModules: string[];
  refreshKey: number;
  setView: (view: View) => void;
  setThemeMode: (mode: ThemeMode) => void;
  setSelectedDate: (date: string) => void;
  setQuery: (query: string) => void;
  toggleSearchModule: (code: string) => void;
  setSearchPhrases: (on: boolean) => void;
  setDetailWordId: (id: number | null) => void;
  setDailyGoal: (n: number) => void;
  toggleSampleModule: (code: string) => void;
  bump: () => void;
}

export const useAppStore = create<AppState>((set, get) => ({
  view: 'search',
  themeMode: 'system',
  selectedDate: todayLocalDate(),
  query: '',
  searchModules: [],
  searchPhrases: false,
  detailWordId: null,
  dailyGoal: readNumber(LS_GOAL, 20),
  sampleModules: readStringArray(LS_SAMPLE_MODULES),
  refreshKey: 0,

  setView: (view) => set({ view }),
  setThemeMode: (themeMode) => {
    set({ themeMode });
    void window.api.setThemeMode(themeMode);
  },
  setSelectedDate: (selectedDate) => set({ selectedDate }),
  setQuery: (query) => set({ query }),
  toggleSearchModule: (code) =>
    set({
      searchModules: get().searchModules.includes(code)
        ? get().searchModules.filter((c) => c !== code)
        : [...get().searchModules, code]
    }),
  setSearchPhrases: (searchPhrases) => set({ searchPhrases }),
  setDetailWordId: (detailWordId) => set({ detailWordId }),
  setDailyGoal: (dailyGoal) => {
    const clamped = Math.min(Math.max(Math.round(dailyGoal) || 1, 1), 200);
    localStorage.setItem(LS_GOAL, String(clamped));
    set({ dailyGoal: clamped });
  },
  toggleSampleModule: (code) => {
    const current = get().sampleModules;
    const next = current.includes(code) ? current.filter((c) => c !== code) : [...current, code];
    localStorage.setItem(LS_SAMPLE_MODULES, JSON.stringify(next));
    set({ sampleModules: next });
  },
  bump: () => set({ refreshKey: get().refreshKey + 1 })
}));
