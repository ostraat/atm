using System.Net;
using System.Net.Http.Json;
using Atm.Api.Models;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Atm.Api.Tests;

public class AtmApiTests
{
    private static (WebApplicationFactory<Program> Factory, HttpClient Client, string DbPath) CreateHost()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"atm-test-{Guid.NewGuid():N}.db");
        Environment.SetEnvironmentVariable("ConnectionStrings__Atm", $"Data Source={dbPath}");

        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Atm", $"Data Source={dbPath}");
        });
        return (factory, factory.CreateClient(), dbPath);
    }

    private static async Task DisposeHostAsync(WebApplicationFactory<Program> factory, HttpClient client, string dbPath)
    {
        client.Dispose();
        await factory.DisposeAsync();
        foreach (var suffix in new[] { "", "-shm", "-wal" })
        {
            var path = dbPath + suffix;
            if (File.Exists(path))
            {
                try { File.Delete(path); } catch { /* ignore */ }
            }
        }
    }

    [Fact]
    public async Task Health_returns_ok()
    {
        var (factory, client, dbPath) = CreateHost();
        try
        {
            var response = await client.GetAsync("/api/health");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
            Assert.Equal("ok", body!.Status);
        }
        finally
        {
            await DisposeHostAsync(factory, client, dbPath);
        }
    }

    [Fact]
    public async Task Swagger_documents_insufficient_funds_and_schemas()
    {
        var (factory, client, dbPath) = CreateHost();
        try
        {
            var response = await client.GetAsync("/swagger/v1/swagger.json");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var json = await response.Content.ReadAsStringAsync();
            Assert.Contains("Insufficient funds", json);
            Assert.Contains("Idempotency-Key", json);
            Assert.Contains("latestTransaction", json);
            Assert.Contains("ProblemDetailsResponse", json);
            Assert.Contains("\"400\"", json);
            Assert.Contains("\"409\"", json);
        }
        finally
        {
            await DisposeHostAsync(factory, client, dbPath);
        }
    }

    [Fact]
    public async Task Swagger_is_available()
    {
        var (factory, client, dbPath) = CreateHost();
        try
        {
            var response = await client.GetAsync("/swagger/v1/swagger.json");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        finally
        {
            await DisposeHostAsync(factory, client, dbPath);
        }
    }

    [Fact]
    public async Task Accounts_list_and_deposit_include_latest_transaction()
    {
        var (factory, client, dbPath) = CreateHost();
        try
        {
            var list = await client.GetFromJsonAsync<AccountsResponse>("/api/v1/accounts");
            Assert.Equal(2, list!.Accounts.Count);
            Assert.All(list.Accounts, a => Assert.True(a.Balance == 10_000 || a.Balance == 50_000));

            var checking = list.Accounts.Single(a => a.Label == "Checking");
            Assert.Equal(10_000, checking.Balance);

            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/deposit")
            {
                Content = JsonContent.Create(new DepositRequest { To = checking.Id, Amount = 250 })
            };
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

            var response = await client.SendAsync(request);
            var raw = await response.Content.ReadAsStringAsync();
            Assert.True(response.IsSuccessStatusCode, raw);
            var body = await response.Content.ReadFromJsonAsync<DepositResponse>();
            Assert.Equal(10_250, body!.To!.Balance);
            Assert.Equal(250, body.To.LatestTransaction!.Amount);
            Assert.Equal("Deposit from ATM", body.To.LatestTransaction.Label);
        }
        finally
        {
            await DisposeHostAsync(factory, client, dbPath);
        }
    }

    [Fact]
    public async Task Transfer_endpoint_works()
    {
        var (factory, client, dbPath) = CreateHost();
        try
        {
            var list = await client.GetFromJsonAsync<AccountsResponse>("/api/v1/accounts");
            var checking = list!.Accounts.Single(a => a.Label == "Checking");
            var savings = list.Accounts.Single(a => a.Label == "Savings");
            Assert.Equal(10_000, checking.Balance);
            Assert.Equal(50_000, savings.Balance);

            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/transfer")
            {
                Content = JsonContent.Create(new TransferRequest
                {
                    From = checking.Id,
                    To = savings.Id,
                    Amount = 100
                })
            };
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

            var response = await client.SendAsync(request);
            var raw = await response.Content.ReadAsStringAsync();
            Assert.True(response.IsSuccessStatusCode, raw);
            var body = await response.Content.ReadFromJsonAsync<TransferResponse>();
            Assert.Equal(9_900, body!.From!.Balance);
            Assert.Equal(50_100, body.To!.Balance);
        }
        finally
        {
            await DisposeHostAsync(factory, client, dbPath);
        }
    }

    [Fact]
    public async Task Missing_idempotency_key_is_bad_request()
    {
        var (factory, client, dbPath) = CreateHost();
        try
        {
            var list = await client.GetFromJsonAsync<AccountsResponse>("/api/v1/accounts");
            var checking = list!.Accounts.Single(a => a.Label == "Checking");
            var response = await client.PostAsJsonAsync(
                "/api/v1/deposit",
                new DepositRequest { To = checking.Id, Amount = 10 });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        finally
        {
            await DisposeHostAsync(factory, client, dbPath);
        }
    }
}
