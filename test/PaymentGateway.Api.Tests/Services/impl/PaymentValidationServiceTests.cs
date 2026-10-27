using PaymentGateway.Api.Services.Impl;
using PaymentGateway.Api.Tests.TestSupport;

using static PaymentGateway.Api.Tests.TestSupport.PaymentTestData;

namespace PaymentGateway.Api.Tests.Services.Impl;

public class PaymentValidationServiceTests
{
    [Theory]
    [InlineData("12345678901234", "123", true)]
    [InlineData("1234567890123456789", "0123", true)]
    [InlineData("1234567890123", "123", false)]
    [InlineData("12345678901234567890", "123", false)]
    [InlineData("1234567890123a", "123", false)]
    [InlineData("１２３４５６７８９０１２３４", "123", false)]
    [InlineData("12345678901234", "12", false)]
    [InlineData("12345678901234", "12345", false)]
    [InlineData("12345678901234", "1a3", false)]
    public void ValidatesCardAndCvv(string card, string cvv, bool valid)
    {
        var request = ValidRequest();
        request.CardNumber = card;
        request.Cvv = cvv;
        Assert.Equal(valid, new PaymentValidationService(new FixedTimeProvider()).Validate(request).Count == 0);
    }

    [Theory]
    [InlineData(9, 2030, false)]
    [InlineData(10, 2030, true)]
    [InlineData(1, 2031, true)]
    [InlineData(12, 2029, false)]
    [InlineData(0, 2031, false)]
    [InlineData(13, 2031, false)]
    [InlineData(1, 10000, false)]
    public void ValidatesExpiry(int month, int year, bool valid)
    {
        var request = ValidRequest();
        request.ExpiryMonth = month;
        request.ExpiryYear = year;
        Assert.Equal(valid, new PaymentValidationService(new FixedTimeProvider()).Validate(request).Count == 0);
    }

    [Theory]
    [InlineData("GBP", 1, true)]
    [InlineData("USD", 100, true)]
    [InlineData("EUR", int.MaxValue, true)]
    [InlineData("JPY", 100, false)]
    [InlineData("gbp", 100, false)]
    [InlineData("GBP", 0, false)]
    [InlineData("GBP", -1, false)]
    public void ValidatesCurrencyAndAmount(string currency, int amount, bool valid)
    {
        var request = ValidRequest();
        request.Currency = currency;
        request.Amount = amount;
        Assert.Equal(valid, new PaymentValidationService(new FixedTimeProvider()).Validate(request).Count == 0);
    }

}