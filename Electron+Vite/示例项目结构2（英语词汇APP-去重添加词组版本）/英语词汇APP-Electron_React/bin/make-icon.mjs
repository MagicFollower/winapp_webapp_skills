#!/usr/bin/env node
// 像素字母 monogram 图标生成器：零外部依赖（只用 node 内置 zlib），本地图像管线。
// 规则：5x7 点阵落在 11 格逻辑网格里，每格取 floor(size/11) 的整数倍像素，所以每一挡都是硬边、无重采样、无抗锯齿。
// BMP 层与 PNG 层共用同一份 argb 缓冲，像素规则只写一处。
import fs from 'node:fs';
import path from 'node:path';
import zlib from 'node:zlib';

const FONT = {
  A: ['.###.', '#...#', '#...#', '#####', '#...#', '#...#', '#...#'],
  B: ['####.', '#...#', '#...#', '####.', '#...#', '#...#', '####.'],
  C: ['.###.', '#...#', '#....', '#....', '#....', '#...#', '.###.'],
  D: ['####.', '#...#', '#...#', '#...#', '#...#', '#...#', '####.'],
  E: ['#####', '#....', '#....', '####.', '#....', '#....', '#####'],
  F: ['#####', '#....', '#....', '####.', '#....', '#....', '#....'],
  G: ['.###.', '#...#', '#....', '#.###', '#...#', '#...#', '.###.'],
  H: ['#...#', '#...#', '#...#', '#####', '#...#', '#...#', '#...#'],
  I: ['#####', '..#..', '..#..', '..#..', '..#..', '..#..', '#####'],
  J: ['..###', '...#.', '...#.', '...#.', '...#.', '#..#.', '.##..'],
  K: ['#...#', '#..#.', '#.#..', '##...', '#.#..', '#..#.', '#...#'],
  L: ['#....', '#....', '#....', '#....', '#....', '#....', '#####'],
  M: ['#...#', '##.##', '#.#.#', '#...#', '#...#', '#...#', '#...#'],
  N: ['#...#', '##..#', '##..#', '#.#.#', '#..##', '#..##', '#...#'],
  O: ['.###.', '#...#', '#...#', '#...#', '#...#', '#...#', '.###.'],
  P: ['####.', '#...#', '#...#', '####.', '#....', '#....', '#....'],
  Q: ['.###.', '#...#', '#...#', '#...#', '#.#.#', '#..#.', '.##.#'],
  R: ['####.', '#...#', '#...#', '####.', '#.#..', '#..#.', '#...#'],
  S: ['.####', '#....', '#....', '.###.', '....#', '....#', '####.'],
  T: ['#####', '..#..', '..#..', '..#..', '..#..', '..#..', '..#..'],
  U: ['#...#', '#...#', '#...#', '#...#', '#...#', '#...#', '.###.'],
  V: ['#...#', '#...#', '#...#', '#...#', '#...#', '.#.#.', '..#..'],
  W: ['#...#', '#...#', '#...#', '#...#', '#.#.#', '##.##', '#...#'],
  X: ['#...#', '#...#', '.#.#.', '..#..', '.#.#.', '#...#', '#...#'],
  Y: ['#...#', '#...#', '.#.#.', '..#..', '..#..', '..#..', '..#..'],
  Z: ['#####', '....#', '...#.', '..#..', '.#...', '#....', '#####'],
  0: ['.###.', '#...#', '#..##', '#.#.#', '##..#', '#...#', '.###.'],
  1: ['..#..', '.##..', '..#..', '..#..', '..#..', '..#..', '.###.'],
  2: ['.###.', '#...#', '....#', '...#.', '..#..', '.#...', '#####'],
  3: ['#####', '...#.', '..#..', '...#.', '....#', '#...#', '.###.'],
  4: ['...#.', '..##.', '.#.#.', '#..#.', '#####', '...#.', '...#.'],
  5: ['#####', '#....', '####.', '....#', '....#', '#...#', '.###.'],
  6: ['..##.', '.#...', '#....', '####.', '#...#', '#...#', '.###.'],
  7: ['#####', '....#', '...#.', '..#..', '.#...', '.#...', '.#...'],
  8: ['.###.', '#...#', '#...#', '.###.', '#...#', '#...#', '.###.'],
  9: ['.###.', '#...#', '#...#', '.####', '....#', '...#.', '.##..']
};

