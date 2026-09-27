import type { ReactNode } from 'react'

export type IconName = 'grid' | 'wallet' | 'arrows' | 'chevron' | 'external' | 'refresh' | 'shield' | 'chart' | 'bank' | 'edit' | 'arrow' | 'check' | 'warning' | 'coins' | 'info'

const paths: Record<IconName, ReactNode> = {
  grid: <><rect x="3" y="3" width="7" height="7" rx="1" /><rect x="14" y="3" width="7" height="7" rx="1" /><rect x="3" y="14" width="7" height="7" rx="1" /><rect x="14" y="14" width="7" height="7" rx="1" /></>,
  wallet: <><path d="M4 6.5h15a2 2 0 0 1 2 2v9a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h13" /><path d="M16 12h5" /><circle cx="16" cy="12" r=".7" /></>,
  arrows: <><path d="M7 7h13l-3-3" /><path d="m20 7-3 3" /><path d="M17 17H4l3 3" /><path d="m4 17 3-3" /></>,
  chevron: <path d="m9 18 6-6-6-6" />,
  external: <><path d="M14 4h6v6" /><path d="m20 4-9 9" /><path d="M18 13v5a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h5" /></>,
  refresh: <><path d="M20 7v5h-5" /><path d="M4 17v-5h5" /><path d="M5.8 9A7 7 0 0 1 18 6l2 6" /><path d="M18.2 15A7 7 0 0 1 6 18l-2-6" /></>,
  shield: <><path d="M12 22s8-4 8-11V5l-8-3-8 3v6c0 7 8 11 8 11Z" /><path d="m9 12 2 2 4-4" /></>,
  chart: <><path d="M3 3v18h18" /><path d="m19 9-5 5-4-4-5 5" /></>,
  bank: <><path d="m3 10 9-7 9 7" /><path d="M5 10v9M9 10v9M15 10v9M19 10v9M3 21h18" /></>,
  edit: <><path d="M12 20h9" /><path d="M16.5 3.5a2.1 2.1 0 0 1 3 3L8 18l-4 1 1-4Z" /></>,
  arrow: <><path d="M5 12h14" /><path d="m13 6 6 6-6 6" /></>,
  check: <><path d="m5 12 4 4L19 6" /></>,
  warning: <><path d="M10.3 3.9 2.5 17.4A1.8 1.8 0 0 0 4.1 20h15.8a1.8 1.8 0 0 0 1.6-2.6L13.7 3.9a2 2 0 0 0-3.4 0Z" /><path d="M12 9v4M12 17h.01" /></>,
  coins: <><circle cx="8" cy="8" r="6" /><path d="M18.1 10.5a6 6 0 1 1-7.6 7.6" /><path d="M8 5v6M6 7h3a1.5 1.5 0 0 1 0 3H6" /><path d="M18 14v6M16 16h3a1.5 1.5 0 0 1 0 3h-3" /></>,
  info: <><circle cx="12" cy="12" r="9" /><path d="M12 11v5" /><path d="M12 8h.01" /></>,
}

export function Icon({ name }: { name: IconName }) {
  return <svg className="icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">{paths[name]}</svg>
}
