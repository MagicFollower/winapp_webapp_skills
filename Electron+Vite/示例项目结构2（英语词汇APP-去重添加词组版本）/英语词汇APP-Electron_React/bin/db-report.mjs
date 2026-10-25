// 数据库结构与空间取证：把 doc/数据库表结构与数据流转设计.md 里的数字变成可复现命令。
//   node bin/db-report.mjs                 用当前 resources/ 建一个临时库、出报告、跑完删掉
//   node bin/db-report.mjs --db <path>     只读打开已有库出报告（不动它）
//   node bin/db-report.mjs --keep          临时库留在 .dbreport/words.db
//   node bin/db-report.mjs --json <out>    报告另存一份 JSON
// 只读语义：全程不写目标库（临时库除外），不做任何删除实验。
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createRequire } from 'node:module';
import { DatabaseSync } from 'node:sqlite';

const require = createRequire(import.meta.url);
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');

const argv = process.argv.slice(2);
const flagValue = (name) => {
  const i = argv.indexOf(name);
  return i >= 0 ? argv[i + 1] : undefined;
};
const keep = argv.includes('--keep');
const jsonOut = flagValue('--json');
const targetDb = flagValue('--db');

let db;
let tmpDir = null;
const logs = [];
if (targetDb) {
  db = new DatabaseSync(targetDb, { readOnly: true });
  logs.push(`open read-only ${targetDb}`);
} else {
  const { ensureDatabase } = require(path.join(root, 'dist-electron/db.js'));
  tmpDir = path.join(root, '.dbreport');
  fs.rmSync(tmpDir, { recursive: true, force: true });
  fs.mkdirSync(tmpDir, { recursive: true });
  const ensured = ensureDatabase(path.join(tmpDir, 'words.db'), path.join(root, 'resources'), (l) => logs.push(l));
  db = ensured.db;
}

const all = (sql, ...p) => db.prepare(sql).all(...p);
const one = (sql, ...p) => db.prepare(sql).get(...p);
const scalar = (sql, ...p) => Object.values(one(sql, ...p))[0];

const report = { logs, runtime: { node: process.version, sqlite: scalar('SELECT sqlite_version() AS v') } };

report.pragmas = {
  journal_mode: scalar('PRAGMA journal_mode'),
  encoding: scalar('PRAGMA encoding'),
  page_size: scalar('PRAGMA page_size'),
  page_count: scalar('PRAGMA page_count'),
  freelist_count: scalar('PRAGMA freelist_count'),
  foreign_keys: scalar('PRAGMA foreign_keys'),
  synchronous: scalar('PRAGMA synchronous'),
  integrity_check: scalar('PRAGMA integrity_check'),
  fts5: all("SELECT 1 AS x FROM pragma_compile_options WHERE compile_options = 'ENABLE_FTS5'").length
};
report.dbBytes = fs.statSync(targetDb ?? path.join(tmpDir, 'words.db')).size;

const master = all('SELECT type, name, tbl_name FROM sqlite_master ORDER BY type, name');
report.objects = {
  realTables: master.filter((r) => r.type === 'table' && !/_fts($|_)/.test(r.name)).map((r) => r.name),
  ftsVirtual: master.filter((r) => r.type === 'table' && /_fts$/.test(r.name)).map((r) => r.name),
  ftsShadow: master.filter((r) => r.type === 'table' && /_fts_/.test(r.name)).map((r) => r.name),
  indexes: master.filter((r) => r.type === 'index').map((r) => r.name),
  counts: {
    tables: master.filter((r) => r.type === 'table').length,
    indexes: master.filter((r) => r.type === 'index').length,
    triggers: master.filter((r) => r.type === 'trigger').length,
    views: master.filter((r) => r.type === 'view').length
  }
};

const logical = report.objects.realTables.filter((t) => t !== 'sqlite_sequence');
report.counts = Object.fromEntries(logical.map((t) => [t, scalar(`SELECT COUNT(*) AS n FROM ${t}`)]));
for (const v of report.objects.ftsVirtual) report.counts[v] = scalar(`SELECT COUNT(*) AS n FROM ${v}`);