const GRID = 11;
const BMP_SIZES = [16, 24, 32, 48, 64, 128];
const PNG_SIZE = 256;

function parseHex(hex) {
  const s = hex.replace('#', '');
  const full = s.length === 3 ? s.split('').map((c) => c + c).join('') : s;
  if (!/^[0-9a-fA-F]{6}$/.test(full)) {
    throw new Error(`bad color: ${hex}`);
  }
  return [parseInt(full.slice(0, 2), 16), parseInt(full.slice(2, 4), 16), parseInt(full.slice(4, 6), 16)];
}

function luminance([r, g, b]) {
  const f = (v) => {
    const s = v / 255;
    return s <= 0.03928 ? s / 12.92 : ((s + 0.055) / 1.055) ** 2.4;
  };
  return 0.2126 * f(r) + 0.7152 * f(g) + 0.0722 * f(b);
}

// 单一像素规则：返回 size x size 的 RGBA 缓冲。
function render(letter, size, bg, fg) {
  const cell = Math.floor(size / GRID);
  const span = cell * GRID;
  const origin = Math.floor((size - span) / 2);
  const buf = Buffer.alloc(size * size * 4, 0);
  const glyph = FONT[letter];
  const gx = Math.floor((GRID - 5) / 2);
  const gy = Math.floor((GRID - 7) / 2);

  const put = (px, py, [r, g, b], a = 255) => {
    const i = (py * size + px) * 4;
    buf[i] = r;
    buf[i + 1] = g;
    buf[i + 2] = b;
    buf[i + 3] = a;
  };

  for (let cy = 0; cy < GRID; cy += 1) {
    for (let cx = 0; cx < GRID; cx += 1) {
      for (let dy = 0; dy < cell; dy += 1) {
        for (let dx = 0; dx < cell; dx += 1) {
          put(origin + cx * cell + dx, origin + cy * cell + dy, bg);
        }
      }
    }
  }
  for (let row = 0; row < 7; row += 1) {
    for (let col = 0; col < 5; col += 1) {
      if (glyph[row][col] !== '#') continue;
      for (let dy = 0; dy < cell; dy += 1) {
        for (let dx = 0; dx < cell; dx += 1) {
          put(origin + (gx + col) * cell + dx, origin + (gy + row) * cell + dy, fg);
        }
      }
    }
  }
  return { buf, cell, span, drawn: [5 * cell, 7 * cell] };
}

const CRC_TABLE = (() => {
  const t = new Int32Array(256);
  for (let n = 0; n < 256; n += 1) {
    let c = n;
    for (let k = 0; k < 8; k += 1) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
    t[n] = c;
  }
  return t;
})();

function crc32(buf) {
  let c = 0xffffffff;
  for (const byte of buf) c = CRC_TABLE[(c ^ byte) & 0xff] ^ (c >>> 8);
  return (c ^ 0xffffffff) >>> 0;
}

function chunk(type, data) {
  const len = Buffer.alloc(4);
  len.writeUInt32BE(data.length);
  const body = Buffer.concat([Buffer.from(type, 'ascii'), data]);
  const crc = Buffer.alloc(4);
  crc.writeUInt32BE(crc32(body));
  return Buffer.concat([len, body, crc]);
}

function encodePng(width, height, rgba) {
  const raw = Buffer.alloc((width * 4 + 1) * height);
  for (let y = 0; y < height; y += 1) {
    raw[y * (width * 4 + 1)] = 0;
    rgba.copy(raw, y * (width * 4 + 1) + 1, y * width * 4, (y + 1) * width * 4);
  }
  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(width, 0);
  ihdr.writeUInt32BE(height, 4);
  ihdr[8] = 8;
  ihdr[9] = 6;
  return Buffer.concat([
    Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
    chunk('IHDR', ihdr),
    chunk('IDAT', zlib.deflateSync(raw, { level: 9 })),
    chunk('IEND', Buffer.alloc(0))
  ]);
}

