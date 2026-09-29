using Atm.Domain;
using Microsoft.EntityFrameworkCore;

namespace Atm.Infrastructure;

public sealed class EfAtmRepository : IAtmRepository
{
    public static readonly Guid AtmAccountId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid EquityAccountId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid CheckingAccountId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid SavingsAccountId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private readonly AtmDbContext _db;

    public EfAtmRepository(AtmDbContext db)
    {
        _db = db;
    }

    public async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        await _db.Database.EnsureCreatedAsync(cancellationToken);

        if (await _db.Accounts.AnyAsync(cancellationToken))
        {
            return;
        }

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        _db.Accounts.AddRange(
            new Account { Id = AtmAccountId, Label = "ATM", Kind = AccountKind.Atm, Balance = 0 },
            new Account { Id = EquityAccountId, Label = "Equity", Kind = AccountKind.Equity, Balance = 0 },
            new Account { Id = CheckingAccountId, Label = "Checking", Kind = AccountKind.Customer, Balance = 0 },
            new Account { Id = SavingsAccountId, Label = "Savings", Kind = AccountKind.Customer, Balance = 0 });

        await _db.SaveChangesAsync(cancellationToken);

        await ApplyTransferCoreAsync(
            new TransferMutation
            {
                TransactionId = Guid.NewGuid(),
                FromAccountId = EquityAccountId,
                ToAccountId = CheckingAccountId,
                Amount = 10_000,
                FromLabel = "Opening balance",
                ToLabel = "Opening balance",
                CreatedAt = DateTimeOffset.UtcNow
            },
            cancellationToken);

        await ApplyTransferCoreAsync(
            new TransferMutation
            {
                TransactionId = Guid.NewGuid(),
                FromAccountId = EquityAccountId,
                ToAccountId = SavingsAccountId,
                Amount = 50_000,
                FromLabel = "Opening balance",
                ToLabel = "Opening balance",
                CreatedAt = DateTimeOffset.UtcNow
            },
            cancellationToken);

        await tx.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Account>> GetCustomerAccountsAsync(
        CancellationToken cancellationToken = default) =>
        await _db.Accounts
            .AsNoTracking()
            .Where(a => a.Kind == AccountKind.Customer)
            .OrderBy(a => a.Label)
            .ToListAsync(cancellationToken);

    public Task<Account?> GetAccountAsync(Guid accountId, CancellationToken cancellationToken = default) =>
        _db.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);

    public Task<Account?> GetSystemAccountAsync(
        AccountKind kind,
        CancellationToken cancellationToken = default) =>
        _db.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.Kind == kind, cancellationToken);

    public async Task<IReadOnlyList<LedgerEntry>> GetTransactionsAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        var entries = await _db.LedgerEntries
            .AsNoTracking()
            .Where(e => e.AccountId == accountId)
            .ToListAsync(cancellationToken);

        return entries
            .OrderByDescending(e => e.CreatedAt)
            .ThenByDescending(e => e.Id)
            .ToList();
    }

    public async Task<LedgerEntry?> GetLatestTransactionAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        var entries = await _db.LedgerEntries
            .AsNoTracking()
            .Where(e => e.AccountId == accountId)
            .ToListAsync(cancellationToken);

        return entries
            .OrderByDescending(e => e.CreatedAt)
            .ThenByDescending(e => e.Id)
            .FirstOrDefault();
    }

    public Task<IdempotencyRecord?> FindIdempotencyAsync(
        string key,
        CancellationToken cancellationToken = default) =>
        _db.IdempotencyRecords.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Key == key, cancellationToken);

    public async Task SaveIdempotencyAsync(
        IdempotencyRecord record,
        CancellationToken cancellationToken = default)
    {
        _db.IdempotencyRecords.Add(record);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<TransferApplyResult> ApplyTransferAsync(
        TransferMutation mutation,
        CancellationToken cancellationToken = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        var result = await ApplyTransferCoreAsync(mutation, cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return result;
    }

    private async Task<TransferApplyResult> ApplyTransferCoreAsync(
        TransferMutation mutation,
        CancellationToken cancellationToken)
    {
        var from = await _db.Accounts.FirstOrDefaultAsync(a => a.Id == mutation.FromAccountId, cancellationToken)
            ?? throw new NotFoundException("Account not found.");
        var to = await _db.Accounts.FirstOrDefaultAsync(a => a.Id == mutation.ToAccountId, cancellationToken)
            ?? throw new NotFoundException("Account not found.");

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

        _db.LedgerEntries.Add(fromEntry);
        _db.LedgerEntries.Add(toEntry);
        await _db.SaveChangesAsync(cancellationToken);

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
