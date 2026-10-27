namespace PaymentGateway.Api;

internal static class Constants
{
    internal static class Telemetry
    {
        public const string ServiceName = "PaymentGateway.Api";
        public const string OtlpEndpointConfigurationKey = "OTEL_EXPORTER_OTLP_ENDPOINT";
        public const string TraceIdExtensionKey = "traceId";
        public const string TraceIdHeaderName = "X-Trace-Id";
        public const string TraceIdLogProperty = "TraceId";
        public const string ExceptionMiddlewareLogCategory = "Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware";
        public const string ProcessPaymentActivityName = "payment.process";
        public const string PaymentsCounterName = "payments.processed";
        public const string BankDurationHistogramName = "bank.authorization.duration";
        public const string RateLimitRejectionsCounterName = "payments.rate_limit.rejections";
        public const string OutcomeTagName = "outcome";
        public const string AuthorizedOutcome = "authorized";
        public const string DeclinedOutcome = "declined";
        public const string RejectedOutcome = "rejected";
        public const string BankFailureOutcome = "bank_failure";
    }

    internal static class Hosting
    {
        public const string UseHttpsRedirectionConfigurationKey = "Hosting:UseHttpsRedirection";
        public const string HealthPath = "/health";
    }

    internal static class Errors
    {
        public const string UnexpectedErrorTitle = "An unexpected error occurred.";
        public const string MissingOrInvalidValueMessage = "A required value is missing or invalid.";
        public const string FallbackValidationField = "request";
    }

    internal static class RateLimiting
    {
        public const string PolicyName = "payments";
        public const string ConfigurationSection = "RateLimiting:Payments";
        public const string RejectedProblemTitle = "Too many requests.";
        public const string RejectedProblemDetail = "The request limit has been reached. Retry after the indicated delay.";
    }

    internal static class Payment
    {
        public const int MinimumCardNumberLength = 14;
        public const int MaximumCardNumberLength = 19;
        public const int MinimumCvvLength = 3;
        public const int MaximumCvvLength = 4;
        public const int VisibleCardNumberDigits = 4;

        public const string RejectedProblemTitle = "The payment request was rejected.";
        public const string StatusExtensionKey = "paymentStatus";
    }

    internal static class Bank
    {
        public const string BaseUrlConfigurationKey = "Bank:BaseUrl";
        public const string DefaultBaseUrl = "http://localhost:8080/";
        public const int TimeoutSeconds = 10;
        public const string PaymentsPath = "payments";
        public const string ExpiryDateFormat = "{0:00}/{1:0000}";
    }
}
