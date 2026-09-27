import { useCallback, useEffect, useState } from 'react'
import { accountsService } from '../services/accounts.service'
import { ApiError } from '../services/http'
import { transferLimitPoliciesService } from '../services/transfer-limit-policies.service'
import type { Account, AccountStatus } from '../types/account'
import type { TransferLimitPolicy, TransferLimitPolicyValues } from '../types/transfer-limit-policy'

export function useDashboard() {
  const [accounts, setAccounts] = useState<Account[]>([])
  const [policies, setPolicies] = useState<TransferLimitPolicy[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [isSaving, setIsSaving] = useState(false)
  const [actionError, setActionError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)

  const fetchDashboard = useCallback((signal?: AbortSignal) => Promise.all([
    accountsService.getAll(signal),
    transferLimitPoliciesService.getAll(signal),
  ]), [])

  const applyDashboard = useCallback(([accountPage, policyPage]: Awaited<ReturnType<typeof fetchDashboard>>) => {
    setAccounts(accountPage.items)
    setPolicies(policyPage.items)
    setLoadError(null)
  }, [])

  useEffect(() => {
    let active = true
    // The dashboard performs small read-only requests. Let them finish during
    // React StrictMode's development remount instead of cancelling an Npgsql open.
    void fetchDashboard().then((data) => {
      if (!active) return
      applyDashboard(data)
      setIsLoading(false)
    }).catch((error: unknown) => {
      if (!active) return
      setLoadError(getErrorMessage(error))
      setIsLoading(false)
    })
    return () => { active = false }
  }, [applyDashboard, fetchDashboard])

  const refresh = useCallback(async () => {
    setIsLoading(true)
    setLoadError(null)
    try {
      applyDashboard(await fetchDashboard())
    } catch (error) {
      setLoadError(getErrorMessage(error))
    } finally {
      setIsLoading(false)
    }
  }, [applyDashboard, fetchDashboard])

  const performAction = useCallback(async (action: () => Promise<unknown>, successMessage: string) => {
    setIsSaving(true)
    setActionError(null)
    setNotice(null)
    try {
      await action()
      setNotice(successMessage)
      await refresh()
      return true
    } catch (error) {
      setActionError(getErrorMessage(error))
      return false
    } finally {
      setIsSaving(false)
    }
  }, [refresh])

  const savePolicy = useCallback((accountId: string, policy: TransferLimitPolicy | null, values: TransferLimitPolicyValues) => {
    const action = () => policy
      ? transferLimitPoliciesService.update(policy.id, values)
      : transferLimitPoliciesService.create({ accountId, ...values })
    return performAction(action, policy ? 'Limites de transferência atualizados.' : 'Limites de transferência configurados.')
  }, [performAction])

  const deletePolicy = useCallback((policyId: string) =>
    performAction(() => transferLimitPoliciesService.delete(policyId), 'Configuração de limites removida.'), [performAction])

  const saveOverdraft = useCallback((accountId: string, limit: number) =>
    performAction(() => accountsService.setOverdraftLimit(accountId, limit), 'Cheque especial atualizado.'), [performAction])

  const clearOverdraft = useCallback((accountId: string) =>
    performAction(() => accountsService.clearOverdraftLimit(accountId), 'Cheque especial definido como zero.'), [performAction])

  const setAccountStatus = useCallback((accountId: string, status: AccountStatus) =>
    performAction(() => accountsService.setStatus(accountId, status), 'Status da conta atualizado.'), [performAction])

  return {
    accounts,
    policies,
    isLoading,
    loadError,
    isSaving,
    actionError,
    notice,
    refresh,
    savePolicy,
    deletePolicy,
    saveOverdraft,
    clearOverdraft,
    setAccountStatus,
    clearActionError: () => setActionError(null),
    clearNotice: () => setNotice(null),
  }
}

function getErrorMessage(error: unknown) {
  if (error instanceof ApiError) return error.message
  return 'Não foi possível concluir a solicitação. Tente novamente.'
}
