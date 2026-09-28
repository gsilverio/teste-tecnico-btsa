import { useCallback, useEffect, useMemo, useRef, useState, type FormEvent } from 'react'
import { AppSidebar } from '../components/layout/AppSidebar'
import { AppTopbar } from '../components/layout/AppTopbar'
import { Icon } from '../components/Icon'
import { ApiError } from '../services/http'
import { accountsService } from '../services/accounts.service'
import { transfersService } from '../services/transfers.service'
import { formatCurrency, parseMoney } from '../services/currency'
import type { Account } from '../types/account'
import type { Transfer, TransferMethod, TransferRequest } from '../types/transfer'
import '../App.css'

type TransferMode = 'immediate' | 'scheduled'

const trackedTransfersStorageKey = 'btsa.tracked-transfers.v1'

export function TransfersPage({ onNavigate, onOpenTransfer }: {
  onNavigate: (page: 'accounts' | 'transfers') => void
  onOpenTransfer: (transferId: string) => void
}) {
  const [accounts, setAccounts] = useState<Account[]>([])
  const [accountsLoading, setAccountsLoading] = useState(true)
  const [accountsError, setAccountsError] = useState<string | null>(null)
  const [mode, setMode] = useState<TransferMode>('immediate')
  const [method, setMethod] = useState<TransferMethod>('Pix')
  const [sourceAccountId, setSourceAccountId] = useState('')
  const [destinationAccountId, setDestinationAccountId] = useState('')
  const [pixKey, setPixKey] = useState('')
  const [bankIspb, setBankIspb] = useState('')
  const [branch, setBranch] = useState('')
  const [accountNumber, setAccountNumber] = useState('')
  const [checkDigit, setCheckDigit] = useState('')
  const [amount, setAmount] = useState('')
  const [scheduledAt, setScheduledAt] = useState(() => defaultScheduledTime())
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [formError, setFormError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [trackedIds, setTrackedIds] = useState<string[]>(readTrackedIds)
  const [transfers, setTransfers] = useState<Transfer[]>([])
  const [lookupId, setLookupId] = useState('')
  const [lookupError, setLookupError] = useState<string | null>(null)
  const [isLookingUp, setIsLookingUp] = useState(false)
  const [cancellingId, setCancellingId] = useState<string | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)
  const idempotencyKeyRef = useRef<{ fingerprint: string; key: string } | null>(null)
  const refreshedCompletions = useRef(new Set<string>())

  const refreshAccounts = useCallback(async () => {
    try {
      const page = await accountsService.getAll()
      setAccounts(page.items)
      setAccountsError(null)
      return true
    } catch (error) {
      setAccountsError(`Não foi possível atualizar os saldos. ${getErrorMessage(error)}`)
      return false
    }
  }, [])

  useEffect(() => {
    let active = true
    void accountsService.getAll().then((page) => {
      if (!active) return
      setAccounts(page.items)
      setSourceAccountId((current) => current || page.items.find((account) => account.status === 'Active')?.id || '')
      setAccountsError(null)
    }).catch((error: unknown) => {
      if (!active) return
      setAccountsError(getErrorMessage(error))
    }).finally(() => {
      if (active) setAccountsLoading(false)
    })
    return () => { active = false }
  }, [])

  useEffect(() => {
    try {
      window.localStorage.setItem(trackedTransfersStorageKey, JSON.stringify(trackedIds))
    } catch {
      // Tracking still works for the current page if browser storage is disabled.
    }
  }, [trackedIds])

  useEffect(() => {
    let active = true
    const refreshTrackedTransfers = async () => {
      const results = await Promise.allSettled(trackedIds.map((id) => transfersService.get(id)))
      if (!active) return
      const received = results.flatMap((result) => result.status === 'fulfilled' ? [result.value] : [])
      const completed = received.filter((transfer) => transfer.status === 'Completed' && !refreshedCompletions.current.has(transfer.id))
      if (completed.length > 0 && await refreshAccounts()) {
        completed.forEach((transfer) => refreshedCompletions.current.add(transfer.id))
      }
      if (!active) return
      setTransfers((current) => {
        const byId = new Map(current.map((transfer) => [transfer.id, transfer]))
        for (const transfer of received) byId.set(transfer.id, transfer)
        return trackedIds.flatMap((id) => {
          const transfer = byId.get(id)
          return transfer ? [transfer] : []
        })
      })
    }

    if (trackedIds.length > 0) {
      void refreshTrackedTransfers()
      const interval = window.setInterval(() => void refreshTrackedTransfers(), 5000)
      return () => {
        active = false
        window.clearInterval(interval)
      }
    }

    return () => { active = false }
  }, [trackedIds, refreshAccounts])

  const sourceAccount = accounts.find((account) => account.id === sourceAccountId)
  const destinationAccounts = accounts.filter((account) => account.id !== sourceAccountId)
  const trackedAccountNames = useMemo(() => new Map(accounts.map((account) => [account.id, account.accountHolderName])), [accounts])
  const pixKeys = useMemo(() => destinationAccounts.flatMap((account) => account.pixKeys.map((key) => key.value)), [destinationAccounts])

  const selectSourceAccount = (id: string) => {
    setSourceAccountId(id)
    setDestinationAccountId('')
    setPixKey('')
    setBankIspb('')
    setBranch('')
    setAccountNumber('')
    setCheckDigit('')
  }

  const selectDestinationAccount = (id: string) => {
    setDestinationAccountId(id)
    const account = accounts.find((item) => item.id === id)
    if (!account) return
    setBankIspb(account.ispb)
    setBranch(account.branch)
    setAccountNumber(account.number)
    setCheckDigit(account.checkDigit ?? '')
  }

  const submitTransfer = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setFormError(null)
    setNotice(null)

    const parsedAmount = parseMoney(amount)
    if (!sourceAccountId) {
      setFormError('Selecione a conta de origem.')
      return
    }
    if (parsedAmount === null || parsedAmount <= 0) {
      setFormError('Informe um valor positivo com no máximo duas casas decimais.')
      return
    }
    if (method === 'Pix' && !pixKey.trim()) {
      setFormError('Informe a chave Pix de destino.')
      return
    }
    if (method === 'BankAccount' && (!bankIspb.trim() || !branch.trim() || !accountNumber.trim())) {
      setFormError('Informe o ISPB, a agência e o número da conta de destino.')
      return
    }
    if (mode === 'scheduled' && (!scheduledAt || new Date(scheduledAt).getTime() <= Date.now())) {
      setFormError('Escolha uma data e hora futuras para o agendamento.')
      return
    }

    const request: TransferRequest = {
      sourceAccountId,
      method,
      amount: parsedAmount,
      ...(method === 'Pix'
        ? { pixKey: pixKey.trim() }
        : {
            bankIspb: bankIspb.trim(),
            branch: branch.trim(),
            accountNumber: accountNumber.trim(),
            ...(checkDigit.trim() ? { checkDigit: checkDigit.trim() } : {}),
          }),
      ...(mode === 'scheduled' ? { scheduledAt: new Date(scheduledAt).toISOString() } : {}),
    }

    setIsSubmitting(true)
    const fingerprint = JSON.stringify({ mode, request })
    if (idempotencyKeyRef.current?.fingerprint !== fingerprint) {
      idempotencyKeyRef.current = { fingerprint, key: crypto.randomUUID() }
    }
    const idempotencyKey = idempotencyKeyRef.current.key
    try {
      if (mode === 'scheduled') {
        const accepted = await transfersService.schedule(request, idempotencyKey)
        setTrackedIds((current) => [accepted.id, ...current.filter((id) => id !== accepted.id)].slice(0, 20))
        const runAt = accepted.scheduledAt ?? request.scheduledAt
        setNotice(runAt ? `Agendamento registrado. A execução está prevista para ${formatDate(runAt)}.` : 'Agendamento registrado.')
        setAmount('')
        idempotencyKeyRef.current = null
      } else {
        const completed = await transfersService.request(request, idempotencyKey)
        setTrackedIds((current) => [completed.id, ...current.filter((id) => id !== completed.id)].slice(0, 20))
        idempotencyKeyRef.current = null
        if (completed.status === 'Completed') {
          setNotice(`Transferência concluída. ID ${completed.id}.`)
          setAmount('')
          if (await refreshAccounts()) refreshedCompletions.current.add(completed.id)
        } else if (completed.status === 'Failed') {
          setFormError(`Transferência recusada: ${failureLabel(completed.failureCode ?? 'unknown')}`)
        } else {
          setNotice(`Processamento finalizado com estado ${statusLabel(completed.status)}. ID ${completed.id}.`)
        }
      }
    } catch (error) {
      setFormError(getErrorMessage(error))
    } finally {
      setIsSubmitting(false)
    }
  }

  const lookupTransfer = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setLookupError(null)
    setActionError(null)
    const id = lookupId.trim()
    if (!id) {
      setLookupError('Informe o identificador da transferência.')
      return
    }

    setIsLookingUp(true)
    try {
      await transfersService.get(id)
      setTrackedIds((current) => [id, ...current.filter((item) => item !== id)].slice(0, 20))
      setLookupId('')
      onOpenTransfer(id)
    } catch (error) {
      setLookupError(getErrorMessage(error))
    } finally {
      setIsLookingUp(false)
    }
  }

  const cancelTransfer = async (transferId: string) => {
    setCancellingId(transferId)
    setActionError(null)
    try {
      const updated = await transfersService.cancel(transferId)
      setTransfers((current) => current.map((transfer) => transfer.id === updated.id ? updated : transfer))
    } catch (error) {
      setActionError(getErrorMessage(error))
      try {
        const latest = await transfersService.get(transferId)
        setTransfers((current) => current.map((transfer) => transfer.id === latest.id ? latest : transfer))
      } catch {
        // Keep the cancellation error visible when the refresh also fails.
      }
    } finally {
      setCancellingId(null)
    }
  }

  return (
    <div className="app-shell">
      <AppSidebar accountCount={accounts.length} activePage="transfers" onNavigate={onNavigate} />
      <main className="main-area" id="inicio">
        <AppTopbar apiAvailable={!accountsLoading && !accountsError} title="Transferências" />
        <div className="page-content transfer-page-content">
          <div className="page-heading">
            <div><div className="eyebrow"><span className="eyebrow-line" /> OPERAÇÃO FINANCEIRA</div><h1>Transferências</h1><p className="page-description">Solicite uma transferência, agende sua execução e acompanhe cada mudança de estado.</p></div>
            <span className="async-chip"><span className="live-indicator" /> Direta síncrona · Agendada pela fila</span>
          </div>

          {accountsError && <div className="state-card state-error transfer-inline-error" role="alert"><span className="state-icon"><Icon name="warning" /></span><div><strong>Não foi possível carregar as contas</strong><p>{accountsError}</p></div></div>}
          {notice && <div className="toast toast-success" role="status"><Icon name="check" />{notice}<button aria-label="Dispensar mensagem" onClick={() => setNotice(null)}>×</button></div>}

          <div className="transfer-layout">
            <section className="transfer-panel">
              <div className="transfer-panel-heading"><div><span className="panel-eyebrow">NOVA SOLICITAÇÃO</span><h2>Preparar transferência</h2><p>{mode === 'immediate' ? 'Aguarde nesta tela até o processamento e a validação financeira terminarem.' : 'Saldo e limites serão revalidados quando o agendamento for processado.'}</p></div><span className="transfer-panel-icon"><Icon name="arrows" /></span></div>

              <div className="segmented-control" role="group" aria-label="Quando processar">
                <button className={mode === 'immediate' ? 'selected' : ''} type="button" onClick={() => setMode('immediate')} disabled={isSubmitting}><span>Agora</span><small>Processamento síncrono</small></button>
                <button className={mode === 'scheduled' ? 'selected' : ''} type="button" onClick={() => setMode('scheduled')} disabled={isSubmitting}><span>Agendar</span><small>Execução assíncrona</small></button>
              </div>

              <form className="transfer-form" onSubmit={(event) => void submitTransfer(event)}>
                <label className="transfer-field"><span>Conta de origem</span>
                  <select value={sourceAccountId} onChange={(event) => selectSourceAccount(event.target.value)} disabled={accountsLoading || accounts.length === 0} required>
                    <option value="">{accountsLoading ? 'Carregando contas…' : 'Selecione uma conta'}</option>
                    {accounts.map((account) => <option key={account.id} value={account.id}>{account.accountHolderName} · {account.bankName} · Ag. {account.branch} / Conta {account.number}-{account.checkDigit ?? '0'} · {accountStatusLabel(account.status)}</option>)}
                  </select>
                  {sourceAccount && <small>Saldo {formatCurrency(sourceAccount.balance)} · Cheque especial {formatCurrency(sourceAccount.overdraftLimit)}</small>}
                </label>

                <div className="transfer-form-row">
                  <label className="transfer-field"><span>Modalidade</span>
                    <select value={method} onChange={(event) => { setMethod(event.target.value as TransferMethod); setDestinationAccountId('') }}>
                      <option value="Pix">Pix</option><option value="BankAccount">Agência e conta</option>
                    </select>
                  </label>
                  <label className="transfer-field"><span>Valor</span><div className="transfer-amount-input"><span>R$</span><input type="number" min="0.01" step="0.01" inputMode="decimal" placeholder="0,00" value={amount} onChange={(event) => setAmount(event.target.value)} required /></div></label>
                </div>

                {method === 'Pix' ? (
                  <label className="transfer-field"><span>Chave Pix de destino</span><input list="pix-key-suggestions" autoComplete="off" placeholder="CPF, e-mail, telefone ou chave aleatória" value={pixKey} onChange={(event) => setPixKey(event.target.value)} required /><datalist id="pix-key-suggestions">{pixKeys.map((key) => <option key={key} value={key} />)}</datalist><small>As chaves de demonstração das outras contas aparecem como sugestões.</small></label>
                ) : (
                  <>
                    <label className="transfer-field"><span>Conta de destino cadastrada <em>opcional</em></span>
                      <select value={destinationAccountId} onChange={(event) => selectDestinationAccount(event.target.value)}>
                        <option value="">Preencher dados manualmente</option>
                        {destinationAccounts.map((account) => <option key={account.id} value={account.id}>{account.accountHolderName} · {account.bankName}</option>)}
                      </select>
                    </label>
                    <div className="transfer-bank-fields">
                      <label className="transfer-field"><span>ISPB</span><input value={bankIspb} onChange={(event) => setBankIspb(event.target.value)} maxLength={8} required /></label>
                      <label className="transfer-field"><span>Agência</span><input value={branch} onChange={(event) => setBranch(event.target.value)} maxLength={20} required /></label>
                      <label className="transfer-field"><span>Conta</span><input value={accountNumber} onChange={(event) => setAccountNumber(event.target.value)} maxLength={30} required /></label>
                      <label className="transfer-field"><span>Dígito</span><input value={checkDigit} onChange={(event) => setCheckDigit(event.target.value)} maxLength={5} /></label>
                    </div>
                  </>
                )}

                {mode === 'scheduled' && <label className="transfer-field"><span>Data e horário da execução</span><input type="datetime-local" min={localDateTimeNow()} value={scheduledAt} onChange={(event) => setScheduledAt(event.target.value)} required /><small>O agendamento será enviado com o fuso local do navegador.</small></label>}

                {formError && <div className="form-error" role="alert">{formError}</div>}
                <div className="transfer-form-footer"><span><Icon name="shield" /> O identificador de idempotência evita solicitações duplicadas.</span><button className="button button-primary" type="submit" disabled={isSubmitting || accountsLoading || !sourceAccountId}>{isSubmitting ? <span className="button-spinner" /> : <Icon name="arrow" />}{isSubmitting ? mode === 'scheduled' ? 'Agendando…' : 'Processando transferência…' : mode === 'scheduled' ? 'Agendar transferência' : 'Transferir agora'}</button></div>
              </form>
            </section>

            <aside className="transfer-aside">
              <section className="transfer-info-card"><div className="transfer-info-heading"><span className="transfer-info-icon"><Icon name="chart" /></span><div><span className="panel-eyebrow">CICLO DA SOLICITAÇÃO</span><h2>Como funciona</h2></div></div><ol className="transfer-steps"><li><span>1</span><div><strong>Direta ou agendada</strong><small>A direta espera a resposta final; a agendada fica na fila até a data.</small></div></li><li><span>2</span><div><strong>Mesmo executor financeiro</strong><small>Execução imediata e consumidor RabbitMQ usam as mesmas regras e locks.</small></div></li><li><span>3</span><div><strong>Resultado registrado</strong><small>Sucesso ou motivo da rejeição fica disponível para acompanhamento.</small></div></li></ol></section>
              <section className="transfer-info-card transfer-lookup-card"><div className="transfer-info-heading"><span className="transfer-info-icon lookup-icon"><Icon name="wallet" /></span><div><span className="panel-eyebrow">ACOMPANHAR OUTRA</span><h2>Buscar pelo ID</h2></div></div><form className="lookup-form" onSubmit={(event) => void lookupTransfer(event)}><input value={lookupId} onChange={(event) => setLookupId(event.target.value)} placeholder="Identificador da transferência" aria-label="Identificador da transferência" /><button className="button button-quiet" type="submit" disabled={isLookingUp}>{isLookingUp ? 'Buscando…' : 'Buscar'}</button></form>{lookupError && <p className="lookup-error" role="alert">{lookupError}</p>}<small>O backend consulta transferências pelo identificador retornado na solicitação.</small></section>
            </aside>
          </div>

          <section className="transfer-history-panel">
            <div className="transfer-history-heading"><div><span className="panel-eyebrow">ACOMPANHAMENTO LOCAL</span><h2>Transferências recentes</h2><p>Atualização automática a cada 5 segundos. O histórico vem dos IDs acompanhados neste navegador.</p></div><span className="history-count">{transfers.length} acompanhada{transfers.length === 1 ? '' : 's'}</span></div>
            {actionError && <div className="form-error transfer-action-error" role="alert">{actionError}</div>}
            {transfers.length === 0 ? <div className="transfer-empty"><span className="transfer-empty-icon"><Icon name="arrows" /></span><strong>Nenhuma transferência acompanhada ainda</strong><p>Envie uma solicitação ou informe um identificador para começar.</p></div> : (
              <div className="transfer-history-list">
                {transfers.map((transfer) => <article className="transfer-history-row" key={transfer.id}>
                  <span className={`transfer-method-icon method-${transfer.method.toLowerCase()}`}><Icon name={transfer.method === 'Pix' ? 'arrow' : 'bank'} /></span>
                  <div className="transfer-row-main"><strong>{formatCurrency(transfer.amount)} <span>· {transfer.method === 'Pix' ? 'Pix' : 'Agência e conta'}</span></strong><small>{formatDate(transfer.createdAt)} · Para {trackedAccountNames.get(transfer.destinationAccountId) ?? `conta ${transfer.destinationAccountId.slice(0, 8)}`}</small><small className="transfer-row-id">ID {transfer.id}</small></div>
                  <div className="transfer-row-status"><span className={`status-pill status-${transfer.status.toLowerCase()}`}><i />{statusLabel(transfer.status)}</span>{transfer.status === 'Failed' && transfer.failureCode && <small title={transfer.failureCode}>{failureLabel(transfer.failureCode)}</small>}</div>
                  <button className="button button-details" type="button" onClick={() => onOpenTransfer(transfer.id)}>Ver detalhes</button>
                  {transfer.status === 'Scheduled' && <button className="button button-cancel" type="button" disabled={cancellingId === transfer.id} onClick={() => void cancelTransfer(transfer.id)}>{cancellingId === transfer.id ? 'Cancelando…' : 'Cancelar'}</button>}
                </article>)}
              </div>
            )}
          </section>
          <footer className="page-footer"><span>BTSA <span>·</span> Plataforma de transferências</span><span>Ambiente de demonstração</span></footer>
        </div>
      </main>
    </div>
  )
}

