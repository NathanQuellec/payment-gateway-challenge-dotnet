using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;

using PaymentGateway.Api.Exceptions;
using PaymentGateway.Api.Services.impl;
using PaymentGateway.Api.Tests.TestSupport;
using static PaymentGateway.Api.Tests.TestSupport.PaymentTestData;

namespace PaymentGateway.Api.Tests.Services.impl;

public class BankClientServiceTests
{
    [Theory]
    [InlineData(true, 1, "01/2030")]
    [InlineData(false, 10, "10/2030")]
    public async Task BankClientMapsContract(bool authorized, int expiryMonth, string expectedExpiryDate)
    {
        using var handler = new StubHttpMessageHandler(async (request, token) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("http://bank/payments", request.RequestUri!.ToString());
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            Assert.Equal("2222405343240001", body.RootElement.GetProperty("card_number").GetString());
            Assert.Equal(expectedExpiryDate, body.RootElement.GetProperty("expiry_date").GetString());
            Assert.Equal("012", body.RootElement.GetProperty("cvv").GetString());
            Assert.Equal("GBP", body.RootElement.GetProperty("currency").GetString());
            Assert.Equal(100, body.RootElement.GetProperty("amount").GetInt32());
            return new(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    authorized,
                    authorization_code = authorized ? "71a64cce-27c4-4166-883b-45604e668031" : ""
                })
            };
        });
        using var http = new HttpClient(handler) { BaseAddress = new("http://bank/") };
        var payment = ValidRequest();
        payment.ExpiryMonth = expiryMonth;
        Assert.Equal(authorized, await new BankClientService(http, NullLogger<BankClientService>.Instance).AuthorizeAsync(payment, default));
    }

    [Theory]
    [InlineData(503, "{}", 503)]
    [InlineData(400, "{}", 502)]
    [InlineData(400, "{\"error_message\":\"Not all required properties were sent in the request\"}", 502)]
    [InlineData(400, "{\"errorMessage\":\"The request supplied is not supported by the simulator\"}", 502)]
    [InlineData(200, "{}", 502)]
    [InlineData(200, "null", 502)]
    [InlineData(200, "invalid", 502)]
    [InlineData(200, "{\"authorized\":true}", 502)]
    [InlineData(200, "{\"authorized\":true,\"authorization_code\":null}", 502)]
    [InlineData(200, "{\"authorized\":true,\"authorization_code\":\"\"}", 502)]
    [InlineData(200, "{\"authorized\":true,\"authorization_code\":\" \"}", 502)]
    [InlineData(200, "{\"authorized\":\"true\"}", 502)]
    public async Task BankErrorsAreNotDeclines(int status, string json, int expected)
    {
        using var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)
        { Content = new StringContent(json, Encoding.UTF8, "application/json") }));
        using var http = new HttpClient(handler) { BaseAddress = new("http://bank/") };
        var exception = await Assert.ThrowsAsync<BankException>(() => new BankClientService(http, NullLogger<BankClientService>.Instance).AuthorizeAsync(ValidRequest(), default));
        Assert.Equal(expected, exception.StatusCode);
    }

    [Fact]
    public async Task CallerCancellationIsPreserved()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var handler = new StubHttpMessageHandler((_, token) => Task.FromCanceled<HttpResponseMessage>(token));
        using var http = new HttpClient(handler) { BaseAddress = new("http://bank/") };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new BankClientService(http, NullLogger<BankClientService>.Instance).AuthorizeAsync(ValidRequest(), cancellation.Token));
    }

    [Theory]
    [InlineData(true, 504)]
    [InlineData(false, 503)]
    public async Task MapsTransportFailures(bool timeout, int expected)
    {
        using var handler = new StubHttpMessageHandler((_, _) => throw (timeout ? (Exception)new TaskCanceledException() : new HttpRequestException()));
        using var http = new HttpClient(handler) { BaseAddress = new("http://bank/") };
        var exception = await Assert.ThrowsAsync<BankException>(() => new BankClientService(http, NullLogger<BankClientService>.Instance).AuthorizeAsync(ValidRequest(), default));
        Assert.Equal(expected, exception.StatusCode);
    }

}
