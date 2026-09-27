import { http } from './http'
import type { CreateTransferLimitPolicy, TransferLimitPolicy, TransferLimitPolicyPage, TransferLimitPolicyValues } from '../types/transfer-limit-policy'

export const transferLimitPoliciesService = {
  async getAll(signal?: AbortSignal) {
    const response = await http.get<TransferLimitPolicyPage>('/transfer-limit-policies?page=1&pageSize=100', { signal })
    return response.data
  },

  async create(values: CreateTransferLimitPolicy) {
    const response = await http.post<TransferLimitPolicy>('/transfer-limit-policies', values)
    return response.data
  },

  async update(policyId: string, values: TransferLimitPolicyValues) {
    const response = await http.put<TransferLimitPolicy>(`/transfer-limit-policies/${policyId}`, values)
    return response.data
  },

  async delete(policyId: string) {
    await http.delete<void>(`/transfer-limit-policies/${policyId}`)
  },
}
