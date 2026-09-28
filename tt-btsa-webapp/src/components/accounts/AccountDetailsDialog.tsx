import { useEffect, useState } from 'react'
import type { Account } from '../../types/account'
import { accountsService } from '../../services/accounts.service'
import { transfersService } from '../../services/transfers.service'
import { ApiError } from '../../services/http'
import { formatCurrency } from '../../services/currency'
import type { AccountTransferPage } from '../../types/transfer'
import { Icon } from '../Icon'

interface AccountDetailsDialogProps {
  accountId: string
  onClose: () => void
  onOpenTransfer: (transferId: string) => void
}

export function AccountDetailsDialog({ accountId, onClose, onOpenTransfer }: AccountDetailsDialogProps) {
  const [loadResult, setLoadResult] = useState<{ key: string; account?: Account; error?: string } | null>(null)
  const [retry, setRetry] = useState(0)
  const requestKey = `${accountId}:${retry}`
  const [transferPageNumber, setTransferPageNumber] = useState(1)
  const [transferRetry, setTransferRetry] = useState(0)
  const [transferResult, setTransferResult] = useState<{ key: string; page?: AccountTransferPage; error?: string } | null>(null)
  const transferRequestKey = `${accountId}:${transferPageNumber}:${transferRetry}`

  useEffect(() => {
    let active = true
    void accountsService.getById(accountId).then((result) => {
      if (active) setLoadResult({ key: requestKey, account: result })
    }).catch((loadError: unknown) => {
      if (active) setLoadResult({
        key: requestKey,
        error: loadError instanceof ApiError ? loadError.message : 'Não foi possível consultar os detalhes da conta.',
      })
    })
    return () => { active = false }
  }, [accountId, requestKey])

  useEffect(() => {
    let active = true
    void transfersService.listByAccount(accountId, transferPageNumber).then((page) => {
      if (active) setTransferResult({ key: transferRequestKey, page })
    }).catch((loadError: unknown) => {
      if (active) setTransferResult({
        key: transferRequestKey,
        error: loadError instanceof ApiError ? loadError.message : 'Não foi possível consultar as transferências.',
      })
    })
    return () => { active = false }
  }, [accountId, transferPageNumber, transferRequestKey])

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => { if (event.key === 'Escape') onClose() }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [onClose])

  const currentResult = loadResult?.key === requestKey ? loadResult : null
  const isLoading = currentResult === null
  const account = currentResult?.account ?? null
  const error = currentResult?.error ?? null
  const currentTransfers = transferResult?.key === transferRequestKey ? transferResult : null

  return (
    <div className="dialog-backdrop" onMouseDown={(event) => { if (event.target === event.currentTarget) onClose() }}>
      <section className="editor-dialog account-details-dialog" role="dialog" aria-modal="true" aria-labelledby="account-details-title">
        <div className="dialog-topline"><span className="dialog-kicker">CONSULTA DA CONTA</span><button type="button" className="close-button" aria-label="Fechar" onClick={onClose}>×</button></div>
        <div className="dialog-title"><span className="dialog-account-avatar"><Icon name="wallet" /></span><div><h2 id="account-details-title">Detalhes da conta</h2><p>Dados atuais consultados pela API.</p></div></div>

        {isLoading ? (
          <div className="account-details-state"><span className="button-spinner" /><strong>Carregando dados da conta…</strong></div>
        ) : error ? (
          <div className="account-details-state state-error" role="alert"><strong>Não foi possível consultar a conta</strong><p>{error}</p><button className="button button-outline" type="button" onClick={() => setRetry((current) => current + 1)}>Tentar novamente</button></div>
        ) : account ? (
          <>
            <div className="account-details-identity"><strong>{account.accountHolderName}</strong><span>CPF {account.accountHolderCpfMasked}</span><span className={`account-status status-${account.status.toLowerCase()}`}>{statusLabel(account.status)}</span></div>
            <dl className="account-details-grid">
              <Detail label="Banco" value={account.bankName} />
              <Detail label="ISPB" value={account.ispb} />
              <Detail label="Tipo" value={account.type === 'Checking' ? 'Conta corrente' : 'Poupança'} />
              <Detail label="Agência" value={account.branch} />
              <Detail label="Número da conta" value={`${account.number}${account.checkDigit ? `-${account.checkDigit}` : ''}`} />
              <Detail label="Saldo" value={formatCurrency(account.balance)} />
              <Detail label="Cheque especial" value={formatCurrency(account.overdraftLimit)} />
              <Detail label="Identificador" value={account.id} />
            </dl>
            <section className="account-pix-details">
              <h3>Chaves Pix</h3>
              {account.pixKeys.length ? account.pixKeys.map((key) => <div className="account-pix-key" key={`${key.type}:${key.value}`}><span>{key.type}</span><code>{key.value}</code></div>) : <p>Nenhuma chave Pix cadastrada.</p>}
            </section>
            <section className="account-transfers" aria-labelledby="account-transfers-title">
              <div className="account-transfers-heading"><h3 id="account-transfers-title">Transferências desta conta</h3>{currentTransfers?.page && <span>{currentTransfers.page.totalCount} no total</span>}</div>
              {!currentTransfers ? (
                <p role="status">Carregando transferências…</p>
              ) : currentTransfers.error ? (
                <div className="account-transfers-error" role="alert"><p>{currentTransfers.error}</p><button className="button button-outline" type="button" onClick={() => setTransferRetry((current) => current + 1)}>Tentar novamente</button></div>
              ) : currentTransfers.page?.items.length === 0 ? (
                <p>Nenhuma transferência enviada ou recebida por esta conta.</p>
              ) : currentTransfers.page ? (
                <>
                  <ul className="account-transfers-list">
                    {currentTransfers.page.items.map((transfer) => {
                      const sent = transfer.sourceAccountId === accountId
                      return <li key={transfer.id}>
                        <div className="account-transfer-main"><strong>{sent ? 'Enviada' : 'Recebida'} · {formatCurrency(transfer.amount)}</strong><span>{transfer.method === 'Pix' ? 'Pix' : 'Agência e conta'} · {formatTransferDate(transfer.createdAt)}</span><span>{sent ? 'Para' : 'De'} conta {`${sent ? transfer.destinationAccountId : transfer.sourceAccountId}`.slice(0, 8)}…</span></div>
                        <div className="account-transfer-actions"><span className={`status-pill status-${transfer.status.toLowerCase()}`}><i />{transferStatusLabel(transfer.status)}</span><button className="button button-details" type="button" onClick={() => onOpenTransfer(transfer.id)}>Ver detalhes</button></div>
                      </li>
                    })}
                  </ul>
                  {currentTransfers.page.totalPages > 1 && <nav className="account-transfers-pagination" aria-label="Páginas de transferências"><button className="button button-outline" type="button" disabled={!currentTransfers.page.hasPreviousPage} onClick={() => setTransferPageNumber((page) => page - 1)}>Anterior</button><span>Página {currentTransfers.page.page} de {currentTransfers.page.totalPages}</span><button className="button button-outline" type="button" disabled={!currentTransfers.page.hasNextPage} onClick={() => setTransferPageNumber((page) => page + 1)}>Próxima</button></nav>}
                </>
              ) : null}
            </section>
          </>
        ) : null}
      </section>
    </div>
  )
}

function Detail({ label, value }: { label: string; value: string }) {
  return <div><dt>{label}</dt><dd>{value}</dd></div>
}

function statusLabel(status: string) {
  return status === 'Blocked' ? 'Conta bloqueada' : status === 'Inactive' ? 'Conta inativa' : 'Conta ativa'
}

function transferStatusLabel(status: string) {
  return ({ Scheduled: 'Agendada', Processing: 'Processando', Completed: 'Concluída', Failed: 'Falhou', Cancelled: 'Cancelada' } as Record<string, string>)[status] ?? status
}

function formatTransferDate(value: string) {
  return new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(value))
}
