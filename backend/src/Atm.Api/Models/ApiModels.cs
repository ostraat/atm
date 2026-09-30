using System.ComponentModel.DataAnnotations;
using Atm.Domain;

namespace Atm.Api.Models;

public sealed class AccountDto
{
    public Guid Id { get; init; }
    public string Label { get; init; } = string.Empty;

    /// <summary>Balance in cents.</summary>
    public long Balance { get; init; }

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

public sealed class TransactionDto
{
    public string Label { get; init; } = string.Empty;

    /// <summary>Signed amount in cents.</summary>
    public long Amount { get; init; }

    /// <summary>UTC timestamp.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    public static TransactionDto From(TransactionView view) => new()
    {
        Label = view.Label,
        Amount = view.Amount,
        CreatedAt = view.CreatedAt
    };
}

public sealed class AccountsResponse
{
    public string? Error { get; init; }
    public IReadOnlyList<AccountDto> Accounts { get; init; } = Array.Empty<AccountDto>();
}

public sealed class AccountResponse
{
    public string? Error { get; init; }
    public AccountDto? Account { get; init; }
}

public sealed class TransactionsResponse
{
    public string? Error { get; init; }
    public IReadOnlyList<TransactionDto> Transactions { get; init; } = Array.Empty<TransactionDto>();
}

public sealed class DepositResponse
{
    public string? Error { get; init; }
    public AccountDto? To { get; init; }
}

public sealed class WithdrawResponse
{
    public string? Error { get; init; }
    public AccountDto? From { get; init; }
}

public sealed class TransferResponse
{
    public string? Error { get; init; }
    public AccountDto? From { get; init; }
    public AccountDto? To { get; init; }
}

public sealed class DepositRequest
{
    public Guid To { get; init; }

    /// <summary>Positive amount in cents.</summary>
    [Range(1, long.MaxValue)]
    public long Amount { get; init; }
}

public sealed class WithdrawRequest
{
    public Guid From { get; init; }

    /// <summary>Positive amount in cents.</summary>
    [Range(1, long.MaxValue)]
    public long Amount { get; init; }
}

public sealed class TransferRequest
{
    public Guid From { get; init; }
    public Guid To { get; init; }

    /// <summary>Positive amount in cents.</summary>
    [Range(1, long.MaxValue)]
    public long Amount { get; init; }
}

public sealed class HealthResponse
{
    public string Status { get; init; } = "ok";
}
