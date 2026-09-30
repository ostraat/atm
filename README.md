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
- Users / authentication
- Multi-currency (pending requirements)
- Transaction filters: date range, offset or cursor pagination
- Pending vs cleared ledger entries / cleared balance
- Stronger audit trail (see docs)
- Low-cash ATM alerts (needs a correct vault cash model)
- Withdraw by selecting bill counts for each denomination
- Distributed flow where individual steps can fail (outbox, retries)
- Swap SQLite → MySQL/Postgres via EF provider
- EF raw SQL for profiled hotspots
