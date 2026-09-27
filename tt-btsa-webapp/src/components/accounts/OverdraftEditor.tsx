import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import type { Account } from '../../types/account'
import { formatCurrency } from '../../services/currency'

interface OverdraftEditorProps {
  account: Account
  isSaving: boolean
  error: string | null
  onClose: () => void
  onSave: (limit: number) => Promise<void>
  onClear: () => Promise<void>
}

export function OverdraftEditor({ account, isSaving, error, onClose, onSave, onClear }: OverdraftEditorProps) {
  const [limit, setLimit] = useState(account.overdraftLimit.toString())
  const [validationError, setValidationError] = useState<string | null>(null)

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape' && !isSaving) onClose()
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [isSaving, onClose])

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const normalized = limit.trim().replace(',', '.')
    if (!/^\d+(\.\d{1,2})?$/.test(normalized)) {
      setValidationError('Informe um valor igual ou maior que zero, com até duas casas decimais.')
      return
    }
    const value = Number(normalized)
    if (!Number.isFinite(value) || !Number.isSafeInteger(value * 100)) {
      setValidationError('O valor informado está fora do intervalo suportado pela tela.')
      return
    }
    setValidationError(null)
    await onSave(value)
  }

  return (
    <div className="dialog-backdrop" onMouseDown={(event) => { if (event.target === event.currentTarget && !isSaving) onClose() }}>
      <section className="editor-dialog overdraft-dialog" role="dialog" aria-modal="true" aria-labelledby="overdraft-title">
        <div className="dialog-topline"><span className="dialog-kicker">CONFIGURAÇÃO DA CONTA</span><button type="button" className="close-button" aria-label="Fechar" onClick={onClose} disabled={isSaving}>×</button></div>
        <div className="dialog-title"><span className="dialog-account-avatar">{initials(account.accountHolderName)}</span><div><h2 id="overdraft-title">Cheque especial</h2><p>{account.accountHolderName}</p></div></div>
        <div className="account-reference"><span>{account.bankName} · Agência {account.branch} · Conta {account.number}</span><span>Saldo {formatCurrency(account.balance)}</span></div>
        <p className="dialog-intro">O limite não pode ficar abaixo do valor de cheque especial já utilizado pela conta.</p>
        <form onSubmit={submit} noValidate>
          <label className="form-field overdraft-field"><span>Limite de cheque especial</span><span className="input-with-prefix"><span>R$</span><input inputMode="decimal" value={limit} onChange={(event) => { setLimit(event.target.value); setValidationError(null) }} aria-label="Limite de cheque especial" /></span></label>
          {(validationError || error) && <div className="form-error" role="alert">{validationError ?? error}</div>}
          <div className="dialog-actions">
            <button type="button" className="button button-danger-quiet" onClick={() => { if (window.confirm('Definir o cheque especial desta conta como zero?')) void onClear() }} disabled={isSaving}>Zerar limite</button>
            <span className="action-spacer" />
            <button type="button" className="button button-quiet" onClick={onClose} disabled={isSaving}>Cancelar</button>
            <button type="submit" className="button button-primary" disabled={isSaving}>{isSaving ? <><span className="button-spinner" /> Salvando…</> : 'Salvar limite'}</button>
          </div>
        </form>
      </section>
    </div>
  )
}

function initials(name: string) {
  return name.split(' ').map((part) => part[0]).slice(0, 2).join('').toUpperCase()
}
