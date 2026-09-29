namespace Atm.Domain;

public sealed class IdempotencyRecord
{
    public string Key { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public string ResponseBody { get; set; } = string.Empty;

    /// <summary>UTC timestamp.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
