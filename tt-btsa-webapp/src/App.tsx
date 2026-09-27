import { useEffect, useState } from 'react'
import { AccountsDashboardPage } from './pages/AccountsDashboardPage'
import { TransferDetailsPage } from './pages/TransferDetailsPage'
import { TransfersPage } from './pages/TransfersPage'

type Page =
  | { kind: 'accounts' }
  | { kind: 'transfers' }
  | { kind: 'transfer-details'; transferId: string }

function readPage(): Page {
  const detailMatch = window.location.hash.match(/^#transferencias\/([0-9a-f-]+)$/i)
  if (detailMatch) return { kind: 'transfer-details', transferId: detailMatch[1] }
  return window.location.hash.startsWith('#transferencias') ? { kind: 'transfers' } : { kind: 'accounts' }
}

export default function App() {
  const [page, setPage] = useState<Page>(readPage)

  useEffect(() => {
    const syncPage = () => setPage(readPage())
    window.addEventListener('hashchange', syncPage)
    return () => window.removeEventListener('hashchange', syncPage)
  }, [])

  const navigate = (nextPage: 'accounts' | 'transfers') => {
    const hash = nextPage === 'transfers' ? '#transferencias' : '#limites'
    if (window.location.hash !== hash) window.location.hash = hash
    setPage(nextPage === 'transfers' ? { kind: 'transfers' } : { kind: 'accounts' })
  }

  const openTransfer = (transferId: string) => {
    const nextPage: Page = { kind: 'transfer-details', transferId }
    window.location.hash = `#transferencias/${transferId}`
    setPage(nextPage)
  }

  if (page.kind === 'transfer-details') {
    return <TransferDetailsPage key={page.transferId} transferId={page.transferId} onBack={() => navigate('transfers')} onNavigate={navigate} />
  }

  return page.kind === 'transfers'
    ? <TransfersPage onNavigate={navigate} onOpenTransfer={openTransfer} />
    : <AccountsDashboardPage onNavigate={navigate} />
}
