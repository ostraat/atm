using Atm.Domain;

namespace Atm.Domain.Tests;

public class AtmServiceTests
{
    private static async Task<(AtmService Service, FakeAtmRepository Repo)> CreateAsync()
    {
        var repo = new FakeAtmRepository();
        var service = new AtmService(repo);
        await service.EnsureInitializedAsync();
        return (service, repo);
    }

    [Fact]
    public async Task Opening_balances_come_from_equity_and_legs_sum_to_zero()
    {
        var (_, repo) = await CreateAsync();
        AssertAllTransactionsBalance(repo);

        var checking = await repo.GetAccountAsync(repo.CheckingId);
        var savings = await repo.GetAccountAsync(repo.SavingsId);
        Assert.Equal(10_000, checking!.Balance);
        Assert.Equal(50_000, savings!.Balance);
    }

    [Fact]
    public async Task Deposit_credits_customer_and_labels_from_atm()
    {
        var (service, repo) = await CreateAsync();
        var result = await service.DepositAsync(repo.CheckingId, 500, "dep-1");

        Assert.Equal(10_500, result.Account.Balance);
        Assert.Equal(500, result.Account.LatestTransaction!.Amount);
        Assert.Equal("Deposit from ATM", result.Account.LatestTransaction.Label);
        AssertAllTransactionsBalance(repo);
    }

    [Fact]
    public async Task Withdraw_debits_customer()
    {
        var (service, repo) = await CreateAsync();
        var result = await service.WithdrawAsync(repo.CheckingId, 2_000, "wd-1");

        Assert.Equal(8_000, result.Account.Balance);
        Assert.Equal(-2_000, result.Account.LatestTransaction!.Amount);
        Assert.Equal("Withdraw to ATM", result.Account.LatestTransaction.Label);
        AssertAllTransactionsBalance(repo);
    }

    [Fact]
    public async Task Withdraw_insufficient_funds()
    {
        var (service, repo) = await CreateAsync();
        await Assert.ThrowsAsync<InsufficientFundsException>(
            () => service.WithdrawAsync(repo.CheckingId, 999_999, "wd-fail"));
    }

    [Fact]
    public async Task Transfer_uses_from_and_to_labels()
    {
        var (service, repo) = await CreateAsync();
        var result = await service.TransferAsync(repo.CheckingId, repo.SavingsId, 1_000, "tr-1");

        Assert.Equal(9_000, result.From.Balance);
        Assert.Equal(51_000, result.To.Balance);

        var checkingTx = await service.GetTransactionsAsync(repo.CheckingId);
        var savingsTx = await service.GetTransactionsAsync(repo.SavingsId);
        Assert.Contains(checkingTx, t => t.Label == "Transfer to Savings" && t.Amount == -1_000);
        Assert.Contains(savingsTx, t => t.Label == "Transfer from Checking" && t.Amount == 1_000);
        AssertAllTransactionsBalance(repo);
    }

    [Fact]
    public async Task Transfer_same_account_rejected()
    {
        var (service, repo) = await CreateAsync();
        await Assert.ThrowsAsync<ValidationException>(
            () => service.TransferAsync(repo.CheckingId, repo.CheckingId, 100, "tr-same"));
    }

    [Fact]
    public async Task Idempotent_deposit_replays_without_second_leg()
    {
        var (service, repo) = await CreateAsync();
        var first = await service.DepositAsync(repo.CheckingId, 100, "same-key");
        var second = await service.DepositAsync(repo.CheckingId, 100, "same-key");

        Assert.Equal(first.Account.Balance, second.Account.Balance);
        Assert.Equal(10_100, first.Account.Balance);
        var depositLegs = repo.AllEntries.Count(e => e.Label == "Deposit from ATM" && e.Amount == 100);
        Assert.Equal(1, depositLegs);
        AssertAllTransactionsBalance(repo);
    }

    [Fact]
    public async Task Idempotency_conflict_on_different_body()
    {
        var (service, repo) = await CreateAsync();
        await service.DepositAsync(repo.CheckingId, 100, "key-x");
        await Assert.ThrowsAsync<ConflictException>(
            () => service.DepositAsync(repo.CheckingId, 200, "key-x"));
    }

    /// <summary>
    /// Concurrent requests with the same Idempotency-Key must apply once and
    /// replay. ASP.NET Core runs concurrent requests on separate scopes; the
    /// barrier forces both past the initial miss so the atomic re-check is hit.
    /// </summary>
    [Fact]
    public async Task Concurrent_identical_deposits_must_not_double_apply()
    {
        var repo = new FakeAtmRepository
        {
            SynchronizeAfterIdempotencyMiss = new Barrier(2)
        };
        var service = new AtmService(repo);
        await service.EnsureInitializedAsync();

        const string key = "race-key";
        // Task.Run so both callers can block in the miss barrier
        // (a sync Barrier would otherwise deadlock on one thread).
        var first = Task.Run(() => service.DepositAsync(repo.CheckingId, 100, key));
        var second = Task.Run(() => service.DepositAsync(repo.CheckingId, 100, key));

        var results = await Task.WhenAll(first, second);

        Assert.Equal(results[0].Account.Balance, results[1].Account.Balance);
        Assert.Equal(10_100, results[0].Account.Balance);

        var depositLegs = repo.AllEntries.Count(
            e => e.Label == "Deposit from ATM" && e.Amount == 100);
        Assert.Equal(1, depositLegs);

        var checking = await repo.GetAccountAsync(repo.CheckingId);
        Assert.Equal(10_100, checking!.Balance);
    }

    private static void AssertAllTransactionsBalance(FakeAtmRepository repo)
    {
        var groups = repo.AllEntries.GroupBy(e => e.TransactionId);
        foreach (var group in groups)
        {
            Assert.Equal(0, group.Sum(e => e.Amount));
            Assert.Equal(2, group.Count());
        }
    }
}
