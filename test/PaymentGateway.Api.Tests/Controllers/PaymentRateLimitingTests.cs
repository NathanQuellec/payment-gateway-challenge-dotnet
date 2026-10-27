using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using PaymentGateway.Api.RateLimiting;
using PaymentGateway.Api.Services;
using PaymentGateway.Api.Services.Impl;
using PaymentGateway.Api.Tests.TestSupport;

using static PaymentGateway.Api.Tests.TestSupport.PaymentTestData;

namespace PaymentGateway.Api.Tests.Controllers;

public class PaymentRateLimitingTests
{
    [Fact]
    public async Task ConcurrentRequestsRespectLimitAndRejectedRequestsDoNotCallBank()
    {
        var bankCalls = 0;
        await using var factory = Factory().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddHttpClient<IBankClient, BankClientService>().ConfigurePrimaryHttpMessageHandler(() =>
                new StubHttpMessageHandler((_, _) =>
                {
                    Interlocked.Increment(ref bankCalls);
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = JsonContent.Create(new { authorized = true, authorization_code = "test-approval" })
                    });
                }))));
        using var client = factory.CreateClient();
        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => SendAsync(factory, "POST")));
        try
        {
            Assert.Equal(2, responses.Count(response => response.StatusCode == HttpStatusCode.Created));
            Assert.Equal(8, responses.Count(response => response.StatusCode == HttpStatusCode.TooManyRequests));
            Assert.Equal(2, bankCalls);
        }
        finally
        {
            foreach (var response in responses) response.Dispose();
        }
    }

    [Fact]
    public async Task ExhaustedWindowReplenishes()
    {
        await using var factory = Factory().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.Configure<PaymentRateLimitOptions>(options => options.WindowSeconds = 1)));
        using var client = factory.CreateClient();
        using var first = await SendAsync(factory, "GET");
        using var second = await SendAsync(factory, "GET");
        using var rejected = await SendAsync(factory, "GET");
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(100);
            using var retry = await SendAsync(factory, "GET");
            if (retry.StatusCode == HttpStatusCode.NotFound) return;
        }
        Assert.Fail("The fixed-window allowance did not replenish within five seconds.");
    }

    private static WebApplicationFactory<Program> Factory() =>
        PaymentsControllerTests.Factory().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.Configure<PaymentRateLimitOptions>(options =>
            {
                options.SubmissionPermitLimit = 2;
                options.LookupPermitLimit = 2;
                options.WindowSeconds = 60;
            })));

    private static async Task<HttpResponseMessage> SendAsync(
        WebApplicationFactory<Program> factory, string method, string ip = "192.0.2.1")
    {
        using var client = new HttpClient(factory.Server.CreateHandler(context =>
            context.Connection.RemoteIpAddress = IPAddress.Parse(ip)));
        client.BaseAddress = new Uri("https://localhost");
        return method == "POST"
            ? await client.PostAsJsonAsync("/api/payments", ValidRequest())
            : await client.GetAsync($"/api/payments/{Guid.NewGuid()}");
    }

    [Theory]
    [InlineData("POST", HttpStatusCode.Created)]
    [InlineData("GET", HttpStatusCode.NotFound)]
    public async Task ExcessRequestsReturnProblemDetailsAndRetryAfter(string method, HttpStatusCode expected)
    {
        await using var factory = Factory();
        using var client = factory.CreateClient();
        for (var i = 0; i < 2; i++)
        {
            using var accepted = await SendAsync(factory, method);
            Assert.Equal(expected, accepted.StatusCode);
        }

        using var rejected = await SendAsync(factory, method);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType?.MediaType);
        Assert.True(rejected.Headers.RetryAfter?.Delta > TimeSpan.Zero);
        using var problem = JsonDocument.Parse(await rejected.Content.ReadAsStringAsync());
        Assert.Equal(429, problem.RootElement.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task ClientsHaveIndependentLimits()
    {
        await using var factory = Factory();
        using var client = factory.CreateClient();
        using var first = await SendAsync(factory, "POST");
        using var second = await SendAsync(factory, "POST");
        using var rejected = await SendAsync(factory, "POST");
        using var otherClient = await SendAsync(factory, "POST", "192.0.2.2");

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal(HttpStatusCode.Created, otherClient.StatusCode);
    }

    [Fact]
    public async Task SubmissionAndLookupHaveIndependentLimits()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        using var first = await SendAsync(factory, "POST");
        using var second = await SendAsync(factory, "POST");
        using var rejected = await SendAsync(factory, "POST");
        using var lookup = await SendAsync(factory, "GET");

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, lookup.StatusCode);
    }

    [Fact]
    public void InvalidLimitsFailAtStartup()
    {
        using var factory = Factory().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.Configure<PaymentRateLimitOptions>(options => options.SubmissionPermitLimit = 0)));

        Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
    }
}