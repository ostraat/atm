using System.Text.Json;
using Atm.Domain;

namespace Atm.Api.Middleware;

public sealed class AtmExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AtmExceptionMiddleware> _logger;

    public AtmExceptionMiddleware(RequestDelegate next, ILogger<AtmExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (AtmException ex)
        {
            var status = ex switch
            {
                NotFoundException => StatusCodes.Status404NotFound,
                ConflictException => StatusCodes.Status409Conflict,
                InsufficientFundsException => StatusCodes.Status400BadRequest,
                ValidationException => StatusCodes.Status400BadRequest,
                _ => StatusCodes.Status400BadRequest
            };

            _logger.LogInformation(ex, "Domain error: {Message}", ex.Message);
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                type = "about:blank",
                title = status switch
                {
                    StatusCodes.Status404NotFound => "Not Found",
                    StatusCodes.Status409Conflict => "Conflict",
                    _ => "Bad Request"
                },
                status,
                detail = ex.Message
            }));
        }
    }
}
