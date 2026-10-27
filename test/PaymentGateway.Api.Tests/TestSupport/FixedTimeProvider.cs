namespace PaymentGateway.Api.Tests.TestSupport;

internal sealed class FixedTimeProvider : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(2030, 10, 15, 0, 0, 0, TimeSpan.Zero);
}