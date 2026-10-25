import { createRequire } from 'node:module';
import { spawn } from 'node:child_process';
import path from 'node:path';

const require = createRequire(import.meta.url);
const ROOT = path.resolve(import.meta.dirname, '..');
const electronBin = require('electron');

const { createServer } = await import('vite');

const server = await createServer({ root: ROOT, mode: 'development' });
await server.listen();

const url = server.resolvedUrls?.local[0];
if (!url) throw new Error('vite did not resolve a local url');
console.log(`vite dev server: ${url}`);

const child = spawn(electronBin, ['.'], {
  cwd: ROOT,
  stdio: 'inherit',
  env: { ...process.env, VITE_DEV_SERVER_URL: url }
});

child.on('exit', (code, signal) => {
  void server.close().finally(() => {
    if (signal) process.kill(process.pid, signal);
    else process.exit(code ?? 0);
  });
});
