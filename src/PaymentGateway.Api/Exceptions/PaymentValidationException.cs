namespace PaymentGateway.Api.Services;

public sealed class PaymentValidationException(Dictionary<string, string[]> errors)
    : Exception("The payment request is invalid.")
{
    public Dictionary<string, string[]> Errors { get; } = errors;
}
