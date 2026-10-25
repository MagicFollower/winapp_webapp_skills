import fs from 'node:fs';
import path from 'node:path';
import { DatabaseSync } from 'node:sqlite';
import { importData } from './importer';

// 数据口径变了就 bump：版本号不同才重建，用户无感知。
// 1.0.1：同词同模块的重复条目改为追加（首版是覆盖，实测少收 75 条词组）。
// 1.1.0：数据源换成完整版 7 个模块（初中/高中/CET4/CET6/考研/托福/SAT），
//        entries 15,500 → 34,169、词组 83,899 → 190,926、唯一词 9,986 → 14,618。
export const DATA_VERSION = '1.1.0';

export interface DbInfo {
  dataVersion: string;
  words: number;
  phrases: number;
  importedInMs: number | null;
}

const SCHEMA = `
CREATE TABLE words (
  id INTEGER PRIMARY KEY,
  word TEXT NOT NULL UNIQUE,
  word_lower TEXT NOT NULL,
  phonetic TEXT,
  meaning TEXT NOT NULL,
  first_letter TEXT NOT NULL
);
CREATE INDEX idx_words_lower ON words(word_lower);
CREATE INDEX idx_words_letter ON words(first_letter);

CREATE TABLE modules (
  id INTEGER PRIMARY KEY,
  code TEXT NOT NULL UNIQUE,
  name TEXT NOT NULL,
  sort_order INTEGER DEFAULT 0
);

CREATE TABLE word_modules (
  word_id INTEGER NOT NULL,
  module_id INTEGER NOT NULL,
  PRIMARY KEY (word_id, module_id),
  FOREIGN KEY (word_id) REFERENCES words(id) ON DELETE CASCADE,
  FOREIGN KEY (module_id) REFERENCES modules(id) ON DELETE CASCADE
);
CREATE INDEX idx_wm_module ON word_modules(module_id);

CREATE TABLE phrases (
  id INTEGER PRIMARY KEY,
  word_id INTEGER NOT NULL,
  module_id INTEGER NOT NULL,
  phrase TEXT NOT NULL,
  usage TEXT,
  example TEXT,
  example_translation TEXT,
  sort_order INTEGER DEFAULT 0,
  FOREIGN KEY (word_id) REFERENCES words(id) ON DELETE CASCADE,
  FOREIGN KEY (module_id) REFERENCES modules(id) ON DELETE CASCADE
);
CREATE INDEX idx_phr_word_module ON phrases(word_id, module_id);

CREATE TABLE study_records (
  id INTEGER PRIMARY KEY,
  word_id INTEGER NOT NULL,
  study_date TEXT NOT NULL,
  status TEXT NOT NULL DEFAULT 'learning',
  rating INTEGER,
  reviewed_at TEXT,
  UNIQUE (word_id, study_date),
  FOREIGN KEY (word_id) REFERENCES words(id) ON DELETE CASCADE
);
CREATE INDEX idx_records_date ON study_records(study_date);
CREATE INDEX idx_records_word ON study_records(word_id);

CREATE TABLE meta (
  key TEXT PRIMARY KEY,
  value TEXT
);

CREATE VIRTUAL TABLE words_fts USING fts5(
  word, meaning,
  content='words',
  content_rowid='id',
  tokenize='unicode61'
);

CREATE VIRTUAL TABLE phrases_fts USING fts5(
  phrase, example, example_translation,
  content='phrases',
  content_rowid='id',
  tokenize='unicode61'
);
`;

const DROP = `
DROP TABLE IF EXISTS phrases_fts;
DROP TABLE IF EXISTS words_fts;
DROP TABLE IF EXISTS study_records;
DROP TABLE IF EXISTS phrases;
DROP TABLE IF EXISTS word_modules;
DROP TABLE IF EXISTS modules;
DROP TABLE IF EXISTS meta;
DROP TABLE IF EXISTS words;
`;

export function createTables(db: DatabaseSync): void {
  db.exec(SCHEMA);
}

