import { useMemo, useState } from 'react'
import type { Account, AccountStatus } from '../types/account'
import type { TransferLimitPolicy } from '../types/transfer-limit-policy'
import { useDashboard } from '../hooks/useDashboard'
import { AccountsTable } from '../components/accounts/AccountsTable'
import { OverdraftEditor } from '../components/accounts/OverdraftEditor'
import { AccountStatusEditor } from '../components/accounts/AccountStatusEditor'
import { AccountDetailsDialog } from '../components/accounts/AccountDetailsDialog'
import { PolicyEditor } from '../components/transfer-limits/PolicyEditor'
import { AppSidebar } from '../components/layout/AppSidebar'
import { AppTopbar } from '../components/layout/AppTopbar'
import { SummaryCard } from '../components/SummaryCard'
import { Icon } from '../components/Icon'
import '../App.css'

export function AccountsDashboardPage({ onNavigate, onOpenTransfer }: { onNavigate: (page: 'accounts' | 'transfers') => void; onOpenTransfer: (transferId: string) => void }) {
  const dashboard = useDashboard()
  const [editingAccount, setEditingAccount] = useState<Account | null>(null)
  const [editingOverdraftAccount, setEditingOverdraftAccount] = useState<Account | null>(null)
  const [editingStatusAccount, setEditingStatusAccount] = useState<Account | null>(null)
  const [viewingAccountId, setViewingAccountId] = useState<string | null>(null)
  const [editingPolicy, setEditingPolicy] = useState<TransferLimitPolicy | null>(null)

  const policiesByAccount = useMemo(
    () => new Map(dashboard.policies.map((policy) => [policy.accountId, policy])),
    [dashboard.policies],
  )

  const openPolicyEditor = (account: Account) => {
    dashboard.clearActionError()
    dashboard.clearNotice()
    setEditingAccount(account)
    setEditingPolicy(policiesByAccount.get(account.id) ?? null)
  }

  const openOverdraftEditor = (account: Account) => {
    dashboard.clearActionError()
    dashboard.clearNotice()
    setEditingOverdraftAccount(account)
  }

  const openStatusEditor = (account: Account) => {
    dashboard.clearActionError()
    dashboard.clearNotice()
    setEditingStatusAccount(account)
  }

  const saveAccountStatus = async (status: AccountStatus) => {
    if (!editingStatusAccount) return
    if (await dashboard.setAccountStatus(editingStatusAccount.id, status)) setEditingStatusAccount(null)
  }

  const savePolicy = async (values: Parameters<typeof dashboard.savePolicy>[2]) => {
    if (!editingAccount) return
    if (await dashboard.savePolicy(editingAccount.id, editingPolicy, values)) {
      setEditingAccount(null)
      setEditingPolicy(null)
    }
  }

  const deletePolicy = async () => {
    if (!editingPolicy) return
    if (await dashboard.deletePolicy(editingPolicy.id)) {
      setEditingAccount(null)
      setEditingPolicy(null)
    }
  }

  const saveOverdraft = async (limit: number) => {
    if (!editingOverdraftAccount) return
    if (await dashboard.saveOverdraft(editingOverdraftAccount.id, limit)) setEditingOverdraftAccount(null)
  }

  const clearOverdraft = async () => {
    if (!editingOverdraftAccount) return
    if (await dashboard.clearOverdraft(editingOverdraftAccount.id)) setEditingOverdraftAccount(null)
  }

  const closePolicyEditor = () => {
    if (dashboard.isSaving) return
    setEditingAccount(null)
    setEditingPolicy(null)
    dashboard.clearActionError()
  }

  const closeOverdraftEditor = () => {
    if (dashboard.isSaving) return
    setEditingOverdraftAccount(null)
    dashboard.clearActionError()
  }

  const pixKeyCount = dashboard.accounts.reduce((count, account) => count + account.pixKeys.length, 0)

  return (
    <div className="app-shell">
      <AppSidebar accountCount={dashboard.accounts.length} activePage="accounts" onNavigate={onNavigate} />
      <main className="main-area" id="inicio">
        <AppTopbar apiAvailable={!dashboard.isLoading && !dashboard.loadError} />
        <div className="page-content" id="limites">
          <div className="page-heading">
            <div><div className="eyebrow"><span className="eyebrow-line" /> CENTRAL DE CONTROLE</div><h1>Contas & limites</h1><p className="page-description">Acompanhe as contas de demonstração e ajuste as regras de transferência.</p></div>
            <div className="page-heading-actions"><button className="button button-quiet" onClick={() => void dashboard.refresh()} disabled={dashboard.isLoading}><Icon name="refresh" /> Atualizar dados</button></div>
          </div>

          <section className="summary-grid" aria-label="Resumo da operação">
            <SummaryCard label="Contas cadastradas" value={dashboard.isLoading ? '—' : String(dashboard.accounts.length)} detail="Contas disponíveis para teste" icon="wallet" tone="violet" />
            <SummaryCard label="Políticas configuradas" value={dashboard.isLoading ? '—' : String(dashboard.policies.length)} detail={dashboard.accounts.length ? `${Math.max(dashboard.accounts.length - dashboard.policies.length, 0)} contas sem configuração` : 'Aguardando contas'} icon="shield" tone="green" />
            <SummaryCard label="Chaves Pix" value={dashboard.isLoading ? '—' : String(pixKeyCount)} detail="Chaves cadastradas nas contas" icon="chart" tone="amber" />
          </section>

          {dashboard.notice && <div className="toast toast-success" role="status"><Icon name="check" />{dashboard.notice}<button aria-label="Dispensar mensagem" onClick={dashboard.clearNotice}>×</button></div>}

          <section className="accounts-panel" id="contas">
            <div className="panel-heading"><div><div className="panel-title-row"><h2>Contas de demonstração</h2><span className="count-pill">{dashboard.accounts.length}</span></div><p>Configure tetos diurnos e noturnos por conta.</p></div><div className="panel-meta"><span className="live-indicator" /> Sincronizado com a API</div></div>
            {dashboard.loadError ? (
              <div className="state-card state-error" role="alert"><span className="state-icon"><Icon name="warning" /></span><div><strong>Não foi possível carregar as contas</strong><p>{dashboard.loadError}</p></div><button className="button button-outline" onClick={() => void dashboard.refresh()}>Tentar novamente</button></div>
            ) : dashboard.isLoading ? (
              <div className="loading-list" aria-label="Carregando contas">{Array.from({ length: 5 }, (_, index) => <div className="skeleton-row" key={index} />)}</div>
            ) : dashboard.accounts.length === 0 ? (
              <div className="state-card"><span className="state-icon"><Icon name="wallet" /></span><div><strong>Nenhuma conta encontrada</strong><p>A API ainda não retornou contas para este ambiente.</p></div></div>
            ) : (
              <AccountsTable accounts={dashboard.accounts} policiesByAccount={policiesByAccount} onEditPolicy={openPolicyEditor} onEditOverdraft={openOverdraftEditor} onEditStatus={openStatusEditor} onViewDetails={(account) => setViewingAccountId(account.id)} />
            )}
            <div className="table-footer"><span>Dados fornecidos pela API de desenvolvimento</span><span>Mostrando {dashboard.accounts.length} contas</span></div>
          </section>
          <footer className="page-footer"><span>BTSA <span>·</span> Plataforma de transferências</span><span>Ambiente de demonstração</span></footer>
        </div>
      </main>

      {editingAccount && <PolicyEditor account={editingAccount} policy={editingPolicy} isSaving={dashboard.isSaving} error={dashboard.actionError} onClose={closePolicyEditor} onSave={savePolicy} onDelete={editingPolicy ? deletePolicy : undefined} />}
      {editingOverdraftAccount && <OverdraftEditor account={editingOverdraftAccount} isSaving={dashboard.isSaving} error={dashboard.actionError} onClose={closeOverdraftEditor} onSave={saveOverdraft} onClear={clearOverdraft} />}
      {editingStatusAccount && <AccountStatusEditor account={editingStatusAccount} isSaving={dashboard.isSaving} error={dashboard.actionError} onClose={() => { setEditingStatusAccount(null); dashboard.clearActionError() }} onSave={saveAccountStatus} />}
      {viewingAccountId && <AccountDetailsDialog accountId={viewingAccountId} onClose={() => setViewingAccountId(null)} onOpenTransfer={onOpenTransfer} />}
    </div>
  )
}

export default AccountsDashboardPage