function encodeBmpLayer(width, height, rgba) {
  const dib = Buffer.alloc(40);
  dib.writeUInt32LE(40, 0);
  dib.writeInt32LE(width, 4);
  dib.writeInt32LE(height * 2, 8);
  dib.writeUInt16LE(1, 12);
  dib.writeUInt16LE(32, 14);
  dib.writeUInt32LE(0, 16);
  const pixels = Buffer.alloc(width * height * 4);
  for (let y = 0; y < height; y += 1) {
    for (let x = 0; x < width; x += 1) {
      const src = (y * width + x) * 4;
      const dst = ((height - 1 - y) * width + x) * 4;
      pixels[dst] = rgba[src + 2];
      pixels[dst + 1] = rgba[src + 1];
      pixels[dst + 2] = rgba[src];
      pixels[dst + 3] = rgba[src + 3];
    }
  }
  const maskRow = Math.ceil(width / 32) * 4;
  const mask = Buffer.alloc(maskRow * height, 0);
  return Buffer.concat([dib, pixels, mask]);
}

function writeIco(entries, outPath) {
  const header = Buffer.alloc(6);
  header.writeUInt16LE(0, 0);
  header.writeUInt16LE(1, 2);
  header.writeUInt16LE(entries.length, 4);
  let offset = 6 + entries.length * 16;
  const dir = Buffer.alloc(entries.length * 16);
  entries.forEach((e, i) => {
    const base = i * 16;
    dir[base] = e.width >= 256 ? 0 : e.width;
    dir[base + 1] = e.height >= 256 ? 0 : e.height;
    dir[base + 2] = 0;
    dir[base + 3] = 0;
    dir.writeUInt16LE(1, base + 4);
    dir.writeUInt16LE(32, base + 6);
    dir.writeUInt32LE(e.data.length, base + 8);
    dir.writeUInt32LE(offset, base + 12);
    offset += e.data.length;
  });
  fs.writeFileSync(outPath, Buffer.concat([header, dir, ...entries.map((e) => e.data)]));
}

const args = process.argv.slice(2);
const read = (name, fallback) => {
  const hit = args.find((a) => a.startsWith(`--${name}=`));
  return hit ? hit.slice(name.length + 3) : fallback;
};

const ROOT = path.resolve(import.meta.dirname, '..');
const letter = read('letter', 'W').toUpperCase();
const out = path.resolve(ROOT, read('out', 'electron/icon.ico'));
const bgHex = read('bg', '#2f6fed');
if (!Object.prototype.hasOwnProperty.call(FONT, letter)) {
  console.error(`!! -Letter 只支持 A-Z / 0-9，收到 "${letter}"；不画空方块糊过去`);
  process.exit(1);
}
const bg = parseHex(bgHex);
const fgAuto = luminance(bg) > 0.5 ? [0x11, 0x11, 0x11] : [0xff, 0xff, 0xff];
const fgArg = args.find((a) => a.startsWith('--fg='));
const fg = fgArg ? parseHex(fgArg.slice(6)) : fgAuto;

const entries = [];
for (const size of [...BMP_SIZES, PNG_SIZE]) {
  const { buf, cell, drawn } = render(letter, size, bg, fg);
  const isPng = size === PNG_SIZE;
  entries.push({
    width: size,
    height: size,
    data: isPng ? encodePng(size, size, buf) : encodeBmpLayer(size, size, buf)
  });
  console.log(
    `layer ${String(size).padStart(3)}px ${isPng ? 'PNG' : 'BMP '} cell=${cell}px glyph=${drawn[0]}x${drawn[1]}px bytes=${entries.at(-1).data.length}`
  );
}

fs.mkdirSync(path.dirname(out), { recursive: true });
writeIco(entries, out);
console.log(
  `ico -> ${path.relative(ROOT, out)} bytes=${fs.statSync(out).size} layers=${entries.length} bg=${bgHex} fg=${fg === fgAuto ? `auto(#${fgAuto.map((v) => v.toString(16).padStart(2, '0')).join('')})` : fgArg.slice(5)}`
);
