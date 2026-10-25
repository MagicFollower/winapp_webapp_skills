import { DatabaseSync } from 'node:sqlite';
import type {
  ModuleRef,
  PhraseSearchResult,
  SearchOptions,
  WordDetail,
  WordListPage,
  WordSearchResult
} from './api-contract';

const CJK = /[㐀-䶿一-鿿豈-﫿぀-ヿ가-힯]/;

interface Row {
  id: number;
  word: string;
  phonetic: string | null;
  meaning: string;
  module_ids: string | null;
  word_html?: string | null;
  meaning_html?: string | null;
}

function escapeFtsToken(token: string): string {
  return '"' + token.replace(/"/g, '""') + '"';
}

export function buildFtsMatch(query: string): string {
  const tokens = query
    .split(/[\s,，、;；]+/)
    .map((t) => t.trim())
    .filter(Boolean);
  if (tokens.length === 0) return '';
  if (tokens.some((t) => CJK.test(t))) return tokens.map(escapeFtsToken).join(' AND ');
  return tokens.map((t) => escapeFtsToken(t) + '*').join(' AND ');
}

function likeEscape(s: string): string {
  return s.replace(/[\\%_]/g, (c) => '\\' + c);
}

export function markMatches(text: string, query: string): string {
  if (!text) return '';
  const tokens = query
    .split(/[\s,，、;；]+/)
    .map((t) => t.trim())
    .filter(Boolean);
  if (tokens.length === 0) return text;
  const pattern = tokens.map((t) => t.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')).join('|');
  return text.replace(new RegExp(`(${pattern})`, 'gi'), '<mark>$1</mark>');
}

function readModuleIds(row: Row): number[] {
  if (!row.module_ids) return [];
  return [...new Set(row.module_ids.split(',').filter(Boolean))].map(Number);
}

export class QueryService {
  private moduleCache: ModuleRef[] | null = null;

  constructor(private readonly db: DatabaseSync) {}

  getModules(): ModuleRef[] {
    if (!this.moduleCache) {
      this.moduleCache = this.db
        .prepare('SELECT id, code, name FROM modules ORDER BY sort_order')
        .all() as unknown as ModuleRef[];
    }
    return this.moduleCache;
  }

  private moduleById(): Map<number, ModuleRef> {
    return new Map(this.getModules().map((m) => [m.id, m]));
  }

  private toResult(row: Row, query: string): WordSearchResult {
    const modules = readModuleIds(row)
      .map((id) => this.moduleById().get(id))
      .filter((m): m is ModuleRef => Boolean(m))
      .sort((a, b) => a.id - b.id);
    const wordHtml = row.word_html ?? markMatches(row.word, query);
    const meaningHtml = row.meaning_html ?? markMatches(row.meaning, query);
    const matchedFields: WordSearchResult['matchedFields'] = [];
    const hit = (html: string) => html.includes('<mark>');
    if (hit(wordHtml)) matchedFields.push('word');
    if (hit(meaningHtml)) matchedFields.push('meaning');
    return {
      id: row.id,
      word: row.word,
      phonetic: row.phonetic,
      meaning: row.meaning,
      wordHtml,
      meaningHtml,
      matchedFields,
      modules
    };
  }

  private moduleFilterSql(codes: string[]): { sql: string; params: (string)[] } {
    const placeholders = codes.map(() => '?').join(', ');
    return {
      sql: `EXISTS (SELECT 1 FROM word_modules fx JOIN modules mx ON mx.id = fx.module_id
                   WHERE fx.word_id = w.id AND mx.code IN (${placeholders}))`,
      params: codes
    };
  }

  searchWords(query: string, opts: SearchOptions = {}): WordSearchResult[] {
    const q = query.trim();
    const limit = Math.min(Math.max(opts.limit ?? 100, 1), 500);
    const offset = Math.max(opts.offset ?? 0, 0);
    if (!q && !opts.firstLetter && !(opts.modules && opts.modules.length)) return [];
    const tokens = q.split(/[\s,，、;；]+/).filter(Boolean);
    const useLike = !q || CJK.test(q);

    const rows = useLike
      ? this.searchWordsLike(q, tokens, opts, limit, offset)
      : this.searchWordsFts(q, tokens, opts, limit, offset);
    return rows.map((r) => this.toResult(r, q));
  }

  private searchWordsFts(
    q: string,
    tokens: string[],
    opts: SearchOptions,
    limit: number,
    offset: number
  ): Row[] {
    const match = buildFtsMatch(q);
    if (!match) return [];
    const where: string[] = ['words_fts MATCH ?'];
    const params: (string | number)[] = [match];
    if (opts.firstLetter) {
      where.push('w.first_letter = ?');
      params.push(opts.firstLetter.toLowerCase());
    }
    if (opts.modules && opts.modules.length) {
      const f = this.moduleFilterSql(opts.modules);
      where.push(f.sql);
      params.push(...f.params);
    }
    const sql = `SELECT w.id, w.word, w.phonetic, w.meaning,
              group_concat(wm.module_id) AS module_ids,
              highlight(words_fts, 0, '<mark>', '</mark>') AS word_html,
              highlight(words_fts, 1, '<mark>', '</mark>') AS meaning_html
        FROM words_fts
        JOIN words w ON w.id = words_fts.rowid
        LEFT JOIN word_modules wm ON wm.word_id = w.id
        WHERE ${where.join(' AND ')}
        GROUP BY w.id
        ORDER BY (w.word_lower = ?) DESC, (w.word_lower LIKE ? || '%') DESC, rank
        LIMIT ? OFFSET ?`;
    const exact = tokens.length === 1 ? tokens[0].toLowerCase() : '';
    try {
      const rows = this.db.prepare(sql).all(...params, exact, exact, limit, offset) as unknown as Row[];
      if (rows.length > 0) return rows;
    } catch {
      /* FTS 语法不接受时回落 LIKE，见 doc */
    }
    return this.searchWordsLike(q, tokens, opts, limit, offset);
  }

  private searchWordsLike(
    q: string,
    tokens: string[],
    opts: SearchOptions,
    limit: number,
    offset: number
  ): Row[] {
    const where: string[] = [];
    const params: (string | number)[] = [];
    if (q) {
      const likeParts: string[] = [];
      for (const t of tokens) {
        const like = '%' + likeEscape(t.toLowerCase()) + '%';
        likeParts.push('(LOWER(w.word) LIKE ? ESCAPE \'\\\' OR w.meaning LIKE ? ESCAPE \'\\\')');
        params.push(like, like);
      }
      where.push('(' + likeParts.join(' AND ') + ')');
    }
    if (opts.firstLetter) {
      where.push('w.first_letter = ?');
      params.push(opts.firstLetter.toLowerCase());
    }
    if (opts.modules && opts.modules.length) {
      const f = this.moduleFilterSql(opts.modules);
      where.push(f.sql);
      params.push(...f.params);
    }
    const clause = where.length ? 'WHERE ' + where.join(' AND ') : '';
    const sql = `SELECT w.id, w.word, w.phonetic, w.meaning, group_concat(wm.module_id) AS module_ids
        FROM words w
        LEFT JOIN word_modules wm ON wm.word_id = w.id
        ${clause}
        GROUP BY w.id
        ORDER BY (w.word_lower = ?) DESC, w.word_lower
        LIMIT ? OFFSET ?`;
    const exact = tokens.length === 1 ? tokens[0].toLowerCase() : '';
    return this.db
      .prepare(sql)
      .all(...params, exact, limit, offset) as unknown as Row[];
  }

  searchPhrases(query: string, opts: SearchOptions = {}): PhraseSearchResult[] {
    const q = query.trim();
    if (!q) return [];
    const limit = Math.min(Math.max(opts.limit ?? 60, 1), 300);
    const tokens = q.split(/[\s,，、;；]+/).filter(Boolean);
    const moduleCodes = opts.modules ?? [];
    const useLike = CJK.test(q);

    interface PRow {
      phrase_id: number;
      word_id: number;
      word: string;
      meaning: string;
      module_id: number;
      phrase: string;
      usage: string | null;
      example: string | null;
      example_translation: string | null;
      phrase_html?: string | null;
      example_html?: string | null;
      translation_html?: string | null;
    }

    let rows: PRow[] = [];
    if (!useLike) {
      const match = buildFtsMatch(q);
      const modSql = moduleCodes.length
        ? `AND EXISTS (SELECT 1 FROM modules mm WHERE mm.id = p.module_id AND mm.code IN (${moduleCodes.map(() => '?').join(', ')}))`
        : '';
      const sql = `SELECT p.id AS phrase_id, p.word_id, w.word, w.meaning, p.module_id, p.phrase, p.usage, p.example,
              p.example_translation,
              highlight(phrases_fts, 0, '<mark>', '</mark>') AS phrase_html,
              highlight(phrases_fts, 1, '<mark>', '</mark>') AS example_html,
              highlight(phrases_fts, 2, '<mark>', '</mark>') AS translation_html
          FROM phrases_fts
          JOIN phrases p ON p.id = phrases_fts.rowid
          JOIN words w ON w.id = p.word_id
          WHERE phrases_fts MATCH ? ${modSql}
          ORDER BY rank
          LIMIT ?`;
      try {
        rows = this.db.prepare(sql).all(match, ...moduleCodes, limit) as unknown as PRow[];
      } catch {
        rows = [];
      }
    }
    if (rows.length === 0) {
      const likeParts: string[] = [];
      const params: (string | number)[] = [];
      for (const t of tokens) {
        const like = '%' + likeEscape(t) + '%';
        likeParts.push('(p.phrase LIKE ? ESCAPE \'\\\' OR p.example LIKE ? ESCAPE \'\\\' OR p.example_translation LIKE ? ESCAPE \'\\\')');
        params.push(like, like, like);
      }
      const modSql = moduleCodes.length
        ? `AND EXISTS (SELECT 1 FROM modules mm WHERE mm.id = p.module_id AND mm.code IN (${moduleCodes.map(() => '?').join(', ')}))`
        : '';
      const sql = `SELECT p.id AS phrase_id, p.word_id, w.word, w.meaning, p.module_id, p.phrase, p.usage, p.example,
              p.example_translation
          FROM phrases p
          JOIN words w ON w.id = p.word_id
          WHERE (${likeParts.join(' AND ')}) ${modSql}
          ORDER BY p.word_id, p.sort_order
          LIMIT ?`;
      rows = this.db.prepare(sql).all(...params, ...moduleCodes, limit) as unknown as PRow[];
    }

    const byId = this.moduleById();
    return rows.map((r) => ({
      phraseId: r.phrase_id,
      wordId: r.word_id,
      word: r.word,
      meaning: r.meaning,
      module: byId.get(r.module_id)!,
      phraseHtml: r.phrase_html ?? markMatches(r.phrase, q),
      usage: r.usage ?? '',
      exampleHtml: r.example_html ?? markMatches(r.example ?? '', q),
      exampleTranslationHtml: r.translation_html ?? markMatches(r.example_translation ?? '', q)
    }));
  }

  getWordDetail(wordId: number): WordDetail | null {
    const w = this.db
      .prepare('SELECT id, word, phonetic, meaning FROM words WHERE id = ?')
      .get(wordId) as unknown as { id: number; word: string; phonetic: string | null; meaning: string } | undefined;
    if (!w) return null;
    const modules = this.db
      .prepare(
        `SELECT m.id, m.code, m.name FROM modules m
         JOIN word_modules wm ON wm.module_id = m.id
         WHERE wm.word_id = ? ORDER BY m.sort_order`
      )
      .all(wordId) as unknown as ModuleRef[];
    const phraseRows = this.db
      .prepare(
        `SELECT p.id, p.module_id, p.phrase, p.usage, p.example, p.example_translation
         FROM phrases p WHERE p.word_id = ? ORDER BY p.module_id, p.sort_order`
      )
      .all(wordId) as {
      id: number;
      module_id: number;
      phrase: string;
      usage: string | null;
      example: string | null;
      example_translation: string | null;
    }[];
    const groups = modules.map((m) => ({
      module: m,
      phrases: phraseRows
        .filter((p) => p.module_id === m.id)
        .map((p) => ({
          id: p.id,
          phrase: p.phrase,
          usage: p.usage ?? '',
          example: p.example ?? '',
          exampleTranslation: p.example_translation ?? ''
        }))
    }));
    return { id: w.id, word: w.word, phonetic: w.phonetic, meaning: w.meaning, modules, groups };
  }

  browseWords(opts: { module?: string; firstLetter?: string; limit: number; offset: number }): WordListPage {
    const limit = Math.min(Math.max(opts.limit, 1), 500);
    const where: string[] = [];
    const params: (string | number)[] = [];
    if (opts.firstLetter) {
      where.push('w.first_letter = ?');
      params.push(opts.firstLetter.toLowerCase());
    }
    if (opts.module) {
      where.push(`EXISTS (SELECT 1 FROM word_modules wm JOIN modules m ON m.id = wm.module_id
                           WHERE wm.word_id = w.id AND m.code = ?)`);
      params.push(opts.module);
    }
    const clause = where.length ? 'WHERE ' + where.join(' AND ') : '';
    const total = (this.db.prepare(`SELECT COUNT(*) AS n FROM words w ${clause}`).get(...params) as { n: number }).n;
    const rows = this.db
      .prepare(
        `SELECT w.id, w.word, w.phonetic, w.meaning, group_concat(wm.module_id) AS module_ids
         FROM words w
         LEFT JOIN word_modules wm ON wm.word_id = w.id
         ${clause}
         GROUP BY w.id ORDER BY w.word_lower LIMIT ? OFFSET ?`
      )
      .all(...params, limit, opts.offset) as unknown as Row[];
    return { items: rows.map((r) => this.toResult(r, '')), total };
  }
}
