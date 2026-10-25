import { contextBridge, ipcRenderer } from 'electron';
import type { Api, SampleOptions, SearchOptions, ThemeMode } from './api-contract';

const api: Api = {
  ping: () => ipcRenderer.invoke('word:ping'),
  getModules: () => ipcRenderer.invoke('word:modules'),
  searchWords: (query: string, opts?: SearchOptions) => ipcRenderer.invoke('word:searchWords', query, opts),
  searchPhrases: (query: string, opts?: SearchOptions) => ipcRenderer.invoke('word:searchPhrases', query, opts),
  getWordDetail: (wordId: number) => ipcRenderer.invoke('word:detail', wordId),
  browseWords: (opts: { module?: string; firstLetter?: string; limit: number; offset: number }) =>
    ipcRenderer.invoke('word:browse', opts),
  getDailyWords: (date: string, opts?: SampleOptions) => ipcRenderer.invoke('word:daily', date, opts),
  resampleDailyWords: (date: string, opts?: SampleOptions) => ipcRenderer.invoke('word:resample', date, opts),
  updateStudyRecord: (wordId: number, rating: number, date: string) =>
    ipcRenderer.invoke('word:rate', wordId, rating, date),
  getWordsByDate: (date: string) => ipcRenderer.invoke('word:wordsByDate', date),
  getHeatmap: (opts: { months: number }) => ipcRenderer.invoke('word:heatmap', opts),
  getStudyStats: () => ipcRenderer.invoke('word:stats'),
  getDbInfo: () => ipcRenderer.invoke('word:dbInfo'),
  getSettings: () => ipcRenderer.invoke('word:getSettings'),
  setThemeMode: (mode: ThemeMode) => ipcRenderer.invoke('word:setThemeMode', mode)
};

contextBridge.exposeInMainWorld('api', api);
