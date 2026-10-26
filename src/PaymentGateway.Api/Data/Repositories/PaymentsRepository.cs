using System.Collections.Concurrent;
using PaymentGateway.Api.Models;

namespace PaymentGateway.Api.Repositories;

public class PaymentsRepository
{
    private readonly ConcurrentDictionary<Guid, Payment> _payments = new();

    public void Add(Payment payment)
    {
        if (!_payments.TryAdd(payment.Id, payment))
            throw new InvalidOperationException("A payment with this ID already exists.");
    }

    public Payment? GetPaymentById(Guid id) => _payments.GetValueOrDefault(id);
}
