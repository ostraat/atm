namespace Atm.Domain;

public sealed class Account
{
    public Guid Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public AccountKind Kind { get; set; }

    /// <summary>Cached balance in cents. Ledger amounts are the source of truth.</summary>
    public long Balance { get; set; }
}
