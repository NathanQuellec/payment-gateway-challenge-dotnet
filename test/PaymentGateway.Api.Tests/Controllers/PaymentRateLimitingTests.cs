using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PaymentGateway.Api.RateLimiting;
using static PaymentGateway.Api.Tests.TestSupport.PaymentTestData;

namespace PaymentGateway.Api.Tests.Controllers;

public class PaymentRateLimitingTests
{
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
            context.Connection.RemoteIpAddress = IPAddress.Parse(ip)))
        {
            BaseAddress = new Uri("https://localhost")
        };
        return method == "POST"
            ? await client.PostAsJsonAsync("/api/payments", ValidRequest())
            : await client.GetAsync($"/api/payments/{Guid.NewGuid()}");
    }

    [Theory]
    [InlineData("POST", HttpStatusCode.Created)]
    [InlineData("GET", HttpStatusCode.NotFound)]
    public async Task ExcessRequestsReturnProblemDetailsAndRetryAfter(string method, HttpStatusCode expected)
    {
        using var factory = Factory();
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
        using var factory = Factory();
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
