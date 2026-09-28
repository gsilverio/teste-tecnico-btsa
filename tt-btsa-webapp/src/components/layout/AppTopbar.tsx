import { Icon } from '../Icon'
import { swaggerUrl } from '../../services/http'

interface AppTopbarProps {
  apiAvailable: boolean
  title?: string
}

export function AppTopbar({ apiAvailable, title = 'Visão geral' }: AppTopbarProps) {
  return (
    <header className="topbar">
      <div className="breadcrumbs"><span>Workspace</span><Icon name="chevron" /><strong>{title}</strong></div>
      <div className="topbar-right"><a className="api-link" href={swaggerUrl} target="_blank" rel="noreferrer"><span className={`api-status-dot ${apiAvailable ? '' : 'api-offline'}`} /> {apiAvailable ? 'API local' : 'API indisponível'} <Icon name="external" /></a><span className="topbar-divider" /><span className="user-avatar">G</span></div>
    </header>
  )
}
