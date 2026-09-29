using Atm.Api.Models;
using Atm.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Atm.Api.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class AtmController : ControllerBase
{
    private readonly AtmService _atm;

    public AtmController(AtmService atm)
    {
        _atm = atm;
    }

    [HttpGet("accounts")]
    [ProducesResponseType(typeof(AccountsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AccountsResponse>> GetAccounts(CancellationToken cancellationToken)
    {
        var accounts = await _atm.GetAccountsAsync(cancellationToken);
        return Ok(new AccountsResponse
        {
            Accounts = accounts.Select(AccountDto.From).ToList()
        });
    }

    [HttpGet("accounts/{accountId:guid}")]
    [ProducesResponseType(typeof(AccountResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AccountResponse>> GetAccount(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        var account = await _atm.GetAccountAsync(accountId, cancellationToken);
        return Ok(new AccountResponse { Account = AccountDto.From(account) });
    }

    [HttpGet("accounts/{accountId:guid}/transactions")]
    [ProducesResponseType(typeof(TransactionsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<TransactionsResponse>> GetTransactions(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        var transactions = await _atm.GetTransactionsAsync(accountId, cancellationToken);
        return Ok(new TransactionsResponse
        {
            Transactions = transactions.Select(TransactionDto.From).ToList()
        });
    }

    [HttpPost("accounts/{accountId:guid}/deposit")]
    [ProducesResponseType(typeof(DepositResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DepositResponse>> Deposit(
        Guid accountId,
        [FromBody] MoneyRequest request,
        CancellationToken cancellationToken)
    {
        var key = RequireIdempotencyKey();
        var result = await _atm.DepositAsync(accountId, request.Amount, key, cancellationToken);
        return Ok(new DepositResponse
        {
            Account = AccountDto.From(result.Account),
            Transaction = TransactionDto.From(result.Transaction)
        });
    }

    [HttpPost("accounts/{accountId:guid}/withdraw")]
    [ProducesResponseType(typeof(WithdrawResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<WithdrawResponse>> Withdraw(
        Guid accountId,
        [FromBody] MoneyRequest request,
        CancellationToken cancellationToken)
    {
        var key = RequireIdempotencyKey();
        var result = await _atm.WithdrawAsync(accountId, request.Amount, key, cancellationToken);
        return Ok(new WithdrawResponse
        {
            Account = AccountDto.From(result.Account),
            Transaction = TransactionDto.From(result.Transaction)
        });
    }

    [HttpPost("transfer")]
    [ProducesResponseType(typeof(TransferResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<TransferResponse>> Transfer(
        [FromBody] TransferRequest request,
        CancellationToken cancellationToken)
    {
        var key = RequireIdempotencyKey();
        var result = await _atm.TransferAsync(request.From, request.To, request.Amount, key, cancellationToken);
        return Ok(new TransferResponse
        {
            From = AccountDto.From(result.From),
            To = AccountDto.From(result.To)
        });
    }

    private string RequireIdempotencyKey()
    {
        if (!Request.Headers.TryGetValue("Idempotency-Key", out var values) ||
            string.IsNullOrWhiteSpace(values.FirstOrDefault()))
        {
            throw new ValidationException("Idempotency-Key header is required.");
        }

        return values.ToString();
    }
}
