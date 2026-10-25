import { useEffect, useState } from 'react';
import Aurora from './ui/Aurora';
import HomeView from './home/HomeView';
import StudyView from './study/StudyView';
import SearchView from './search/SearchView';
import { useAppStore, type View } from '../lib/store';
import type { ThemeMode } from '../../electron/api-contract';

function useSystemDark(): boolean {
  const [dark, setDark] = useState(() => window.matchMedia('(prefers-color-scheme: dark)').matches);
  useEffect(() => {
    const mq = window.matchMedia('(prefers-color-scheme: dark)');
    const onChange = (e: MediaQueryListEvent) => setDark(e.matches);
    mq.addEventListener('change', onChange);
    return () => mq.removeEventListener('change', onChange);
  }, []);
  return dark;
}

const MODES: Array<{ mode: ThemeMode; label: string }> = [
  { mode: 'system', label: '跟随系统' },
  { mode: 'light', label: '亮' },
  { mode: 'dark', label: '暗' }
];

export default function App() {
  const view = useAppStore((s) => s.view);
  const setView = useAppStore((s) => s.setView);
  const themeMode = useAppStore((s) => s.themeMode);
  const setThemeMode = useAppStore((s) => s.setThemeMode);
  const systemDark = useSystemDark();
  // 顶栏那行列模块名以前写死在 JSX 里，数据源从 4 个模块变 7 个之后就一直在漏。
  // 现在从 modules 表取，顺序也跟 sort_order 一致。
  const [moduleNames, setModuleNames] = useState<string[]>([]);

  useEffect(() => {
    void window.api.getModules().then((ms) => setModuleNames(ms.map((m) => m.name)));
  }, []);

  useEffect(() => {
    void window.api.getSettings().then((s) => useAppStore.setState({ themeMode: s.themeMode }));
  }, []);

  useEffect(() => {
    // 'system' 时不写 data-theme，让 @media (prefers-color-scheme) 驱动令牌；
    // 写了就等于把当前系统值钉死，之后跟随系统会失效。
    if (themeMode === 'system') {
      delete document.documentElement.dataset.theme;
      document.documentElement.style.colorScheme = 'light dark';
    } else {
      document.documentElement.dataset.theme = themeMode;
      document.documentElement.style.colorScheme = themeMode;
    }
  }, [themeMode]);

  const resolved: 'light' | 'dark' = themeMode === 'system' ? (systemDark ? 'dark' : 'light') : themeMode;

  return (
    <div className="app-shell" data-view={view}>
      {view === 'search' || view === 'study' ? (
        <div className="bg-layer" aria-hidden>
          <Aurora colorStops={['#2f6fed', '#2f9e8f', '#6b5bd2']} amplitude={0.7} lightMode={resolved === 'light'} />
        </div>
      ) : null}

      <header className="topbar">
        <div className="brand">
          <strong style={{ fontSize: '1.05rem' }}>单词学习</strong>
          {moduleNames.length > 0 ? (
            <span className="brand-sub" title={moduleNames.join(' · ')}>
              {moduleNames.join(' · ')}
            </span>
          ) : null}
        </div>
        <div className="topbar-right">
          <nav className="segmented" aria-label="视图">
            {(['home', 'search', 'study'] as View[]).map((v) => (
              <button key={v} type="button" aria-pressed={view === v} onClick={() => setView(v)}>
                {v === 'home' ? '主页' : v === 'search' ? '查询' : '学习'}
              </button>
            ))}
          </nav>
          <div className="segmented" aria-label="主题">
            {MODES.map((m) => (
              <button key={m.mode} type="button" aria-pressed={themeMode === m.mode} onClick={() => setThemeMode(m.mode)}>
                {m.label}
              </button>
            ))}
          </div>
        </div>
      </header>

      <main className="main-area">
        {view === 'home' ? <HomeView colorScheme={resolved} /> : null}
        {view === 'study' ? <StudyView colorScheme={resolved} /> : null}
        {view === 'search' ? <SearchView /> : null}
      </main>
    </div>
  );
}
