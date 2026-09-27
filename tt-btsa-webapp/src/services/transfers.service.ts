import { http } from './http'
import type { AcceptedTransfer, Transfer, TransferRequest } from '../types/transfer'

export const transfersService = {
  async request(values: TransferRequest, idempotencyKey: string) {
    const response = await http.post<Transfer>('/transfers', values, {
      headers: { 'Idempotency-Key': idempotencyKey },
      timeout: 60_000,
    })
    return response.data
  },

  async schedule(values: TransferRequest, idempotencyKey: string) {
    const response = await http.post<AcceptedTransfer>('/transfers/scheduled', values, {
      headers: { 'Idempotency-Key': idempotencyKey },
    })
    return response.data
  },

  async get(transferId: string) {
    const response = await http.get<Transfer>(`/transfers/${transferId}`)
    return response.data
  },

  async cancel(transferId: string) {
    const response = await http.post<Transfer>(`/transfers/${transferId}/cancel`)
    return response.data
  },
}
