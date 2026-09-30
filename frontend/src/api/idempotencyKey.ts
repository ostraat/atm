/**
 * Idempotency-Key UUID. Prefer crypto.randomUUID when available; fall back to
 * getRandomValues for non-secure contexts (e.g. http://LAN-IP on mobile Brave).
 */
export function newIdempotencyKey(): string {
  const c = globalThis.crypto
  if (typeof c?.randomUUID === 'function') {
    return c.randomUUID()
  }
  if (typeof c?.getRandomValues !== 'function') {
    throw new Error('Web Crypto API unavailable')
  }
  const bytes = new Uint8Array(16)
  c.getRandomValues(bytes)
  bytes[6] = (bytes[6] & 0x0f) | 0x40
  bytes[8] = (bytes[8] & 0x3f) | 0x80
  const hex = Array.from(bytes, (b) => b.toString(16).padStart(2, '0')).join('')
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`
}
