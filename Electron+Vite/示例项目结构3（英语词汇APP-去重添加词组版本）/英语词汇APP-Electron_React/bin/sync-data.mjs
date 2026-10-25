// 把「英文单词数据源」目录里的 N-名称-顺序.json 同步进 resources/，并据此重写 resources/sources.json。
// 数据源换版本时只跑这一条命令：文件名字典 -> 模块码 -> 种子文件名 -> manifest 计数，全部由脚本产出，
// 不再手改 manifest（旧版就是这么写的，换了数据源之后 entries 全是旧的）。
//
//   node bin/sync-data.mjs [源目录]            同步并改写 resources/
//   node bin/sync-data.mjs --check [源目录]    只比对：源与种子不一致就 exit 1
import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';

const ROOT = path.resolve(import.meta.dirname, '..');
const RES = path.join(ROOT, 'resources');
const MANIFEST = path.join(RES, 'sources.json');

// 文件名里的中文模块名 -> 库里用的 code。新增模块只改这张表。
const NAME_TO_CODE = {
  初中: 'junior',
  高中: 'senior',
  CET4: 'cet4',
  CET6: 'cet6',
  考研: 'kaoyan',
  托福: 'toefl',
  雅思: 'ielts',
  SAT: 'sat',
  GRE: 'gre'
};

const args = process.argv.slice(2);
const checkOnly = args[0] === '--check';
if (checkOnly) args.shift();
const SRC = path.resolve(args[0] ?? path.join(ROOT, '..', '英文单词数据源'));

const NAME_RE = /^(\d+)-(.+)-(顺序|乱序)\.json$/;

function summarize(items) {
  let entries = 0;
  let phrases = 0;
  let withExample = 0;
  const words = new Set();
  // 导入器按 (词, 模块) 去重文本，这里用同一口径先算出来写进 manifest：
  // 否则源里 19 万条、库里 18.6 万条，差额没人解释得清（上一版少收 75 条就是靠这本账发现的）。
  const seenPhraseByText = new Map();
  let dupTexts = 0;
  for (const it of items) {
    const w = typeof it.word === 'string' ? it.word.trim() : '';
    if (!w) continue;
    entries += 1;
    const key = w.toLowerCase();
    words.add(key);
    for (const p of it.phrases ?? []) {
      const text = typeof p.phrase === 'string' ? p.phrase.trim() : '';
      if (!text) continue;
      phrases += 1;
      if (p.example) withExample += 1;
      const bucket = seenPhraseByText.get(key) ?? new Set();
      if (bucket.has(text)) dupTexts += 1;
      else bucket.add(text);
      seenPhraseByText.set(key, bucket);
    }
  }
  return { entries, phrases, phrasesDup: dupTexts, phrasesUnique: phrases - dupTexts, withExample, uniqueInFile: words.size };
}

if (!fs.existsSync(SRC)) {
  console.error(`SOURCE-NOT-FOUND ${SRC}`);
  process.exit(2);
}

const found = fs.readdirSync(SRC).filter((f) => f.endsWith('.json'));

const parsed = [];
const unknown = [];
for (const f of found) {
  const m = NAME_RE.exec(f);
  if (!m) {
    unknown.push(f);
    continue;
  }
  const order = Number(m[1]);
  const name = m[2];
  const code = NAME_TO_CODE[name];
  if (!code) {
    unknown.push(`${f}（模块名「${name}」不在 NAME_TO_CODE 里）`);
    continue;
  }
  parsed.push({ order, name, code, source: f, variant: m[3] });
}
if (unknown.length > 0) {
  console.error(`UNKNOWN-FILES ${unknown.join(' | ')}`);
  console.error('文件名要匹配 N-模块名-顺序.json，且模块名要在 bin/sync-data.mjs 的 NAME_TO_CODE 里登记。');
  process.exit(2);
}
parsed.sort((a, b) => a.order - b.order);

const seenCodes = new Set();
for (const p of parsed) {
  if (seenCodes.has(p.code)) {
    console.error(`DUPLICATE-MODULE-CODE ${p.code}（${p.source}）`);
    process.exit(2);
  }
  seenCodes.add(p.code);
}

