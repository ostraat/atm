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
  to: Account
}

export interface WithdrawResponse {
  error?: string | null
  from: Account
}

export interface TransferResponse {
  error?: string | null
  from: Account
  to: Account
}
