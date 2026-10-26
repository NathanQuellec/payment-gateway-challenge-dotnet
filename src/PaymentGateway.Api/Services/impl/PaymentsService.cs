using PaymentGateway.Api.Exceptions;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Data.Repositories;

namespace PaymentGateway.Api.Services.impl;

public class PaymentsService(
    PaymentsRepository paymentsRepository,
    IPaymentValidationService validationService,
    IBankClient bankClient,
    ILogger<PaymentsService> logger) : IPaymentsService
{
    public PaymentResponse? GetPaymentDetails(Guid id)
    {
        var payment = paymentsRepository.GetPaymentRecordById(id);
        logger.LogDebug("Payment lookup for {PaymentId} completed. Found: {Found}.", id, payment is not null);
        return payment is null ? null : ToResponse(payment);
    }

    public async Task<PaymentResponse> ProcessPayment(
        PaymentRequest paymentRequest, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Processing payment.");
        ValidatePaymentRequest(paymentRequest);

        Payment payment = await RequestBankAuthorizationAsync(paymentRequest, cancellationToken);
        paymentsRepository.AddPaymentRecord(payment);
        logger.LogInformation("Payment {PaymentId} stored with status {PaymentStatus}.", payment.Id, payment.Status);
        return ToResponse(payment);
    }

    private void ValidatePaymentRequest(PaymentRequest paymentRequest)
    {
        ArgumentNullException.ThrowIfNull(paymentRequest);
        var errors = validationService.Validate(paymentRequest);
        if (errors.Count > 0)
        {
            logger.LogWarning("Payment request rejected with {ValidationErrorCount} validation errors.", errors.Count);
            throw new PaymentValidationException(errors);
        }
    }

    private async Task<Payment> RequestBankAuthorizationAsync(PaymentRequest paymentRequest, CancellationToken cancellationToken)
    {
        logger.LogDebug("Requesting acquiring bank authorization.");
        bool authorized;
        try
        {
            authorized = await bankClient.AuthorizeAsync(paymentRequest, cancellationToken);
        }
        catch (BankException exception)
        {
            logger.LogError(exception, "Bank authorization failed with status code {StatusCode}.", exception.StatusCode);
            throw;
        }
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            Status = authorized ? PaymentStatus.Authorized : PaymentStatus.Declined,
            CardNumberLastFour = paymentRequest.CardNumber![^Constants.Payment.VisibleCardNumberDigits..],
            ExpiryMonth = paymentRequest.ExpiryMonth!.Value,
            ExpiryYear = paymentRequest.ExpiryYear!.Value,
            Currency = paymentRequest.Currency!,
            Amount = paymentRequest.Amount!.Value
        };
        return payment;
    }

    private static PaymentResponse ToResponse(Payment payment) => new()
    {
        Id = payment.Id,
        Status = payment.Status,
        CardNumberLastFour = payment.CardNumberLastFour,
        ExpiryMonth = payment.ExpiryMonth,
        ExpiryYear = payment.ExpiryYear,
        Currency = payment.Currency,
        Amount = payment.Amount
    };
}
