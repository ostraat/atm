using System.ComponentModel.DataAnnotations;
using Atm.Api.Models;
using Atm.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Atm.Api.Controllers;

/// <summary>Customer account balances, history, and ATM money movements.</summary>
[ApiController]
[Route("api/v1")]
[Produces("application/json")]
public sealed class AtmController : ControllerBase
{
    private readonly AtmService _atm;

    public AtmController(AtmService atm)
    {
        _atm = atm;
    }

    /// <summary>List customer accounts with balances and latest transactions.</summary>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>Checking and Savings accounts.</returns>
    /// <response code="200">Customer accounts with balances and latest transactions.</response>
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

    /// <summary>Get one customer account by id.</summary>
    /// <param name="accountId">Customer account id.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>Account balance and latest transaction.</returns>
    /// <response code="200">Account balance and latest transaction.</response>
    /// <response code="404">Account id is unknown or not a customer account.</response>
    [HttpGet("accounts/{accountId:guid}")]
    [ProducesResponseType(typeof(AccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccountResponse>> GetAccount(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        var account = await _atm.GetAccountAsync(accountId, cancellationToken);
        return Ok(new AccountResponse { Account = AccountDto.From(account) });
    }

    /// <summary>List ledger transactions for a customer account.</summary>
    /// <param name="accountId">Customer account id.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>Signed ledger legs for the account.</returns>
    /// <response code="200">Ledger legs for the account.</response>
    /// <response code="404">Account id is unknown or not a customer account.</response>
    [HttpGet("accounts/{accountId:guid}/transactions")]
    [ProducesResponseType(typeof(TransactionsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status404NotFound)]
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

    /// <summary>Deposit cash from the ATM vault into a customer account.</summary>
    /// <param name="idempotencyKey">
    /// Client-generated key so retries do not double-apply the deposit.
    /// Replays with the same key and body return the original result.
    /// </param>
    /// <param name="request">Destination account and positive amount in cents.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>Updated destination account (includes latestTransaction).</returns>
    /// <response code="200">Deposit applied; destination account includes latestTransaction.</response>
    /// <response code="400">Invalid amount, missing Idempotency-Key, or other validation failure.</response>
    /// <response code="404">Destination account id is unknown or not a customer account.</response>
    /// <response code="409">Idempotency-Key was already used with a different request body.</response>
    [HttpPost("deposit")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(DepositResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status409Conflict)]
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

    /// <summary>Withdraw cash from a customer account to the ATM vault.</summary>
    /// <param name="idempotencyKey">
    /// Client-generated key so retries do not double-apply the withdrawal.
    /// Replays with the same key and body return the original result.
    /// </param>
    /// <param name="request">Source account and positive amount in cents.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>Updated source account (includes latestTransaction).</returns>
    /// <response code="200">Withdrawal applied; source account includes latestTransaction.</response>
    /// <response code="400">Insufficient funds, invalid amount, missing Idempotency-Key, or other validation failure.</response>
    /// <response code="404">Source account id is unknown or not a customer account.</response>
    /// <response code="409">Idempotency-Key was already used with a different request body.</response>
    [HttpPost("withdraw")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(WithdrawResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status409Conflict)]
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

    /// <summary>Transfer funds between two customer accounts.</summary>
    /// <param name="idempotencyKey">
    /// Client-generated key so retries do not double-apply the transfer.
    /// Replays with the same key and body return the original result.
    /// </param>
    /// <param name="request">Source, destination, and positive amount in cents.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>Updated source and destination accounts.</returns>
    /// <response code="200">Transfer applied; from and to accounts are updated.</response>
    /// <response code="400">Insufficient funds, same from/to account, invalid amount, missing Idempotency-Key, or other validation failure.</response>
    /// <response code="404">From or to account id is unknown or not a customer account.</response>
    /// <response code="409">Idempotency-Key was already used with a different request body.</response>
    [HttpPost("transfer")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(TransferResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status409Conflict)]
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
