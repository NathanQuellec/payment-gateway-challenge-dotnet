using Microsoft.AspNetCore.Mvc;

using PaymentGateway.Api.Exceptions;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PaymentsController(IPaymentsService paymentsService) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public ActionResult<PaymentResponse> GetPaymentDetails(Guid id)
    {
        var payment = paymentsService.GetPaymentDetails(id);
        return payment is null ? NotFound() : Ok(payment);
    }

    [HttpPost]
    public async Task<ActionResult<PaymentResponse>> ProcessPaymentAsync(
        [FromBody] PaymentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var payment = await paymentsService.ProcessPayment(request, cancellationToken);
            return CreatedAtAction(nameof(GetPaymentDetails), new { id = payment.Id }, payment);
        }
        catch (PaymentValidationException exception)
        {
            var problem = new ValidationProblemDetails(exception.Errors)
            {
                Status = 400,
                Title = "The payment request was rejected."
            };
            problem.Extensions["paymentStatus"] = "Rejected";
            return BadRequest(problem);
        }
        catch (BankException exception)
        {
            return Problem(statusCode: exception.StatusCode, title: exception.Message);
        }
    }
}

