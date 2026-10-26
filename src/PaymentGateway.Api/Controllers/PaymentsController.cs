using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Controllers;

/// <summary>
/// Submit card payments and retrieve their processing results.
/// </summary>
[Route("api/[controller]")]
[ApiController]
[EnableRateLimiting(Constants.RateLimiting.PolicyName)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
public class PaymentsController(IPaymentsService paymentsService) : ControllerBase
{
    /// <summary>
    /// Retrieve a payment by its identifier.
    /// </summary>
    /// <remarks>
    /// Returns a previously processed payment with its Authorized or Declined status.
    /// Only the last four digits of the card number are returned; the full card number and CVV are omitted.
    /// </remarks>
    /// <param name="id">The payment identifier returned when the payment was created.</param>
    /// <returns>The stored payment details.</returns>
    /// <response code="200">The payment was found.</response>
    /// <response code="404">No payment exists with the supplied identifier.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public ActionResult<PaymentResponse> GetPaymentDetails(Guid id)
    {
        var payment = paymentsService.GetPaymentDetails(id);
        return payment is null ? NotFound() : Ok(payment);
    }

    /// <summary>
    /// Submit a card payment for authorization.
    /// </summary>
    /// <remarks>
    /// All payment fields are required. The card number must contain 14-19 ASCII digits and
    /// the CVV must contain 3-4 ASCII digits. The expiry month must be between 1 and 12,
    /// and the card must not be expired. Currency must be GBP, USD, or EUR.
    /// Amount must be a positive integer in minor units (for example, 1050 represents GBP 10.50).
    ///
    /// Example request:
    ///
    ///     POST /api/payments
    ///     {
    ///       "cardNumber": "2222405343248877",
    ///       "expiryMonth": 12,
    ///       "expiryYear": 2099,
    ///       "currency": "GBP",
    ///       "amount": 1050,
    ///       "cvv": "123"
    ///     }
    ///
    /// Both Authorized and Declined payments are stored and return 201, with a Location header
    /// pointing to the payment details. The response contains only the last four card digits and omits the CVV.
    /// Invalid requests return 400 with field errors and a paymentStatus extension of Rejected;
    /// they are not sent to the bank or stored. Bank failures do not create a payment.
    /// </remarks>
    /// <param name="request">The card details, currency, and amount to authorize.</param>
    /// <param name="cancellationToken">Cancels processing when the client disconnects.</param>
    /// <returns>The created payment and its authorization outcome.</returns>
    /// <response code="201">The payment was stored with an Authorized or Declined status. Location identifies its retrieval URL.</response>
    /// <response code="400">The request is malformed or fails validation. Field errors and paymentStatus Rejected are returned.</response>
    /// <response code="502">The acquiring bank returned an unexpected status or an invalid authorization response.</response>
    /// <response code="503">The acquiring bank is unavailable or could not be reached.</response>
    /// <response code="504">The acquiring bank did not respond before the timeout.</response>
    [HttpPost]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status504GatewayTimeout)]
    public async Task<ActionResult<PaymentResponse>> ProcessPaymentAsync(
        [FromBody] PaymentRequest request, CancellationToken cancellationToken)
    {
        var payment = await paymentsService.ProcessPayment(request, cancellationToken);
        return CreatedAtAction(nameof(GetPaymentDetails), new { id = payment.Id }, payment);
    }
}