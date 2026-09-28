import { useEffect, useState } from 'react'
import { AppSidebar } from '../components/layout/AppSidebar'
import { AppTopbar } from '../components/layout/AppTopbar'
import { Icon } from '../components/Icon'
import { ApiError } from '../services/http'
import { accountsService } from '../services/accounts.service'
import { formatCurrency } from '../services/currency'
import { transfersService } from '../services/transfers.service'
import type { Account } from '../types/account'
import type { Transfer } from '../types/transfer'
import '../App.css'

interface TransferDetailsPageProps {
  transferId: string
  onBack: () => void
  backLabel?: string
  onNavigate: (page: 'accounts' | 'transfers') => void
}

export function TransferDetailsPage({ transferId, onBack, backLabel = 'Voltar às transferências', onNavigate }: TransferDetailsPageProps) {
  const [transfer, setTransfer] = useState<Transfer | null>(null)
  const [accounts, setAccounts] = useState<Account[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [isCancelling, setIsCancelling] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)

  useEffect(() => {
    let active = true
    let timer: number | undefined

    const loadTransfer = async () => {
      try {
        const current = await transfersService.get(transferId)
        if (!active) return
        setTransfer(current)
        setError(null)
        setIsLoading(false)
        if (current.status === 'Scheduled' || current.status === 'Processing') {
          timer = window.setTimeout(() => void loadTransfer(), 5000)
        }
      } catch (loadError) {
        if (!active) return
        setError(getErrorMessage(loadError))
        setIsLoading(false)
        if (!(loadError instanceof ApiError && loadError.status === 404)) {
          timer = window.setTimeout(() => void loadTransfer(), 5000)
        }
      }
    }

    void loadTransfer()
    void accountsService.getAll().then((page) => {
      if (active) setAccounts(page.items)
    }).catch(() => {
      // The transfer details remain useful if account summaries are unavailable.
    })

    return () => {
      active = false
      if (timer !== undefined) window.clearTimeout(timer)
    }
  }, [transferId])

  const cancelScheduledTransfer = async () => {
    setIsCancelling(true)
    setActionError(null)
    try {
      await transfersService.cancel(transferId)
      setTransfer(await transfersService.get(transferId))
    } catch (cancelError) {
      setActionError(getErrorMessage(cancelError))
    } finally {
      setIsCancelling(false)
    }
  }

  const accountById = new Map(accounts.map((account) => [account.id, account]))
  const source = transfer ? accountById.get(transfer.sourceAccountId) : undefined
  const destination = transfer ? accountById.get(transfer.destinationAccountId) : undefined
  const apiAvailable = !isLoading && !error

  return (
    <div className="app-shell">
      <AppSidebar accountCount={accounts.length} activePage="transfers" onNavigate={onNavigate} />
      <main className="main-area" id="inicio">
        <AppTopbar apiAvailable={apiAvailable} title="Detalhes da transferência" />
        <div className="page-content transfer-page-content detail-page-content">
          <div className="detail-page-toolbar">
            <button className="button button-quiet" type="button" onClick={onBack}><Icon name="chevron" /> {backLabel}</button>
            <span className="detail-refresh-note"><span className="live-indicator" /> Atualiza enquanto estiver em processamento</span>
          </div>

          {error && !transfer ? (
            <section className="state-card state-error detail-error" role="alert">
              <span className="state-icon"><Icon name="warning" /></span>
              <div><strong>{isLoading ? 'Carregando transferência' : 'Não foi possível consultar a transferência'}</strong><p>{error}</p></div>
              <button className="button button-outline" type="button" onClick={() => window.location.reload()}>Tentar novamente</button>
            </section>
          ) : isLoading && !transfer ? (
            <section className="detail-loading" aria-label="Carregando detalhes"><span className="button-spinner" /><strong>Consultando os dados da transferência…</strong></section>
          ) : transfer ? (
            <>
              <section className="transfer-detail-hero">
                <div className="detail-hero-main">
                  <span className="panel-eyebrow">TRANSFERÊNCIA {transfer.method === 'Pix' ? 'PIX' : 'BANCÁRIA'}</span>
                  <h1>{formatCurrency(transfer.amount)}</h1>
                  <span className={`status-pill status-${transfer.status.toLowerCase()}`}><i />{statusLabel(transfer.status)}</span>
                </div>
                <div className="detail-hero-actions">
                  {transfer.status === 'Scheduled' && <button className="button button-cancel" type="button" disabled={isCancelling} onClick={() => void cancelScheduledTransfer()}>{isCancelling ? 'Cancelando…' : 'Cancelar agendamento'}</button>}
                  <span>Identificador<br /><code>{transfer.id}</code></span>
                </div>
              </section>

              {actionError && <div className="form-error" role="alert">{actionError}</div>}
              {error && <div className="form-error detail-refresh-error" role="status">Atualização automática indisponível: {error}</div>}
              {transfer.processingError && <div className="state-card state-error" role="status"><span className="state-icon"><Icon name="warning" /></span><div><strong>{transfer.isInDeadLetter ? 'Processamento técnico precisa de intervenção' : 'Falha técnica no processamento'}</strong><p>{transfer.processingError}</p><small>Consulte a fila transfers.processing.dead-letter no RabbitMQ Management. Após corrigir a causa, reencaminhe a mensagem para transfers.processing.</small></div></div>}

              <div className="detail-content-grid">
                <section className="transfer-detail-card">
                  <div className="detail-card-heading"><span className="transfer-info-icon"><Icon name="arrows" /></span><div><span className="panel-eyebrow">MOVIMENTAÇÃO</span><h2>Origem e destino</h2></div></div>
                  <div className="detail-account-pair">
                    <AccountSummary label="Origem" account={source} accountId={transfer.sourceAccountId} />
                    <span className="detail-direction"><Icon name="arrow" /></span>
                    <AccountSummary label="Destino" account={destination} accountId={transfer.destinationAccountId} />
                  </div>
                </section>

                <section className="transfer-detail-card">
                  <div className="detail-card-heading"><span className="transfer-info-icon lookup-icon"><Icon name="chart" /></span><div><span className="panel-eyebrow">REGISTRO</span><h2>Dados da solicitação</h2></div></div>
                  <dl className="detail-data-list">
                    <DetailRow label="Modalidade" value={transfer.method === 'Pix' ? 'Pix' : 'Agência e conta'} />
                    <DetailRow label="Criada em" value={formatDate(transfer.createdAt)} />
                    {transfer.scheduledAt && <DetailRow label="Agendada para" value={formatDate(transfer.scheduledAt)} />}
                    {transfer.finishedAt && <DetailRow label={transfer.status === 'Failed' ? 'Finalizada em' : 'Concluída em'} value={formatDate(transfer.finishedAt)} />}
                    {transfer.cancelledAt && <DetailRow label="Cancelada em" value={formatDate(transfer.cancelledAt)} />}
                    {transfer.failureCode && <DetailRow label="Motivo da falha" value={failureLabel(transfer.failureCode)} />}
                  </dl>
                </section>
              </div>

              <section className="transfer-detail-card transfer-audit-card">
                <div className="detail-card-heading"><span className="transfer-info-icon lookup-icon"><Icon name="chart" /></span><div><span className="panel-eyebrow">AUDITORIA</span><h2>Histórico da transferência</h2></div></div>
                {transfer.auditTrail?.length ? (
                  <ol className="transfer-audit-list">
                    {transfer.auditTrail.map((event) => <li key={event.id}>
                      <span className={`audit-state-dot status-${event.status.toLowerCase()}`} />
                      <div><strong>{auditActionLabel(event.action)}</strong><span>{event.previousStatus ? `${statusLabel(event.previousStatus)} → ` : ''}{statusLabel(event.status)}{event.failureCode ? ` · ${failureLabel(event.failureCode)}` : ''}</span></div>
                      <time dateTime={event.occurredAt}>{formatDate(event.occurredAt)}</time>
                    </li>)}
                  </ol>
                ) : <p className="transfer-audit-empty">Ainda não há mudanças registradas.</p>}
              </section>
            </>
          ) : null}

          <footer className="page-footer"><span>BTSA <span>·</span> Plataforma de transferências</span><span>Consulta por identificador</span></footer>
        </div>
      </main>
    </div>
  )
}

