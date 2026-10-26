using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Services;

public interface IBankClient
{
    Task<bool> AuthorizeAsync(PaymentRequest request, CancellationToken cancellationToken);
}