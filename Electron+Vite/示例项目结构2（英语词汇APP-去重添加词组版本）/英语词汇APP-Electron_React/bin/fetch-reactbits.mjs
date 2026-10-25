#!/usr/bin/env node
// ReactBits copy-in 通路：原文固化进 reactbits/raw/（种子），产物写进 src/components/ui/。
// 默认本地命中就零联网；--offline 有缺口即退出码 1；--fetch-only 只补种子不写产物；--check 校验种子哈希。
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import https from 'node:https';

const ROOT = path.resolve(import.meta.dirname, '..');
const SEED_DIR = path.join(ROOT, 'reactbits', 'raw');
const LOCK = path.join(ROOT, 'reactbits', 'lock.json');
const MANIFEST_PATH = path.join(ROOT, 'reactbits', 'manifest.json');

const argv = process.argv.slice(2);
const flags = new Set(argv.filter((a) => a.startsWith('--')));
const onlyNames = argv.filter((a) => !a.startsWith('--'));
const OFFLINE = flags.has('--offline');
const FETCH_ONLY = flags.has('--fetch-only');
const CHECK = flags.has('--check');

const manifest = JSON.parse(fs.readFileSync(MANIFEST_PATH, 'utf8'));
const hosts = manifest.source.hosts;
const variant = manifest.source.variant;
const targetDir = path.join(ROOT, manifest.targetDir);

function sha256(buf) {
  return crypto.createHash('sha256').update(buf).digest('hex');
}

function httpGet(url) {
  return new Promise((resolve) => {
    const req = https.get(url, { headers: { 'user-agent': 'word-app-reactbits' }, timeout: 20000 }, (res) => {
      if (res.statusCode >= 300 && res.statusCode < 400 && res.headers.location) {
        res.resume();
        return resolve(httpGet(new URL(res.headers.location, url).href));
      }
      const chunks = [];
      res.on('data', (c) => chunks.push(c));
      res.on('end', () => resolve({ status: res.statusCode, body: Buffer.concat(chunks) }));
    });
    req.on('timeout', () => req.destroy(new Error('timeout')));
    req.on('error', (e) => resolve({ status: 0, body: Buffer.from(String(e.message)) }));
  });
}

async function fetchToRaw(rel, urlPath) {
  for (const host of hosts) {
    const res = await httpGet(host + urlPath);
    if (res.status === 200 && res.body.length > 0) {
      const out = path.join(SEED_DIR, rel);
      fs.mkdirSync(path.dirname(out), { recursive: true });
      fs.writeFileSync(out, res.body);
      return res.body;
    }
  }
  return null;
}

const wanted = [];
for (const comp of manifest.components) {
  if (onlyNames.length && !onlyNames.includes(comp.name)) continue;
  for (const file of comp.files) {
    wanted.push({
      name: comp.name,
      file,
      rel: path.join(comp.name, file),
      urlPath: `${variant}/${comp.category}/${comp.name}/${file}`
    });
  }
}

let localHits = 0;
let fetched = 0;
const gaps = [];

for (const item of wanted) {
  const rawPath = path.join(SEED_DIR, item.rel);
  if (fs.existsSync(rawPath)) {
    localHits += 1;
    continue;
  }
  if (OFFLINE || CHECK) {
    gaps.push(item.rel);
    continue;
  }
  const body = await fetchToRaw(item.rel, item.urlPath);
  if (body === null) gaps.push(item.rel);
  else fetched += 1;
}

if (CHECK) {
  const lock = fs.existsSync(LOCK) ? JSON.parse(fs.readFileSync(LOCK, 'utf8')) : null;
  if (!lock) {
    console.log(`check: 无 lock.json，按当前种子生成 ${wanted.length} 条哈希`);
    const entries = {};
    for (const item of wanted) entries[item.rel.replace(/\\/g, '/')] = sha256(fs.readFileSync(path.join(SEED_DIR, item.rel)));
    fs.writeFileSync(LOCK, JSON.stringify(entries, null, 2) + '\n');
    process.exit(0);
  }
  const drift = [];
  for (const item of wanted) {
    const key = item.rel.replace(/\\/g, '/');
    const p = path.join(SEED_DIR, item.rel);
    if (!fs.existsSync(p)) { drift.push(`${key}: 缺失`); continue; }
    if (lock[key] && lock[key] !== sha256(fs.readFileSync(p))) drift.push(`${key}: 哈希不符`);
  }
  console.log(`check: 种子 ${wanted.length} 条，漂移 ${drift.length} 条`);
  drift.forEach((d) => console.log('   ' + d));
  process.exit(drift.length || gaps.length ? 1 : 0);
}

console.log(`种子：本地命中 ${localHits}/${wanted.length}，联网取回 ${fetched}，缺口 ${gaps.length}`);
gaps.forEach((g) => console.log('   缺口 ' + g));
if (gaps.length) {
  console.log(OFFLINE ? '--offline：种子不全，退出' : '取回失败：检查网络或换 host');
  process.exit(1);
}

if (!FETCH_ONLY) {
  fs.mkdirSync(targetDir, { recursive: true });
  let written = 0;
  const NOCHECK = "// @ts-nocheck -- vendored ReactBits source; upstream lint 口径不并入本工程，接口仍然类型化。see reactbits/ATTRIBUTION.md\n";
  // 上游 CSS 里出现过 var(-0.5rem) / var(50%) 这种把字面量塞进 var() 的写法，
  // lightningcss（Vite 8 默认 CSS 压缩器）会直接报错；非自定义属性的 var() 一律展开回字面量。
  const unwrapNonCustomPropVar = (css) => css.replace(/var\((?!--)([^()]*)\)/g, '$1');
  for (const item of wanted) {
    const src = fs.readFileSync(path.join(SEED_DIR, item.rel), 'utf8');
    let text = src.replace(/\r\n/g, '\n');
    if (item.file.endsWith('.tsx')) {
      text = text.replace(/^'use client';\s*\n/, '');
      text = NOCHECK + text;
    }
    if (item.file.endsWith('.css')) {
      const fixed = unwrapNonCustomPropVar(text);
      const changed = text.split('\n').filter((line, i) => line !== fixed.split('\n')[i]).length;
      if (changed > 0) console.log(`CSS 修正：${item.file} 展开 ${changed} 行非自定义属性 var()`);
      text = fixed;
    }
    if (!text.endsWith('\n')) text += '\n';
    fs.writeFileSync(path.join(targetDir, item.file), text);
    written += 1;
  }
  console.log(`产物：写入 ${written} 个文件 -> ${path.relative(ROOT, targetDir)}`);
}

const entries = {};
if (fs.existsSync(LOCK)) Object.assign(entries, JSON.parse(fs.readFileSync(LOCK, 'utf8')));
for (const item of wanted) entries[item.rel.replace(/\\/g, '/')] = sha256(fs.readFileSync(path.join(SEED_DIR, item.rel)));
fs.writeFileSync(LOCK, JSON.stringify(entries, null, 2) + '\n');
console.log(`lock：${Object.keys(entries).length} 条哈希`);
