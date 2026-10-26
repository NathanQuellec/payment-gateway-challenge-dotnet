using System.ComponentModel.DataAnnotations;

namespace PaymentGateway.Api.RateLimiting;

public sealed class PaymentRateLimitOptions
{
    [Range(1, int.MaxValue)]
    public int SubmissionPermitLimit { get; set; } = 10;

    [Range(1, int.MaxValue)]
    public int LookupPermitLimit { get; set; } = 60;

    [Range(1, int.MaxValue)]
    public int WindowSeconds { get; set; } = 60;
}