export function dropAllTables(db: DatabaseSync): void {
  db.exec(DROP);
}

function readMeta(db: DatabaseSync, key: string): string | undefined {
  const row = db.prepare('SELECT value FROM meta WHERE key = ?').get(key) as { value: string } | undefined;
  return row?.value;
}

function writeMeta(db: DatabaseSync, key: string, value: string): void {
  const exists = db.prepare('SELECT 1 AS hit FROM meta WHERE key = ?').get(key);
  if (exists) db.prepare('UPDATE meta SET value = ? WHERE key = ?').run(value, key);
  else db.prepare('INSERT INTO meta (key, value) VALUES (?, ?)').run(key, value);
}

function counts(db: DatabaseSync): { words: number; phrases: number } {
  const w = db.prepare('SELECT COUNT(*) AS n FROM words').get() as { n: number };
  const p = db.prepare('SELECT COUNT(*) AS n FROM phrases').get() as { n: number };
  return { words: w.n, phrases: p.n };
}

export interface EnsureResult {
  db: DatabaseSync;
  info: DbInfo;
}

export function ensureDatabase(dbPath: string, resourcesDir: string, log: (line: string) => void): EnsureResult {
  const existed = fs.existsSync(dbPath);
  const db = new DatabaseSync(dbPath);
  db.exec('PRAGMA journal_mode = WAL');
  db.exec('PRAGMA synchronous = NORMAL');
  const t0 = Date.now();
  log(`node:sqlite open ms=${Date.now() - t0} wal=${String(
    (db.prepare('PRAGMA journal_mode').get() as { journal_mode: string }).journal_mode
  )}`);

  if (!existed) {
    log('first run: create schema + import');
    createTables(db);
    const imported = seed(db, resourcesDir, log);
    writeMeta(db, 'dataVersion', DATA_VERSION);
    return { db, info: { ...counts(db), dataVersion: DATA_VERSION, importedInMs: imported } };
  }

  const stored = readMeta(db, 'dataVersion');
  if (stored !== DATA_VERSION) {
    log(`dataVersion ${stored ?? '(none)'} -> ${DATA_VERSION}: rebuild + reimport`);
    const t = Date.now();
    dropAllTables(db);
    createTables(db);
    const imported = seed(db, resourcesDir, log);
    writeMeta(db, 'dataVersion', DATA_VERSION);
    log(`reimport ms=${Date.now() - t}`);
    return { db, info: { ...counts(db), dataVersion: DATA_VERSION, importedInMs: imported } };
  }

  const cached = readMeta(db, 'importedInMs');
  return {
    db,
    info: { ...counts(db), dataVersion: stored, importedInMs: cached ? Number(cached) : null }
  };
}

function seed(db: DatabaseSync, resourcesDir: string, log: (line: string) => void): number {
  const manifest = JSON.parse(fs.readFileSync(path.join(resourcesDir, 'sources.json'), 'utf8')) as {
    files: { code: string; name: string; file: string }[];
  };
  const t = Date.now();
  const insertModule = db.prepare('INSERT INTO modules (code, name, sort_order) VALUES (?, ?, ?)');
  manifest.files.forEach((f, i) => insertModule.run(f.code, f.name, i + 1));
  const stats = importData(
    db,
    manifest.files.map((f) => ({ moduleId: f.code, filePath: path.join(resourcesDir, f.file) }))
  );
  const tFts = Date.now();
  db.exec("INSERT INTO words_fts(words_fts) VALUES('rebuild')");
  db.exec("INSERT INTO phrases_fts(phrases_fts) VALUES('rebuild')");
  const ftsMs = Date.now() - tFts;
  const total = Date.now() - t;
  writeMeta(db, 'importedInMs', String(total));
  log(
    `import entries=${stats.sourceEntries} uniqueWords=${stats.uniqueWords} phraseRows=${stats.phraseRows} rows+txn ms=${stats.ms} fts-rebuild ms=${ftsMs} total ms=${total}`
  );
  return total;
}