function AccountSummary({ label, account, accountId }: { label: string; account?: Account; accountId: string }) {
  return (
    <div className="detail-account">
      <span className="detail-account-label">{label}</span>
      <strong>{account?.accountHolderName ?? 'Conta'}</strong>
      {account ? <>
        <span>{account.bankName}</span>
        <span>Ag. {account.branch} · Conta {account.number}-{account.checkDigit ?? '0'}</span>
      </> : <span>Os dados cadastrais desta conta não estão disponíveis.</span>}
      <code>{accountId}</code>
    </div>
  )
}

function DetailRow({ label, value }: { label: string; value: string }) {
  return <div className="detail-data-row"><dt>{label}</dt><dd>{value}</dd></div>
}

function statusLabel(status: string) {
  const labels: Record<string, string> = {
    Scheduled: 'Agendada', Processing: 'Processando', Completed: 'Concluída', Failed: 'Falhou', Cancelled: 'Cancelada',
  }
  return labels[status] ?? status
}

function auditActionLabel(action: string) {
  const labels: Record<string, string> = {
    Requested: 'Solicitação recebida', Scheduled: 'Agendamento criado', ProcessingStarted: 'Processamento iniciado',
    Completed: 'Transferência concluída', Failed: 'Transferência rejeitada', Cancelled: 'Agendamento cancelado',
  }
  return labels[action] ?? action
}

function failureLabel(code: string) {
  const labels: Record<string, string> = {
    'transfer.limit_policy_missing': 'Conta sem política de limites.',
    'transfer.attempt_limit_exceeded': 'Esta conta atingiu o limite de tentativas de transferência na última hora para o período atual (dia ou noite). Tentativas recusadas também contam. Aguarde as tentativas anteriores saírem dessa janela antes de tentar novamente ou ajuste os limites da conta.',
    'transfer.amount_limit_exceeded': 'Limite de transferência por hora excedido para o período diurno ou noturno. Confira a política da conta; saldo e cheque especial são avaliados separadamente.',
    'account.insufficient_funds': 'Saldo e cheque especial insuficientes.',
    'account.source_blocked': 'A conta de origem está bloqueada.',
    'account.source_inactive': 'A conta de origem está inativa.',
    'account.destination_blocked': 'A conta de destino está bloqueada.',
    'account.destination_inactive': 'A conta de destino está inativa.',
    'account.source_not_active': 'A conta de origem não está ativa.',
    'account.destination_not_active': 'A conta de destino não está ativa.',
    'transfer.account_missing': 'Uma das contas não foi encontrada.',
  }
  return labels[code] ?? code
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat('pt-BR', { dateStyle: 'long', timeStyle: 'short' }).format(new Date(value))
}

function getErrorMessage(error: unknown) {
  if (error instanceof ApiError) return error.message
  return 'Não foi possível consultar a API. Confira a conexão e tente novamente.'
}