report.columns = Object.fromEntries(logical.map((t) => [t, all(`PRAGMA table_info(${t})`)]));
report.indexes = Object.fromEntries(
  logical.map((t) => [
    t,
    all(`PRAGMA index_list(${t})`).map((ix) => ({
      name: ix.name,
      unique: ix.unique,
      origin: ix.origin,
      cols: all(`PRAGMA index_info(${ix.name})`).map((c) => c.name)
    }))
  ])
);
report.foreignKeys = Object.fromEntries(logical.map((t) => [t, all(`PRAGMA foreign_key_list(${t})`)]));
report.ddl = Object.fromEntries(
  master.map((r) => [r.name, one('SELECT sql AS s FROM sqlite_master WHERE name = ?', r.name)?.s ?? null])
);

try {
  report.space = all(
    `SELECT name, SUM(pgsize) AS bytes, COUNT(*) AS pages FROM dbstat GROUP BY name ORDER BY bytes DESC`
  );
  report.spaceTotal = scalar('SELECT SUM(pgsize) AS n FROM dbstat');
} catch (e) {
  report.space = 'dbstat 不可用: ' + e.message;
}

report.modules = all('SELECT * FROM modules ORDER BY sort_order');
report.meta = all('SELECT * FROM meta ORDER BY key');
report.perModule = all(
  `SELECT m.code, m.name, m.sort_order,
          (SELECT COUNT(*) FROM word_modules wm WHERE wm.module_id = m.id) AS words,
          (SELECT COUNT(*) FROM phrases p WHERE p.module_id = m.id) AS phrases
   FROM modules m ORDER BY m.sort_order`
);
report.shape = {
  moduleCountHistogram: all(
    `SELECT n_modules, COUNT(*) AS words FROM (SELECT word_id, COUNT(*) AS n_modules FROM word_modules GROUP BY word_id) GROUP BY n_modules ORDER BY n_modules`
  ),
  phraseNulls: one(
    `SELECT SUM(usage IS NULL) AS usage_null, SUM(example IS NULL) AS example_null, SUM(example_translation IS NULL) AS tr_null, COUNT(*) AS total FROM phrases`
  ),
  phraseNullsByModule: all(
    `SELECT m.code, COUNT(*) AS total, SUM(p.usage IS NULL) AS usage_null FROM phrases p JOIN modules m ON m.id = p.module_id GROUP BY m.id ORDER BY m.sort_order`
  ),
  wordNulls: one(
    `SELECT SUM(phonetic IS NULL) AS phonetic_null, SUM(meaning = '') AS meaning_empty, COUNT(*) AS total FROM words`
  ),
  phrasesPerWordModule: one(
    `SELECT ROUND(AVG(n), 2) AS avg_phrases, MAX(n) AS max_phrases FROM (SELECT COUNT(*) AS n FROM phrases GROUP BY word_id, module_id)`
  ),
  wordsWithoutPhrases: all(
    `SELECT w.word FROM words w WHERE NOT EXISTS (SELECT 1 FROM phrases p WHERE p.word_id = w.id) ORDER BY w.word`
  ),
  firstLetterSpread: one(`SELECT COUNT(DISTINCT first_letter) AS letters FROM words`)
};

const { QueryService } = require(path.join(root, 'dist-electron/queries.js'));
const queries = new QueryService(db);
const timeIt = (label, fn) => {
  const runs = [];
  let res;
  for (let i = 0; i < 5; i++) {
    const t = Date.now();
    res = fn();
    runs.push(Date.now() - t);
  }
  return { label, msMin: Math.min(...runs), msMax: Math.max(...runs), hits: Array.isArray(res) ? res.length : 1 };
};
report.latency = [
  timeIt('searchWords 英文前缀 ab（FTS5）', () => queries.searchWords('ab', { limit: 100 })),
  timeIt('searchWords 中文子串 放弃（LIKE）', () => queries.searchWords('放弃', { limit: 100 })),
  timeIt('searchWords 模块过滤 cet6+ab', () => queries.searchWords('ab', { modules: ['cet6'], limit: 100 })),
  timeIt('searchPhrases 英文 give up（FTS5）', () => queries.searchPhrases('give up', { limit: 60 })),
  timeIt('searchPhrases 中文 放弃（LIKE）', () => queries.searchPhrases('放弃', { limit: 60 })),
  timeIt('browseWords 首字母 a', () => queries.browseWords({ firstLetter: 'a', limit: 100, offset: 0 }))
];
report.ftsEvidence = {
  englishPrefixAb: scalar(`SELECT COUNT(*) AS n FROM words_fts WHERE words_fts MATCH '"ab"*'`),
  englishExactAbandon: scalar(`SELECT COUNT(*) AS n FROM words_fts WHERE words_fts MATCH '"abandon"'`),
  cjkToken: scalar(`SELECT COUNT(*) AS n FROM words_fts WHERE words_fts MATCH '"放弃"'`),
  cjkSingleChar: scalar(`SELECT COUNT(*) AS n FROM words_fts WHERE words_fts MATCH '"弃"'`),
  likeCjkToken: scalar(`SELECT COUNT(*) AS n FROM words WHERE meaning LIKE '%放弃%'`),
  likeCjkSingleChar: scalar(`SELECT COUNT(*) AS n FROM words WHERE meaning LIKE '%弃%'`)
};

