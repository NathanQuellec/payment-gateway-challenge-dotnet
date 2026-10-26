using PaymentGateway.Api.Models;

namespace PaymentGateway.Api.Models.Responses;

public class PaymentResponse
{
    public Guid Id { get; init; }
    public PaymentStatus Status { get; init; }
    public string CardNumberLastFour { get; init; } = "";
    public int ExpiryMonth { get; init; }
    public int ExpiryYear { get; init; }
    public string Currency { get; init; } = "";
    public int Amount { get; init; }
}

