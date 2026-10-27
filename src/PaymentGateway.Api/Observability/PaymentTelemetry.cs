using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace PaymentGateway.Api.Observability;

public static class PaymentTelemetry
{
    public static readonly ActivitySource Activities = new(Constants.Telemetry.ServiceName);
    private static readonly Meter Meter = new(Constants.Telemetry.ServiceName);
    private static readonly Counter<long> Payments = Meter.CreateCounter<long>(Constants.Telemetry.PaymentsCounterName, "{payment}");
    private static readonly Histogram<double> BankDuration = Meter.CreateHistogram<double>(Constants.Telemetry.BankDurationHistogramName, "s");
    private static readonly Counter<long> RateLimitRejections = Meter.CreateCounter<long>(Constants.Telemetry.RateLimitRejectionsCounterName, "{request}");

    public static void RecordPayment(string outcome) => Payments.Add(1, new KeyValuePair<string, object?>(Constants.Telemetry.OutcomeTagName, outcome));
    public static void RecordBankDuration(TimeSpan elapsed) => BankDuration.Record(elapsed.TotalSeconds);
    public static void RecordRateLimitRejection() => RateLimitRejections.Add(1);
}