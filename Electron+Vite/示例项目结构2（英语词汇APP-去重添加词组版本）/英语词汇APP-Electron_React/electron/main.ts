import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { app, BrowserWindow, Menu, nativeTheme, screen } from 'electron';
import type { MenuItemConstructorOptions } from 'electron';
import { ensureDatabase } from './db';
import { QueryService } from './queries';
import { StudyService, todayLocalDate } from './study';
import { Settings, type WindowBounds } from './settings';
import { registerIpc } from './ipc';
import type { DbInfo } from './db';
import type { ThemeMode } from './api-contract';

const APP_TITLE = '单词学习';
const MIN_WIDTH = 680;
const MIN_HEIGHT = 560;
const DEFAULT_BOUNDS: WindowBounds = { x: 0, y: 0, width: 1645, height: 1215 };
const SELFTEST = process.argv.includes('--selftest');
const SELFTEST_GEOMETRY = process.argv.includes('--selftest-geometry');
const STUDY_AUDIT = process.argv.includes('--selftest-study');
const MENU_AUDIT = process.argv.includes('--selftest-menu');
const BOOT_T0 = Date.now();

function log(line: string): void {
  process.stdout.write(`${line}\n`);
}

// 便携版宿主不会把子进程 stdout 冒泡出来，所以自证另走标记文件（见 doc 第 3 节）。
const MARKER_DIR = process.env.WORD_APP_MARKERS;

function marker(name: string): void {
  if (!MARKER_DIR) return;
  try {
    fs.mkdirSync(MARKER_DIR, { recursive: true });
    fs.writeFileSync(path.join(MARKER_DIR, name), new Date().toISOString());
    log(`marker ${name}`);
  } catch (err) {
    log(`marker-failed ${name} ${(err as Error).message}`);
  }
}

// 单例锁与 Chromium 缓存目录都在 whenReady 之前就按 userData 定好了，
// 所以自证的隔离目录必须在这里设，晚一步就会和正在运行的正式实例撞锁、抢缓存。
if (SELFTEST) {
  const isolated = process.env.SELFTEST_USERDATA ?? path.join(os.tmpdir(), `word-app-selftest-${process.pid}`);
  fs.mkdirSync(isolated, { recursive: true });
  app.setPath('userData', isolated);
}

const gotLock = app.requestSingleInstanceLock();
if (!gotLock) {
  log('single-instance-lock refused, quitting');
  app.quit();
} else {
  boot();
}

function resourcesDir(): string {
  return path.join(__dirname, '..', 'resources');
}

function clampBounds(input: WindowBounds): { bounds: WindowBounds; clamped: boolean } {
  const displays = screen.getAllDisplays();
  const workAreas = displays.map((d) => d.workArea);
  const largest = workAreas.reduce((a, b) => (a.width * a.height > b.width * b.height ? a : b));
  const width = Math.min(Math.max(input.width, MIN_WIDTH), largest.width);
  const height = Math.min(Math.max(input.height, MIN_HEIGHT), largest.height);
  const visible = 80;
  const intersects = workAreas.some(
    (wa) =>
      input.x < wa.x + wa.width - visible &&
      input.x + width > wa.x + visible &&
      input.y < wa.y + wa.height - visible &&
      input.y + height > wa.y + visible
  );
  if (intersects && width === input.width && height === input.height) {
    return { bounds: { x: input.x, y: input.y, width, height }, clamped: false };
  }
  if (intersects) return { bounds: { x: input.x, y: input.y, width, height }, clamped: true };
  const wa = largest;
  return {
    bounds: { x: Math.round(wa.x + (wa.width - width) / 2), y: Math.round(wa.y + (wa.height - height) / 2), width, height },
    clamped: true
  };
}

// 顶部那条是 Electron 原生应用菜单（Windows 非客户区，不在 DOM 里，探针只能走主进程）。
// 不显式安装时看到的是 Electron 自带的默认菜单，标签写死在英文串表里：实测本机
// app.getLocale() 与 getSystemLocale() 都是 'zh-CN'，菜单仍然是 File/Edit/View/Window，
// 所以"改 locale 就自动中文"不成立，只能自己出模板。
// 口径：与默认菜单逐项 1:1（4 个顶级 + 18 个叶子，见 doc/顶部菜单与工具栏中文化.md 的对照表），
// 行为一律走 role，快捷键与启用条件跟原生一致，这里只换 label。
// 唯一例外是「最大化」：role:'zoom' 在文档里标的是 macOS 行为，Windows 下不保证生效，
// 所以显式绑到 isMaximized() 切换，语义与标签一致。
// 不加 '&' 助记符：中文标签没有单字助记符惯例，写 '&文件' 会按字面把 & 显示出来。
function installAppMenu(): void {
  const template: MenuItemConstructorOptions[] = [
    {
      label: '文件',
      submenu: [{ role: 'quit', label: '退出' }]
    },
    {
      label: '编辑',
      submenu: [
        { role: 'undo', label: '撤销' },
        { role: 'redo', label: '重做' },
        { type: 'separator' },
        { role: 'cut', label: '剪切' },
        { role: 'copy', label: '复制' },
        { role: 'paste', label: '粘贴' },
        { role: 'delete', label: '删除' },
        { type: 'separator' },
        { role: 'selectAll', label: '全选' }
      ]
    },
    {
      label: '视图',
      submenu: [
        { role: 'reload', label: '重新加载' },
        { role: 'forceReload', label: '强制重新加载' },
        { role: 'toggleDevTools', label: '切换开发者工具' },
        { type: 'separator' },
        { role: 'resetZoom', label: '实际大小' },
        { role: 'zoomIn', label: '放大' },
        { role: 'zoomOut', label: '缩小' },
        { type: 'separator' },
        { role: 'togglefullscreen', label: '进入或退出全屏' }
      ]
    },
    {
      label: '窗口',
      submenu: [
        { role: 'minimize', label: '最小化' },
        {
          label: '最大化',
          click: () => {
            const w = BrowserWindow.getFocusedWindow() ?? BrowserWindow.getAllWindows()[0];
            if (!w) return;
            if (w.isMaximized()) w.unmaximize();
            else w.maximize();
          }
        },
        { role: 'close', label: '关闭' }
      ]
    }
  ];
  Menu.setApplicationMenu(Menu.buildFromTemplate(template));
}