const uniq = new Map();
const files = [];
for (const p of parsed) {
  const srcPath = path.join(SRC, p.source);
  const buf = fs.readFileSync(srcPath);
  const items = JSON.parse(buf.toString('utf8'));
  if (!Array.isArray(items)) {
    console.error(`NOT-AN-ARRAY ${p.source}`);
    process.exit(2);
  }
  const s = summarize(items);
  const digest = crypto.createHash('sha256').update(buf).digest('hex').slice(0, 16);
  for (const it of items) {
    const w = typeof it.word === 'string' ? it.word.trim().toLowerCase() : '';
    if (!w) continue;
    // 按模块码去重：同一文件里重复出现的词（初中 May/may 这类）不能算成"跨模块"。
    const rec = uniq.get(w) ?? new Set();
    rec.add(p.code);
    uniq.set(w, rec);
  }
  files.push({ code: p.code, name: p.name, file: `${p.code}.json`, originalName: p.source, order: p.order, ...s, bytes: buf.length, sha256_16: digest });
}

const totals = {
  files: files.length,
  entries: files.reduce((n, f) => n + f.entries, 0),
  phrases: files.reduce((n, f) => n + f.phrases, 0),
  phrasesDup: files.reduce((n, f) => n + f.phrasesDup, 0),
  phrasesUnique: files.reduce((n, f) => n + f.phrasesUnique, 0),
  uniqueWords: uniq.size,
  wordsInMoreThanOneModule: [...uniq.values()].filter((codes) => codes.size > 1).length
};

const manifest = {
  capturedAt: new Date().toISOString().slice(0, 10),
  source: path.relative(ROOT, SRC).split(path.sep).join('/'),
  files,
  totals,
  itemShape: {
    word: 'string',
    translations: [{ translation: 'string', type: 'string' }],
    phrases: [{ phrase: 'string', usage: 'string', example: 'string', example_translation: 'string' }]
  },
  notes: [
    '本文件与 resources/*.json 都由 bin/sync-data.mjs 生成：换数据源后跑 npm run sync-data，不要手改。',
    '种子是源文件的原样字节拷贝（不转换、不裁剪），sha256_16 用于 --check 判断是否过期。',
    'totals.phrasesUnique 才是入库行数：导入器按 (词, 模块) 去掉重复文本，phrases − phrasesDup = phrasesUnique。',
    '数据源没有 phonetic 字段，所以 words.phonetic 恒为 NULL。',
    '例句内嵌在 phrases[].example / example_translation，没有独立例句表。'
  ]
};

let stale = false;
for (const f of files) {
  const srcBuf = fs.readFileSync(path.join(SRC, f.originalName));
  const dstPath = path.join(RES, f.file);
  const dstBuf = fs.existsSync(dstPath) ? fs.readFileSync(dstPath) : null;
  const same = dstBuf !== null && dstBuf.equals(srcBuf);
  if (!same) stale = true;
  console.log(`${same ? 'same  ' : 'write '} ${f.code.padEnd(7)} ${String(f.entries).padStart(6)} 条 ${String(f.phrases).padStart(7)} 词组  ${f.originalName}`);
  if (!same && !checkOnly) {
    fs.mkdirSync(RES, { recursive: true });
    fs.writeFileSync(dstPath, srcBuf);
  }
}
console.log(
  `TOTAL files=${totals.files} entries=${totals.entries} phrases=${totals.phrases} (dupTexts=${totals.phrasesDup} unique=${totals.phrasesUnique}) uniqueWords=${totals.uniqueWords} multiModule=${totals.wordsInMoreThanOneModule}`
);

const fresh = `${JSON.stringify(manifest, null, 2)}\n`;
const old = fs.existsSync(MANIFEST) ? fs.readFileSync(MANIFEST, 'utf8') : '';
const manifestDrift = fresh.replace(/"capturedAt": *"[^"]*",?/, '') !== old.replace(/"capturedAt": *"[^"]*",?/, '');

if (checkOnly) {
  const dirty = stale || manifestDrift;
  console.log(`${dirty ? 'CHECK-FAIL' : 'CHECK-OK'} staleSeeds=${stale} manifestDrift=${manifestDrift}`);
  process.exit(dirty ? 1 : 0);
}

fs.writeFileSync(MANIFEST, fresh);
console.log(`SYNCED ${files.length} files -> resources/ + sources.json`);
