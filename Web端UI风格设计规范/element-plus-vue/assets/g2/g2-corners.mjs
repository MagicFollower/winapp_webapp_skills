// G2 角形几何：形状生成、曲率闸门、SVG / clip-path 路径。零依赖，Node 与浏览器同码。
// 约定：p = 2^v（corner-shape 的 superellipse(v)），round=2、squircle=4。

export const kappa = (c, t) => {
  const [x1, y1] = c.d1(t), [x2, y2] = c.d2(t);
  return Math.abs(x1 * y2 - y1 * x2) / Math.pow(x1 * x1 + y1 * y1, 1.5);
};

export const arcCorner = (r) => ({
  tMax: Math.PI / 2,
  P: (t) => [r - r * Math.cos(t), r - r * Math.sin(t)],
  d1: (t) => [r * Math.sin(t), -r * Math.cos(t)],
  d2: (t) => [r * Math.cos(t), r * Math.sin(t)],
});

export const superCorner = (r, p) => {
  const m = 2 / p, c = (v, e) => Math.pow(Math.max(0, v), e);
  return {
    tMax: Math.PI / 2,
    P: (t) => [r - r * c(Math.cos(t), m), r - r * c(Math.sin(t), m)],
    d1: (t) => [r * m * c(Math.cos(t), m - 1) * Math.sin(t), -r * m * c(Math.sin(t), m - 1) * Math.cos(t)],
    d2: (t) => [r * m * (c(Math.cos(t), m) - (m - 1) * c(Math.cos(t), m - 2) * Math.sin(t) ** 2),
                -r * m * ((m - 1) * c(Math.sin(t), m - 2) * Math.cos(t) ** 2 - c(Math.sin(t), m))],
  };
};

export function peakKappa(c, n = 360) {
  let k = 0;
  for (let i = 1; i < n; i++) k = Math.max(k, kappa(c, c.tMax * i / n));
  return k;
}

export function arcLength(c, to = c.tMax, n = 600) {
  let L = 0, prev = c.P(0);
  for (let i = 1; i <= n; i++) {
    const q = c.P(to * i / n);
    L += Math.hypot(q[0] - prev[0], q[1] - prev[1]);
    prev = q;
  }
  return L;
}

export const maxChord = (eps, kPeak) => Math.sqrt(8 * eps / kPeak);
export const segPerCorner = (c, eps) => Math.ceil(arcLength(c) / maxChord(eps, peakKappa(c)));

// CSS 的 f 缩放：相邻半径之和超过边长时按比例压小，getComputedStyle 读不回用过值。
export function usedRadius(w, h, r) {
  const f = Math.min(1, w / (2 * r), h / (2 * r));
  return { f, used: r * f, bitten: f < 1, kSeam: 1 / (r * f) };
}

// 描边 / inset / outline-offset 的上限 = 1/κ峰值；留 20% 余量。
export function cornerGate(p, r, w) {
  const k = peakKappa(superCorner(r, p));
  return { p, r, kPeak: k, safeInner: 1 / k, want: w, ok: 1 / k >= 1.2 * w };
}

// 四角平端三次贝塞尔：P1=P2=盒角，端点 κ=0，几何上等价 p≈5.19。
export function rectPath(w, h, r) {
  const R = Math.min(r, w / 2, h / 2);
  return `M ${R},0 H ${w - R} C ${w},0 ${w},0 ${w},${R} V ${h - R} C ${w},${h} ${w},${h} ${w - R},${h}`
       + ` H ${R} C 0,${h} 0,${h} 0,${h - R} V ${R} C 0,0 0,0 ${R},0 Z`;
}

export function rectPoints(w, h, r, p, eps = 0.5) {
  const R = Math.min(r, w / 2, h / 2);
  const c = superCorner(R, p), seg = segPerCorner(c, eps);
  const A = [];
  for (let i = 0; i <= seg; i++) A.push(c.P((Math.PI / 2) * i / seg));
  const mx = ([x, y]) => [w - x, y], my = ([x, y]) => [x, h - y], mxy = ([x, y]) => [w - x, h - y];
  const pts = [];
  const push = (q) => {
    const l = pts[pts.length - 1];
    if (!l || Math.abs(l[0] - q[0]) > 1e-9 || Math.abs(l[1] - q[1]) > 1e-9) {
      pts.push([+q[0].toFixed(3), +q[1].toFixed(3)]);
    }
  };
  push([R, 0]); push([w - R, 0]);
  A.slice().reverse().forEach((q) => push(mx(q)));
  push([w, h - R]);
  A.forEach((q) => push(mxy(q)));
  push([R, h]);
  A.slice().reverse().forEach((q) => push(my(q)));
  push([0, R]);
  A.forEach((q) => push(q));
  return pts;
}

export const clipPathOf = (pts) =>
  `path("M ${pts[0][0]},${pts[0][1]} ${pts.slice(1).map((q) => `L ${q[0]},${q[1]}`).join(' ')} Z")`;

export const svgPolyOf = (pts) =>
  `M ${pts[0][0]},${pts[0][1]} ${pts.slice(1).map((q) => `L ${q[0]},${q[1]}`).join(' ')} Z`;

// J1：κ(0.25px) / κ(0.0156px)。≥2 才是 G2；圆弧恒 1.00×。
export function sTable(c, N = 6000) {
  const out = []; let prev = c.P(0), s = 0;
  for (let i = 1; i <= N; i++) {
    const t = c.tMax * Math.pow(i / N, 4), q = c.P(t);
    s += Math.hypot(q[0] - prev[0], q[1] - prev[1]);
    prev = q;
    out.push({ s, k: kappa(c, t) });
  }
  return out;
}

const kAtS = (g, s) => g.reduce((b, p) => (Math.abs(p.s - s) < Math.abs(b.s - s) ? p : b), g[0]);

export function j1(c, sA = 0.25, sB = 0.0156) {
  const g = sTable(c), a = kAtS(g, sA), b = kAtS(g, sB);
  return { ratio: a.k / Math.max(b.k, 1e-30), sA, sB };
}
