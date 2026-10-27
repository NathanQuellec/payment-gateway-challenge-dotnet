using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Services;

public interface IPaymentsService
{
    PaymentResponse? GetPaymentDetails(Guid id);

    Task<PaymentResponse> ProcessPayment(
        PaymentRequest paymentRequest, CancellationToken cancellationToken = default);
}