function boot(): void {
  let settings: Settings;
  let info: DbInfo;
  let queries: QueryService;
  let study: StudyService;
  let win: BrowserWindow | null = null;

  app.whenReady().then(() => {
    const userData = app.getPath('userData');
    fs.mkdirSync(userData, { recursive: true });
    settings = new Settings(path.join(userData, 'settings.json'));

    nativeTheme.themeSource = settings.themeMode;

    const t0 = Date.now();
    const ensured = ensureDatabase(path.join(userData, 'words.db'), resourcesDir(), log);
    queries = new QueryService(ensured.db);
    study = new StudyService(ensured.db, queries);
    info = ensured.info;
    log(`db-ready ms=${Date.now() - t0} words=${info.words} phrases=${info.phrases} dataVersion=${info.dataVersion}`);

    registerIpc({ queries, study, settings, info, log });

    if (SELFTEST_GEOMETRY) {
      // 伪造一份越界的持久化几何，走正常建窗路径，验证 clampBounds 会把它拉回工作区。
      settings.setBounds({ x: -24000, y: -24000, width: 9000, height: 9000 });
      log('geometry-seed bounds={"x":-24000,"y":-24000,"width":9000,"height":9000}');
    }
    installAppMenu();
    createWindow();
  });

  function createWindow(): void {
    const saved = settings?.bounds;
    const prepared = clampBounds(saved ?? DEFAULT_BOUNDS);
    if (saved && prepared.clamped) log(`bounds-clamped from=${JSON.stringify(saved)} to=${JSON.stringify(prepared.bounds)}`);

    win = new BrowserWindow({
      title: APP_TITLE,
      width: prepared.bounds.width,
      height: prepared.bounds.height,
      x: prepared.bounds.x,
      y: prepared.bounds.y,
      minWidth: MIN_WIDTH,
      minHeight: MIN_HEIGHT,
      show: false,
      backgroundColor: nativeTheme.shouldUseDarkColors ? '#10151c' : '#f6f8fb',
      webPreferences: {
        preload: path.join(__dirname, 'preload.js'),
        contextIsolation: true,
        nodeIntegration: false,
        sandbox: true
      }
    });

    win.on('page-title-updated', (e) => e.preventDefault());
    win.webContents.on('console-message', (_e, level, message) => {
      log(`renderer-console level=${level} ${message.slice(0, 400)}`);
    });
    win.webContents.on('did-fail-load', (_e, code, desc, url) => {
      log(`renderer-did-fail-load code=${code} desc=${desc} url=${url}`);
    });

    const persist = () => {
      if (!win || SELFTEST) return;
      const b = win.getBounds();
      settings.setBounds({ x: b.x, y: b.y, width: b.width, height: b.height });
    };
    win.on('resize', persist);
    win.on('move', persist);

    win.once('ready-to-show', () => {
      win?.show();
      log(`ready-to-show ms=${Date.now() - BOOT_T0}`);
      log(`title=${win?.getTitle()}`);
      marker('window-shown');
      if (SELFTEST) void (MENU_AUDIT ? runMenuAudit() : STUDY_AUDIT ? runStudyAudit() : runSelfTest());
    });

    if (SELFTEST_GEOMETRY) {
      const b = win.getBounds();
      const inside = screen.getAllDisplays().some((d) => {
        const wa = d.workArea;
        return b.x >= wa.x && b.y >= wa.y && b.x + b.width <= wa.x + wa.width + 1 && b.y + b.height <= wa.y + wa.height + 1;
      });
      log(`geometry-from-settings bounds=${JSON.stringify(b)} within-workarea=${String(inside)}`);
      log(inside ? 'SELFTEST-OK' : 'SELFTEST-FAIL');
      app.quit();
      return;
    }

    if (process.env.VITE_DEV_SERVER_URL) {
      void win.loadURL(process.env.VITE_DEV_SERVER_URL);
    } else {
      void win.loadFile(path.join(__dirname, '..', 'dist', 'index.html'));
    }
  }

  const js = (code: string) => win!.webContents.executeJavaScript(code, true);

    // 定向测试：只审学习页的每个组件在多挡窗口尺寸下的尺寸与溢出，不跑全局回归。
    // 三类判据：比父容器宽（overW）、不可横向滚动却内容溢出（clipX）、被窗口右边界裁掉（outOfWindow）。
    const STUDY_WIDTHS = [1440, 1280, 1100, 960, 860, 760, 680];
    const STUDY_HEIGHTS = [700, 820, 1000, 1180];
    type Box = { left: number; top: number; right: number; bottom: number };
    const TILT_WIDTHS = [1440, 1280, 960, 760];
    type TiltSide = { left: number; right: number; w: number; h: number; tf: string };
    type TiltProbe = {
      vw: number;
      reduce: boolean;
      attrs: string;
      tf0: string;
      lift: number;
      base: { left: number; right: number; w: number };
      sides: { left: TiltSide; right: TiltSide };
    };
    type HeightProbe = {
      vh: number; panelH: number; rows: number; chipsOut: number;
      cardH: number; clipH: number; inside: boolean; hoverFits: boolean;
    };
    const detector = `(async () => {
      const wait = (ms) => new Promise((r) => setTimeout(r, ms));
      await wait(120);
      const root = document.querySelector('.main-area');
      if (!root) return { error: 'no .main-area' };
      const name = (el) => {
        const cls = String(el.className || '').trim().split(/\\s+/).filter(Boolean).slice(0, 2).join('.');
        return el.tagName.toLowerCase() + (cls ? '.' + cls : '');
      };
      const bad = [];
      const seen = new Set();
      const walk = (el) => {
        for (const kid of Array.from(el.children)) {
          const kb = kid.getBoundingClientRect();
          const pb = kid.parentElement.getBoundingClientRect();
          const cs = getComputedStyle(kid);
          // SVG 内部元素没有 scroll 语义（text 的 scrollWidth 恒大于 clientWidth），只审 HTML 组件盒。
          if (kb.width > 0 && kb.height > 0 && kid.namespaceURI !== 'http://www.w3.org/2000/svg') {
            const scrollableX = /auto|scroll|hidden/.test(cs.overflowX);
            const scrollableY = /auto|scroll|hidden/.test(cs.overflowY);
            // 自己带 3D 变换的层（FlipCard 的转子：tilt + hoverScale）投影出来本来就比布局盒大，
            // 那是设计不是缺陷；它的越界由 study-card-tilt-stays-in-window 用真实投影判。
            const projected = cs.transform.startsWith('matrix3d');
            // 带 3D 投影子层时不能信 scrollWidth/scrollHeight：转子投影比布局盒宽 22px，
            // 会把父盒的滚动尺寸撑大，量出来的"横向裁切"是投影不是缺陷。
            const hasProjectedKid = Array.from(kid.children).some((c) => getComputedStyle(c).transform.startsWith('matrix3d'));
            const row = {
              el: name(kid),
              w: Math.round(kb.width),
              h: Math.round(kb.height),
              parent: name(kid.parentElement),
              pw: Math.round(pb.width),
              overW: !projected && kb.width > pb.width + 2,
              clipX: !scrollableX && !hasProjectedKid && kid.scrollWidth > kid.clientWidth + 1,
              // 竖向也要量：overflow:hidden + min-height:0 会把行高压成"看不见的下一屏"，按钮整排消失。
              // 用子元素实际底边判定，不用 scrollHeight——卡片的装饰阴影（aria-hidden）会把滚动高度撑大 20px 造成假阳性。
              clipY: !scrollableY && Array.from(kid.children).some((c) => {
                if (c.getAttribute('aria-hidden') === 'true') return false;
                if (getComputedStyle(c).transform.startsWith('matrix3d')) return false;
                return c.getBoundingClientRect().bottom > kb.bottom + 2;
              }),
              ch: kid.clientHeight,
              sh: kid.scrollHeight,
              outOfWindow: kb.right > window.innerWidth + 1 || kb.left < -1,
              aspect: +(kb.width / kb.height).toFixed(2)
            };
            if ((row.overW || row.clipX || row.clipY || row.outOfWindow) && !seen.has(row.el + row.w)) {
              seen.add(row.el + row.w);
              bad.push(row);
            }
          }
          walk(kid);
        }
      };
      walk(root);
      return { offenders: bad.slice(0, 20), total: bad.length };
    })()`;

    // 倾斜探针：真把指针打到卡片左右两端去驱动 FlipCard 的 tilt（onPointerMove 委托到根容器，合成 pointermove 进得去；
    // hoverScale 走的 pointerenter 进不去，所以那条只能判几何不变量）。3D 透视下近端外扩，量的是投影后的实际盒。
    const tiltProbe = `(async () => {
      const wait = (ms) => new Promise((r) => setTimeout(r, ms));
      const card = document.querySelector('.flip-card');
      if (!card) return null;
      // 量转子而不是根盒：倾斜与悬浮缩放都打在 .flip-card__rotor 上，
      // 根的 getBoundingClientRect 不随子元素变换变化（实测恒为 720，会把外扩量成 0）。
      const rotor = card.querySelector('.flip-card__rotor') || card;
      // 先把上一次可能残留的悬浮态清掉，否则 base 量到的是抬升后的盒（实测 1440 挡 base 直接是 742）。
      card.dispatchEvent(new PointerEvent('pointerout', {
        bubbles: true, pointerType: 'mouse', clientX: 4, clientY: 4, relatedTarget: document.documentElement
      }));
      await new Promise((r) => setTimeout(r, 520));
      const r0 = rotor.getBoundingClientRect();
      const out = {
        vw: window.innerWidth,
        reduce: matchMedia('(prefers-reduced-motion: reduce)').matches,
        attrs: card.getAttributeNames().join(','),
        tf0: rotor ? getComputedStyle(rotor).transform.slice(0, 46) : 'no-rotor',
        base: { left: Math.round(r0.left), right: Math.round(r0.right), w: Math.round(r0.width) },
        lift: 1,
        sides: {}
      };
      for (const side of ['left', 'right']) {
        const x0 = side === 'left' ? r0.left + 6 : r0.right - 6;
        const y0 = r0.top + r0.height / 2;
        // 悬浮抬升 lift 走的是 React 合成 onPointerEnter：要带 relatedTarget（在卡片外）才会生成 enter 事件，
        // 只发 pointerenter 进不去（实测 grew=1）。用户看到的是 tilt + lift 叠加后的投影，两样都得驱动。
        card.dispatchEvent(new PointerEvent('pointerover', {
          bubbles: true, pointerType: 'mouse', clientX: x0, clientY: y0, relatedTarget: document.documentElement
        }));
        await wait(420);
        out.lift = Math.max(out.lift ?? 1, +(rotor.getBoundingClientRect().width / r0.width).toFixed(3));
        for (let i = 0; i < 6; i += 1) {
          card.dispatchEvent(new PointerEvent('pointermove', {
            bubbles: true, pointerType: 'mouse', clientX: x0 + (side === 'left' ? -i : i), clientY: y0
          }));
          await wait(70);
        }
        await wait(760);
        const r = rotor.getBoundingClientRect();
        out.sides[side] = { left: Math.round(r.left), right: Math.round(r.right), w: Math.round(r.width), h: Math.round(r.height), tf: rotor ? getComputedStyle(rotor).transform.slice(0, 46) : 'no-rotor' };
        card.dispatchEvent(new PointerEvent('pointerout', {
          bubbles: true, pointerType: 'mouse', clientX: x0, clientY: y0, relatedTarget: document.documentElement
        }));
        card.dispatchEvent(new PointerEvent('pointerleave', { bubbles: true, pointerType: 'mouse', clientX: x0, clientY: y0 }));
        await wait(520);
      }
      return out;
    })()`;

    // 竖向探针：工具条的每一行是否落在面板自己的背景框内；卡片是否落在裁切祖先（.stack，overflow:hidden）内；
    // 以及卡片按 hoverScale 放大后是否仍在裁切框内——悬浮截断就是这么来的。
    const heightProbe = `(() => {
      const box = (el) => { const b = el.getBoundingClientRect(); return { top: +b.top.toFixed(1), bottom: +b.bottom.toFixed(1), h: Math.round(b.height) }; };
      const panel = document.querySelector('.main-area > .stack > .panel');
      const card = document.querySelector('.flip-card');
      const clip = card ? card.closest('.stack') : null;
      if (!panel || !card || !clip) return null;
      const pb = box(panel);
      const rows = Array.from(panel.querySelectorAll('.row')).map(box);
      const cb = box(card);
      const kb = box(clip);
      return {
        vh: window.innerHeight,
        panelH: pb.h,
        rows: rows.length,
        chipsOut: rows.filter((r) => r.bottom > pb.bottom - 0.5 || r.top < pb.top + 0.5).length,
        cardH: cb.h,
        clipH: kb.h,
        inside: cb.bottom <= kb.bottom + 1 && cb.top >= kb.top - 1,
        hoverFits: cb.h * 1.03 <= kb.h + 1
      };
    })()`;

    // 共享判据容器：全局自证与学习页定向审计都要写它，所以声明在两者共同的外层作用域。
    const checks: string[] = [];
    const ok = (name: string, pass: boolean, detail: string) => {
      checks.push(`${name}=${pass ? 'PASS' : 'FAIL'} ${detail}`);
      log(`check ${name} ${pass ? 'PASS' : 'FAIL'} ${detail}`);
    };
    const shotsDir = process.env.SELFTEST_SHOTS;

    // 定向挡：只判顶部原生应用菜单。菜单不在 DOM 里，探针走主进程；改菜单跑这条就够，别惊动全量 42 条。
    const runMenuAudit = async (): Promise<void> => {
      const lines: string[] = [];
      const walk = (nodes: Electron.MenuItem[], depth: number): void => {
        for (const it of nodes) {
          if (it.type === 'separator') {
            lines.push(`${'    '.repeat(depth)}— 分隔线 —`);
            continue;
          }
          lines.push(
            `${'  '.repeat(depth)}${it.label || '(无标签)'}${it.role ? ` [role=${it.role}]` : ''}${it.accelerator ? ` <${it.accelerator}>` : ''}${it.enabled ? '' : ' (禁用)'}`
          );
          if (it.submenu) walk(it.submenu.items, depth + 1);
        }
      };
      walk(Menu.getApplicationMenu()?.items ?? [], 0);
      lines.forEach((l) => log(`  menu ${l}`));
      const tops = (Menu.getApplicationMenu()?.items ?? []).map((i) => i.label);
      const leaves = lines.filter((l) => !l.includes('分隔线')).length - tops.length;
      const nonChinese = lines.filter((l) => !l.includes('分隔线') && !/[一-鿿]/.test(l));
      ok(
        'menu-labels-are-chinese',
        tops.length === 4 && leaves === 18 && nonChinese.length === 0,
        `locale=${app.getLocale()} system=${app.getSystemLocale()} tops=${JSON.stringify(tops)} leaves=${leaves} 非中文=${JSON.stringify(nonChinese)}`
      );
      const passed = checks.filter((c) => c.includes('PASS')).length;
      log(`MENU-AUDIT-SUMMARY checks=${checks.length} pass=${passed} fail=${checks.length - passed} mode=menu-audit`);
      log(passed === checks.length && checks.length === 1 ? 'SELFTEST-OK' : 'SELFTEST-FAIL');
      // 必须显式退出：不 quit 的话定向挡会一直开着窗口，被 selftest.mjs 的 300 s 超时 SIGTERM 掉，
      // 判据全绿也拿不到退出码 0（实测踩过）。另两挡同理，见 :237 / :737 / :1780。
      app.quit();
    };

    const runStudyAudit = async (): Promise<void> => {
      const failures: string[] = [];
      const rows: string[] = [];
      const b0 = win!.getBounds();
      await js('(() => { const b = document.querySelectorAll(\'.topbar .segmented[aria-label="视图"] button\')[2]; if (b) b.click(); return Boolean(b); })()');
      await new Promise((r) => setTimeout(r, 1200));

      // 只有"直达学习页"才观察得到 store 的默认日期：全量挡会先点主页的「重新抽样这一天」
      // （HomeView.tsx:76 → setSelectedDate(stats.today)，stats.today 是主进程的本地今天），把默认值盖掉。
      // store 之前写的是 toISOString().slice(0,10)（UTC 日期），东八区 00:00–07:59 与主进程的本地今天差一天
      // → StudyView「是今天才自动抽样」不成立 → 学习页只显昨天那批、当天没有卡（实测 14 条判据全 null）。
      const wantDate = todayLocalDate();
      const defaultDate = (await js(`(() => {
        const input = document.querySelector('input[type="date"]');
        const hint = Array.from(document.querySelectorAll('.panel-hint')).find((e) => (e.textContent || '').includes('没有学习记录'));
        const d = new Date();
        const p = (n) => String(n).padStart(2, '0');
        return {
          value: input ? input.value : 'no-input',
          card: Boolean(document.querySelector('.flip-card')),
          hint: hint ? String(hint.textContent).slice(0, 16) : '',
          utcIso: d.toISOString().slice(0, 10),
          localIso: d.getFullYear() + '-' + p(d.getMonth() + 1) + '-' + p(d.getDate())
        };
      })()`)) as { value: string; card: boolean; hint: string; utcIso: string; localIso: string } | null;
      ok(
        'study-default-date-is-local',
        defaultDate !== null && defaultDate.value === wantDate && defaultDate.card,
        `want=${wantDate} value=${defaultDate?.value} utc=${defaultDate?.utcIso} discriminating=${defaultDate !== null && wantDate !== defaultDate.utcIso} card=${defaultDate?.card} hint="${defaultDate?.hint}"`
      );
      for (const w of STUDY_WIDTHS) {
        win!.setBounds({ x: b0.x, y: b0.y, width: w, height: 820 }, false);
        await new Promise((r) => setTimeout(r, 520));
        for (const face of ['front', 'back'] as const) {
          if (face === 'back') {
            await js('(() => { const b = document.querySelector("[data-flip-btn]"); if (b) b.click(); return Boolean(b); })()');
          }
          // 等弹簧停稳再量：动画途中正反面会被 perspective/rotateX 拉开，量出来的"变形"是假的。
          await new Promise((r) => setTimeout(r, 1400));
          const faces = (await js(`(() => {
            const card = document.querySelector('.flip-card');
            const f = document.querySelector('.flip-card__face--front');
            const k = document.querySelector('.flip-card__face--back');
            if (!card || !f || !k) return null;
            // 参照盒取转子而不是根：lift/tilt 都加在转子上，两个卡面与转子同生同灭；
            // 拿未变换的根盒当参照会在鼠标恰好停在卡片上时把 1.03 的缩放误判成"卡面没对齐"。
            const a = (document.querySelector('.flip-card__rotor') || card).getBoundingClientRect();
            const b = f.getBoundingClientRect();
            const c = k.getBoundingClientRect();
            return {
              card: [Math.round(a.width), Math.round(a.height)],
              front: [Math.round(b.width), Math.round(b.height)],
              back: [Math.round(c.width), Math.round(c.height)],
              dTop: Math.abs(b.top - c.top),
              dLeft: Math.abs(b.left - c.left),
              frontMatchesCard: Math.abs(b.width - a.width) <= 2 && Math.abs(b.height - a.height) <= 2,
              backMatchesCard: Math.abs(c.width - a.width) <= 2 && Math.abs(c.height - a.height) <= 2
            };
          })()`)) as { card: number[]; front: number[]; back: number[]; dTop: number; dLeft: number; frontMatchesCard: boolean; backMatchesCard: boolean } | null;
          // 变形判据：卡面塌扁（高度不足）或长宽比越界，都算"内容严重变形"，不只是溢出。
          const aspect = faces ? faces.card[0] / faces.card[1] : 0;
          const geo = (await js(`(() => {
            const sels = ['.panel', '.flashcard-wrap', '.flashcard-wrap > div', '.card-row', '.side-card', '.card-slot', '.flip-card', '.flip-card__rotor', '.flip-card__face--front', '.flip-card__face--back', '.card-face', '.card-index', '.card-word', '.rating-row', '.study-heat'];
            const out = [];
            for (const s of sels) {
              for (const el of Array.from(document.querySelectorAll(s))) {
                const b = el.getBoundingClientRect();
                const cs = getComputedStyle(el);
                out.push(s + ' x=' + Math.round(b.x) + ' y=' + Math.round(b.y) + ' ' + Math.round(b.width) + 'x' + Math.round(b.height)
                  + ' pos=' + cs.position + ' op=' + cs.opacity + ' vis=' + cs.visibility + ' bb=' + cs.backfaceVisibility
                  + ' fcw=' + (cs.getPropertyValue('--fc-w') || '-')
                  + ' tf=' + cs.transform.replace(/\\s+/g, '').slice(0, 64));
              }
            }
            return out;
          })()`)) as string[];
          geo.forEach((g) => log(`  study-geo w=${w} face=${face} ${g}`));
          // 错位判据（三卡行版）：工具条/整行/评分行/热力图左边缘对齐；主卡在这一行里真居中；
          // 左右侧卡与主卡顶边底边齐平；热力图既不内滚（竖向滚动条会吃掉 10px 让右边缘错位）也不被裁。
          if (face === 'front') {
            const edges = (await js(`(() => {
              const r = (el) => { const b = el.getBoundingClientRect(); return { left: +b.left.toFixed(1), top: +b.top.toFixed(1), right: +b.right.toFixed(1), bottom: +b.bottom.toFixed(1) }; };
              const pick = (s) => { const el = document.querySelector(s); return el ? r(el) : null; };
              const heat = document.querySelector('.study-heat');
              const rowEl = document.querySelector('.card-row');
              return {
                toolbar: pick('.main-area > .stack > .panel'),
                row: pick('.card-row'),
                // padding-top 从 CSS 现读，别在判据里再写一遍 48：两处数字迟早会漂。
                rowPad: rowEl ? parseFloat(getComputedStyle(rowEl).paddingTop) || 0 : -1,
                card: pick('.flip-card'),
                rating: pick('.rating-row'),
                heat: pick('.study-heat'),
                heatBox: heat ? { ch: heat.clientHeight, sh: heat.scrollHeight, sw: heat.scrollWidth, cw: heat.clientWidth } : null,
                sides: Array.from(document.querySelectorAll('[data-side-card]')).map(r)
              };
            })()`)) as { toolbar: Box | null; row: Box | null; rowPad: number; card: Box | null; rating: Box | null; heat: Box | null; heatBox: { ch: number; sh: number; sw: number; cw: number } | null; sides: Box[] };
            const lefts = [edges.toolbar?.left, edges.row?.left, edges.rating?.left, edges.heat?.left].filter((v): v is number => typeof v === 'number');
            const leftSpread = lefts.length === 4 ? Math.max(...lefts) - Math.min(...lefts) : 999;
            const rightSpread = edges.toolbar && edges.heat ? Math.abs(edges.toolbar.right - edges.heat.right) : 999;
            const centered = edges.card !== null && edges.row !== null
              && Math.abs((edges.card.left + edges.card.right) / 2 - (edges.row.left + edges.row.right) / 2) <= 2;
            // 整行离工具条一截（padding-top），三张卡都落在这条线以下并且顶边齐平。
            const rowDropped = edges.card !== null && edges.row !== null && edges.rowPad > 0
              && Math.abs(edges.card.top - edges.row.top - edges.rowPad) <= 2;
            // 侧卡收起时（窄列）只查主卡那一截下移；有侧卡时正方形与两端位置也一并查。
            const sidesVerified = edges.sides.length === 0
              || (edges.row !== null && edges.card !== null && edges.sides.length === 2
                // 1) 两张都是正方形（宽高相等）
                && edges.sides.every((s) => Math.abs((s.right - s.left) - (s.bottom - s.top)) <= 2)
                // 2) 钉在行的两端：左卡贴行左、右卡贴行右
                && Math.abs(edges.sides[0].left - edges.row.left) <= 2
                && Math.abs(edges.sides[1].right - edges.row.right) <= 2
                // 3) 侧卡顶边与主卡顶边齐平（是侧卡下移去齐中间，不是把中间拉上去）
                && edges.sides.every((s) => Math.abs(s.top - edges.card!.top) <= 2));
            const heatUnclipped = edges.heatBox !== null && edges.heatBox.sh <= edges.heatBox.ch + 1 && edges.heatBox.sw <= edges.heatBox.cw + 1;
            ok(
              `study-blocks-edge-aligned-${w}`,
              leftSpread <= 2 && rightSpread <= 2 && centered && sidesVerified && rowDropped && heatUnclipped,
              `leftSpread=${leftSpread.toFixed(1)} rightSpread=${rightSpread.toFixed(1)} centered=${centered} sides=${edges.sides.length} sidesVerified=${sidesVerified} rowDropped=${rowDropped} drop=${edges.card && edges.row ? (edges.card.top - edges.row.top).toFixed(1) : '-'} pad=${edges.rowPad} sideBox=${JSON.stringify(edges.sides)} cardLeft=${edges.card?.left} rowLeft=${edges.row?.left} heatUnclipped=${heatUnclipped} heat=${JSON.stringify(edges.heatBox)}`
            );
          }
          // 高度下限取 StudyView 的 CARD_MIN_H（300）：7 个模块 chip 会让工具条在窄窗口换行，
          // 卡片于是落到自己的地板上（实测 680 挡 544×300）。那是设计，地板以下才算塌。
          const shaped = faces !== null && faces.card[1] >= 300 && aspect >= 0.8 && aspect <= 2.6;
          const faceOk = faces !== null && faces.frontMatchesCard && faces.backMatchesCard && faces.dTop <= 2 && faces.dLeft <= 2 && shaped;
          if (!faceOk) failures.push(`w=${w} face=${face} 卡面异常 ${JSON.stringify(faces)} aspect=${aspect.toFixed(2)}`);
          else rows.push(`w=${w} face=${face} faces=${JSON.stringify(faces.card)} aspect=${aspect.toFixed(2)}`);
          const res = (await js(detector)) as { error?: string; offenders?: unknown[]; total?: number };
          const label = `w=${w} face=${face}`;
          if (res.error) {
            failures.push(`${label} ${res.error}`);
            continue;
          }
          rows.push(`${label} offenders=${res.total}`);
          if (res.total && res.total > 0) {
            failures.push(`${label} -> ${JSON.stringify(res.offenders)}`);
          }
          if (shotsDir) {
            const image = await win!.webContents.capturePage();
            fs.mkdirSync(shotsDir, { recursive: true });
            fs.writeFileSync(path.join(shotsDir, `study-audit-${w}-${face}.png`), image.toPNG());
          }
        }
        await js('(() => { const b = document.querySelector("[data-flip-btn]"); const f = document.querySelector(\'.flip-card__face--back\'); if (b && f && f.getAttribute("aria-hidden") === "false") b.click(); return true; })()');
        await new Promise((r) => setTimeout(r, 300));
      }
      // 第二轮：竖向。横向扫不出这三类问题——工具条内容超出自己的背景栏、卡片悬浮放大后被上层的
      // overflow:hidden 裁掉、以及卡片高度根本不跟窗高联动（用户报的"高度会截断/不跟随"）。
      const heightRuns: Array<{ h: number; r: HeightProbe | null }> = [];
      for (const h of STUDY_HEIGHTS) {
        win!.setBounds({ x: b0.x, y: b0.y, width: 1280, height: h }, false);
        await new Promise((r) => setTimeout(r, 640));
        heightRuns.push({ h, r: (await js(heightProbe)) as HeightProbe | null });
        if (shotsDir) {
          const image = await win!.webContents.capturePage();
          fs.mkdirSync(shotsDir, { recursive: true });
          fs.writeFileSync(path.join(shotsDir, `study-h-${h}.png`), image.toPNG());
        }
      }
      // 卡片被压到最小挡（700 窗高 → 300px 卡）时，背面滚动区还能不能滚到最后一块词组。
      win!.setBounds({ x: b0.x, y: b0.y, width: 1280, height: 700 }, false);
      await new Promise((r) => setTimeout(r, 640));
      await js('(() => { const b = document.querySelector("[data-flip-btn]"); if (b) b.click(); return Boolean(b); })()');
      await new Promise((r) => setTimeout(r, 1400));
      const backMin = (await js(`(() => {
        const face = document.querySelector('.flip-card__face--back');
        const el = document.querySelector('[data-card-back]');
        if (!el) return null;
        el.scrollTop = el.scrollHeight;
        const cs = getComputedStyle(el);
        const blocks = el.querySelectorAll('.phrase-block');
        const last = blocks.length ? blocks[blocks.length - 1] : null;
        const rect = el.getBoundingClientRect();
        const lastRect = last ? last.getBoundingClientRect() : null;
        const card = document.querySelector('.flip-card');
        return {
          cardH: card ? Math.round(card.getBoundingClientRect().height) : -1,
          shown: face ? face.getAttribute('aria-hidden') : 'no-face',
          blocks: blocks.length,
          scrollH: el.scrollHeight,
          clientH: el.clientHeight,
          overflowY: cs.overflowY,
          lastWithin: lastRect ? lastRect.bottom <= rect.bottom + 2 : false
        };
      })()`)) as { cardH: number; shown: string; blocks: number; scrollH: number; clientH: number; overflowY: string; lastWithin: boolean } | null;
      ok(
        'study-card-back-reachable-at-min-height',
        backMin !== null &&
          backMin.blocks > 0 &&
          backMin.shown === 'false' &&
          (backMin.scrollH <= backMin.clientH + 2 || (/auto|scroll/.test(backMin.overflowY) && backMin.lastWithin)),
        JSON.stringify(backMin)
      );
      await js('(() => { const b = document.querySelector("[data-flip-btn]"); if (b) b.click(); return true; })()');
      await new Promise((r) => setTimeout(r, 400));
      win!.setBounds({ x: b0.x, y: b0.y, width: 1440, height: b0.height }, false);
      heightRuns.forEach((x) => log(`  study-h h=${x.h} ${JSON.stringify(x.r)}`));
      const bars = heightRuns.map((x) => x.r).filter((r): r is HeightProbe => r !== null);
      ok(
        'study-toolbar-chips-inside-bar',
        bars.length === STUDY_HEIGHTS.length && bars.every((r) => r.rows > 0 && r.chipsOut === 0),
        `heights=${heightRuns.map((x) => `${x.h}:${x.r ? `${x.r.chipsOut}/${x.r.panelH}` : 'null'}`).join(' ')} (超出条数/面板高)`
      );
      ok(
        'study-card-hover-fits-vertically',
        bars.length > 0 && bars.every((r) => r.inside && r.hoverFits),
        `heights=${heightRuns.map((x) => `${x.h}:${x.r ? `${x.r.cardH}/${x.r.clipH}(${x.r.inside ? 'in' : 'CUT'},${x.r.hoverFits ? 'scale-ok' : 'scale-cut'})` : 'null'}`).join(' ')}`
      );
      const cardHeights = bars.map((r) => r.cardH);
      const grew = cardHeights.every((v, i) => i === 0 || v >= cardHeights[i - 1]);
      ok(
        'study-card-height-tracks-window',
        bars.length >= 3 && grew && Math.max(...cardHeights) - Math.min(...cardHeights) >= 60,
        `cardHeights=${cardHeights.join('→')} monotonic=${grew} span=${cardHeights.length ? Math.max(...cardHeights) - Math.min(...cardHeights) : 0} vhs=${bars.map((r) => r.vh).join('/')}`
      );
      // 三卡行行为：侧卡显示相邻词，点侧卡要把那个词换到中间，并且两侧跟着刷新（循环序列）。
      // 用新的默认窗口尺寸跑，顺带证明默认大小下侧卡确实是展开的。
      win!.setBounds({ x: b0.x, y: b0.y, width: DEFAULT_BOUNDS.width, height: DEFAULT_BOUNDS.height }, false);
      await new Promise((r) => setTimeout(r, 760));
      if (shotsDir) {
        const image = await win!.webContents.capturePage();
        fs.mkdirSync(shotsDir, { recursive: true });
        fs.writeFileSync(path.join(shotsDir, 'study-default.png'), image.toPNG());
      }
      const nav = (await js(`(async () => {
        const wait = (ms) => new Promise((r) => setTimeout(r, ms));
        const wordOf = () => (document.querySelector('.card-word')?.textContent || '').trim();
        const side = (label) => (document.querySelector('[data-side-card="' + label + '"] .side-word')?.textContent || '').trim();
        const start = wordOf();
        const p0 = side('上一个');
        const n0 = side('下一个');
        const btn = document.querySelector('[data-side-card="下一个"]');
        if (!btn) return { start, p0, n0, clicked: false, after: '', p1: '', n1: '' };
        btn.click();
        await wait(900);
        return { start, p0, n0, clicked: true, after: wordOf(), p1: side('上一个'), n1: side('下一个') };
      })()`)) as { start: string; p0: string; n0: string; clicked: boolean; after: string; p1: string; n1: string };
      ok(
        'study-side-cards-navigate',
        nav.clicked && nav.p0 !== '' && nav.n0 !== '' && nav.after === nav.n0 && nav.p1 === nav.start && nav.n1 !== nav.after,
        JSON.stringify(nav)
      );
      // 第三轮：3D 倾斜投影。透视下近端外扩，卡片离窗口边太近时那半张卡就被裁掉
      // （用户报的"鼠标悬浮到右侧时左侧悬浮的部分显示不全"）。量的是投影后的实际盒，不是布局盒。
      const tiltRuns: Array<{ w: number; r: TiltProbe | null }> = [];
      for (const w of TILT_WIDTHS) {
        win!.setBounds({ x: b0.x, y: b0.y, width: w, height: 820 }, false);
        await new Promise((r) => setTimeout(r, 620));
        tiltRuns.push({ w, r: (await js(tiltProbe)) as TiltProbe | null });
      }
      tiltRuns.forEach((x) => log(`  study-tilt w=${x.w} ${JSON.stringify(x.r)}`));
      const tilts = tiltRuns.map((x) => x.r).filter((r): r is TiltProbe => r !== null);
      // 先确认倾斜真的被驱动了：投影宽没变就说明探针空转，那条 PASS 不能算数。
      // 证探针不是空转：每一挡至少要有一侧的投影盒相对静止盒真的移动（>2px）。
      // 不拿 lift 当闸门——抬升弹簧偶尔在某一挡没起来（实测 1280 挡 lift=1），那只会把判据弄成假红。
      const movedBox = (a: { left: number; right: number }, b: { left: number; right: number }) =>
        Math.abs(a.left - b.left) > 2 || Math.abs(a.right - b.right) > 2;
      const tiltObserved = tilts.every((r) => movedBox(r.sides.left, r.base) || movedBox(r.sides.right, r.base));
      const tiltInside = tilts.every((r) =>
        r.sides.left.left >= 2 && r.sides.left.right <= r.vw - 2 && r.sides.right.left >= 2 && r.sides.right.right <= r.vw - 2
      );
      ok(
        'study-card-tilt-stays-in-window',
        tilts.length === TILT_WIDTHS.length && tiltObserved && tiltInside,
        `observed=${tiltObserved} inside=${tiltInside} runs=${tiltRuns
          .map((x) => (x.r ? `w${x.w}:lift=${x.r.lift} base=${x.r.base.left}..${x.r.base.right} L=${x.r.sides.left.left}..${x.r.sides.left.right} R=${x.r.sides.right.left}..${x.r.sides.right.right} vw=${x.r.vw}` : `w${x.w}:null`))
          .join(' | ')}`
      );
      // 留一张"指针钉在右边缘、倾斜到位"的现场图：探针结束时已经 pointerleave 归位，截图要另开一趟。
      if (shotsDir) {
        win!.setBounds({ x: b0.x, y: b0.y, width: 960, height: 820 }, false);
        await new Promise((r) => setTimeout(r, 620));
        await js(`(async () => {
          const c = document.querySelector('.flip-card');
          if (!c) return false;
          const r0 = c.getBoundingClientRect();
          const y = r0.top + r0.height / 2;
          for (let i = 0; i < 6; i += 1) {
            c.dispatchEvent(new PointerEvent('pointermove', { bubbles: true, pointerType: 'mouse', clientX: r0.right - 6 + i, clientY: y }));
            await new Promise((r) => setTimeout(r, 70));
          }
          await new Promise((r) => setTimeout(r, 760));
          return true;
        })()`);
        const image = await win!.webContents.capturePage();
        fs.mkdirSync(shotsDir, { recursive: true });
        fs.writeFileSync(path.join(shotsDir, 'study-tilt-right-960.png'), image.toPNG());
      }
      rows.forEach((r) => log(`  study-audit ${r}`));
      failures.slice(0, 8).forEach((f) => log(`    ! ${f.slice(0, 1200)}`));
      ok('study-components-no-overflow', failures.length === 0, `${rows.join(' | ')} failing=${failures.length}`);
      const passed = checks.filter((c) => c.includes('PASS')).length;
      log(`STUDY-AUDIT-SUMMARY checks=${checks.length} pass=${passed} fail=${checks.length - passed} mode=study-audit`);
      log(passed === checks.length ? 'SELFTEST-OK' : 'SELFTEST-FAIL');
      app.quit();
    };

  async function runSelfTest(): Promise<void> {
    await new Promise((r) => setTimeout(r, 900));

    const pong = (await js('window.api.ping()')) as string;
    ok('ipc-roundtrip', pong === 'pong', `pong=${pong}`);
    marker('probed');

    const title = (await js('document.title')) as string;
    ok('title-kept-by-main', title === APP_TITLE, `renderer-title=${JSON.stringify(title)} main=${JSON.stringify(win!.getTitle())}`);

    // 默认值两条：首屏落在查询页、新配置下窗口是 1280×800。必须在任何点击/改尺寸之前量，否则测的是自己改出来的状态。
    const bootBounds = win!.getBounds();
    ok(
      'default-bounds',
      bootBounds.width === DEFAULT_BOUNDS.width && bootBounds.height === DEFAULT_BOUNDS.height,
      `bounds=${bootBounds.width}x${bootBounds.height} want=${DEFAULT_BOUNDS.width}x${DEFAULT_BOUNDS.height}`
    );
    const bootView = (await js('document.querySelector(\'.app-shell\')?.getAttribute(\'data-view\') || \'\'')) as string;
    ok('default-view-is-search', bootView === 'search', `data-view=${JSON.stringify(bootView)}`);
    // 首屏证据要在任何 resize 之前抓，否则截图里的尺寸是自证脚本改出来的。
    if (shotsDir) {
      fs.mkdirSync(shotsDir, { recursive: true });
      fs.writeFileSync(path.join(shotsDir, 'boot.png'), (await win!.webContents.capturePage()).toPNG());
    }

    const bgProbe = `(async () => {
      const el = document.querySelector('[data-bg-probe]');
      const styles = getComputedStyle(el || document.body);
      return {
        dark: matchMedia('(prefers-color-scheme: dark)').matches,
        bg: el ? el.getAttribute('data-bg-probe') : styles.backgroundColor,
        token: getComputedStyle(document.documentElement).getPropertyValue('--bg').trim(),
        dataTheme: document.documentElement.dataset.theme ?? '(unset)'
      };
    })()`;
    const bgByMode: Record<string, string> = {};
    for (const mode of ['light', 'dark'] as ThemeMode[]) {
      nativeTheme.themeSource = mode;
      await new Promise((r) => setTimeout(r, 350));
      const r = (await js(bgProbe)) as { dark: boolean; bg: string; token: string; dataTheme: string };
      bgByMode[mode] = r.bg;
      const expected = mode === 'dark' ? 'rgb(16, 21, 28)' : 'rgb(246, 248, 251)';
      ok(
        `theme-${mode}`,
        r.dark === nativeTheme.shouldUseDarkColors && r.bg === expected,
        `main=${nativeTheme.shouldUseDarkColors} renderer=${r.dark} bg=${r.bg} expect=${expected} token=${r.token} data-theme=${r.dataTheme}`
      );
    }
    ok('theme-bg-flips', bgByMode.light !== bgByMode.dark, `light=${bgByMode.light} dark=${bgByMode.dark}`);
    nativeTheme.themeSource = settings.themeMode;

    const mount = (await js(`(() => {
      const root = document.getElementById('root');
      return {
        children: root ? root.childElementCount : -1,
        shell: Boolean(document.querySelector('.app-shell')),
        topbar: Boolean(document.querySelector('.topbar')),
        main: Boolean(document.querySelector('.main-area > .stack'))
      };
    })()`)) as { children: number; shell: boolean; topbar: boolean; main: boolean };
    ok('renderer-mounted', mount.children > 0 && mount.shell && mount.topbar && mount.main, JSON.stringify(mount));

    const colProbe = `(async () => {
      const el = document.querySelector('[data-grid-probe]');
      if (!el) return null;
      return getComputedStyle(el).gridTemplateColumns.split(' ').filter(Boolean).length;
    })()`;
    // 默认页已改成查询页：响应式与最小尺寸这组判据要的是主页栅格，先切回主页再量。
    await js('(() => { const b = document.querySelectorAll(\'.topbar .segmented[aria-label="视图"] button\')[0]; if (b) b.click(); return Boolean(b); })()');
    await new Promise((r) => setTimeout(r, 700));

    const widths = [1440, 1100, 684];
    const cols: number[] = [];
    for (const wpx of widths) {
      win!.setBounds({ x: win!.getBounds().x, y: win!.getBounds().y, width: wpx, height: 800 }, false);
      await new Promise((r) => setTimeout(r, 250));
      cols.push((await js(colProbe)) as number);
    }
    ok(
      'grid-breakpoints',
      cols.every((c) => c > 0) && cols[0] >= cols[1] && cols[1] >= cols[2] && cols[2] < cols[0],
      `cols@1440=${cols[0]} cols@1100=${cols[1]} cols@684=${cols[2]}`
    );

    const before = win!.getBounds();
    win!.setBounds({ x: before.x, y: before.y, width: 100, height: 100 }, false);
    await new Promise((r) => setTimeout(r, 200));
    const after = win!.getBounds();
    ok(
      'min-size-enforced',
      after.width >= MIN_WIDTH && after.height >= MIN_HEIGHT,
      `after=${after.width}x${after.height} min=${MIN_WIDTH}x${MIN_HEIGHT}`
    );

    // 逐视图点导航并断言挂载：任何一个视图整棵树抛错，都会在这里显形而不是等 null。
    const b0 = win!.getBounds();
    win!.setBounds({ x: b0.x, y: b0.y, width: 1440, height: 900 }, false);
    await new Promise((r) => setTimeout(r, 300));
    // 多宽度扫一遍：上一版只在 1440 量过，那一挡格子被 maxBlockSize 夹住，恰好掩盖了窄窗时的溢出。
    const sweepWidths = async (label: string) => {
      const sweep: Array<{ w: number; overflow: number; lastColVisible: boolean; insideSvg: boolean }> = [];
      for (const w of [2560, 1920, 1440, 1280, 1120, 1000, 880, 760, 700]) {
        const b = win!.getBounds();
        win!.setBounds({ x: b.x, y: b.y, width: w, height: b.height }, false);
        await new Promise((r) => setTimeout(r, 420));
        const m = (await js(`(() => {
          const wrap = document.querySelector("[data-heatmap]");
          const svg = wrap ? wrap.querySelector("svg") : null;
          if (!wrap || !svg) return null;
          const cells = [...svg.querySelectorAll("rect[data-date]")];
          const last = cells.sort((a, z) => Number(z.getAttribute("data-date").replace(/-/g, "")) - Number(a.getAttribute("data-date").replace(/-/g, "")))[0];
          const wb = wrap.getBoundingClientRect();
          const sb = svg.getBoundingClientRect();
          const lb = last ? last.getBoundingClientRect() : null;
          return {
            clientW: wrap.clientWidth,
            scrollW: wrap.scrollWidth,
            svgW: Math.round(sb.width),
            lastRight: lb ? +lb.right.toFixed(1) : -1,
            lastBottom: lb ? +lb.bottom.toFixed(1) : -1,
            wrapRight: +wb.right.toFixed(1),
            svgRight: +sb.right.toFixed(1),
            svgBottom: +sb.bottom.toFixed(1)
          };
        })()`)) as { clientW: number; scrollW: number; svgW: number; lastRight: number; lastBottom: number; wrapRight: number; svgRight: number; svgBottom: number } | null;
        if (!m) break;
        const row = {
          w,
          overflow: m.scrollW - m.clientW,
          lastColVisible: m.lastRight <= m.wrapRight + 0.5,
          // 关键差值：末列被 SVG 自身视口裁掉时，容器层面完全看不出来，但那一列既画不全也点不着。
          insideSvg: m.lastRight <= m.svgRight + 0.5 && m.lastBottom <= m.svgBottom + 0.5
        };
        sweep.push(row);
        log(`  sweep[${label}] w=${w} client=${m.clientW} svg=${m.svgW} overflow=${row.overflow} wrapFit=${row.lastColVisible} svgFit=${row.insideSvg}`);
      }
      const back = win!.getBounds();
      win!.setBounds({ x: back.x, y: back.y, width: 1440, height: back.height }, false);
      await new Promise((r) => setTimeout(r, 350));
      const bad = sweep.filter((s) => s.overflow > 2 || !s.lastColVisible || !s.insideSvg);
      ok(`heatmap-no-overflow-${label}`, sweep.length > 0 && bad.length === 0, `widths=${sweep.length} failing=${bad.map((s) => s.w).join("/")}`);
    };

    // 主题审计：逐视图取"有效背景 + 文字色"，断言两主题下背景真的翻转、且对比度达标。
    // 只看元素自己的 background 会被 transparent 骗过，所以向上找第一个不透明层。
    const auditSelectors: Record<string, string[]> = {
      home: [
        '.panel', '.stat-card', '.stat-value', '.stat-label', '.chip[data-module=junior]',
        '.progress > div', '.legend i', '.btn', '.segmented button[aria-pressed=true]', '.panel-hint'
      ],
      search: [
        '.searchbar', '.searchbar input', '.chip-toggle', '.letter-strip button', '.panel-title',
        '.panel-hint', '.btn', '.word-row-head .word', '.word-meaning', '.stat-label', '.phrase-item-text'
      ],
      study: [
        '.panel', '.card-word', '.card-meaning', '.card-index', '.phrase-block', '.phrase-block .usage',
        '.btn', '.legend i', '.panel-hint', '.side-card', '.side-word',
        // 7 个模块的 chip 配色全部进审计：新增模块的令牌只要对比度不够就会在这里被拦。
        '.chip[data-module=junior]', '.chip[data-module=senior]', '.chip[data-module=cet4]',
        '.chip[data-module=cet6]', '.chip[data-module=kaoyan]', '.chip[data-module=toefl]', '.chip[data-module=sat]'
      ]
    };

    const probeTheme = async (mode: 'light' | 'dark', selectors: string[]) => {
      nativeTheme.themeSource = mode;
      await new Promise((r) => setTimeout(r, 420));
      const script = `((() => {
        const parse = (s) => {
          const m = String(s).match(/rgba?\\(([^)]+)\\)/);
          if (!m) return null;
          const p = m[1].split(',').map((x) => parseFloat(x.trim()));
          return { r: p[0], g: p[1], b: p[2], a: p.length > 3 ? p[3] : 1 };
        };
        const out = {};
        for (const sel of ${JSON.stringify(selectors)}) {
          const el = document.querySelector(sel);
          if (!el) continue;
          let node = el;
          let bg = null;
          while (node && node !== document.documentElement) {
            const c = parse(getComputedStyle(node).backgroundColor);
            if (c && c.a > 0.9) { bg = c; break; }
            node = node.parentElement;
          }
          if (!bg) bg = parse(getComputedStyle(document.body).backgroundColor);
          let pnode = el.parentElement;
          let parentBg = null;
          while (pnode && pnode !== document.documentElement) {
            const c = parse(getComputedStyle(pnode).backgroundColor);
            if (c && c.a > 0.9) { parentBg = c; break; }
            pnode = pnode.parentElement;
          }
          out[sel] = {
            bg: bg ? [bg.r, bg.g, bg.b] : null,
            parentBg: parentBg ? [parentBg.r, parentBg.g, parentBg.b] : null,
            fg: (() => { const c = parse(getComputedStyle(el).color); return c ? [c.r, c.g, c.b] : null; })(),
            textish: Boolean((el.textContent || '').trim()) || el.tagName === 'INPUT'
          };
        }
        return out;
      })())`;
      return (await js(script)) as Record<string, { bg: number[] | null; parentBg: number[] | null; fg: number[] | null; textish: boolean }>;
    };

    const lum = (c: number[] | null) => {
      if (!c) return 0;
      const f = (v: number) => (v / 255 <= 0.03928 ? v / 255 / 12.92 : ((v / 255 + 0.055) / 1.055) ** 2.4);
      return 0.2126 * f(c[0]) + 0.7152 * f(c[1]) + 0.0722 * f(c[2]);
    };
    const contrast = (a: number[] | null, b: number[] | null) => {
      const l1 = lum(a);
      const l2 = lum(b);
      return +((Math.max(l1, l2) + 0.05) / (Math.min(l1, l2) + 0.05)).toFixed(2);
    };
    const sameColor = (a: number[] | null, b: number[] | null) =>
      !a || !b ? a === b : a.every((v, i) => Math.abs(v - b[i]) <= 2);

    const themeAudit = async (label: string) => {
      const selectors = auditSelectors[label];
      const light = await probeTheme('light', selectors);
      const dark = await probeTheme('dark', selectors);
      nativeTheme.themeSource = settings.themeMode;
      const problems: string[] = [];
      const seen = Object.keys(light);
      for (const sel of seen) {
        const l = light[sel];
        const d = dark[sel];
        if (!d) continue;
        if (sameColor(l.bg, d.bg)) problems.push(`${label}:背景未翻转 ${sel} [${l.bg}]`);
        // 有文字的比"文字/有效背景"；无文字的图形件（进度条填充）比"自身/父层背景"。
        const pairs: Array<[number[] | null, number[] | null, string]> = l.textish
          ? [[l.fg, l.bg, '亮'], [d.fg, d.bg, '暗']]
          : sel === '.progress > div'
            ? [[l.bg, l.parentBg, '亮'], [d.bg, d.parentBg, '暗']]
            : [];
        for (const [fg, bg, mode] of pairs) {
          const ratio = contrast(fg, bg);
          if (ratio < 3) problems.push(`${label}:${mode}色对比不足 ${sel} ratio=${ratio}`);
        }
      }
      log(`  theme-audit[${label}] probed=${seen.length} problems=${problems.length}`);
      // 只报数量等于没报：哪条选择器、哪种主题、差多少必须打出来。
      problems.slice(0, 6).forEach((p) => log(`    ! theme ${p}`));
      problems.slice(0, 12).forEach((p) => log(`    ! ${p}`));
      ok(`theme-audit-${label}`, seen.length >= 8 && problems.length === 0, `probed=${seen.length} problems=${problems.length}`);
    };

    const VIEWS: Array<{ index: number; name: string; selector: string }> = [
      { index: 0, name: 'home', selector: '[data-grid-probe]' },
      { index: 1, name: 'search', selector: '.searchbar input' },
      { index: 2, name: 'study', selector: '.flip-card' }
    ];
    for (const v of VIEWS) {
      await js(`(() => { const b = document.querySelectorAll('.topbar .segmented[aria-label="视图"] button')[${v.index}]; if (b) b.click(); return Boolean(b); })()`);
      await new Promise((r) => setTimeout(r, 900));
      const mounted = (await js(
        `(() => ({ children: document.getElementById('root')?.childElementCount ?? -1, hit: Boolean(document.querySelector(${JSON.stringify(v.selector)})) }))()`
      )) as { children: number; hit: boolean };
      ok(`view-${v.name}`, mounted.children > 0 && mounted.hit, `root-children=${mounted.children} selector=${v.selector} hit=${mounted.hit}`);

      // 卡片要真撑满 app 高度：主面板高度至少占视口 45%（否则说明自适应没生效）。
      const fillSel = v.name === 'home' ? '.home-grid > .stack > .panel' : v.name === 'search' ? '.search-grid > .panel' : '.flip-card';
      const fill = (await js(`(() => {
        const el = document.querySelector(${JSON.stringify(fillSel)});
        if (!el) return null;
        const b = el.getBoundingClientRect();
        const hostSel = ${JSON.stringify(v.name === 'study' ? '.flashcard-wrap' : '.main-area')};
        const host = document.querySelector(hostSel);
        const hb = host ? host.getBoundingClientRect() : null;
        return {
          h: Math.round(b.height),
          w: Math.round(b.width),
          vh: window.innerHeight,
          hostH: hb ? Math.round(hb.height) : -1,
          ratio: hb && hb.height > 0 ? +(b.height / hb.height).toFixed(2) : +(b.height / window.innerHeight).toFixed(2)
        };
      })()`)) as { h: number; w: number; vh: number; hostH: number; ratio: number } | null;
      // 学习页的卡片与评分行、按钮行同列，所以判"填满所在列"；主页/查询页判"占满视口的主要部分"。
      // 两个条件都要满足：只比列高的话，宿主本身很小也会"PASS"（实测踩过 260/289=0.9 的假绿）。
      const minRatio = v.name === 'study' ? 0.55 : 0.45;
      const minHeightPx = v.name === 'study' ? 340 : 320;
      ok(
        `layout-fills-${v.name}`,
        fill !== null && fill.ratio >= minRatio && fill.h >= minHeightPx,
        `${JSON.stringify(fill)} min=${minRatio}/${minHeightPx}px`
      );

      // 页面不该出现横向滚动条；出现就把最靠右的溢出元素报出来。
      // 页面不该出现横向滚动条；多挡宽度都测并报告最宽元素与 scrollLeft。
      // 只在宽窗测会漏：窄窗下溢出会把整版内容横向推移（截图里两处"错位"就是这个现象）。
      const hscrollProbe = `(async () => {
        const wait = (ms) => new Promise((r) => setTimeout(r, ms));
        await wait(80);
        const root = document.querySelector('.main-area > .stack') || document.body;
        const rb = root.getBoundingClientRect();
        const offenders = [];
        let widest = null;
        for (const el of document.querySelectorAll('.main-area *')) {
          const b = el.getBoundingClientRect();
          if (b.width === 0) continue;
          if (!widest || b.width > widest.w) {
            widest = { tag: el.tagName.toLowerCase(), cls: String(el.className || '').slice(0, 30), w: Math.round(b.width) };
          }
          if (b.right > rb.right + 2 || b.left < rb.left - 2) {
            offenders.push({ tag: el.tagName.toLowerCase(), cls: String(el.className || '').slice(0, 30), left: Math.round(b.left), right: Math.round(b.right) });
            if (offenders.length >= 2) break;
          }
        }
        return { scrollW: root.scrollWidth, clientW: root.clientWidth, scrollLeft: root.scrollLeft, winW: window.innerWidth, widest, offenders };
      })()`;
      type HRun = { scrollW: number; clientW: number; scrollLeft: number; winW: number; widest: { w: number } | null; offenders: unknown[] };
      const hruns: Array<{ w: number; r: HRun | null }> = [];
      for (const wpx of [1440, 1100, 900, 800, 700]) {
        const hb = win!.getBounds();
        win!.setBounds({ x: hb.x, y: hb.y, width: wpx, height: hb.height }, false);
        await new Promise((r) => setTimeout(r, 420));
        hruns.push({ w: wpx, r: (await js(hscrollProbe)) as HRun | null });
      }
      const hrestore = win!.getBounds();
      win!.setBounds({ x: hrestore.x, y: hrestore.y, width: 1440, height: hrestore.height }, false);
      await new Promise((r) => setTimeout(r, 350));
      hruns.forEach((h) => log(`  hscroll[${h.w}] ${JSON.stringify(h.r)}`));
      const hbad = hruns.filter((h) => !h.r || h.r.scrollW > h.r.clientW + 2 || h.r.offenders.length > 0);
      ok(
        `no-horizontal-scroll-${v.name}`,
        hruns.length === 5 && hbad.length === 0,
        hbad.length === 0
          ? 'widths=1440/1100/900/800/700 clean'
          : `failing=${hbad.map((h) => h.w).join('/')} ${JSON.stringify(hbad[0]?.r)}`
      );


      if (v.name === 'home') {
        // 热力图三条判据：不被容器横向裁、末周是满列（残列会让最右月标签消失）、月标签数等于跨的月份数。
        const geo = (await js(`(() => {
          const wrap = document.querySelector('[data-heatmap]');
          const svg = wrap ? wrap.querySelector('svg') : null;
          if (!wrap || !svg) return null;
          const cells = new Map();
          const p2 = (n) => String(n).padStart(2, '0');
          const dd = new Date();
          const todayKey = dd.getFullYear() + '-' + p2(dd.getMonth() + 1) + '-' + p2(dd.getDate());
          let futureCells = 0;
          svg.querySelectorAll('rect[data-date]').forEach((r) => {
            if (r.getAttribute('data-date') > todayKey) futureCells += 1;
          });
          svg.querySelectorAll('rect[data-date]').forEach((r) => {
            const x = Math.round(r.getBoundingClientRect().x);
            cells.set(x, (cells.get(x) || 0) + 1);
          });
          const xs = [...cells.keys()].sort((a, b) => a - b);
          const labels = [...wrap.querySelectorAll('*')]
            .map((e) => (e.childElementCount === 0 ? (e.textContent || '').trim() : ''))
            .filter((t) => /^[0-9]{1,2}月$/.test(t));
          // 选中格的描边必须完整落在滚动容器可视区内（右下角被裁就是这里量出来的）。
          const sel = wrap.querySelector('rect[stroke-width="2"], rect[data-selected="true"]')
            || [...wrap.querySelectorAll('rect')].find((r) => (r.getAttribute('style') || '').includes('stroke-width: 2'));
          const selBox = sel ? sel.getBoundingClientRect() : null;
          const box = wrap.getBoundingClientRect();
          return {
            clientW: wrap.clientWidth,
            scrollW: wrap.scrollWidth,
            svgW: Math.round(svg.getBoundingClientRect().width),
            weeks: xs.length,
            lastColBlocks: xs.length ? cells.get(xs[xs.length - 1]) : 0,
            futureCells,
            monthLabels: labels.join(','),
            text: wrap.textContent.replace(/\s+/g, ' ').slice(0, 90),
            hasOct: [...wrap.querySelectorAll('*')].some((e) => e.textContent.trim() === '10月'),
            selFound: Boolean(selBox),
            selInsideRight: selBox ? selBox.right <= box.right + 0.5 : false,
            selInsideBottom: selBox ? selBox.bottom <= box.bottom + 0.5 : false
          };
        })()`)) as { clientW: number; scrollW: number; svgW: number; weeks: number; lastColBlocks: number; futureCells: number; monthLabels: string; text: string; hasOct: boolean; selFound: boolean; selInsideRight: boolean; selInsideBottom: boolean } | null;
        const monthsSpanned = geo ? geo.monthLabels.split(',').filter(Boolean).length : 0;
        // 末列不能有今日之后的格子（补齐整周会把未来日期画出来）；月标签少最后一个属组件最小间距规则，
        // 见 doc 第 3 节，这里只断言"跨了 6 个月以上就得有 ≥5 个刻度"。
        ok(
          'heatmap-fits-without-tail-cut',
          geo !== null &&
            geo.scrollW <= geo.clientW + 2 &&
            geo.lastColBlocks >= 1 && geo.lastColBlocks <= 7 && geo.futureCells === 0 &&
            geo.weeks >= 26 &&
            monthsSpanned >= 5,
          JSON.stringify(geo)
        );

        // 选中框完整性：把选中挪到最右列那一格，再逐边取色证明四条边都真画出来了。
        // 上一版这里只比盒模型，结果"PASS 但肉眼看不见右边线"——盒模型对这种裁切是瞎的。
        type SelProbe = {
          found: boolean;
          selFound?: boolean;
          left?: number;
          right?: number;
          top?: number;
          bottom?: number;
          midY?: number;
          midX?: number;
          viewportW?: number;
        };
        const corner = (await js(`(async () => {
          const wait = (ms) => new Promise((r) => setTimeout(r, ms));
          const p = (n) => String(n).padStart(2, '0');
          const d = new Date();
          const todayIso = d.getFullYear() + '-' + p(d.getMonth() + 1) + '-' + p(d.getDate());
          const wrap = document.querySelector('[data-heatmap]');
          if (!wrap) return { found: false };
          const cell = [...wrap.querySelectorAll('rect[data-date]')].find((r) => r.getAttribute('data-date') === todayIso);
          if (!cell) return { found: false };
          cell.dispatchEvent(new MouseEvent('click', { bubbles: true }));
          await wait(450);
          const mark = wrap.querySelector('rect[fill="none"][stroke]');
          if (!mark) return { found: true, selFound: false };
          const b = mark.getBoundingClientRect();
          return {
            found: true,
            selFound: true,
            left: +b.left.toFixed(1),
            right: +b.right.toFixed(1),
            top: +b.top.toFixed(1),
            bottom: +b.bottom.toFixed(1),
            midY: +(b.top + b.height / 2).toFixed(1),
            midX: +(b.left + b.width / 2).toFixed(1),
            viewportW: window.innerWidth
          };
        })()`)) as SelProbe;
        ok('selection-marker-present', corner.found && corner.selFound === true, JSON.stringify(corner));

        if (corner.found && corner.selFound && corner.left !== undefined) {
          const image = await win!.webContents.capturePage();
          // electron.d.ts 把 getBitmap() 的返回类型声明成 void，实测返回的是 Buffer（BGRA 序）；按事实转。
          const bmp = image.getBitmap() as unknown as Uint8Array;
          const imgW = image.getSize().width;
          const scale = imgW / Math.max(corner.viewportW ?? 1, 1);
          const pxAt = (x: number, y: number): number[] => {
            const xi = Math.min(imgW - 1, Math.max(0, Math.round(x * scale)));
            const yi = Math.max(0, Math.round(y * scale));
            const i = (yi * imgW + xi) * 4;
            return [bmp[i], bmp[i + 1], bmp[i + 2]];
          };
          // getBitmap 的字节序按平台不同（BGRA/RGBA），所以判据只看"蓝通道显著占优"，并打出原始值核对。
          const isBlue = (v: number[]): boolean => Math.max(v[0], v[2]) > 150 && Math.abs(v[0] - v[2]) > 50;
          const blueNear = (xs: number[], y: number): boolean => xs.some((x) => isBlue(pxAt(x, y)));
          const L = corner.left;
          const R = corner.right ?? 0;
          const T = corner.top ?? 0;
          const B = corner.bottom ?? 0;
          const midY = corner.midY ?? 0;
          const midX = corner.midX ?? 0;
          const edges = {
            left: blueNear([L - 1, L, L + 1, L + 2], midY),
            right: blueNear([R - 2, R - 1, R, R + 1], midY),
            top: blueNear([midX], T - 1) || blueNear([midX], T) || blueNear([midX], T + 1),
            bottom: blueNear([midX], B - 1) || blueNear([midX], B) || blueNear([midX], B + 1)
          };
          const rawProbe = { right: pxAt(R - 1, midY), left: pxAt(L, midY) };
          ok(
            'selection-stroke-painted-on-all-edges',
            edges.left && edges.right && edges.top && edges.bottom,
            `edges=${JSON.stringify(edges)} box=[${L},${T},${R},${B}] raw=${JSON.stringify(rawProbe)}`
          );
        }

        // 主页底部这块原来是 500px 宽的纵向列表，面板右侧必然空一大片。
        // 换成圆角小卡片网格后判四件事：卡片够多、真是圆角、同行等宽（均匀）、网格铺到面板右边、列间有留白。
        // 隔离库刚建好时当天没有记录，走面板自己的「重新抽样这一天」按钮把数据造出来（顺带验这条路径）。
        const cards = (await js(`(async () => {
          const wait = (ms) => new Promise((r) => setTimeout(r, ms));
          let grid = document.querySelector('.word-cards');
          if (!grid) {
            const btn = Array.from(document.querySelectorAll('.panel .btn')).find((b) => (b.textContent || '').includes('重新抽样这一天'));
            if (!btn) return { count: 0, radius: -1, sameRowWidths: false, fillRight: -1, minColGap: -1, noButton: true };
            btn.click();
            await wait(1400);
            grid = document.querySelector('.word-cards');
          }
          const panel = grid ? grid.closest('.panel') : null;
          const list = grid ? Array.from(grid.querySelectorAll('.word-card')) : [];
          if (!grid || !panel || list.length === 0) return { count: list.length, radius: -1, sameRowWidths: false, fillRight: -1, minColGap: -1 };
          const gb = grid.getBoundingClientRect();
          const boxes = list.map((el) => el.getBoundingClientRect());
          const rows = new Map();
          boxes.forEach((b) => {
            const key = Math.round(b.top);
            const arr = rows.get(key) || [];
            arr.push(b);
            rows.set(key, arr);
          });
          const rowGaps = Array.from(rows.values()).filter((a) => a.length > 1).map((a) => Math.round(a[1].left - a[0].right));
          return {
            count: list.length,
            radius: Math.round(parseFloat(getComputedStyle(list[0]).borderRadius) || 0),
            sameRowWidths: Array.from(rows.values()).every((a) => Math.max(...a.map((b) => Math.round(b.width))) - Math.min(...a.map((b) => Math.round(b.width))) <= 2),
            fillRight: Math.round(gb.right - Math.max(...boxes.map((b) => b.right))),
            minColGap: rowGaps.length ? Math.min(...rowGaps) : 999,
            rows: rows.size
          };
        })()`)) as { count: number; radius: number; sameRowWidths: boolean; fillRight: number; minColGap: number; rows?: number; noButton?: boolean } | null;
        ok(
          'home-word-cards-grid',
          cards !== null && cards.count >= 6 && cards.radius >= 8 && cards.sameRowWidths && cards.fillRight <= 24 && cards.minColGap >= 8,
          JSON.stringify(cards)
        );

      }

      if (v.name === 'search') {
        // 右侧词组列表三条一起判：铺满面板宽度、滚动体真的能滚、选中只跟点击不跟悬停。
        // 悬停那条是用户报的原症状（AnimatedList 的 onMouseEnter 会把悬停项设成 selected，移开还留着）。
        const phrase = (await js(`(async () => {
          const wait = (ms) => new Promise((r) => setTimeout(r, ms));
          const row = document.querySelector('.word-row');
          if (row) { row.click(); await wait(800); }
          const body = document.querySelector('[data-detail-body]');
          const items = Array.from(document.querySelectorAll('[data-phrase-item]'));
          if (!body || items.length === 0) return { items: items.length, hasBody: Boolean(body) };
          const panel = body.closest('.panel');
          const bb = body.getBoundingClientRect();
          const ib = items.map((el) => el.getBoundingClientRect());
          const innerRight = panel ? panel.getBoundingClientRect().right - (parseFloat(getComputedStyle(panel).paddingRight) || 0) : 0;
          const idxOf = () => items.indexOf(document.querySelector('.phrase-item.is-active'));
          const before = idxOf();
          const target = items[Math.min(items.length - 1, Math.max(0, before + 2))];
          for (const type of ['mouseover', 'mouseenter']) {
            target.dispatchEvent(new MouseEvent(type, { bubbles: type === 'mouseover', cancelable: true }));
          }
          await wait(280);
          const afterHover = idxOf();
          for (const type of ['mouseout', 'mouseleave']) {
            target.dispatchEvent(new MouseEvent(type, { bubbles: type === 'mouseout', cancelable: true }));
          }
          await wait(220);
          const hoveredStillActive = target.classList.contains('is-active');
          target.click();
          await wait(280);
          return {
            items: items.length,
            hasBody: true,
            worstBlank: Math.round(innerRight - Math.max(...ib.map((b) => b.right))),
            minItemW: Math.min(...ib.map((b) => Math.round(b.width))),
            scrollable: /auto|scroll/.test(getComputedStyle(body).overflowY),
            overflow: body.scrollHeight - body.clientHeight,
            bodyBottomInView: Math.round(window.innerHeight - bb.bottom),
            hoverMovedSelection: afterHover !== before,
            hoveredStillActive,
            clickSelected: target.classList.contains('is-active'),
            activeCount: document.querySelectorAll('.phrase-item.is-active').length
          };
        })()`)) as {
          items: number; hasBody: boolean; worstBlank?: number; minItemW?: number; scrollable?: boolean;
          overflow?: number; bodyBottomInView?: number; hoverMovedSelection?: boolean; hoveredStillActive?: boolean;
          clickSelected?: boolean; activeCount?: number;
        } | null;
        ok(
          'search-phrase-list-behavior',
          phrase !== null && phrase.hasBody && phrase.items >= 5
            && (phrase.worstBlank ?? 999) <= 24 && (phrase.minItemW ?? 0) >= 260
            && phrase.scrollable === true && (phrase.bodyBottomInView ?? -99) >= -1
            && phrase.hoverMovedSelection === false && phrase.hoveredStillActive === false
            && phrase.clickSelected === true && phrase.activeCount === 1,
          JSON.stringify(phrase)
        );

        // 第二次及以后选中，详情标题必须还是那个词：SplitText 的 animationCompletedRef 闸门
        // 让它在换词时不再重播（字元被 revert 成 opacity:0，标题就空了），所以必须连着选两次才判得出来。
        const title = (await js(`(async () => {
          const wait = (ms) => new Promise((r) => setTimeout(r, ms));
          const norm = (s) => String(s || '').replace(/\\s+/g, '');
          const rows = Array.from(document.querySelectorAll('.word-row'));
          if (rows.length < 2) return { rows: rows.length };
          rows[0].click();
          await wait(1500);
          const first = norm(document.querySelector('.detail-word')?.textContent);
          const want = norm(rows[1].querySelector('.word')?.textContent);
          rows[1].click();
          await wait(1500);
          const el = document.querySelector('.detail-word');
          const chars = Array.from(el ? el.querySelectorAll('.split-char') : []);
          const invisible = chars.filter((c) => parseFloat(getComputedStyle(c).opacity) < 0.9).length;
          const cs = el ? getComputedStyle(el) : null;
          const mean = document.querySelector('.detail-meaning');
          const mcs = mean ? getComputedStyle(mean) : null;
          return {
            rows: rows.length, first, want,
            got: norm(el ? el.textContent : ''),
            h: el ? Math.round(el.getBoundingClientRect().height) : 0,
            scrollH: el ? el.scrollHeight : 0,
            clientH: el ? el.clientHeight : 0,
            shrink: cs ? cs.flexShrink : 'none',
            overflow: cs ? cs.overflow : 'none',
            disp: cs ? cs.display : 'none',
            charH: chars.length ? Math.round(chars[0].getBoundingClientRect().height) : 0,
            meanH: mean ? Math.round(mean.getBoundingClientRect().height) : 0,
            meanShrink: mcs ? mcs.flexShrink : 'none',
            panelH: (() => {
              const p = el ? el.closest('.panel') : null;
              return p ? Math.round(p.getBoundingClientRect().height) : 0;
            })(),
            chars: chars.length, invisible
          };
        })()`)) as { rows: number; first?: string; want?: string; got?: string; h?: number; chars?: number; invisible?: number } | null;
        ok(
          'search-detail-title-on-reswitch',
          title !== null && title.rows >= 2 && Boolean(title.want) && title.got === title.want
            && (title.h ?? 0) >= 16 && (title.chars ?? 0) >= 4 && title.invisible === 0,
          JSON.stringify(title)
        );
      }

      if (v.name === 'study') {
        await sweepWidths('study');

        // 悬浮溢出：React 的合成 onPointerEnter 在 executeJavaScript 里模拟不出可靠 hover（实测 grew=1），
        // 所以判几何不变量：卡片布局宽 × hoverScale 余量必须仍在列内。
        const hoverProbe = `(async () => {
          const wait = (ms) => new Promise((r) => setTimeout(r, ms));
          const card = document.querySelector('.flip-card');
          const col = document.querySelector('.flashcard-wrap');
          if (!card || !col) return null;
          const before = card.getBoundingClientRect();
          for (const type of ['pointerover', 'pointerenter']) {
            card.dispatchEvent(new PointerEvent(type, { bubbles: true, pointerType: 'mouse' }));
          }
          await wait(500);
          const hovered = card.getBoundingClientRect();
          const cb = col.getBoundingClientRect();
          const scaleRoom = (cb.width - 16) / 1.03;
          const out = {
            cardW: Math.round(before.width),
            colW: Math.round(cb.width),
            grew: +(hovered.width / before.width).toFixed(3),
            // 关键：卡片宽度不能超过"列宽 / hoverScale"，否则一悬浮就溢出。
            fitsWhenScaled: before.width * 1.03 <= cb.width + 1,
            widthWithinCap: before.width <= scaleRoom + 1
          };
          card.dispatchEvent(new PointerEvent('pointerleave', { bubbles: true, pointerType: 'mouse' }));
          await wait(250);
          return out;
        })()`;
        // 宽窗口下列远宽于卡片上限，这条判据会空过；必须在窄窗口再测一次才咬得住。
        const hoverRuns: Array<{ w: number; r: { cardW: number; colW: number; fitsWhenScaled: boolean; widthWithinCap: boolean } | null }> = [];
        for (const w of [1440, 900, 760]) {
          const hb = win!.getBounds();
          win!.setBounds({ x: hb.x, y: hb.y, width: w, height: hb.height }, false);
          await new Promise((r) => setTimeout(r, 450));
          hoverRuns.push({ w, r: (await js(hoverProbe)) as { cardW: number; colW: number; fitsWhenScaled: boolean; widthWithinCap: boolean } | null });
        }
        const back1 = win!.getBounds();
        win!.setBounds({ x: back1.x, y: back1.y, width: 1440, height: back1.height }, false);
        await new Promise((r) => setTimeout(r, 350));
        hoverRuns.forEach((h) => log(`  hover[${h.w}] ${JSON.stringify(h.r)}`));
        ok(
          'card-hover-stays-in-column',
          hoverRuns.length === 3 && hoverRuns.every((h) => h.r !== null && h.r.fitsWhenScaled && h.r.widthWithinCap),
          `widths=${hoverRuns.map((h) => `${h.w}:${h.r ? h.r.cardW : 'null'}/${h.r ? h.r.colW : '-'}`).join(' ')}`
        );

        // 闪烁取证：切换日期时，占位符不得再次出现（旧版每次都会闪一下），日历 SVG 节点不得被重建。
        const flicker = (await js(`(async () => {
          const wait = (ms) => new Promise((r) => setTimeout(r, ms));
          const p = (n) => String(n).padStart(2, '0');
          const iso = (d) => d.getFullYear() + '-' + p(d.getMonth() + 1) + '-' + p(d.getDate());
          const yesterday = iso(new Date(Date.now() - 86400000));
          await window.api.resampleDailyWords(yesterday, { count: 8 });
          await new Promise((r) => setTimeout(r, 350));
          const wrap = document.querySelector('.main-area');
          let placeholderIn = 0;
          let svgOut = 0;
          const obs = new MutationObserver((records) => {
            for (const m of records) {
              for (const n of m.addedNodes) {
                const text = n.textContent || '';
                if (text.includes('正在准备今日词卡')) placeholderIn += 1;
              }
              for (const n of m.removedNodes) {
                if (n.tagName && n.tagName.toLowerCase() === 'svg') svgOut += 1;
              }
            }
          });
          obs.observe(wrap, { childList: true, subtree: true });
          const cell = [...document.querySelectorAll('[data-heatmap] rect[data-date]')].find((r) => r.getAttribute('data-date') === yesterday);
          if (!cell) { obs.disconnect(); return { error: 'no yesterday cell' }; }
          cell.dispatchEvent(new MouseEvent('click', { bubbles: true }));
          await wait(900);
          const landed = document.querySelector('input[type="date"]').value;
          const cardText = (document.querySelector('.card-word') || {}).textContent || '';
          obs.disconnect();
          return { placeholderIn, svgOut, landed, cardCount: document.querySelectorAll('.card-word').length, cardText: cardText.slice(0, 18) };
        })()`)) as { error?: string; placeholderIn?: number; svgOut?: number; landed?: string; cardCount?: number; cardText?: string };
        ok(
          'study-date-switch-no-flicker',
          !flicker.error && flicker.placeholderIn === 0 && flicker.svgOut === 0 && flicker.cardCount === 1,
          JSON.stringify(flicker)
        );


        // 先测卡面，因为后面的日期探针会把 selectedDate 挪走、那天按新设计就没有卡了。
        // 学习页首帧要等抽样与背景就位，轮询到按钮可点再点一次；点两下会翻回去。
        const flipClicked = (await js(`(async () => {
          const wait = (ms) => new Promise((r) => setTimeout(r, ms));
          let b = null;
          for (let i = 0; i < 50; i += 1) {
            b = document.querySelector('[data-flip-btn]');
            if (b && !b.disabled) break;
            await wait(100);
          }
          if (!b) return false;
          b.click();
          return true;
        })()`)) as boolean;
        await new Promise((r) => setTimeout(r, 900));
        const back = (await js(`(() => {
          const face = document.querySelector('.flip-card__face--back');
          const el = document.querySelector('[data-card-back]');
          if (!el) return null;
          el.scrollTop = el.scrollHeight;
          const cs = getComputedStyle(el);
          const blocks = el.querySelectorAll('.phrase-block');
          const last = blocks.length ? blocks[blocks.length - 1] : null;
          const rect = el.getBoundingClientRect();
          const lastRect = last ? last.getBoundingClientRect() : null;
          return {
            shown: face ? face.getAttribute('aria-hidden') : 'no-face',
            blocks: blocks.length,
            scrollH: el.scrollHeight,
            clientH: el.clientHeight,
            overflowY: cs.overflowY,
            lastWithin: lastRect ? lastRect.bottom <= rect.bottom + 2 : false
          };
        })()`)) as { shown: string; blocks: number; scrollH: number; clientH: number; overflowY: string; lastWithin: boolean } | null;
        const flipped = back !== null && back.blocks > 0 && back.shown === 'false';
        const reachable =
          back !== null && (back.scrollH <= back.clientH + 2 || (/auto|scroll/.test(back.overflowY) && back.lastWithin));
        ok('card-back-reachable', flipClicked && flipped && reachable, `flipClicked=${flipClicked} ${JSON.stringify(back)}`);

        // 滚轮不灵敏的三条根因要能证伪：① 列表区内按下-抬起不得把卡翻回正面（用户报的"有时滚不动"就是
        // 点一下/拖选之后滚轮落到了正面上）；② 端点不得把滚轮外溢给祖先（外层是 overflow:auto hidden，会被吞）；
        // ③ 按下落在释义区（列表区之外）时 root 会 setPointerCapture，真实设备的抬起被重定向回 root，
        // 所以这里照真实设备的口径把 up 直接打在 root 上——背面必须仍是背面。
        // 没有 wheel 注入通道（sendInputEvent 不支持），所以判这三条机制本身，手感仍要人眼看。
        const backKeep = (await js(`(async () => {
          const wait = (ms) => new Promise((r) => setTimeout(r, ms));
          const el = document.querySelector('[data-card-back]');
          const face = document.querySelector('.flip-card__face--back');
          const root = document.querySelector('.flip-card');
          const mean = document.querySelector('.card-meaning');
          if (!el || !face || !root || !mean) return null;
          const shown = () => face.getAttribute('aria-hidden') === 'false';
          const at = (node, frac) => {
            const r = node.getBoundingClientRect();
            return [Math.round(r.left + r.width / 2), Math.round(r.top + Math.min(60, r.height * frac))];
          };
          const mk = (type, down, p) => new PointerEvent(type, {
            bubbles: true, cancelable: true, pointerId: 1, pointerType: 'mouse', isPrimary: true,
            button: 0, buttons: down ? 1 : 0, clientX: p[0], clientY: p[1]
          });
          const q = (node, frac, down) => node.dispatchEvent(mk(down ? 'pointerdown' : 'pointerup', down, at(node, frac)));
          q(el, 0.5, true);
          await wait(80);
          q(el, 0.5, false);
          await wait(900);
          const inIn = shown();
          q(mean, 0.5, true);
          await wait(80);
          q(root, 0.5, false);
          await wait(900);
          const outToRoot = shown();
          // 抬起若被吞在列表区里（拖选出界），root 会留着半截 grip；下一条内部手势必须不吃它——
          // 实测这就是"偶尔还能点翻"的时序来源：释义区按下 + 抬起落在列表区。
          q(el, 0.3, true);
          await wait(80);
          q(el, 0.3, false);
          await wait(900);
          return {
            inIn,
            outToRoot,
            afterStale: shown(),
            overscrollY: getComputedStyle(el).overscrollBehaviorY,
            hiddenAfter: face.getAttribute('aria-hidden'),
            pressed: root.getAttribute('aria-pressed') ?? 'none'
          };
        })()`)) as {
          inIn: boolean; outToRoot: boolean; afterStale: boolean;
          overscrollY: string; hiddenAfter: string; pressed: string;
        } | null;
        ok(
          'study-back-scroll-keeps-flip',
          backKeep !== null &&
            backKeep.inIn &&
            backKeep.outToRoot &&
            backKeep.afterStale &&
            backKeep.overscrollY === 'contain' &&
            backKeep.hiddenAfter === 'false',
          JSON.stringify(backKeep)
        );

        // 正面词区的下伸部裁切：判据打在"墨迹会不会出盒"这一层，而不是盒高。
        // 两条各自成立的原因要分别量得住：① 行盒矮于字身（line-height < ascent+descent → half-leading 为负，
        // 基线被往下挤）；② GradientText 的 .animated-gradient-text 带 overflow:hidden，它就是那个削掉笔画的盒子。
        // 墨迹量用 canvas 按元素自己的 computed font 取，所以跟当天抽到哪个词无关（抽到 hemolysis 也测得出来）。
        const wordFit = (await js(`(async () => {
          const wait = (ms) => new Promise((r) => setTimeout(r, ms));
          const btn = document.querySelector('[data-flip-btn]');
          const front = document.querySelector('.flip-card__face--front');
          if (btn && front && front.getAttribute('aria-hidden') !== 'true') { btn.click(); await wait(900); }
          const h2 = document.querySelector('.card-word');
          const txt = document.querySelector('.card-word .text-content');
          const wrap = document.querySelector('.card-word .animated-gradient-text');
          if (!h2 || !txt || !wrap || !front) return null;
          const cs = getComputedStyle(txt);
          const c = document.createElement('canvas').getContext('2d');
          c.font = cs.fontStyle + ' ' + cs.fontWeight + ' ' + cs.fontSize + ' ' + cs.fontFamily;
          const m = c.measureText('pgyqj');
          const lh = parseFloat(cs.lineHeight);
          const half = (lh - (m.fontBoundingBoxAscent + m.fontBoundingBoxDescent)) / 2;
          const room = half + m.fontBoundingBoxDescent;
          const need = m.actualBoundingBoxDescent;
          const tr = txt.getBoundingClientRect();
          // 末行墨迹底 = 行盒底 - 行盒内余量 + 墨迹下沉；再逐层向上找第一个真会裁它的祖先。
          const inkBottom = tr.bottom - room + need;
          let clip = null;
          for (let node = wrap; node && node !== document.documentElement; node = node.parentElement) {
            if (getComputedStyle(node).overflowY !== 'visible') { clip = node; break; }
          }
          const cr = clip ? clip.getBoundingClientRect() : null;
          const r2 = (n) => Math.round(n * 100) / 100;
          return {
            frontShown: front.getAttribute('aria-hidden') === 'true',
            fontSize: cs.fontSize, fontWeight: cs.fontWeight, lineHeight: cs.lineHeight,
            asc: r2(m.fontBoundingBoxAscent), desc: r2(m.fontBoundingBoxDescent), need: r2(need),
            halfLeading: r2(half), room: r2(room), slackInLine: r2(room - need),
            wrapOverflowY: getComputedStyle(wrap).overflowY,
            clipClass: clip ? String(clip.className).slice(0, 40) : 'none',
            slackInClip: cr ? r2(cr.bottom - inkBottom) : 9999
          };
        })()`)) as {
          frontShown: boolean; fontSize: string; fontWeight: string; lineHeight: string;
          asc: number; desc: number; need: number; halfLeading: number; room: number;
          slackInLine: number; wrapOverflowY: string; clipClass: string; slackInClip: number;
        } | null;
        ok(
          'study-word-descender-fits',
          wordFit !== null &&
            wordFit.frontShown &&
            wordFit.slackInLine >= 0 &&
            wordFit.slackInClip >= 0 &&
            wordFit.wrapOverflowY === 'visible' &&
            wordFit.fontWeight === '700',
          JSON.stringify(wordFit)
        );

        // 回归护栏：查看某天绝不能给它造记录（实测过 17 天 n=20 全未评分、含未来日期）。
        const sideEffect = (await js(`(async () => {
          const pad = (n) => String(n).padStart(2, '0');
          const localIso = (d) => d.getFullYear() + '-' + pad(d.getMonth() + 1) + '-' + pad(d.getDate());
          const futureIso = localIso(new Date(Date.now() + 2 * 86400000));
          const probe = document.querySelector('[data-heatmap] svg rect');
          const attrs = probe ? [...probe.attributes].map((a) => a.name).join(',') : 'none';
          const cell = document.querySelector('[data-heatmap] svg rect[data-date="' + futureIso + '"]');
          if (cell) cell.dispatchEvent(new MouseEvent('click', { bubbles: true }));
          await new Promise((r) => setTimeout(r, 300));
          const afterFutureClick = (await window.api.getWordsByDate(futureIso)).length;
          // 未来格不吃指针，所以也不该拿到焦点——否则浏览器焦点环会单独留在它上面，出现"蓝框白框两个、移动不同步"。
          const active = document.activeElement;
          const activeIsCell = active ? active.tagName === 'rect' : false;

          const pastIso = '2020-01-02';
          const input = document.querySelector('input[type="date"]');
          let moved = false;
          if (input) {
            const setter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value').set;
            setter.call(input, pastIso);
            input.dispatchEvent(new Event('input', { bubbles: true }));
            input.dispatchEvent(new Event('change', { bubbles: true }));
            moved = true;
          }
          await new Promise((r) => setTimeout(r, 800));
          const afterDatePick = (await window.api.getWordsByDate(pastIso)).length;
          const emptyCard = Boolean(document.querySelector('.flip-card'));
          if (input) {
            const setter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value').set;
            setter.call(input, localIso(new Date()));
            input.dispatchEvent(new Event('input', { bubbles: true }));
            input.dispatchEvent(new Event('change', { bubbles: true }));
          }
          await new Promise((r) => setTimeout(r, 500));
          const restored = Boolean(document.querySelector('.flip-card'));
          return { futureIso, attrs, hasFutureCell: Boolean(cell), afterFutureClick, activeIsCell, moved, afterDatePick, stillHasCard: emptyCard, restored };
        })()`)) as { futureIso: string; attrs: string; hasFutureCell: boolean; afterFutureClick: number; activeIsCell: boolean; moved: boolean; afterDatePick: number; stillHasCard: boolean; restored: boolean };
        ok(
          'viewing-a-day-creates-no-records',
          // 未来格现在根本不渲染（hasFutureCell 必须为 false），日期选择器那条路也不能造记录。
          !sideEffect.hasFutureCell &&
            sideEffect.afterFutureClick === 0 &&
            sideEffect.activeIsCell === false &&
            sideEffect.afterDatePick === 0 &&
            sideEffect.restored,
          JSON.stringify(sideEffect)
        );
      }
        // 堆叠块的左右边缘必须对齐——截图里那两处"错位"就是这个性质，不靠肉眼判。
      const align = (await js(`(() => {
        const root = document.querySelector('.main-area > .stack');
        if (!root) return null;
        const kids = [...root.children].filter((k) => k.getBoundingClientRect().width > 0);
        const boxes = kids.map((k) => k.getBoundingClientRect());
        const lefts = boxes.map((b) => Math.round(b.left));
        const rights = boxes.map((b) => Math.round(b.right));
        // 上下相邻的两块不能重叠：底部改成卡片墙之后，flex 收缩把热力图压成一条、被下一块盖住，
        // 左右对齐与占比判据都看不见这件事（截图才发现）。
        let worstOverlap = 0;
        for (let i = 1; i < boxes.length; i += 1) {
          const gap = Math.round(boxes[i].top - boxes[i - 1].bottom);
          if (gap < worstOverlap) worstOverlap = gap;
        }
        return {
          blocks: kids.length,
          leftSpread: Math.max(...lefts) - Math.min(...lefts),
          rightSpread: Math.max(...rights) - Math.min(...rights),
          minGap: worstOverlap
        };
      })()`)) as { blocks: number; leftSpread: number; rightSpread: number; minGap: number } | null;
      ok(
        `stack-blocks-aligned-${v.name}`,
        align !== null && align.blocks >= 2 && align.leftSpread <= 2 && align.rightSpread <= 2 && align.minGap >= -2,
        JSON.stringify(align)
      );
      await themeAudit(v.name);

      if (shotsDir) {
        // 每个视图各存亮暗两张：主题切换的视觉证据要能逐张对。
        const keepMode = settings.themeMode;
        for (const mode of ['light', 'dark'] as ThemeMode[]) {
          nativeTheme.themeSource = mode;
          await new Promise((r) => setTimeout(r, 500));
          const image = await win!.webContents.capturePage();
          const png = image.toPNG();
          fs.mkdirSync(shotsDir, { recursive: true });
          fs.writeFileSync(path.join(shotsDir, `${v.name}.${mode}.png`), png);
          log(`shot ${v.name}.${mode}.png bytes=${png.length}`);
        }
        nativeTheme.themeSource = keepMode;
        await new Promise((r) => setTimeout(r, 300));
      }
    }
    await js('(() => { const b = document.querySelectorAll(\'.topbar .segmented[aria-label="视图"] button\')[0]; if (b) b.click(); })()');
    await new Promise((r) => setTimeout(r, 400));



    const data = (await js(`(async () => {
      const t = {};
      let s = performance.now();
      const eng = await window.api.searchWords('aban');
      t.engRows = eng.length; t.engMs = Math.round(performance.now() - s);
      s = performance.now();
      const cjk = await window.api.searchWords('放弃');
      t.cjkRows = cjk.length; t.cjkMs = Math.round(performance.now() - s);
      s = performance.now();
      const ph = await window.api.searchPhrases('a lot');
      t.phraseRows = ph.length; t.phraseMs = Math.round(performance.now() - s);
      s = performance.now();
      const detail = await window.api.getWordDetail(eng[0] ? eng[0].id : 1);
      t.detailGroups = detail ? detail.groups.length : 0; t.detailMs = Math.round(performance.now() - s);
      s = performance.now();
      const daily = await window.api.getDailyWords(new Date().toISOString().slice(0, 10), { count: 20 });
      t.daily = daily.length; t.dailyMs = Math.round(performance.now() - s);
      const again = await window.api.getDailyWords(new Date().toISOString().slice(0, 10), { count: 20 });
      t.dailyIdempotent = again.length === daily.length && again.every((w, i) => w.wordId === daily[i].wordId);
      if (daily[0]) await window.api.updateStudyRecord(daily[0].wordId, 3, new Date().toISOString().slice(0, 10));
      const heat = await window.api.getHeatmap({ months: 6 });
      t.heatDays = heat.length;
      const stats = await window.api.getStudyStats();
      t.todayCount = stats.todayCount; t.todayMastered = stats.todayMastered; t.streak = stats.streakDays; t.uniqueWords = stats.uniqueWords;
      const browse = await window.api.browseWords({ firstLetter: 'a', limit: 30, offset: 0 });
      t.browseA = browse.items.length; t.browseATotal = browse.total;
      return t;
    })()`)) as Record<string, number | boolean>;
    log(`data ${JSON.stringify(data)}`);
    ok(
      'data-layer',
      (data.engRows as number) > 0 &&
        (data.cjkRows as number) > 0 &&
        (data.phraseRows as number) > 0 &&
        (data.detailGroups as number) > 0 &&
        (data.daily as number) > 0 &&
        (data.dailyIdempotent as boolean) === true &&
        (data.engMs as number) <= 500 &&
        (data.cjkMs as number) <= 500,
      `eng=${data.engRows}/${data.engMs}ms cjk=${data.cjkRows}/${data.cjkMs}ms phrases=${data.phraseRows}/${data.phraseMs}ms daily=${data.daily} idempotent=${data.dailyIdempotent} today=${data.todayCount}/${data.todayMastered} browse-a=${data.browseA}/${data.browseATotal}`
    );

    // 数据源账目：库里的行数必须等于 manifest 算出来的期望值（源行数 − 同词同模块重复文本）。
    // 数据源从 4 模块换到 7 模块时，190,926 → 185,942 这 4,984 条差额就是靠这条对上的，
    // 不靠"看起来差不多"——上一版少收 75 条词组也是这么发现的。
    type SourceTotals = { files?: number; entries?: number; phrases?: number; phrasesDup?: number; phrasesUnique?: number; uniqueWords?: number };
    let manifestTotals: SourceTotals | null = null;
    try {
      const parsed = JSON.parse(fs.readFileSync(path.join(resourcesDir(), 'sources.json'), 'utf8')) as { totals?: SourceTotals };
      manifestTotals = parsed.totals ?? null;
    } catch (err) {
      log(`manifest-read-failed ${String(err)}`);
    }
    const moduleCount = queries.getModules().length;
    ok(
      'data-source-accounting',
      manifestTotals !== null &&
        manifestTotals.files === moduleCount &&
        manifestTotals.uniqueWords === info.words &&
        manifestTotals.phrasesUnique === info.phrases &&
        (manifestTotals.phrases ?? 0) - (manifestTotals.phrasesDup ?? 0) === (manifestTotals.phrasesUnique ?? 0),
      `manifest files=${manifestTotals?.files} words=${manifestTotals?.uniqueWords} phrases=${manifestTotals?.phrases}−dup${manifestTotals?.phrasesDup}=${manifestTotals?.phrasesUnique} | db modules=${moduleCount} words=${info.words} phrases=${info.phrases} version=${info.dataVersion}`
    );

    const dpr = (await js('window.devicePixelRatio')) as number;
    const scaleFactor = screen.getDisplayMatching(win!.getBounds()).scaleFactor;
    const fontProbe = (await js(`(() => {
      const root = getComputedStyle(document.documentElement).fontSize;
      const body = getComputedStyle(document.querySelector('[data-body-probe]') || document.body).fontSize;
      return { root, body, ua: navigator.userAgent.includes('Electron') };
    })()`)) as { root: string; body: string };
    log(`adaptive dpr=${dpr} scaleFactor=${scaleFactor} rootPx=${fontProbe.root} bodyPx=${fontProbe.body} today=${todayLocalDate()}`);
    ok('font-base', fontProbe.root === '16px' || fontProbe.root === '16.0px', `rootPx=${fontProbe.root} bodyPx=${fontProbe.body}`);

    marker('checks-done');

    const errors = (await js('window.__rendererErrors || []')) as string[];
    errors.forEach((line) => log(`renderer-error ${line.slice(0, 300)}`));
    ok('renderer-no-errors', errors.length === 0, `count=${errors.length}`);

    const passed = checks.filter((c) => c.includes('PASS')).length;
    // 判据条数闸门：加/删断言时必须同步这里，否则某条判据被误删（实测发生过 themeAudit 调用被替换吞掉、
    // 32 项静默变 29 项）时，剩下的全 PASS 也会报成功。
    const EXPECTED_CHECKS = 42;
    log(`SELFTEST-SUMMARY checks=${checks.length} pass=${passed} fail=${checks.length - passed} expected=${EXPECTED_CHECKS}`);
    if (checks.length !== EXPECTED_CHECKS) {
      log(`SELFTEST-COUNT-MISMATCH actual=${checks.length} expected=${EXPECTED_CHECKS}`);
      log(`names=${checks.map((c) => c.split(' ')[0]).join(',')}`);
    }
    log(passed === checks.length && checks.length === EXPECTED_CHECKS ? 'SELFTEST-OK' : 'SELFTEST-FAIL');
    app.quit();
  }

  app.on('window-all-closed', () => {
    app.quit();
  });
}
