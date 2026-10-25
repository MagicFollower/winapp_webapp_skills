import CountUp from '../ui/CountUp';

interface Props {
  label: string;
  value: number;
  unit: string;
  accent?: 'primary' | 'fire' | 'muted';
  onClick?: () => void;
}

export default function StatCard({ label, value, unit, accent = 'muted', onClick }: Props) {
  return (
    <button
      type="button"
      className="stat-card"
      onClick={onClick}
      style={{
        textAlign: 'left',
        cursor: onClick ? 'pointer' : 'default',
        borderColor: accent === 'primary' ? 'var(--accent)' : 'var(--border)'
      }}
    >
      <div className="stat-label">{label}</div>
      <div className="stat-value" style={{ color: accent === 'fire' ? 'var(--danger)' : undefined }}>
        <CountUp to={value} duration={0.9} separator="," />
        <span className="stat-unit">{unit}</span>
      </div>
    </button>
  );
}
