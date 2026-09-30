using Atm.Domain;

namespace Atm.Domain.Tests;

internal sealed class FakeAtmRepository : IAtmRepository
{
    private readonly object _sync = new();
    private readonly Dictionary<Guid, Account> _accounts = new();
    private readonly List<LedgerEntry> _entries = new();
    private readonly Dictionary<string, IdempotencyRecord> _idempotency = new();
    private bool _initialized;

    /// <summary>
    /// When set, ApplyTransferOnceAsync blocks after an initial miss until all
    /// participants arrive, so the atomic re-check under the lock is exercised.
    /// </summary>
    public Barrier? SynchronizeAfterIdempotencyMiss { get; set; }

    public Guid AtmId { get; } = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public Guid EquityId { get; } = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public Guid CheckingId { get; } = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public Guid SavingsId { get; } = Guid.Parse("44444444-4444-4444-4444-444444444444");

    public Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            if (_initialized)
            {
                return Task.CompletedTask;
            }

            _accounts[AtmId] = new Account { Id = AtmId, Label = "ATM", Kind = AccountKind.Atm, Balance = 0 };
            _accounts[EquityId] = new Account { Id = EquityId, Label = "Equity", Kind = AccountKind.Equity, Balance = 0 };
            _accounts[CheckingId] = new Account { Id = CheckingId, Label = "Checking", Kind = AccountKind.Customer, Balance = 0 };
            _accounts[SavingsId] = new Account { Id = SavingsId, Label = "Savings", Kind = AccountKind.Customer, Balance = 0 };

            ApplyTransferUnlocked(
                new TransferMutation
                {
                    TransactionId = Guid.NewGuid(),
                    FromAccountId = EquityId,
                    ToAccountId = CheckingId,
                    Amount = 10_000,
                    FromLabel = "Opening balance",
                    ToLabel = "Opening balance",
                    CreatedAt = DateTimeOffset.UtcNow
                });

            ApplyTransferUnlocked(
                new TransferMutation
                {
                    TransactionId = Guid.NewGuid(),
                    FromAccountId = EquityId,
                    ToAccountId = SavingsId,
                    Amount = 50_000,
                    FromLabel = "Opening balance",
                    ToLabel = "Opening balance",
                    CreatedAt = DateTimeOffset.UtcNow
                });

