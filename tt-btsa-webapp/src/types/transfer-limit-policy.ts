export interface TransferLimitPolicy {
  id: string
  accountId: string
  accountHolderName: string
  dayMaximumAmount: number
  dayMaximumAttempts: number
  nightMaximumAmount: number
  nightMaximumAttempts: number
}

export interface TransferLimitPolicyPage {
  items: TransferLimitPolicy[]
  page: number
  pageSize: number
  totalCount: number
}

export interface TransferLimitPolicyValues {
  dayMaximumAmount: number
  dayMaximumAttempts: number
  nightMaximumAmount: number
  nightMaximumAttempts: number
}

export interface CreateTransferLimitPolicy extends TransferLimitPolicyValues {
  accountId: string
}
