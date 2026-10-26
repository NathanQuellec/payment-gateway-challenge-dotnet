using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Data.Repositories;
using PaymentGateway.Api.Services;
using PaymentGateway.Api.Services.impl;
using PaymentGateway.Api.Tests.TestSupport;
using static PaymentGateway.Api.Tests.TestSupport.PaymentTestData;

namespace PaymentGateway.Api.Tests.Services.impl;

public class PaymentsServiceTests
{
    private sealed class FakeBank(bool authorized) : IBankClient
    {
        public int Calls { get; private set; }
        public Task<bool> AuthorizeAsync(PaymentRequest request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(authorized);
        }
    }

    [Theory]
    [InlineData(true, PaymentStatus.Authorized)]
    [InlineData(false, PaymentStatus.Declined)]
    public async Task ProcessesAndStoresDecision(bool authorized, PaymentStatus expected)
    {
        var bank = new FakeBank(authorized);
        var repository = new PaymentsRepository();
        var service = new PaymentsService(repository, new PaymentValidationService(new FixedTimeProvider()), bank,
            NullLogger<PaymentsService>.Instance);
        var payment = await service.ProcessPayment(ValidRequest());
        Assert.NotEqual(Guid.Empty, payment.Id);
        Assert.Equal(expected, payment.Status);
        Assert.Equal("0001", payment.CardNumberLastFour);
        var stored = Assert.IsType<Payment>(repository.GetPaymentRecordById(payment.Id));
        Assert.Equal(payment.Id, stored.Id);
        Assert.Equal(payment.Status, stored.Status);
        Assert.Equal(payment.CardNumberLastFour, stored.CardNumberLastFour);
        Assert.Equal(payment.ExpiryMonth, stored.ExpiryMonth);
        Assert.Equal(payment.ExpiryYear, stored.ExpiryYear);
        Assert.Equal(payment.Currency, stored.Currency);
        Assert.Equal(payment.Amount, stored.Amount);
        var retrieved = service.GetPaymentDetails(payment.Id);
        Assert.NotNull(retrieved);
        Assert.NotSame(payment, retrieved);
        Assert.Equal(JsonSerializer.Serialize(payment), JsonSerializer.Serialize(retrieved));
        Assert.Equal(1, bank.Calls);
    }

    [Fact]
    public async Task InvalidRequestNeverCallsBank()
    {
        var bank = new FakeBank(true);
        var service = new PaymentsService(new(), new PaymentValidationService(new FixedTimeProvider()), bank,
            NullLogger<PaymentsService>.Instance);
        var exception = await Assert.ThrowsAsync<PaymentValidationException>(() => service.ProcessPayment(new()));
        Assert.Equal(6, exception.Errors.Count);
        Assert.Equal(0, bank.Calls);
    }

}
