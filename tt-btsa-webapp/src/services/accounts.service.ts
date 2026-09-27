import { http } from './http'
import type { Account, AccountPage, AccountStatus } from '../types/account'

export interface OverdraftLimit {
  accountId: string
  balance: number
  overdraftLimit: number
}

export const accountsService = {
  async getAll(signal?: AbortSignal) {
    const response = await http.get<AccountPage>('/accounts?page=1&pageSize=100', { signal })
    return response.data
  },

  async getById(accountId: string, signal?: AbortSignal) {
    const response = await http.get<Account>(`/accounts/${accountId}`, { signal })
    return response.data
  },

  async setStatus(accountId: string, status: AccountStatus) {
    const response = await http.put<{ accountId: string; status: AccountStatus }>(`/accounts/${accountId}/status`, { status })
    return response.data
  },

  async setOverdraftLimit(accountId: string, limit: number) {
    const response = await http.put<OverdraftLimit>(`/accounts/${accountId}/overdraft-limit`, { limit })
    return response.data
  },

  async clearOverdraftLimit(accountId: string) {
    const response = await http.delete<OverdraftLimit>(`/accounts/${accountId}/overdraft-limit`)
    return response.data
  },
}
