/**
 * Regression: Idempotency-Key generation must work when crypto.randomUUID
 * is missing (non-secure contexts: http://LAN-IP on mobile browsers).
 *
 * Usage: node scripts/check-idempotency-key.mjs
 */
import { buildSync } from 'esbuild'
import { createRequire } from 'module'
import { mkdtempSync, writeFileSync, rmSync } from 'fs'
import { tmpdir } from 'os'
import { join } from 'path'
import { fileURLToPath } from 'url'

const root = join(fileURLToPath(import.meta.url), '../..')
const entry = join(root, 'src/api/idempotencyKey.ts')
const outDir = mkdtempSync(join(tmpdir(), 'atm-idem-'))
const outFile = join(outDir, 'idempotencyKey.cjs')

try {
  buildSync({
    entryPoints: [entry],
    outfile: outFile,
    bundle: true,
    format: 'cjs',
    platform: 'neutral'
  })

  const real = globalThis.crypto
  Object.defineProperty(globalThis, 'crypto', {
    value: {
      getRandomValues(arr) {
        return real.getRandomValues(arr)
      }
    },
    configurable: true,
    writable: true
  })

  if (typeof crypto.randomUUID === 'function') {
    console.error('FAIL: mock did not remove randomUUID')
    process.exit(1)
  }

  const require = createRequire(import.meta.url)
  const { newIdempotencyKey } = require(outFile)
  const key = newIdempotencyKey()
  const uuid =
    /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i

  if (!uuid.test(key)) {
    console.error('FAIL: not a UUID v4:', key)
    process.exit(1)
  }

  console.log('OK:', key)
} catch (e) {
  console.error('RED:', e.message)
  process.exit(1)
} finally {
  rmSync(outDir, { recursive: true, force: true })
}
