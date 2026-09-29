using Atm.Domain;

namespace Atm.Domain.Tests;

internal sealed class FakeAtmRepository : IAtmRepository
{
    private readonly Dictionary<Guid, Account> _accounts = new();
    private readonly List<LedgerEntry> _entries = new();
    private readonly Dictionary<string, IdempotencyRecord> _idempotency = new();
    private bool _initialized;

    public Guid AtmId { get; } = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public Guid EquityId { get; } = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public Guid CheckingId { get; } = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public Guid SavingsId { get; } = Guid.Parse("44444444-4444-4444-4444-444444444444");

    public Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return Task.CompletedTask;
        }

        _accounts[AtmId] = new Account { Id = AtmId, Label = "ATM", Kind = AccountKind.Atm, Balance = 0 };
        _accounts[EquityId] = new Account { Id = EquityId, Label = "Equity", Kind = AccountKind.Equity, Balance = 0 };
        _accounts[CheckingId] = new Account { Id = CheckingId, Label = "Checking", Kind = AccountKind.Customer, Balance = 0 };
        _accounts[SavingsId] = new Account { Id = SavingsId, Label = "Savings", Kind = AccountKind.Customer, Balance = 0 };

        ApplyTransfer(
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

        ApplyTransfer(
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

    public Task<IReadOnlyList<Account>> GetCustomerAccountsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Account>>(
            _accounts.Values.Where(a => a.Kind == AccountKind.Customer).OrderBy(a => a.Label).ToList());

    public Task<Account?> GetAccountAsync(Guid accountId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_accounts.TryGetValue(accountId, out var a) ? Clone(a) : null);

    public Task<Account?> GetSystemAccountAsync(AccountKind kind, CancellationToken cancellationToken = default) =>
        Task.FromResult(_accounts.Values.Where(a => a.Kind == kind).Select(Clone).FirstOrDefault());

    public Task<IReadOnlyList<LedgerEntry>> GetTransactionsAsync(
        Guid accountId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<LedgerEntry>>(
            _entries.Where(e => e.AccountId == accountId)
                .OrderByDescending(e => e.CreatedAt)
                .ThenByDescending(e => e.Id)
                .Select(Clone)
                .ToList());

    public Task<LedgerEntry?> GetLatestTransactionAsync(
        Guid accountId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(
            _entries.Where(e => e.AccountId == accountId)
                .OrderByDescending(e => e.CreatedAt)
                .ThenByDescending(e => e.Id)
                .Select(Clone)
                .FirstOrDefault());

    public Task<IdempotencyRecord?> FindIdempotencyAsync(
        string key,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_idempotency.TryGetValue(key, out var r) ? r : null);

    public Task SaveIdempotencyAsync(
        IdempotencyRecord record,
        CancellationToken cancellationToken = default)
    {
        _idempotency[record.Key] = record;
        return Task.CompletedTask;
    }

    public Task<TransferApplyResult> ApplyTransferAsync(
        TransferMutation mutation,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(ApplyTransfer(mutation));

    public IReadOnlyList<LedgerEntry> AllEntries => _entries;

    private TransferApplyResult ApplyTransfer(TransferMutation mutation)
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
