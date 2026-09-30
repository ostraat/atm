# ATM

Simple web ATM for managing two customer bank accounts (Checking and Savings): deposit, withdraw, and transfer.

## Quick start

```bash
docker compose up --build
```

- App UI: http://localhost:8080
- API (Swagger): http://localhost:8080/swagger
- Health: http://localhost:8080/api/health

Amounts in the API are **integer cents** (e.g. `10000` = $100.00).

## Development

See [docs/architecture.md](docs/architecture.md) for design notes.

```bash
# API unit/integration tests
docker compose --profile test run --rm api-test

# System smoke (app must be up)
./tests/system/smoke.sh

# Frontend only
cd frontend && npm ci && npm run dev
```

## Next steps

- Gather requirements to separate accounts / ATM / transfers (services and APIs) and use events per concern
- Consider GraphQL (or REST with a second per-account transactions request, count=1) so the account view can include the latest transaction in one client round-trip
- Standard error responses (`application/problem+json` / RFC 7807 Problem Details)
- Users / authentication
- Multi-currency (pending requirements)
- Transaction filters: date range, offset or cursor pagination
- Transaction timestamps: group by day; show relative time ("1 day ago", "2 hours ago"); hover for full timestamp
- Pending vs cleared ledger entries / cleared balance
- Stronger audit trail (see docs)
- Low-cash ATM alerts (needs a correct vault cash model)
- Withdraw by selecting bill counts for each denomination
- Withdraw UX: if amount exceeds account balance, gray out that account and show a red "Insufficient funds" warning
- Wizard UX: allow tapping a completed step to change that selection/value
- Transfer UX: gray out the "to" account that matches "from", or show both account picks on one page
- Distributed flow where individual steps can fail (outbox, retries)
- Swap SQLite → MySQL/Postgres via EF provider
- EF raw SQL for profiled hotspots
