using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Predicates;

public sealed class PaymentValidationPredicates(TimeProvider timeProvider)
{
    public Predicate<string?> IsInvalidCardNumber { get; } = value =>
        !IsDigits(value, Constants.Payment.MinimumCardNumberLength, Constants.Payment.MaximumCardNumberLength);
    public Predicate<string?> IsInvalidCvv { get; } = value =>
        !IsDigits(value, Constants.Payment.MinimumCvvLength, Constants.Payment.MaximumCvvLength);
    public Predicate<string?> IsInvalidCurrency { get; } = value => value is not ("GBP" or "USD" or "EUR");
    public Predicate<int?> IsInvalidAmount { get; } = value => value is null or <= 0;

    public Predicate<int?> IsInvalidExpiryMonth { get; } = value => value is not (>= 1 and <= 12);
    public Predicate<int?> IsInvalidExpiryYear { get; } = value => value is not (>= 1 and <= 9999);

    public Predicate<PaymentRequest> IsExpired { get; } = payment =>
    {
        var now = timeProvider.GetUtcNow();
        return payment.ExpiryYear < now.Year ||
            (payment.ExpiryYear == now.Year && payment.ExpiryMonth < now.Month);
    };

    private static bool IsDigits(string? value, int minimum, int maximum) =>
        value is not null && value.Length >= minimum && value.Length <= maximum &&
        value.All(c => c is >= '0' and <= '9');
}
