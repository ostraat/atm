using System.ComponentModel.DataAnnotations;
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

    [HttpPost("deposit")]
    [ProducesResponseType(typeof(DepositResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DepositResponse>> Deposit(
        [FromHeader(Name = "Idempotency-Key"), Required] string idempotencyKey,
        [FromBody] DepositRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _atm.DepositAsync(request.To, request.Amount, idempotencyKey, cancellationToken);
        return Ok(new DepositResponse
        {
            To = AccountDto.From(result.Account)
        });
    }

    [HttpPost("withdraw")]
    [ProducesResponseType(typeof(WithdrawResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<WithdrawResponse>> Withdraw(
        [FromHeader(Name = "Idempotency-Key"), Required] string idempotencyKey,
        [FromBody] WithdrawRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _atm.WithdrawAsync(request.From, request.Amount, idempotencyKey, cancellationToken);
        return Ok(new WithdrawResponse
        {
            From = AccountDto.From(result.Account)
        });
    }

    [HttpPost("transfer")]
    [ProducesResponseType(typeof(TransferResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<TransferResponse>> Transfer(
        [FromHeader(Name = "Idempotency-Key"), Required] string idempotencyKey,
        [FromBody] TransferRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _atm.TransferAsync(request.From, request.To, request.Amount, idempotencyKey, cancellationToken);
        return Ok(new TransferResponse
        {
            From = AccountDto.From(result.From),
            To = AccountDto.From(result.To)
        });
    }
}
