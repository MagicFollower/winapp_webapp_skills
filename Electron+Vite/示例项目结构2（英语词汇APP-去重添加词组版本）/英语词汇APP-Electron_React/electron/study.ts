import { DatabaseSync } from 'node:sqlite';
import type { HeatmapDay, ModuleRef, SampleOptions, StudyStats, StudyWord } from './api-contract';
import { QueryService } from './queries';

export function todayLocalDate(now = new Date()): string {
  const y = now.getFullYear();
  const m = String(now.getMonth() + 1).padStart(2, '0');
  const d = String(now.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

function shiftDays(date: string, delta: number): string {
  const [y, m, d] = date.split('-').map(Number);
  const dt = new Date(y, m - 1, d + delta);
  return todayLocalDate(dt);
}

function monthsAgo(months: number, now = new Date()): string {
  const dt = new Date(now.getFullYear(), now.getMonth() - months, now.getDate());
  return todayLocalDate(dt);
}

export function countToLevel(count: number): number {
  if (count <= 0) return 0;
  if (count <= 10) return 1;
  if (count <= 20) return 2;
  if (count <= 50) return 3;
  return 4;
}

const DEFAULT_DAILY_COUNT = 20;

interface RecordRow {
  word_id: number;
  status: string;
  rating: number | null;
  reviewed_at: string | null;
}

export class StudyService {
  constructor(
    private readonly db: DatabaseSync,
    private readonly queries: QueryService
  ) {}

  private scopeCondition(opts: SampleOptions): { sql: string; params: string[] } {
    const codes = (opts.modules ?? []).filter(Boolean);
    if (codes.length === 0) return { sql: '', params: [] };
    return {
      sql: `AND EXISTS (SELECT 1 FROM word_modules wm JOIN modules m ON m.id = wm.module_id
                        WHERE wm.word_id = w.id AND m.code IN (${codes.map(() => '?').join(', ')}))`,
      params: codes
    };
  }

  getWordsByDate(date: string): StudyWord[] {
    const rows = this.db
      .prepare(
        'SELECT word_id, status, rating, reviewed_at FROM study_records WHERE study_date = ? ORDER BY id'
      )
      .all(date) as unknown as RecordRow[];
    return rows.map((r) => this.toStudyWord(r));
  }

  private toStudyWord(row: RecordRow): StudyWord {
    const detail = this.queries.getWordDetail(row.word_id);
    if (!detail) {
      return {
        wordId: row.word_id,
        word: '(missing)',
        meaning: '',
        status: row.status,
        rating: row.rating,
        reviewedAt: row.reviewed_at,
        modules: [] as ModuleRef[],
        groups: []
      };
    }
    return {
      wordId: detail.id,
      word: detail.word,
      meaning: detail.meaning,
      status: row.status,
      rating: row.rating,
      reviewedAt: row.reviewed_at,
      modules: detail.modules,
      groups: detail.groups
    };
  }

  // 随机抽样：同一天幂等——当天已有记录就直接返回那批，换批要走 resampleDailyWords。
  ensureDailySample(date: string, opts: SampleOptions = {}): StudyWord[] {
    const existing = this.getWordsByDate(date);
    if (existing.length > 0) return existing;
    return this.sample(date, opts);
  }

  resampleDailyWords(date: string, opts: SampleOptions = {}): StudyWord[] {
    this.db.prepare('DELETE FROM study_records WHERE study_date = ?').run(date);
    return this.sample(date, opts);
  }

  resetDay(date: string): void {
    this.db.prepare('DELETE FROM study_records WHERE study_date = ?').run(date);
  }

  private sample(date: string, opts: SampleOptions): StudyWord[] {
    const count = Math.min(Math.max(opts.count ?? DEFAULT_DAILY_COUNT, 1), 200);
    const scope = this.scopeCondition(opts);
    const picked = this.db
      .prepare(
        `SELECT w.id FROM words w WHERE 1 = 1 ${scope.sql} ORDER BY RANDOM() LIMIT ?`
      )
      .all(...scope.params, count) as unknown as { id: number }[];
    if (picked.length === 0) return [];
    const insert = this.db.prepare(
      "INSERT INTO study_records (word_id, study_date, status) VALUES (?, ?, 'learning')"
    );
    this.db.exec('BEGIN');
    try {
      for (const p of picked) insert.run(p.id, date);
      this.db.exec('COMMIT');
    } catch (err) {
      this.db.exec('ROLLBACK');
      throw err;
    }
    return this.getWordsByDate(date);
  }

  updateStudyRecord(wordId: number, rating: number, date: string): void {
    const status = rating >= 3 ? 'mastered' : 'review';
    const existing = this.db
      .prepare('SELECT id FROM study_records WHERE word_id = ? AND study_date = ?')
      .get(wordId, date) as unknown as { id: number } | undefined;
    if (existing) {
      this.db
        .prepare('UPDATE study_records SET rating = ?, status = ?, reviewed_at = ? WHERE id = ?')
        .run(rating, status, new Date().toISOString(), existing.id);
      return;
    }
    this.db
      .prepare(
        'INSERT INTO study_records (word_id, study_date, status, rating, reviewed_at) VALUES (?, ?, ?, ?, ?)'
      )
      .run(wordId, date, status, rating, new Date().toISOString());
  }

  getHeatmap(months: number): HeatmapDay[] {
    const from = monthsAgo(months);
    const rows = this.db
      .prepare(
        `SELECT study_date AS date,
                COUNT(*) AS count,
                SUM(CASE WHEN status = 'mastered' THEN 1 ELSE 0 END) AS mastered
         FROM study_records
         WHERE study_date >= ?
         GROUP BY study_date
         ORDER BY study_date`
      )
      .all(from) as { date: string; count: number; mastered: number }[];
    return rows.map((r) => ({ date: r.date, count: r.count, mastered: r.mastered, level: countToLevel(r.count) }));
  }

  getStats(): StudyStats {
    const today = todayLocalDate();
    const scalar = (sql: string, ...params: (string | number)[]) =>
      (this.db.prepare(sql).get(...params) as unknown as { n: number }).n;

    const todayRow = this.db
      .prepare(
        `SELECT COUNT(*) AS count, SUM(CASE WHEN status = 'mastered' THEN 1 ELSE 0 END) AS mastered
         FROM study_records WHERE study_date = ?`
      )
      .get(today) as { count: number; mastered: number };

    const days = new Set(
      (this.db.prepare('SELECT DISTINCT study_date AS d FROM study_records').all() as { d: string }[]).map(
        (r) => r.d
      )
    );
    let streak = 0;
    let cursor = days.has(today) ? today : shiftDays(today, -1);
    while (days.has(cursor)) {
      streak += 1;
      cursor = shiftDays(cursor, -1);
    }

    const monthPrefix = today.slice(0, 7);
    const monthRow = this.db
      .prepare(
        `SELECT COUNT(DISTINCT study_date) AS d, COUNT(DISTINCT word_id) AS w,
                SUM(CASE WHEN status = 'mastered' THEN 1 ELSE 0 END) AS m
         FROM study_records WHERE substr(study_date, 1, 7) = ?`
      )
      .get(monthPrefix) as { d: number; w: number; m: number };

    return {
      today,
      todayCount: todayRow.count ?? 0,
      todayMastered: todayRow.mastered ?? 0,
      streakDays: streak,
      totalLearned: scalar('SELECT COUNT(DISTINCT word_id) AS n FROM study_records'),
      totalMastered: scalar("SELECT COUNT(DISTINCT word_id) AS n FROM study_records WHERE status = 'mastered'"),
      monthDays: monthRow.d ?? 0,
      monthWords: monthRow.w ?? 0,
      monthMastered: monthRow.m ?? 0,
      uniqueWords: scalar('SELECT COUNT(*) AS n FROM words')
    };
  }
}
