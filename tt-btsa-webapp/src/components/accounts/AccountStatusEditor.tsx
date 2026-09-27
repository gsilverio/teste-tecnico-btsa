import { useEffect, useState } from 'react'
import type { Account, AccountStatus } from '../../types/account'

interface AccountStatusEditorProps {
  account: Account
  isSaving: boolean
  error: string | null
  onClose: () => void
  onSave: (status: AccountStatus) => Promise<void>
}

export function AccountStatusEditor({ account, isSaving, error, onClose, onSave }: AccountStatusEditorProps) {
  const [status, setStatus] = useState<AccountStatus>(account.status as AccountStatus)

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape' && !isSaving) onClose()
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [isSaving, onClose])

  return (
    <div className="dialog-backdrop" onMouseDown={(event) => { if (event.target === event.currentTarget && !isSaving) onClose() }}>
      <section className="editor-dialog" role="dialog" aria-modal="true" aria-labelledby="account-status-title">
        <div className="dialog-topline"><span className="dialog-kicker">SITUAÇÃO DA CONTA</span><button type="button" className="close-button" aria-label="Fechar" onClick={onClose} disabled={isSaving}>×</button></div>
        <div className="dialog-title"><span className="dialog-account-avatar">{initials(account.accountHolderName)}</span><div><h2 id="account-status-title">Status operacional</h2><p>{account.accountHolderName} · CPF {account.accountHolderCpfMasked}</p></div></div>
        <div className="account-reference"><span>{account.bankName} · Agência {account.branch} · Conta {account.number}</span><span>Status atual: {statusLabel(account.status)}</span></div>
        <p className="dialog-intro">Contas bloqueadas ou inativas não podem enviar nem receber transferências. Reativar libera os dois fluxos.</p>
        <label className="form-field overdraft-field"><span>Novo status</span>
          <select value={status} onChange={(event) => setStatus(event.target.value as AccountStatus)} disabled={isSaving}>
            <option value="Active">Ativa</option><option value="Blocked">Bloqueada</option><option value="Inactive">Inativa</option>
          </select>
        </label>
        {error && <div className="form-error" role="alert">{error}</div>}
        <div className="dialog-actions"><span className="action-spacer" /><button type="button" className="button button-quiet" onClick={onClose} disabled={isSaving}>Cancelar</button><button type="button" className="button button-primary" onClick={() => void onSave(status)} disabled={isSaving}>{isSaving ? <><span className="button-spinner" /> Salvando…</> : 'Salvar status'}</button></div>
      </section>
    </div>
  )
}

function initials(name: string) { return name.split(' ').map((part) => part[0]).slice(0, 2).join('').toUpperCase() }
function statusLabel(status: string) { return status === 'Blocked' ? 'Bloqueada' : status === 'Inactive' ? 'Inativa' : 'Ativa' }
