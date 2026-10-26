using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Services.Impl;

public class PaymentValidationService(TimeProvider timeProvider) : IPaymentValidationService
{
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

    private static void ValidateCardNumber(PaymentRequest request, Dictionary<string, string[]> errors)
    {
        if (!IsDigits(request.CardNumber, Constants.Payment.MinimumCardNumberLength, Constants.Payment.MaximumCardNumberLength))
            errors[nameof(request.CardNumber)] =
                [$"Card number must contain {Constants.Payment.MinimumCardNumberLength}-{Constants.Payment.MaximumCardNumberLength} ASCII digits."];
    }

    private static void ValidateCvv(PaymentRequest request, Dictionary<string, string[]> errors)
    {
        if (!IsDigits(request.Cvv, Constants.Payment.MinimumCvvLength, Constants.Payment.MaximumCvvLength))
            errors[nameof(request.Cvv)] =
                [$"CVV must contain {Constants.Payment.MinimumCvvLength}-{Constants.Payment.MaximumCvvLength} ASCII digits."];
    }

    private static void ValidateExpiryMonth(PaymentRequest request, Dictionary<string, string[]> errors)
    {
        if (request.ExpiryMonth is not (>= 1 and <= 12))
            errors[nameof(request.ExpiryMonth)] = ["Expiry month must be between 1 and 12."];
    }

    private void ValidateExpiryYear(PaymentRequest request, Dictionary<string, string[]> errors)
    {
        var now = timeProvider.GetUtcNow();
        if (request.ExpiryYear is not (>= 1 and <= 9999) || request.ExpiryYear < now.Year ||
            (request.ExpiryYear == now.Year && request.ExpiryMonth < now.Month))
            errors[nameof(request.ExpiryYear)] = ["Card must not be expired."];
    }

    private static void ValidateCurrency(PaymentRequest request, Dictionary<string, string[]> errors)
    {
        if (request.Currency is not ("GBP" or "USD" or "EUR"))
            errors[nameof(request.Currency)] = ["Currency must be GBP, USD, or EUR."];
    }

    private static void ValidateAmount(PaymentRequest request, Dictionary<string, string[]> errors)
    {
        if (request.Amount is null or <= 0)
            errors[nameof(request.Amount)] = ["Amount must be a positive integer in minor units."];
    }

    private static bool IsDigits(string? value, int minimum, int maximum) =>
        value is not null && value.Length >= minimum && value.Length <= maximum &&
        value.All(c => c is >= '0' and <= '9');
}
