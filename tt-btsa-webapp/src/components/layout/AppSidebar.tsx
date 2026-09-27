import { Icon } from '../Icon'

interface AppSidebarProps {
  accountCount: number
  activePage: 'accounts' | 'transfers'
  onNavigate: (page: 'accounts' | 'transfers') => void
}

export function AppSidebar({ accountCount, activePage, onNavigate }: AppSidebarProps) {
  return (
    <aside className="sidebar">
      <a className="brand" href="#inicio" aria-label="BTSA, início"><span className="brand-mark">b</span><span>bt<span className="brand-accent">sa</span></span></a>
      <div className="workspace-label">WORKSPACE</div>
      <nav className="primary-nav" aria-label="Navegação principal">
        <a className={`nav-item ${activePage === 'accounts' ? 'active' : ''}`} href="#limites" aria-current={activePage === 'accounts' ? 'page' : undefined} onClick={() => onNavigate('accounts')}><Icon name="grid" /><span>Visão geral</span></a>
        <a className="nav-item" href="#contas" onClick={() => onNavigate('accounts')}><Icon name="wallet" /><span>Contas</span><span className="nav-count">{accountCount || '—'}</span></a>
        <button className={`nav-item nav-button ${activePage === 'transfers' ? 'active' : ''}`} type="button" aria-current={activePage === 'transfers' ? 'page' : undefined} onClick={() => onNavigate('transfers')}><Icon name="arrows" /><span>Transferências</span></button>
      </nav>
      <div className="sidebar-bottom"><span className="sidebar-avatar">BT</span><span className="sidebar-user"><strong>Ambiente local</strong><small>Desenvolvimento</small></span><span className="online-dot" title="Interface pronta" /></div>
    </aside>
  )
}
