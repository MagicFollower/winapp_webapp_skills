import type { ModuleRef } from '../../../electron/api-contract';

export default function ModuleChips({ modules, max }: { modules: ModuleRef[]; max?: number }) {
  const shown = max ? modules.slice(0, max) : modules;
  const rest = max ? Math.max(modules.length - max, 0) : 0;
  return (
    <span className="row" style={{ gap: 5 }}>
      {shown.map((m) => (
        <span key={m.code} className="chip" data-module={m.code}>
          {m.name}
        </span>
      ))}
      {rest > 0 ? <span className="chip">+{rest}</span> : null}
    </span>
  );
}
