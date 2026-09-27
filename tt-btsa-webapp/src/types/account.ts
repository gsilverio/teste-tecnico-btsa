export interface Account {
  id: string
  accountHolderId: string
  accountHolderName: string
  accountHolderCpfMasked: string
  bankId: string
  bankName: string
  ispb: string
  type: string
  branch: string
  number: string
  checkDigit: string | null
  balance: number
  overdraftLimit: number
  status: string
  pixKeys: PixKey[]
}

export type AccountStatus = 'Active' | 'Blocked' | 'Inactive'

export interface PixKey {
  type: string
  value: string
}

export interface AccountPage {
  items: Account[]
  page: number
  pageSize: number
  totalCount: number
}
