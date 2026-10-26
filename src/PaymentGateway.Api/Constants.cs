namespace PaymentGateway.Api;

internal static class Constants
{
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
