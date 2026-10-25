import { spawnSync } from 'node:child_process';
import path from 'node:path';

const ROOT = path.resolve(import.meta.dirname, '..');
const { build } = await import('vite');

await build({ root: ROOT, mode: 'production' });
console.log('vite build ok -> dist/');

const tsc = path.join(ROOT, 'node_modules', 'typescript', 'bin', 'tsc');
const res = spawnSync(process.execPath, [tsc, '-p', path.join(ROOT, 'electron', 'tsconfig.json')], {
  cwd: ROOT,
  stdio: 'inherit'
});
if (res.status !== 0) {
  console.error(`tsc -p electron/tsconfig.json exited ${res.status}`);
  process.exit(res.status ?? 1);
}
console.log('tsc ok -> dist-electron/');
