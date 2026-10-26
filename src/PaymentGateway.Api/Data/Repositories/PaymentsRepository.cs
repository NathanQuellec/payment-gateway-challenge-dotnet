using System.Collections.Concurrent;
using PaymentGateway.Api.Models;

namespace PaymentGateway.Api.Data.Repositories;

public class PaymentsRepository
{
    private readonly ConcurrentDictionary<Guid, Payment> _payments = new();

    public void AddPaymentRecord(Payment payment)
    {
        if (!_payments.TryAdd(payment.Id, payment))
            throw new InvalidOperationException("A payment with this ID already exists.");
    }

    public Payment? GetPaymentRecordById(Guid id) => _payments.GetValueOrDefault(id);
}
