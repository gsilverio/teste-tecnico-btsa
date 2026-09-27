import type { Account } from '../../types/account'
import type { TransferLimitPolicy } from '../../types/transfer-limit-policy'
import { formatCurrency } from '../../services/currency'
import { Icon } from '../Icon'

interface AccountsTableProps {
  accounts: Account[]
  policiesByAccount: Map<string, TransferLimitPolicy>
  onEditPolicy: (account: Account) => void
  onEditOverdraft: (account: Account) => void
  onEditStatus: (account: Account) => void
  onViewDetails: (account: Account) => void
}

export function AccountsTable({ accounts, policiesByAccount, onEditPolicy, onEditOverdraft, onEditStatus, onViewDetails }: AccountsTableProps) {
  return (
    <div className="table-scroll">
      <table className="accounts-table">
        <thead><tr><th>Conta</th><th>Instituição</th><th>Saldo disponível</th><th>Cheque especial</th><th>Limite de transferência</th><th><span className="sr-only">Ações</span></th></tr></thead>
        <tbody>
          {accounts.map((account) => {
            const policy = policiesByAccount.get(account.id)
            return <tr key={account.id}>
              <td><div className="account-cell"><span className={`account-avatar avatar-${account.number.slice(-1)}`}>{initials(account.accountHolderName)}</span><span className="account-text"><strong>{account.accountHolderName}</strong><small>CPF {account.accountHolderCpfMasked}</small><small>Ag. {account.branch} · Conta {account.number}{account.checkDigit ? `-${account.checkDigit}` : ''}</small></span></div></td>
              <td><div className="bank-cell"><span className="bank-icon"><Icon name="bank" /></span><span><strong>{account.bankName}</strong><small>ISPB {account.ispb}</small></span></div></td>
              <td><span className="money-value">{formatCurrency(account.balance)}</span><small className={`account-status status-${account.status.toLowerCase()}`}>{statusLabel(account.status)}</small></td>
              <td><span className="money-value">{formatCurrency(account.overdraftLimit)}</span><small className="table-subtext">Limite contratado</small></td>
              <td>{policy ? <><span className="policy-status configured"><span />Configurado</span><small className="table-subtext">Dia {formatCurrency(policy.dayMaximumAmount)} · Noite {formatCurrency(policy.nightMaximumAmount)}</small></> : <span className="policy-status pending"><span />Pendente</span>}</td>
              <td className="action-cell">
                <div className="row-actions">
                  <button className="button button-small button-details" onClick={() => onViewDetails(account)}><Icon name="info" /> Ver detalhes</button>
                  <button className="icon-button" onClick={() => onEditStatus(account)} aria-label={`Alterar status de ${account.accountHolderName}`} title="Bloquear, inativar ou reativar conta"><Icon name="shield" /></button>
                  <button className="icon-button" onClick={() => onEditOverdraft(account)} aria-label={`Editar cheque especial de ${account.accountHolderName}`} title="Editar cheque especial"><Icon name="coins" /></button>
                  <button className={policy ? 'icon-button' : 'button button-small'} onClick={() => onEditPolicy(account)} aria-label={`${policy ? 'Editar' : 'Configurar'} limites de ${account.accountHolderName}`} title={policy ? 'Editar limites de transferência' : undefined}>
                    {policy ? <Icon name="edit" /> : <>Configurar <Icon name="arrow" /></>}
                  </button>
                </div>
              </td>
            </tr>
          })}
        </tbody>
      </table>
    </div>
  )
}

function initials(name: string) { return name.split(' ').map((part) => part[0]).slice(0, 2).join('').toUpperCase() }
function statusLabel(status: string) { return status === 'Blocked' ? 'Conta bloqueada' : status === 'Inactive' ? 'Conta inativa' : 'Conta ativa' }
