using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Predicates;

public sealed class PaymentValidationPredicates(TimeProvider timeProvider)
{
    public bool IsInvalidCardNumber(string? value) =>
        !IsDigits(value, Constants.Payment.MinimumCardNumberLength, Constants.Payment.MaximumCardNumberLength);
    public bool IsInvalidCvv(string? value) =>
        !IsDigits(value, Constants.Payment.MinimumCvvLength, Constants.Payment.MaximumCvvLength);
    public bool IsInvalidCurrency(string? value) => value is not ("GBP" or "USD" or "EUR");
    public bool IsInvalidAmount(int? value) => value is null or <= 0;

    public bool IsInvalidExpiryMonth(int? value) => value is not (>= 1 and <= 12);
    public bool IsInvalidExpiryYear(int? value) => value is not (>= 1 and <= 9999);

    public bool IsExpired(PaymentRequest payment)
    {
        var now = timeProvider.GetUtcNow();
        return payment.ExpiryYear < now.Year ||
            (payment.ExpiryYear == now.Year && payment.ExpiryMonth < now.Month);
    }

    private static bool IsDigits(string? value, int minimum, int maximum) =>
        value is not null && value.Length >= minimum && value.Length <= maximum &&
        value.All(c => c is >= '0' and <= '9');
}
