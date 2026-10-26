using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Predicates;

namespace PaymentGateway.Api.Services.impl;

public class PaymentValidationService(TimeProvider timeProvider) : IPaymentValidationService
{
    private readonly PaymentValidationPredicates _predicates = new(timeProvider);

    public Dictionary<string, string[]> Validate(PaymentRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        ValidateCardNumber(request, errors);
        ValidateCvv(request, errors);
        ValidateExpiryMonth(request, errors);
        ValidateExpiryYear(request, errors);
        ValidateCurrency(request, errors);
        ValidateAmount(request, errors);
        return errors;
    }

    private void ValidateCardNumber(PaymentRequest request, Dictionary<string, string[]> errors)
    {
        if (_predicates.IsInvalidCardNumber(request.CardNumber))
            errors[nameof(request.CardNumber)] =
                [$"Card number must contain {Constants.Payment.MinimumCardNumberLength}-{Constants.Payment.MaximumCardNumberLength} ASCII digits."];
    }

    private void ValidateCvv(PaymentRequest request, Dictionary<string, string[]> errors)
    {
        if (_predicates.IsInvalidCvv(request.Cvv))
            errors[nameof(request.Cvv)] =
                [$"CVV must contain {Constants.Payment.MinimumCvvLength}-{Constants.Payment.MaximumCvvLength} ASCII digits."];
    }

    private void ValidateExpiryMonth(PaymentRequest request, Dictionary<string, string[]> errors)
    {
        if (_predicates.IsInvalidExpiryMonth(request.ExpiryMonth))
            errors[nameof(request.ExpiryMonth)] = ["Expiry month must be between 1 and 12."];
    }

    private void ValidateExpiryYear(PaymentRequest request, Dictionary<string, string[]> errors)
    {
        if (_predicates.IsInvalidExpiryYear(request.ExpiryYear) || _predicates.IsExpired(request))
            errors[nameof(request.ExpiryYear)] = ["Card must not be expired."];
    }

    private void ValidateCurrency(PaymentRequest request, Dictionary<string, string[]> errors)
    {
        if (_predicates.IsInvalidCurrency(request.Currency))
            errors[nameof(request.Currency)] = ["Currency must be GBP, USD, or EUR."];
    }

    private void ValidateAmount(PaymentRequest request, Dictionary<string, string[]> errors)
    {
        if (_predicates.IsInvalidAmount(request.Amount))
            errors[nameof(request.Amount)] = ["Amount must be a positive integer in minor units."];
    }
}


