import fs from 'node:fs';
import { DatabaseSync } from 'node:sqlite';

interface RawTranslation {
  translation?: string;
  type?: string;
}

interface RawPhrase {
  phrase?: string;
  usage?: string;
  example?: string;
  example_translation?: string;
}

interface RawItem {
  word?: string;
  translations?: RawTranslation[];
  phrases?: RawPhrase[];
}

interface MergeRecord {
  word: string;
  wordLower: string;
  phonetic: string | null;
  meaning: string;
  firstLetter: string;
  modules: string[];
  phrasesByModule: Record<string, RawPhrase[]>;
}

export interface ImportStats {
  uniqueWords: number;
  phraseRows: number;
  sourceEntries: number;
  ms: number;
}

function buildMeaning(translations: RawTranslation[] | undefined): string {
  if (!translations || translations.length === 0) return '';
  const parts = translations
    .map((t) => {
      const text = (t.translation ?? '').trim();
      const type = (t.type ?? '').trim();
      if (!text) return '';
      return type ? `${type} ${text}` : text;
    })
    .filter(Boolean);
  return parts.join('；');
}

export function importData(db: DatabaseSync, files: { moduleId: string; filePath: string }[]): ImportStats {
  const t0 = Date.now();
  const map = new Map<string, MergeRecord>();
  let sourceEntries = 0;

  for (const file of files) {
    const items = JSON.parse(fs.readFileSync(file.filePath, 'utf8')) as RawItem[];
    for (const item of items) {
      const rawWord = typeof item.word === 'string' ? item.word.trim() : '';
      if (!rawWord) continue;
      sourceEntries += 1;
      const key = rawWord.toLowerCase();
      let rec = map.get(key);
      if (!rec) {
        rec = {
          word: rawWord,
          wordLower: key,
          phonetic: null,
          meaning: buildMeaning(item.translations),
          firstLetter: key.slice(0, 1),
          modules: [],
          phrasesByModule: {}
        };
        map.set(key, rec);
      } else if (!rec.meaning) {
        rec.meaning = buildMeaning(item.translations);
      }
      if (!rec.modules.includes(file.moduleId)) rec.modules.push(file.moduleId);
      const phrases = item.phrases ?? [];
      // 同一文件里同一单词可能重复出现（例如 junior 的 May/may），重复条目要并入而不是覆盖；
      // 只有同一 (词, 模块) 内词组文本完全相同才当作重复丢掉。
      const bucket = rec.phrasesByModule[file.moduleId] ?? (rec.phrasesByModule[file.moduleId] = []);
      for (const p of phrases) {
        const text = (p.phrase ?? '').trim();
        if (!text) continue;
        if (bucket.some((existing) => (existing.phrase ?? '').trim() === text)) continue;
        bucket.push(p);
      }
    }
  }

  const insertWord = db.prepare(
    'INSERT INTO words (word, word_lower, phonetic, meaning, first_letter) VALUES (?, ?, ?, ?, ?)'
  );
  const insertWM = db.prepare('INSERT INTO word_modules (word_id, module_id) VALUES (?, ?)');
  const insertPhrase = db.prepare(
    'INSERT INTO phrases (word_id, module_id, phrase, usage, example, example_translation, sort_order) VALUES (?, ?, ?, ?, ?, ?, ?)'
  );
  const moduleIdOf = new Map(
    (db.prepare('SELECT id, code FROM modules').all() as { id: number; code: string }[]).map((r) => [r.code, r.id])
  );

  let phraseRows = 0;
  db.exec('BEGIN');
  try {
    for (const rec of map.values()) {
      const info = insertWord.run(rec.word, rec.wordLower, rec.phonetic, rec.meaning, rec.firstLetter);
      const wordId = Number(info.lastInsertRowid);
      for (const code of rec.modules) {
        const moduleId = moduleIdOf.get(code);
        if (moduleId === undefined) continue;
        insertWM.run(wordId, moduleId);
        const phrases = rec.phrasesByModule[code] ?? [];
        phrases.forEach((p, idx) => {
          const text = (p.phrase ?? '').trim();
          if (!text) return;
          insertPhrase.run(
            wordId,
            moduleId,
            text,
            (p.usage ?? '').trim() || null,
            (p.example ?? '').trim() || null,
            (p.example_translation ?? '').trim() || null,
            idx
          );
          phraseRows += 1;
        });
      }
    }
    db.exec('COMMIT');
  } catch (err) {
    db.exec('ROLLBACK');
    throw err;
  }

  return { uniqueWords: map.size, phraseRows, sourceEntries, ms: Date.now() - t0 };
}