const plan = (label, sql, ...params) => ({
  label,
  plan: all('EXPLAIN QUERY PLAN ' + sql, ...params).map((r) => r.detail)
});
report.plans = [
  plan(
    'searchWords 中文 LIKE',
    `SELECT w.id, group_concat(wm.module_id) AS module_ids FROM words w
       LEFT JOIN word_modules wm ON wm.word_id = w.id
       WHERE ((LOWER(w.word) LIKE ? ESCAPE '\\' OR w.meaning LIKE ? ESCAPE '\\'))
       GROUP BY w.id ORDER BY (w.word_lower = ?) DESC, w.word_lower LIMIT ? OFFSET ?`,
    '%放弃%',
    '%放弃%',
    '',
    100,
    0
  ),
  plan(
    'searchWords 英文 FTS',
    `SELECT w.id FROM words_fts JOIN words w ON w.id = words_fts.rowid
       LEFT JOIN word_modules wm ON wm.word_id = w.id
       WHERE words_fts MATCH ? GROUP BY w.id
       ORDER BY (w.word_lower = ?) DESC, (w.word_lower LIKE ? || '%') DESC, rank LIMIT ? OFFSET ?`,
    '"ab"*',
    'ab',
    'ab',
    100,
    0
  ),
  plan(
    'browseWords 首字母',
    `SELECT w.id FROM words w WHERE w.first_letter = ? ORDER BY w.word_lower LIMIT ?`,
    'a',
    100
  ),
  plan(
    '模块过滤 EXISTS',
    `SELECT COUNT(*) AS n FROM words w WHERE EXISTS (SELECT 1 FROM word_modules wm JOIN modules m ON m.id = wm.module_id WHERE wm.word_id = w.id AND m.code = ?)`,
    'cet6'
  ),
  plan(
    'getWordDetail 词组',
    `SELECT p.id FROM phrases p WHERE p.word_id = ? ORDER BY p.module_id, p.sort_order`,
    1
  ),
  plan(
    'searchPhrases 中文 LIKE',
    `SELECT p.id FROM phrases p JOIN words w ON w.id = p.word_id
       WHERE ((p.phrase LIKE ? ESCAPE '\\' OR p.example LIKE ? ESCAPE '\\' OR p.example_translation LIKE ? ESCAPE '\\'))
       ORDER BY p.word_id, p.sort_order LIMIT ?`,
    '%放弃%',
    '%放弃%',
    '%放弃%',
    60
  ),
  plan(
    'searchPhrases 英文 FTS',
    `SELECT p.id FROM phrases_fts JOIN phrases p ON p.id = phrases_fts.rowid JOIN words w ON w.id = p.word_id
       WHERE phrases_fts MATCH ? ORDER BY rank LIMIT ?`,
    '"give"* AND "up"*',
    60
  ),
  plan(
    '每日随机抽样',
    `SELECT w.id FROM words w WHERE 1 = 1 AND EXISTS (SELECT 1 FROM word_modules wm JOIN modules m ON m.id = wm.module_id WHERE wm.word_id = w.id AND m.code IN (?)) ORDER BY RANDOM() LIMIT ?`,
    'cet6',
    20
  ),
  plan(
    '热力图区间聚合',
    `SELECT study_date AS date, COUNT(*) AS count FROM study_records WHERE study_date >= ? GROUP BY study_date ORDER BY study_date`,
    '2025-10-06'
  ),
  plan('连续天数 distinct 日期', `SELECT DISTINCT study_date AS d FROM study_records`),
  plan('累计学习词数', `SELECT COUNT(DISTINCT word_id) AS n FROM study_records`)
];

db.close();
if (tmpDir && !keep) fs.rmSync(tmpDir, { recursive: true, force: true });

const text = JSON.stringify(report, null, 2);
if (jsonOut) fs.writeFileSync(jsonOut, text, 'utf8');
console.log(text);
