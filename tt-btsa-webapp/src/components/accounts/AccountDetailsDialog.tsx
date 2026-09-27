import { useEffect, useState } from 'react'
import type { Account } from '../../types/account'
import { accountsService } from '../../services/accounts.service'
import { ApiError } from '../../services/http'
import { formatCurrency } from '../../services/currency'
import { Icon } from '../Icon'

interface AccountDetailsDialogProps {
  accountId: string
  onClose: () => void
}

export function AccountDetailsDialog({ accountId, onClose }: AccountDetailsDialogProps) {
  const [loadResult, setLoadResult] = useState<{ key: string; account?: Account; error?: string } | null>(null)
  const [retry, setRetry] = useState(0)
  const requestKey = `${accountId}:${retry}`

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
    const onKeyDown = (event: KeyboardEvent) => { if (event.key === 'Escape') onClose() }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [onClose])

  const currentResult = loadResult?.key === requestKey ? loadResult : null
  const isLoading = currentResult === null
  const account = currentResult?.account ?? null
  const error = currentResult?.error ?? null

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
