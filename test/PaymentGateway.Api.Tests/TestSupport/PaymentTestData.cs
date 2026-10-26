using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Tests.TestSupport;

internal static class PaymentTestData
{
    internal static PaymentRequest ValidRequest() => new()
    {
        CardNumber = "2222405343240001", Cvv = "012",
        ExpiryMonth = 10, ExpiryYear = 2030, Currency = "GBP", Amount = 100
    };
}