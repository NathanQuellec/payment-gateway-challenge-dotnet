using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using PaymentGateway.Api.Services;
using PaymentGateway.Api.Services.Impl;
using PaymentGateway.Api.Tests.TestSupport;

using static PaymentGateway.Api.Tests.TestSupport.PaymentTestData;

namespace PaymentGateway.Api.Tests.Integration;

[Trait("Category", "Integration")]
public class PaymentFlowTests
{
    private static WebApplicationFactory<Program> Factory(BankStubServer bank, TimeSpan? timeout = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Bank:BaseUrl"] = bank.BaseAddress.ToString() }));
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<TimeProvider>(new FixedTimeProvider());
                if (timeout is { } value)
                    services.AddHttpClient<IBankClient, BankClientService>().ConfigureHttpClient(client => client.Timeout = value);
            });
        });

    [Theory]
    [InlineData(true, "Authorized", "USD", 1050)]
    [InlineData(false, "Declined", "EUR", 1)]
    [InlineData(true, "Authorized", "GBP", int.MaxValue)]
    public async Task PaymentTravelsToBankAndCanBeRetrievedWithAllDetails(
        bool authorized, string status, string currency, int amount)
    {
        await using var bank = await BankStubServer.StartAsync(context => context.Response.WriteAsJsonAsync(new
        {
            authorized,
            authorization_code = authorized ? "bank-approval" : ""
        }));
        await using var factory = Factory(bank);
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var request = ValidRequest();
        request.Currency = currency;
        request.Amount = amount;

        using var response = await client.PostAsJsonAsync("/api/payments", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var sent = Assert.Single(bank.Requests);
        Assert.Equal(5, sent.EnumerateObject().Count());
        Assert.Equal(request.CardNumber, sent.GetProperty("card_number").GetString());
        Assert.Equal("10/2030", sent.GetProperty("expiry_date").GetString());
        Assert.Equal(request.Cvv, sent.GetProperty("cvv").GetString());
        Assert.Equal(currency, sent.GetProperty("currency").GetString());
        Assert.Equal(amount, sent.GetProperty("amount").GetInt32());

        var json = await response.Content.ReadAsStringAsync();
        using var body = JsonDocument.Parse(json);
        var payment = body.RootElement;
        var id = payment.GetProperty("id").GetGuid();
        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal(status, payment.GetProperty("status").GetString());
        Assert.Equal("0001", payment.GetProperty("cardNumberLastFour").GetString());
        Assert.Equal(request.ExpiryMonth, payment.GetProperty("expiryMonth").GetInt32());
        Assert.Equal(request.ExpiryYear, payment.GetProperty("expiryYear").GetInt32());
        Assert.Equal(currency, payment.GetProperty("currency").GetString());
        Assert.Equal(amount, payment.GetProperty("amount").GetInt32());
        Assert.Equal(7, payment.EnumerateObject().Count());
        Assert.False(payment.TryGetProperty("cardNumber", out _));
        Assert.False(payment.TryGetProperty("cvv", out _));
        Assert.DoesNotContain(request.CardNumber!, json);
        Assert.NotNull(response.Headers.Location);
        Assert.Equal($"https://localhost/api/Payments/{id}", response.Headers.Location.ToString());

        using var get = await client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.Equal(json, await get.Content.ReadAsStringAsync());
        Assert.Single(bank.Requests); // Retrieval must not reauthorize the payment.
    }

    [Theory]
    [InlineData("{", null)]
    [InlineData("{\"amount\":1.5}", "Amount")]
    [InlineData("{\"amount\":2147483648}", "Amount")]
    [InlineData("{\"cardNumber\":\"short\",\"cvv\":\"012\",\"expiryMonth\":10,\"expiryYear\":2030,\"currency\":\"GBP\",\"amount\":100}", "CardNumber")]
    public async Task InvalidInputReturnsFieldErrorsWithoutAnHttpRequestToBank(string json, string? field)
    {
        await using var bank = await BankStubServer.StartAsync(context => context.Response.WriteAsJsonAsync(new
        {
            authorized = true,
            authorization_code = "bank-approval"
        }));
        await using var factory = Factory(bank);
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

        using var response = await client.PostAsync("/api/payments", new StringContent(json, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Rejected", problem.RootElement.GetProperty("paymentStatus").GetString());
        var errors = problem.RootElement.GetProperty("errors");
        Assert.NotEmpty(errors.EnumerateObject());
        if (field is not null) Assert.True(errors.TryGetProperty(field, out _));
        Assert.Empty(bank.Requests);
    }

    [Theory]
    [InlineData(503, "{}", 503)]
    [InlineData(400, "{}", 502)]
    [InlineData(200, "invalid-json", 502)]
    [InlineData(200, "{\"authorized\":true}", 502)]
    public async Task BankFailuresReturnProblemDetailsInsteadOfPayment(int bankStatus, string bankBody, int expectedStatus)
    {
        await using var bank = await BankStubServer.StartAsync(async context =>
        {
            context.Response.StatusCode = bankStatus;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(bankBody);
        });
        await using var factory = Factory(bank);
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

        using var response = await client.PostAsJsonAsync("/api/payments", ValidRequest());

        Assert.Equal(expectedStatus, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(expectedStatus, problem.RootElement.GetProperty("status").GetInt32());
        Assert.False(problem.RootElement.TryGetProperty("id", out _));
        Assert.Equal(response.Headers.GetValues("X-Trace-Id").Single(), problem.RootElement.GetProperty("traceId").GetString());
        Assert.Single(bank.Requests); // Payment requests must not be retried automatically.
    }

    [Fact]
    public async Task MultiplePaymentsRemainIndependentlyRetrievable()
    {
        await using var bank = await BankStubServer.StartAsync(context => context.Response.WriteAsJsonAsync(new
        {
            authorized = true,
            authorization_code = "bank-approval"
        }));
        await using var factory = Factory(bank);
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var locations = new List<Uri>();
        var bodies = new List<string>();
        foreach (var amount in new[] { 100, 2500 })
        {
            var request = ValidRequest();
            request.Amount = amount;
            using var response = await client.PostAsJsonAsync("/api/payments", request);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.NotNull(response.Headers.Location);
            locations.Add(response.Headers.Location);
            bodies.Add(await response.Content.ReadAsStringAsync());
        }

        Assert.NotEqual(locations[0], locations[1]);
        for (var i = 0; i < locations.Count; i++)
        {
            using var response = await client.GetAsync(locations[i]);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(bodies[i], await response.Content.ReadAsStringAsync());
        }
        Assert.Equal(2, bank.Requests.Count);
    }

    [Fact]
    public async Task SlowBankReturnsGatewayTimeout()
    {
        await using var bank = await BankStubServer.StartAsync(context =>
            Task.Delay(Timeout.InfiniteTimeSpan, context.RequestAborted));
        await using var factory = Factory(bank, TimeSpan.FromSeconds(1));
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

        using var response = await client.PostAsJsonAsync("/api/payments", ValidRequest());

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(504, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Single(bank.Requests);
    }
}