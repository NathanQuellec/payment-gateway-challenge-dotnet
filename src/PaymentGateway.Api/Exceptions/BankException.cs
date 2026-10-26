namespace PaymentGateway.Api.Exceptions;

public sealed class BankException(int statusCode) : Exception("The acquiring bank could not process the request.")
{
    public int StatusCode { get; } = statusCode;
}
