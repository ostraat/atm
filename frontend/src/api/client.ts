import type {
  AccountResponse,
  AccountsResponse,
  DepositResponse,
  TransactionsResponse,
  TransferResponse,
  WithdrawResponse
} from '../types/api'

const jsonHeaders = { 'Content-Type': 'application/json' }

async function readError(response: Response): Promise<string> {
  try {
    const body = await response.json()
    return body.detail ?? body.title ?? response.statusText
  } catch {
    return response.statusText
  }
}

export async function getAccounts(): Promise<AccountsResponse> {
  const response = await fetch('/api/v1/accounts')
  if (!response.ok) throw new Error(await readError(response))
  return response.json()
}

export async function getAccount(id: string): Promise<AccountResponse> {
  const response = await fetch(`/api/v1/accounts/${id}`)
  if (!response.ok) throw new Error(await readError(response))
  return response.json()
}

export async function getTransactions(id: string): Promise<TransactionsResponse> {
  const response = await fetch(`/api/v1/accounts/${id}/transactions`)
  if (!response.ok) throw new Error(await readError(response))
  return response.json()
}

export async function deposit(accountId: string, amount: number): Promise<DepositResponse> {
  const response = await fetch(`/api/v1/accounts/${accountId}/deposit`, {
    method: 'POST',
    headers: { ...jsonHeaders, 'Idempotency-Key': crypto.randomUUID() },
    body: JSON.stringify({ amount })
  })
  if (!response.ok) throw new Error(await readError(response))
  return response.json()
}

export async function withdraw(accountId: string, amount: number): Promise<WithdrawResponse> {
  const response = await fetch(`/api/v1/accounts/${accountId}/withdraw`, {
    method: 'POST',
    headers: { ...jsonHeaders, 'Idempotency-Key': crypto.randomUUID() },
    body: JSON.stringify({ amount })
  })
  if (!response.ok) throw new Error(await readError(response))
  return response.json()
}

export async function transfer(from: string, to: string, amount: number): Promise<TransferResponse> {
  const response = await fetch('/api/v1/transfer', {
    method: 'POST',
    headers: { ...jsonHeaders, 'Idempotency-Key': crypto.randomUUID() },
    body: JSON.stringify({ from, to, amount })
  })
  if (!response.ok) throw new Error(await readError(response))
  return response.json()
}
