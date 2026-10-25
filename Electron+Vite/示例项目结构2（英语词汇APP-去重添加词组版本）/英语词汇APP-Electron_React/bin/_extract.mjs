import fs from 'node:fs';

const p = 'electron/main.ts';
let src = fs.readFileSync(p, 'utf8');

const startMark = '    // 定向测试：只审学习页的每个组件';
const endMark = "    const data = (await js(`(async () => {";
const s = src.indexOf(startMark);
const e = src.indexOf(endMark);
if (s < 0 || e < 0 || e <= s) { console.error('marks not found', s, e); process.exit(1); }

let region = src.slice(s, e);
src = src.slice(0, s) + src.slice(e);

// 把内联的早退分支改成独立函数：runStudyAudit 定义 + 由 ready-to-show 决定跑哪个模式
region = region.replace(
  '    if (STUDY_AUDIT) {\n      await runStudyAudit();',
  '    if (STUDY_AUDIT_DISABLED_PLACEHOLDER) {\n      await runStudyAudit();'
);

const fnStart = region.indexOf('const runStudyAudit = async (): Promise<void> => {');
const wrapperStart = region.indexOf('    if (STUDY_AUDIT) {');
if (fnStart < 0 || wrapperStart < 0) { console.error('fn/wrapper not found', fnStart, wrapperStart); process.exit(1); }

const head = region.slice(0, fnStart);            // STUDY_WIDTHS + detector 定义
const body = region.slice(fnStart, wrapperStart); // runStudyAudit 函数体
const wrapper = region.slice(wrapperStart);       // 原早退分支（含 summary + quit）

const summary = `    const passed = checks.filter((c) => c.includes('PASS')).length;
    log(\`STUDY-AUDIT-SUMMARY checks=\${checks.length} pass=\${passed} fail=\${checks.length - passed} mode=study-audit\`);
    log(passed === checks.length ? 'SELFTEST-OK' : 'SELFTEST-FAIL');
    app.quit();
  };

`;
const newFn = head + body + summary;

const anchor = '  async function runSelfTest(): Promise<void> {';
if (!src.includes(anchor)) { console.error('runSelfTest anchor missing'); process.exit(1); }
src = src.replace(anchor, newFn + anchor);

// ready-to-show 分派：study 模式只跑定向测试，不跑全局回归
const oldDispatch = '      if (SELFTEST) void runSelfTest();';
if (!src.includes(oldDispatch)) { console.error('dispatch line missing'); process.exit(1); }
src = src.replace(oldDispatch, '      if (SELFTEST) void (STUDY_AUDIT ? runStudyAudit() : runSelfTest());');

// 清掉占位符残留（原 wrapper 里的早退块已不需要）
src = src.replace(/if \(STUDY_AUDIT_DISABLED_PLACEHOLDER\) \{[\s\S]*?\n    \}\n/, '');

fs.writeFileSync(p, src);
console.log('extracted runStudyAudit as a standalone mode; dispatch wired');
