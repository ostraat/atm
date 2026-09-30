namespace Atm.Domain;

public sealed class TransferMutation
{
    public required Guid TransactionId { get; init; }
    public required Guid FromAccountId { get; init; }
    public required Guid ToAccountId { get; init; }

    /// <summary>Positive amount in cents moved from From to To.</summary>
    public required long Amount { get; init; }

    public required string FromLabel { get; init; }
    public required string ToLabel { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}

public sealed class TransferApplyResult
{
    public required Account FromAccount { get; init; }
    public required Account ToAccount { get; init; }
    public required LedgerEntry FromEntry { get; init; }
    public required LedgerEntry ToEntry { get; init; }
}

/// <summary>
/// Result of an idempotent transfer: either a fresh apply or a replayed body.
/// </summary>
public sealed class IdempotentTransferResult
{
    public required string ResponseBody { get; init; }
    public required bool Replayed { get; init; }
}

public interface IAtmRepository
{
    Task EnsureInitializedAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Account>> GetCustomerAccountsAsync(CancellationToken cancellationToken = default);

    Task<Account?> GetAccountAsync(Guid accountId, CancellationToken cancellationToken = default);

    Task<Account?> GetSystemAccountAsync(AccountKind kind, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LedgerEntry>> GetTransactionsAsync(
        Guid accountId,
        CancellationToken cancellationToken = default);

    Task<LedgerEntry?> GetLatestTransactionAsync(
        Guid accountId,
        CancellationToken cancellationToken = default);

    Task<IdempotencyRecord?> FindIdempotencyAsync(
        string key,
        CancellationToken cancellationToken = default);

    Task SaveIdempotencyAsync(
        IdempotencyRecord record,
        CancellationToken cancellationToken = default);

    Task<TransferApplyResult> ApplyTransferAsync(
        TransferMutation mutation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically apply <paramref name="mutation"/> and store the idempotency
    /// record, or replay a prior success for the same key. Concurrent callers
    /// with the same key must not double-apply the ledger.
    /// </summary>
    Task<IdempotentTransferResult> ApplyTransferOnceAsync(
        string idempotencyKey,
        string requestHash,
        TransferMutation mutation,
        Func<TransferApplyResult, string> serializeResponse,
        CancellationToken cancellationToken = default);
}
