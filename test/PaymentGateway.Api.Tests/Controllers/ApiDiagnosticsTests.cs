using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;
using PaymentGateway.Api.Services.Impl;
using PaymentGateway.Api.Tests.TestSupport;

using static PaymentGateway.Api.Tests.TestSupport.PaymentTestData;

namespace PaymentGateway.Api.Tests.Controllers;

public class ApiDiagnosticsTests
{
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"cardNumber\":2222405343240001}")]
    [InlineData("{\"amount\":{\"2222405343240001\":\"secret-cvv\"}}")]
    public async Task BindingErrorsHaveTraceIdsAndDoNotEchoSubmittedValues(string json)
    {
        var logs = new RecordingLoggerProvider();
        await using var factory = PaymentsControllerTests.Factory().WithWebHostBuilder(builder =>
            builder.ConfigureLogging(logging => logging.AddProvider(logs)));
        using var client = factory.CreateClient();
        using var response = await client.PostAsync("/api/payments", new StringContent(json, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var problem = JsonDocument.Parse(body);
        Assert.Equal(response.Headers.GetValues("X-Trace-Id").Single(), problem.RootElement.GetProperty("traceId").GetString());
        Assert.DoesNotContain("2222405343240001", body);
        Assert.DoesNotContain("secret-cvv", body);
        Assert.DoesNotContain(logs.Messages, message => message.Contains("2222405343240001") || message.Contains("secret-cvv"));
    }

    [Fact]
    public async Task UnexpectedErrorsAreSanitizedInResponsesAndLogs()
    {
        var logs = new RecordingLoggerProvider();
        await using var factory = PaymentsControllerTests.Factory().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services => services.AddScoped<IPaymentsService, FailingPaymentsService>());
            builder.ConfigureLogging(logging => logging.AddProvider(logs));
        });
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/payments", ValidRequest());
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("2222405343240001", body);
        using var problem = JsonDocument.Parse(body);
        Assert.Equal(response.Headers.GetValues("X-Trace-Id").Single(), problem.RootElement.GetProperty("traceId").GetString());
        Assert.DoesNotContain(logs.Messages, message => message.Contains("2222405343240001"));
        Assert.Contains(logs.Messages, message => message.Contains("InvalidOperationException"));
    }

    [Fact]
    public async Task TransportErrorsDoNotLogOriginalExceptionMessages()
    {
        var logs = new RecordingLoggerProvider();
        await using var factory = PaymentsControllerTests.Factory().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services => services.AddHttpClient<IBankClient, BankClientService>()
                .ConfigurePrimaryHttpMessageHandler(() => new StubHttpMessageHandler((_, _) =>
                    throw new HttpRequestException("Failed request: 2222405343240001"))));
            builder.ConfigureLogging(logging => logging.AddProvider(logs));
        });
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/payments", ValidRequest());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.DoesNotContain(logs.Messages, message => message.Contains("2222405343240001"));
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task HealthProbesDoNotSubmitPayments(string path)
    {
        await using var factory = PaymentsControllerTests.Factory().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddScoped<IPaymentsService, FailingPaymentsService>()));
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task MissingPaymentHasConsistentProblemDetails()
    {
        await using var factory = PaymentsControllerTests.Factory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync($"/api/payments/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(response.Headers.GetValues("X-Trace-Id").Single(), problem.RootElement.GetProperty("traceId").GetString());
    }

    private sealed class FailingPaymentsService : IPaymentsService
    {
        public PaymentResponse? GetPaymentDetails(Guid id) => throw new InvalidOperationException("2222405343240001");
        public Task<PaymentResponse> ProcessPayment(PaymentRequest request, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("2222405343240001");
    }
}