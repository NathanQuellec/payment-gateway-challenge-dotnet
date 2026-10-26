using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using PaymentGateway.Api.Infrastructure;
using PaymentGateway.Api.Observability;

namespace PaymentGateway.Api.RateLimiting;

public sealed class PaymentRateLimitPolicy(IOptions<PaymentRateLimitOptions> options)
    : IRateLimiterPolicy<string>
{
    private readonly PaymentRateLimitOptions _options = options.Value;

    public RateLimitPartition<string> GetPartition(HttpContext httpContext)
    {
        var clientIp = httpContext.Connection.RemoteIpAddress?.MapToIPv6().ToString() ?? "unknown";
        var method = httpContext.Request.Method;

        return RateLimitPartition.GetFixedWindowLimiter($"{clientIp}:{method}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = HttpMethods.IsPost(method)
                ? _options.SubmissionPermitLimit
                : _options.LookupPermitLimit,
            Window = TimeSpan.FromSeconds(_options.WindowSeconds),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    }

    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => HandleRejectionAsync;

    private static async ValueTask HandleRejectionAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var response = context.HttpContext.Response;
        PaymentTelemetry.RecordRateLimitRejection();
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }

        await Results.Problem(
            statusCode: StatusCodes.Status429TooManyRequests,
            title: Constants.RateLimiting.RejectedProblemTitle,
            detail: Constants.RateLimiting.RejectedProblemDetail,
            extensions: new Dictionary<string, object?> { [Constants.Telemetry.TraceIdExtensionKey] = ApiProblems.TraceId(context.HttpContext) })
            .ExecuteAsync(context.HttpContext);
    }
}