function readTrackedIds() {
  try {
    const parsed: unknown = JSON.parse(window.localStorage.getItem(trackedTransfersStorageKey) ?? '[]')
    return Array.isArray(parsed) ? parsed.filter((value): value is string => typeof value === 'string').slice(0, 20) : []
  } catch {
    return []
  }
}

function localDateTimeNow() {
  return toLocalDateTimeInput(new Date())
}

function defaultScheduledTime() {
  return toLocalDateTimeInput(new Date(Date.now() + 10 * 60 * 1000))
}

function toLocalDateTimeInput(date: Date) {
  const localDate = new Date(date.getTime() - date.getTimezoneOffset() * 60_000)
  return localDate.toISOString().slice(0, 16)
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(value))
}

function statusLabel(status: string) {
  const labels: Record<string, string> = {
    Scheduled: 'Agendada', Processing: 'Processando', Completed: 'Concluída', Failed: 'Falhou', Cancelled: 'Cancelada',
  }
  return labels[status] ?? status
}

function accountStatusLabel(status: string) {
  return status === 'Blocked' ? 'Bloqueada' : status === 'Inactive' ? 'Inativa' : 'Ativa'
}

function failureLabel(code: string) {
  const labels: Record<string, string> = {
    'transfer.limit_policy_missing': 'Conta sem política de limites.',
    'transfer.attempt_limit_exceeded': 'Limite de tentativas excedido.',
    'transfer.amount_limit_exceeded': 'Limite de valor excedido.',
    'account.source_blocked': 'A conta de origem está bloqueada.',
    'account.source_inactive': 'A conta de origem está inativa.',
    'account.destination_blocked': 'A conta de destino está bloqueada.',
    'account.destination_inactive': 'A conta de destino está inativa.',
    'account.source_not_active': 'A conta de origem não está ativa.',
    'account.destination_not_active': 'A conta de destino não está ativa.',
    'transfer.account_missing': 'Uma das contas não foi encontrada.',
  }
  return labels[code] ?? `Motivo: ${code}`
}

function getErrorMessage(error: unknown) {
  if (error instanceof ApiError) return error.message
  return 'Não foi possível concluir a solicitação. Verifique a API e tente novamente.'
}
