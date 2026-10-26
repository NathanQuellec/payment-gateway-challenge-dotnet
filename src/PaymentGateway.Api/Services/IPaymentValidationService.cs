using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Services;

public interface IPaymentValidationService
{
    Dictionary<string, string[]> Validate(PaymentRequest request);
}