            _initialized = true;
            return Task.CompletedTask;
        }
    }

    public Task<IReadOnlyList<Account>> GetCustomerAccountsAsync(CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            return Task.FromResult<IReadOnlyList<Account>>(
                _accounts.Values.Where(a => a.Kind == AccountKind.Customer).OrderBy(a => a.Label).Select(Clone).ToList());
        }
    }

    public Task<Account?> GetAccountAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            return Task.FromResult(_accounts.TryGetValue(accountId, out var a) ? Clone(a) : null);
        }
    }

    public Task<Account?> GetSystemAccountAsync(AccountKind kind, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            return Task.FromResult(_accounts.Values.Where(a => a.Kind == kind).Select(Clone).FirstOrDefault());
        }
    }

    public Task<IReadOnlyList<LedgerEntry>> GetTransactionsAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            return Task.FromResult<IReadOnlyList<LedgerEntry>>(
                _entries.Where(e => e.AccountId == accountId)
                    .OrderByDescending(e => e.CreatedAt)
                    .ThenByDescending(e => e.Id)
                    .Select(Clone)
                    .ToList());
        }
    }

    public Task<LedgerEntry?> GetLatestTransactionAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            return Task.FromResult(
                _entries.Where(e => e.AccountId == accountId)
                    .OrderByDescending(e => e.CreatedAt)
                    .ThenByDescending(e => e.Id)
                    .Select(Clone)
                    .FirstOrDefault());
        }
    }

    public Task<IdempotencyRecord?> FindIdempotencyAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            return Task.FromResult(_idempotency.TryGetValue(key, out var r) ? r : null);
        }
    }

    public Task SaveIdempotencyAsync(
        IdempotencyRecord record,
        CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            if (!_idempotency.TryAdd(record.Key, record))
            {
                throw new InvalidOperationException($"Idempotency key '{record.Key}' already exists.");
            }
        }

        return Task.CompletedTask;
    }

    public Task<TransferApplyResult> ApplyTransferAsync(
        TransferMutation mutation,
        CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            return Task.FromResult(ApplyTransferUnlocked(mutation));
        }
    }

    public Task<IdempotentTransferResult> ApplyTransferOnceAsync(
        string idempotencyKey,
        string requestHash,
        TransferMutation mutation,
        Func<TransferApplyResult, string> serializeResponse,
        CancellationToken cancellationToken = default)
    {
        IdempotencyRecord? peeked;
        lock (_sync)
        {
            peeked = _idempotency.TryGetValue(idempotencyKey, out var r) ? r : null;
        }

        if (peeked is null && SynchronizeAfterIdempotencyMiss is not null)
        {
            SynchronizeAfterIdempotencyMiss.SignalAndWait(cancellationToken);
        }

        lock (_sync)
        {
            if (_idempotency.TryGetValue(idempotencyKey, out var existing))
            {
                EnsureMatchingHash(existing, requestHash);
                return Task.FromResult(new IdempotentTransferResult
                {
                    ResponseBody = existing.ResponseBody,
                    Replayed = true
                });
            }

            var applied = ApplyTransferUnlocked(mutation);
            var body = serializeResponse(applied);
            _idempotency.Add(idempotencyKey, new IdempotencyRecord
            {
                Key = idempotencyKey,
                RequestHash = requestHash,
                StatusCode = 200,
                ResponseBody = body,
                CreatedAt = DateTimeOffset.UtcNow
            });

            return Task.FromResult(new IdempotentTransferResult
            {
                ResponseBody = body,
                Replayed = false
            });
        }
    }

    public IReadOnlyList<LedgerEntry> AllEntries
    {
        get
        {
            lock (_sync)
            {
                return _entries.Select(Clone).ToList();
            }
        }
    }

    private static void EnsureMatchingHash(IdempotencyRecord existing, string requestHash)
    {
        if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
        {
            throw new ConflictException(
                "Idempotency-Key was already used with a different request.");
        }
    }

    private TransferApplyResult ApplyTransferUnlocked(TransferMutation mutation)
    {
        if (!_accounts.TryGetValue(mutation.FromAccountId, out var from) ||
            !_accounts.TryGetValue(mutation.ToAccountId, out var to))
        {
            throw new NotFoundException("Account not found.");
        }

        if (mutation.Amount <= 0)
        {
            throw new ValidationException("Amount must be positive.");
        }

        var fromEntry = new LedgerEntry
        {
            Id = Guid.NewGuid(),
            TransactionId = mutation.TransactionId,
            AccountId = from.Id,
            CounterpartyAccountId = to.Id,
            Amount = -mutation.Amount,
            Label = mutation.FromLabel,
            CreatedAt = mutation.CreatedAt
        };

        var toEntry = new LedgerEntry
        {
            Id = Guid.NewGuid(),
            TransactionId = mutation.TransactionId,
            AccountId = to.Id,
            CounterpartyAccountId = from.Id,
            Amount = mutation.Amount,
            Label = mutation.ToLabel,
            CreatedAt = mutation.CreatedAt
        };

        if (fromEntry.Amount + toEntry.Amount != 0)
        {
            throw new InvalidOperationException("Ledger legs must sum to zero.");
        }

        from.Balance += fromEntry.Amount;
        to.Balance += toEntry.Amount;
        _entries.Add(fromEntry);
        _entries.Add(toEntry);

        return new TransferApplyResult
        {
            FromAccount = Clone(from),
            ToAccount = Clone(to),
            FromEntry = Clone(fromEntry),
            ToEntry = Clone(toEntry)
        };
    }

    private static Account Clone(Account a) => new()
    {
        Id = a.Id,
        Label = a.Label,
        Kind = a.Kind,
        Balance = a.Balance
    };

    private static LedgerEntry Clone(LedgerEntry e) => new()
    {
        Id = e.Id,
        TransactionId = e.TransactionId,
        AccountId = e.AccountId,
        CounterpartyAccountId = e.CounterpartyAccountId,
        Amount = e.Amount,
        Label = e.Label,
        CreatedAt = e.CreatedAt
    };
}
