export function formatMoney(cents: number): string {
  const sign = cents < 0 ? '-' : ''
  const abs = Math.abs(cents)
  return `${sign}$${(abs / 100).toFixed(2)}`
}

/** Parse a dollars string like 12.34 into positive cents. */
export function parseDollarsToCents(value: string): number | null {
  const trimmed = value.trim()
  if (!/^\d+(\.\d{0,2})?$/.test(trimmed)) return null
  const [dollars, fraction = ''] = trimmed.split('.')
  const cents = Number(dollars) * 100 + Number((fraction + '00').slice(0, 2))
  if (!Number.isFinite(cents) || cents <= 0) return null
  return cents
}
