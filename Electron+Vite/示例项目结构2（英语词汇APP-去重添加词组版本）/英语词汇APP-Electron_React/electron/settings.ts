import fs from 'node:fs';
import path from 'node:path';
import type { AppSettings, ThemeMode } from './api-contract';

export interface WindowBounds {
  x: number;
  y: number;
  width: number;
  height: number;
}

interface SettingsFile {
  themeMode: ThemeMode;
  bounds?: WindowBounds;
}

const DEFAULTS: SettingsFile = { themeMode: 'system' };

export class Settings {
  private data: SettingsFile;

  constructor(private readonly filePath: string) {
    this.data = { ...DEFAULTS };
    try {
      const raw = JSON.parse(fs.readFileSync(filePath, 'utf8')) as Partial<SettingsFile>;
      if (raw.themeMode === 'light' || raw.themeMode === 'dark' || raw.themeMode === 'system') {
        this.data.themeMode = raw.themeMode;
      }
      const b = raw.bounds;
      if (b && typeof b.width === 'number' && typeof b.height === 'number') {
        this.data.bounds = { x: Number(b.x ?? 0), y: Number(b.y ?? 0), width: b.width, height: b.height };
      }
    } catch {
      this.data = { ...DEFAULTS };
    }
  }

  get themeMode(): ThemeMode {
    return this.data.themeMode;
  }

  get bounds(): WindowBounds | undefined {
    return this.data.bounds;
  }

  setThemeMode(mode: ThemeMode): AppSettings {
    this.data.themeMode = mode;
    this.flush();
    return { themeMode: mode };
  }

  setBounds(bounds: WindowBounds): void {
    this.data.bounds = bounds;
    this.flush();
  }

  private flush(): void {
    fs.mkdirSync(path.dirname(this.filePath), { recursive: true });
    fs.writeFileSync(this.filePath, JSON.stringify(this.data, null, 2), 'utf8');
  }

  snapshot(): AppSettings {
    return { themeMode: this.data.themeMode };
  }
}
