using System.ComponentModel.DataAnnotations;
using Atm.Domain;

namespace Atm.Api.Models;

/// <summary>Customer account snapshot returned by the API.</summary>
public sealed class AccountDto
{
    /// <summary>Account identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Display name (e.g. Checking, Savings).</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>Available balance in integer cents (e.g. 10000 = $100.00).</summary>
    public long Balance { get; init; }

    /// <summary>Most recent ledger leg for this account, if any.</summary>
    public TransactionDto? LatestTransaction { get; init; }

    public static AccountDto From(AccountSummary summary) => new()
    {
        Id = summary.Id,
        Label = summary.Label,
        Balance = summary.Balance,
        LatestTransaction = summary.LatestTransaction is null
            ? null
            : TransactionDto.From(summary.LatestTransaction)
    };
}

/// <summary>One signed ledger leg for a customer account.</summary>
public sealed class TransactionDto
{
    /// <summary>Human-readable description of the movement.</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>Signed amount in cents (positive = credit, negative = debit).</summary>
    public long Amount { get; init; }

    /// <summary>When the ledger entry was created (UTC).</summary>
    public DateTimeOffset CreatedAt { get; init; }

    public static TransactionDto From(TransactionView view) => new()
    {
        Label = view.Label,
        Amount = view.Amount,
        CreatedAt = view.CreatedAt
    };
}

/// <summary>List of customer accounts.</summary>
public sealed class AccountsResponse
{
    /// <summary>Error message when present; null on success.</summary>
    public string? Error { get; init; }

    /// <summary>Customer accounts (Checking and Savings).</summary>
    public IReadOnlyList<AccountDto> Accounts { get; init; } = Array.Empty<AccountDto>();
}

/// <summary>Single customer account.</summary>
public sealed class AccountResponse
{
    /// <summary>Error message when present; null on success.</summary>
    public string? Error { get; init; }

    /// <summary>The requested account.</summary>
    public AccountDto? Account { get; init; }
}

/// <summary>Ledger history for one account.</summary>
public sealed class TransactionsResponse
{
    /// <summary>Error message when present; null on success.</summary>
    public string? Error { get; init; }

    /// <summary>Ledger legs for the account, newest first.</summary>
    public IReadOnlyList<TransactionDto> Transactions { get; init; } = Array.Empty<TransactionDto>();
}

/// <summary>Result of a successful deposit.</summary>
public sealed class DepositResponse
{
    /// <summary>Error message when present; null on success.</summary>
    public string? Error { get; init; }

    /// <summary>Credited customer account, including updated balance and latestTransaction.</summary>
    public AccountDto? To { get; init; }
}

/// <summary>Result of a successful withdrawal.</summary>
public sealed class WithdrawResponse
{
    /// <summary>Error message when present; null on success.</summary>
    public string? Error { get; init; }

    /// <summary>Debited customer account, including updated balance and latestTransaction.</summary>
    public AccountDto? From { get; init; }
}

/// <summary>Result of a successful transfer between customer accounts.</summary>
public sealed class TransferResponse
{
    /// <summary>Error message when present; null on success.</summary>
    public string? Error { get; init; }

    /// <summary>Source account after the transfer.</summary>
    public AccountDto? From { get; init; }

    /// <summary>Destination account after the transfer.</summary>
    public AccountDto? To { get; init; }
}

/// <summary>Deposit cash from the ATM vault into a customer account.</summary>
public sealed class DepositRequest
{
    /// <summary>Customer account to credit.</summary>
    public Guid To { get; init; }

    /// <summary>Positive amount in integer cents.</summary>
    [Range(1, long.MaxValue)]
    public long Amount { get; init; }
}

/// <summary>Withdraw cash from a customer account to the ATM vault.</summary>
public sealed class WithdrawRequest
{
    /// <summary>Customer account to debit.</summary>
    public Guid From { get; init; }

    /// <summary>Positive amount in integer cents.</summary>
    [Range(1, long.MaxValue)]
    public long Amount { get; init; }
}

/// <summary>Move funds between two customer accounts.</summary>
public sealed class TransferRequest
{
    /// <summary>Customer account to debit.</summary>
    public Guid From { get; init; }

    /// <summary>Customer account to credit (must differ from From).</summary>
    public Guid To { get; init; }

    /// <summary>Positive amount in integer cents.</summary>
    [Range(1, long.MaxValue)]
    public long Amount { get; init; }
}

/// <summary>Liveness probe payload.</summary>
public sealed class HealthResponse
{
    /// <summary>Service status string; typically "ok".</summary>
    public string Status { get; init; } = "ok";
}

/// <summary>
/// RFC 7807-style error body returned for domain failures
/// (<c>application/problem+json</c>).
/// </summary>
public sealed class ProblemDetailsResponse
{
    /// <summary>Problem type URI; this API uses <c>about:blank</c>.</summary>
    public string Type { get; init; } = "about:blank";

    /// <summary>Short title matching the HTTP status (e.g. Bad Request, Not Found).</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>HTTP status code.</summary>
    public int Status { get; init; }

    /// <summary>Human-readable explanation (e.g. "Insufficient funds.").</summary>
    public string Detail { get; init; } = string.Empty;
}
