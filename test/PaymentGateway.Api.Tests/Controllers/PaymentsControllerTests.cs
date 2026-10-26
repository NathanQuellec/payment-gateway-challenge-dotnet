using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PaymentGateway.Api.Services;
using PaymentGateway.Api.Services.impl;
using PaymentGateway.Api.Tests.TestSupport;
using static PaymentGateway.Api.Tests.TestSupport.PaymentTestData;

namespace PaymentGateway.Api.Tests.Controllers;

public class PaymentsControllerTests
{
    private static WebApplicationFactory<Program> Factory(HttpStatusCode bankStatus = HttpStatusCode.OK, bool authorized = true) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddSingleton<TimeProvider>(new FixedTimeProvider());
            services.AddHttpClient<IBankClient, BankClientService>()
                .ConfigurePrimaryHttpMessageHandler(() => new StubHttpMessageHandler((_, _) =>
                    Task.FromResult(new HttpResponseMessage(bankStatus)
                    {
                        Content = JsonContent.Create(new
                        {
                            authorized,
                            authorization_code = authorized ? "71a64cce-27c4-4166-883b-45604e668031" : ""
                        })
                    })));
        }));

    [Theory]
    [InlineData(true, "Authorized")]
    [InlineData(false, "Declined")]
    public async Task ApiProcessesAndRetrievesPayment(bool authorized, string status)
    {
        using var factory = Factory(authorized: authorized);
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/payments", ValidRequest());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var json = await response.Content.ReadAsStringAsync();
        using var body = JsonDocument.Parse(json);
        Assert.Equal(status, body.RootElement.GetProperty("status").GetString());
        Assert.Equal("0001", body.RootElement.GetProperty("cardNumberLastFour").GetString());
        Assert.False(body.RootElement.TryGetProperty("cardNumber", out _));
        Assert.False(body.RootElement.TryGetProperty("cvv", out _));
        Assert.DoesNotContain(ValidRequest().CardNumber!, json);
        var get = await client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.Equal(json, await get.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ApiUnknownPaymentReturns404()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/payments/{Guid.NewGuid()}")).StatusCode);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("{\"amount\":1.5}")]
    [InlineData("{\"cardNumber\":2222405343240001}")]
    public async Task ApiRejectsInvalidJsonOrFields(string json)
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        var response = await client.PostAsync("/api/payments", new StringContent(json, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Rejected", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ApiBankUnavailableReturns503()
    {
        using var factory = Factory(HttpStatusCode.ServiceUnavailable);
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/payments", ValidRequest());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}
