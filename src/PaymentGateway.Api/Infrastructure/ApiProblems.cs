using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Observability;

namespace PaymentGateway.Api.Infrastructure;

internal static class ApiProblems
{
    private static readonly string[] RequestFields = typeof(PaymentRequest).GetProperties()
        .Select(property => property.Name).ToArray();

    public static IActionResult InvalidModelState(ActionContext context)
    {
        // Never echo binding messages or arbitrary submitted JSON property names.
        var errors = context.ModelState.Where(entry => entry.Value?.Errors.Count > 0)
            .Select(entry => RequestFields.FirstOrDefault(field =>
                string.Equals(entry.Key, field, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(entry.Key, $"$.{field}", StringComparison.OrdinalIgnoreCase)) ?? Constants.Errors.FallbackValidationField)
            .Distinct().ToDictionary(field => field, _ => new[] { Constants.Errors.MissingOrInvalidValueMessage });
        var problem = Rejected(errors);
        problem.Extensions[Constants.Telemetry.TraceIdExtensionKey] = TraceId(context.HttpContext);
        PaymentTelemetry.RecordPayment(Constants.Telemetry.RejectedOutcome);
        return new BadRequestObjectResult(problem);
    }

    public static string TraceId(HttpContext context) => Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;

    public static ValidationProblemDetails Rejected(Dictionary<string, string[]> errors) => new(errors)
    {
        Status = StatusCodes.Status400BadRequest,
        Title = Constants.Payment.RejectedProblemTitle,
        Extensions = { [Constants.Payment.StatusExtensionKey] = nameof(PaymentStatus.Rejected) }
    };
}
