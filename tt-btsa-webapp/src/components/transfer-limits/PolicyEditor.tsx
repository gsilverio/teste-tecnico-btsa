import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import type { Account } from '../../types/account'
import type { TransferLimitPolicy, TransferLimitPolicyValues } from '../../types/transfer-limit-policy'
import { formatCurrency } from '../../services/currency'

interface PolicyEditorProps {
  account: Account
  policy: TransferLimitPolicy | null
  isSaving: boolean
  error: string | null
  onClose: () => void
  onSave: (values: TransferLimitPolicyValues) => Promise<void>
  onDelete?: () => Promise<void>
}

interface PolicyForm {
  dayAmount: string
  dayAttempts: string
  nightAmount: string
  nightAttempts: string
}

export function PolicyEditor({ account, policy, isSaving, error, onClose, onSave, onDelete }: PolicyEditorProps) {
  const [form, setForm] = useState<PolicyForm>(() => ({
    dayAmount: policy?.dayMaximumAmount.toString() ?? '5000',
    dayAttempts: policy?.dayMaximumAttempts.toString() ?? '5',
    nightAmount: policy?.nightMaximumAmount.toString() ?? '1000',
    nightAttempts: policy?.nightMaximumAttempts.toString() ?? '3',
  }))
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
    const dayAmount = parseMoney(form.dayAmount)
    const nightAmount = parseMoney(form.nightAmount)
    const dayAttempts = parseAttempts(form.dayAttempts)
    const nightAttempts = parseAttempts(form.nightAttempts)

    if (dayAmount === null || nightAmount === null) {
      setValidationError('Informe valores iguais ou maiores que zero, com até duas casas decimais.')
      return
    }
    if (dayAttempts === null || nightAttempts === null) {
      setValidationError('Informe uma quantidade inteira de tentativas igual ou maior que zero.')
      return
    }

    setValidationError(null)
    await onSave({
      dayMaximumAmount: dayAmount,
      dayMaximumAttempts: dayAttempts,
      nightMaximumAmount: nightAmount,
      nightMaximumAttempts: nightAttempts,
    })
  }

  const updateField = (field: keyof PolicyForm, value: string) => {
    setForm((current) => ({ ...current, [field]: value }))
    setValidationError(null)
  }

  const remove = async () => {
    if (!onDelete || isSaving) return
    if (window.confirm(`Remover a configuração de transferência de ${account.accountHolderName}?`)) {
      await onDelete()
    }
  }

  return (
    <div className="dialog-backdrop" onMouseDown={(event) => { if (event.target === event.currentTarget && !isSaving) onClose() }}>
      <section className="editor-dialog" role="dialog" aria-modal="true" aria-labelledby="editor-title">
        <div className="dialog-topline"><span className="dialog-kicker">CONFIGURAÇÃO DA CONTA</span><button type="button" className="close-button" aria-label="Fechar" onClick={onClose} disabled={isSaving}>×</button></div>
        <div className="dialog-title"><span className="dialog-account-avatar">{account.accountHolderName.split(' ').map((part) => part[0]).slice(0, 2).join('').toUpperCase()}</span><div><h2 id="editor-title">{policy ? 'Editar limites' : 'Configurar limites'}</h2><p>{account.accountHolderName}</p></div></div>
        <div className="account-reference"><span>{account.bankName} · Agência {account.branch} · Conta {account.number}</span><span>Saldo {formatCurrency(account.balance)}</span></div>
        <p className="dialog-intro">Defina os valores máximos e a quantidade de transferências permitidas para cada período.</p>

        <form onSubmit={submit} noValidate>
          <fieldset className="limit-fieldset">
            <legend><span className="period-icon day-icon">☀</span><span><strong>Período diurno</strong><small>Das 06h às 22h</small></span></legend>
            <label className="form-field"><span>Limite por período</span><span className="input-with-prefix"><span>R$</span><input inputMode="decimal" value={form.dayAmount} onChange={(event) => updateField('dayAmount', event.target.value)} aria-label="Limite monetário diurno" /></span></label>
            <label className="form-field"><span>Tentativas permitidas</span><span className="input-suffix"><input type="number" min="0" step="1" value={form.dayAttempts} onChange={(event) => updateField('dayAttempts', event.target.value)} /><span>por hora</span></span></label>
          </fieldset>

          <fieldset className="limit-fieldset night-fieldset">
            <legend><span className="period-icon night-icon">☾</span><span><strong>Período noturno</strong><small>Das 22h às 06h</small></span></legend>
            <label className="form-field"><span>Limite por período</span><span className="input-with-prefix"><span>R$</span><input inputMode="decimal" value={form.nightAmount} onChange={(event) => updateField('nightAmount', event.target.value)} aria-label="Limite monetário noturno" /></span></label>
            <label className="form-field"><span>Tentativas permitidas</span><span className="input-suffix"><input type="number" min="0" step="1" value={form.nightAttempts} onChange={(event) => updateField('nightAttempts', event.target.value)} /><span>por hora</span></span></label>
          </fieldset>

          <p className="zero-hint">Defina o valor ou tentativas como zero para bloquear transferências no período.</p>
          {(validationError || error) && <div className="form-error" role="alert">{validationError ?? error}</div>}

          <div className="dialog-actions">
            {onDelete && <button type="button" className="button button-danger-quiet" onClick={() => void remove()} disabled={isSaving}>Remover</button>}
            <span className="action-spacer" />
            <button type="button" className="button button-quiet" onClick={onClose} disabled={isSaving}>Cancelar</button>
            <button type="submit" className="button button-primary" disabled={isSaving}>{isSaving ? <><span className="button-spinner" /> Salvando…</> : 'Salvar configuração'}</button>
          </div>
        </form>
      </section>
    </div>
  )
}

function parseMoney(value: string): number | null {
  const normalized = value.trim().replace(',', '.')
  if (!/^\d+(\.\d{1,2})?$/.test(normalized)) return null
  const amount = Number(normalized)
  return Number.isFinite(amount) && Number.isSafeInteger(amount * 100) ? amount : null
}

function parseAttempts(value: string): number | null {
  if (!/^\d+$/.test(value.trim())) return null
  const attempts = Number(value)
  return Number.isSafeInteger(attempts) ? attempts : null
}
