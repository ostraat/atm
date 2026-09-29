namespace Atm.Domain;

/// <summary>
/// One double-entry leg. Amount is signed cents: positive = credit, negative = debit.
/// Legs that share <see cref="TransactionId"/> must sum to zero.
/// </summary>
public sealed class LedgerEntry
{
    public Guid Id { get; set; }
    public Guid TransactionId { get; set; }
    public Guid AccountId { get; set; }
    public Guid CounterpartyAccountId { get; set; }

    /// <summary>Signed amount in cents.</summary>
    public long Amount { get; set; }

    public string Label { get; set; } = string.Empty;

    /// <summary>UTC timestamp.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
