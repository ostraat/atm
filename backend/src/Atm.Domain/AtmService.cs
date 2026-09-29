using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Atm.Domain;

public sealed class AtmService
{
    private readonly IAtmRepository _repository;

    public AtmService(IAtmRepository repository)
    {
        _repository = repository;
    }

    public Task EnsureInitializedAsync(CancellationToken cancellationToken = default) =>
        _repository.EnsureInitializedAsync(cancellationToken);

    public async Task<IReadOnlyList<AccountSummary>> GetAccountsAsync(
        CancellationToken cancellationToken = default)
    {
        var accounts = await _repository.GetCustomerAccountsAsync(cancellationToken);
        var result = new List<AccountSummary>(accounts.Count);
        foreach (var account in accounts)
        {
            var latest = await _repository.GetLatestTransactionAsync(account.Id, cancellationToken);
            result.Add(ToSummary(account, latest));
        }

        return result;
    }

    public async Task<AccountSummary> GetAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        var account = await RequireCustomerAccountAsync(accountId, cancellationToken);
        var latest = await _repository.GetLatestTransactionAsync(account.Id, cancellationToken);
        return ToSummary(account, latest);
    }

    public async Task<IReadOnlyList<TransactionView>> GetTransactionsAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        await RequireCustomerAccountAsync(accountId, cancellationToken);
        var entries = await _repository.GetTransactionsAsync(accountId, cancellationToken);
        return entries.Select(ToTransaction).ToList();
    }

    public async Task<MoneyMovementResult> DepositAsync(
        Guid accountId,
        long amount,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidatePositiveAmount(amount);
        ValidateIdempotencyKey(idempotencyKey);

        var requestHash = HashRequest("deposit", accountId, null, amount);
        var cached = await TryReplayAsync(idempotencyKey, requestHash, cancellationToken);
        if (cached is not null)
        {
            return DeserializeMoney(cached);
        }

        var customer = await RequireCustomerAccountAsync(accountId, cancellationToken);
        var atm = await RequireSystemAccountAsync(AccountKind.Atm, cancellationToken);

        var applied = await _repository.ApplyTransferAsync(
            new TransferMutation
            {
                TransactionId = Guid.NewGuid(),
                FromAccountId = atm.Id,
                ToAccountId = customer.Id,
                Amount = amount,
                FromLabel = $"Deposit from {atm.Label}",
                ToLabel = $"Deposit from {atm.Label}",
                CreatedAt = DateTimeOffset.UtcNow
            },
            cancellationToken);

        // Customer receives credit: ToEntry is the customer leg.
        var result = new MoneyMovementResult
        {
            Account = ToSummary(applied.ToAccount, applied.ToEntry),
            Transaction = ToTransaction(applied.ToEntry)
        };

        await StoreIdempotencyAsync(idempotencyKey, requestHash, result, cancellationToken);
        return result;
    }

    public async Task<MoneyMovementResult> WithdrawAsync(
        Guid accountId,
        long amount,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidatePositiveAmount(amount);
        ValidateIdempotencyKey(idempotencyKey);

        var requestHash = HashRequest("withdraw", accountId, null, amount);
        var cached = await TryReplayAsync(idempotencyKey, requestHash, cancellationToken);
        if (cached is not null)
        {
            return DeserializeMoney(cached);
        }

        var customer = await RequireCustomerAccountAsync(accountId, cancellationToken);
        if (customer.Balance < amount)
        {
            throw new InsufficientFundsException("Insufficient funds.");
        }

        var atm = await RequireSystemAccountAsync(AccountKind.Atm, cancellationToken);

        var applied = await _repository.ApplyTransferAsync(
            new TransferMutation
            {
                TransactionId = Guid.NewGuid(),
                FromAccountId = customer.Id,
                ToAccountId = atm.Id,
                Amount = amount,
                FromLabel = $"Withdraw to {atm.Label}",
                ToLabel = $"Withdraw to {atm.Label}",
                CreatedAt = DateTimeOffset.UtcNow
            },
            cancellationToken);

        var result = new MoneyMovementResult
        {
            Account = ToSummary(applied.FromAccount, applied.FromEntry),
            Transaction = ToTransaction(applied.FromEntry)
        };

        await StoreIdempotencyAsync(idempotencyKey, requestHash, result, cancellationToken);
        return result;
    }

    public async Task<TransferResult> TransferAsync(
        Guid fromAccountId,
        Guid toAccountId,
        long amount,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidatePositiveAmount(amount);
        ValidateIdempotencyKey(idempotencyKey);

        if (fromAccountId == toAccountId)
        {
            throw new ValidationException("From and to accounts must be different.");
        }

        var requestHash = HashRequest("transfer", fromAccountId, toAccountId, amount);
        var cached = await TryReplayAsync(idempotencyKey, requestHash, cancellationToken);
        if (cached is not null)
        {
            return DeserializeTransfer(cached);
        }

        var from = await RequireCustomerAccountAsync(fromAccountId, cancellationToken);
        var to = await RequireCustomerAccountAsync(toAccountId, cancellationToken);

        if (from.Balance < amount)
        {
            throw new InsufficientFundsException("Insufficient funds.");
        }

        var applied = await _repository.ApplyTransferAsync(
            new TransferMutation
            {
                TransactionId = Guid.NewGuid(),
                FromAccountId = from.Id,
                ToAccountId = to.Id,
                Amount = amount,
                FromLabel = $"Transfer to {to.Label}",
                ToLabel = $"Transfer from {from.Label}",
                CreatedAt = DateTimeOffset.UtcNow
            },
            cancellationToken);

        var result = new TransferResult
        {
            From = ToSummary(applied.FromAccount, applied.FromEntry),
            To = ToSummary(applied.ToAccount, applied.ToEntry)
        };

        await StoreIdempotencyAsync(idempotencyKey, requestHash, result, cancellationToken);
        return result;
    }

    private async Task<Account> RequireCustomerAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        var account = await _repository.GetAccountAsync(accountId, cancellationToken)
            ?? throw new NotFoundException("Account not found.");

        if (account.Kind != AccountKind.Customer)
        {
            throw new NotFoundException("Account not found.");
        }

        return account;
    }

    private async Task<Account> RequireSystemAccountAsync(
        AccountKind kind,
        CancellationToken cancellationToken)
    {
        return await _repository.GetSystemAccountAsync(kind, cancellationToken)
            ?? throw new ValidationException($"System account {kind} is not configured.");
    }

    private static void ValidatePositiveAmount(long amount)
    {
        if (amount <= 0)
        {
            throw new ValidationException("Amount must be a positive integer (cents).");
        }
    }

    private static void ValidateIdempotencyKey(string idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ValidationException("Idempotency-Key header is required.");
        }
    }

    private async Task<string?> TryReplayAsync(
        string key,
        string requestHash,
        CancellationToken cancellationToken)
    {
        var existing = await _repository.FindIdempotencyAsync(key, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
        {
            throw new ConflictException(
                "Idempotency-Key was already used with a different request.");
        }

        return existing.ResponseBody;
    }

    private async Task StoreIdempotencyAsync<T>(
        string key,
        string requestHash,
        T payload,
        CancellationToken cancellationToken)
    {
        var body = JsonSerializer.Serialize(payload);
        await _repository.SaveIdempotencyAsync(
            new IdempotencyRecord
            {
                Key = key,
                RequestHash = requestHash,
                StatusCode = 200,
                ResponseBody = body,
                CreatedAt = DateTimeOffset.UtcNow
            },
            cancellationToken);
    }

    private static MoneyMovementResult DeserializeMoney(string body) =>
        JsonSerializer.Deserialize<MoneyMovementResult>(body)
        ?? throw new InvalidOperationException("Corrupt idempotency payload.");

    private static TransferResult DeserializeTransfer(string body) =>
        JsonSerializer.Deserialize<TransferResult>(body)
        ?? throw new InvalidOperationException("Corrupt idempotency payload.");

    private static string HashRequest(string op, Guid a, Guid? b, long amount)
    {
        var raw = $"{op}:{a}:{b}:{amount}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash);
    }

    private static AccountSummary ToSummary(Account account, LedgerEntry? latest) =>
        new()
        {
            Id = account.Id,
            Label = account.Label,
            Balance = account.Balance,
            LatestTransaction = latest is null ? null : ToTransaction(latest)
        };

    private static TransactionView ToTransaction(LedgerEntry entry) =>
        new()
        {
            Label = entry.Label,
            Amount = entry.Amount,
            CreatedAt = entry.CreatedAt
        };
}

public sealed class AccountSummary
{
    public Guid Id { get; init; }
    public string Label { get; init; } = string.Empty;
    public long Balance { get; init; }
    public TransactionView? LatestTransaction { get; init; }
}

public sealed class TransactionView
{
    public string Label { get; init; } = string.Empty;
    public long Amount { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class MoneyMovementResult
{
    public required AccountSummary Account { get; init; }
    public required TransactionView Transaction { get; init; }
}

public sealed class TransferResult
{
    public required AccountSummary From { get; init; }
    public required AccountSummary To { get; init; }
}
