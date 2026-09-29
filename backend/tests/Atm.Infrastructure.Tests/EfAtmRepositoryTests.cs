using Atm.Domain;
using Atm.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Atm.Infrastructure.Tests;

public class EfAtmRepositoryTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private AtmDbContext _db = null!;
    private EfAtmRepository _repo = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AtmDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new AtmDbContext(options);
        _repo = new EfAtmRepository(_db);
        await _repo.EnsureInitializedAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task Init_seeds_from_equity_once()
    {
        var checking = await _repo.GetAccountAsync(EfAtmRepository.CheckingAccountId);
        Assert.Equal(10_000, checking!.Balance);

        await _repo.EnsureInitializedAsync();
        var again = await _repo.GetAccountAsync(EfAtmRepository.CheckingAccountId);
        Assert.Equal(10_000, again!.Balance);

        var legs = await _db.LedgerEntries.AsNoTracking().ToListAsync();
        foreach (var group in legs.GroupBy(e => e.TransactionId))
        {
            Assert.Equal(0, group.Sum(e => e.Amount));
        }
    }

    [Fact]
    public async Task ApplyTransfer_updates_cached_balances_and_balances_legs()
    {
        var result = await _repo.ApplyTransferAsync(new TransferMutation
        {
            TransactionId = Guid.NewGuid(),
            FromAccountId = EfAtmRepository.CheckingAccountId,
            ToAccountId = EfAtmRepository.SavingsAccountId,
            Amount = 500,
            FromLabel = "Transfer to Savings",
            ToLabel = "Transfer from Checking",
            CreatedAt = DateTimeOffset.UtcNow
        });

        Assert.Equal(9_500, result.FromAccount.Balance);
        Assert.Equal(50_500, result.ToAccount.Balance);
        Assert.Equal(0, result.FromEntry.Amount + result.ToEntry.Amount);
    }

    [Fact]
    public async Task Idempotency_key_is_unique()
    {
        await _repo.SaveIdempotencyAsync(new IdempotencyRecord
        {
            Key = "k1",
            RequestHash = "h",
            StatusCode = 200,
            ResponseBody = "{}",
            CreatedAt = DateTimeOffset.UtcNow
        });

        _db.ChangeTracker.Clear();

        await Assert.ThrowsAsync<DbUpdateException>(() => _repo.SaveIdempotencyAsync(new IdempotencyRecord
        {
            Key = "k1",
            RequestHash = "h2",
            StatusCode = 200,
            ResponseBody = "{}",
            CreatedAt = DateTimeOffset.UtcNow
        }));
    }
}
