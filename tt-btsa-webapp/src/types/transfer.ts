export type TransferMethod = 'Pix' | 'BankAccount'

export interface TransferRequest {
  sourceAccountId: string
  method: TransferMethod
  amount: number
  pixKey?: string
  bankIspb?: string
  branch?: string
  accountNumber?: string
  checkDigit?: string
  scheduledAt?: string
}

export interface AcceptedTransfer {
  id: string
  status: string
  createdAt: string
  scheduledAt: string | null
}

export interface Transfer {
  id: string
  sourceAccountId: string
  destinationAccountId: string
  amount: number
  method: TransferMethod
  status: string
  createdAt: string
  scheduledAt: string | null
  finishedAt: string | null
  cancelledAt: string | null
  failureCode: string | null
  auditTrail?: TransferAuditEvent[] | null
  processingError?: string | null
  isInDeadLetter?: boolean
}

export interface AccountTransferSummary {
  id: string
  sourceAccountId: string
  destinationAccountId: string
  amount: number
  method: TransferMethod
  status: string
  createdAt: string
  scheduledAt: string | null
  failureCode: string | null
}

export interface AccountTransferPage {
  items: AccountTransferSummary[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
  hasPreviousPage: boolean
  hasNextPage: boolean
}

export interface TransferAuditEvent {
  id: string
  action: string
  sequence: number
  previousStatus: string | null
  status: string
  occurredAt: string
  failureCode: string | null
}
