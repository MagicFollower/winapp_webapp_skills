import { spawnSync } from 'node:child_process';
import path from 'node:path';
// 定向自证启动器：npm run test:study-overflow 只跑学习页组件审计，不跑全局回归。
const ROOT = path.resolve(import.meta.dirname, '..');
const bin = path.join(ROOT, 'node_modules', 'electron', 'dist', 'electron.exe');
const args = process.argv.slice(2);
if (!args.includes('--selftest')) args.unshift('--selftest');
// 先重新构建：跑旧 dist-electron/main.js 会把已修的代码当成还在坏，判据直接失真。
if (!args.includes('--no-build')) {
  const built = spawnSync(process.execPath, [path.join(ROOT, 'bin', 'build.mjs')], { cwd: ROOT, stdio: 'inherit' });
  if (built.status !== 0) {
    console.error('SELFTEST-BUILD-FAILED exit=' + built.status);
    process.exit(built.status ?? 1);
  }
}
const res = spawnSync(bin, ['.', ...args], { cwd: ROOT, stdio: 'inherit', timeout: 300000, killSignal: 'SIGTERM' });
if (res.signal) console.error('SELFTEST-TIMEOUT killed by signal=' + res.signal);
process.exit(res.status ?? 1);
