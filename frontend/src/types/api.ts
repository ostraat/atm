export interface Transaction {
  label: string
  amount: number
  createdAt: string
}

export interface Account {
  id: string
  label: string
  balance: number
  latestTransaction?: Transaction | null
}

export interface AccountsResponse {
  error?: string | null
  accounts: Account[]
}

export interface AccountResponse {
  error?: string | null
  account: Account
}

export interface TransactionsResponse {
  error?: string | null
  transactions: Transaction[]
}

export interface DepositResponse {
  error?: string | null
  account: Account
  transaction: Transaction
}

export interface WithdrawResponse {
  error?: string | null
  account: Account
  transaction: Transaction
}

export interface TransferResponse {
  error?: string | null
  from: Account
  to: Account
}
