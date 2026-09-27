import { Icon } from './Icon'
import type { IconName } from './Icon'

interface SummaryCardProps {
  label: string
  value: string
  detail: string
  icon: IconName
  tone: string
}

export function SummaryCard({ label, value, detail, icon, tone }: SummaryCardProps) {
  return <article className="summary-card"><span className={`summary-icon ${tone}`}><Icon name={icon} /></span><div className="summary-copy"><span>{label}</span><strong>{value}</strong><small>{detail}</small></div><span className="summary-spark" aria-hidden="true"><i /><i /><i /><i /><i /><i /><i /></span></article>
}
