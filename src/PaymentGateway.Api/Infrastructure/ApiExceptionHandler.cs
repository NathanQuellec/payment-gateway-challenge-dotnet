using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PaymentGateway.Api.Exceptions;

namespace PaymentGateway.Api.Infrastructure;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
            return false;

        ProblemDetails problem = exception switch
        {
            PaymentValidationException validation => ApiProblems.Rejected(validation.Errors),
            BankException bank => new ProblemDetails { Status = bank.StatusCode, Title = bank.Message },
            _ => new ProblemDetails { Status = StatusCodes.Status500InternalServerError, Title = Constants.Errors.UnexpectedErrorTitle }
        };

        // Exception messages may contain submitted values. Log only the error type and trace context.
        if (problem.Status >= StatusCodes.Status500InternalServerError)
            logger.LogError("Request failed with {ErrorType} and HTTP {StatusCode}.", exception.GetType().Name, problem.Status);

        problem.Extensions[Constants.Telemetry.TraceIdExtensionKey] = ApiProblems.TraceId(context);
        context.Response.StatusCode = problem.Status!.Value;
        await context.Response.WriteAsJsonAsync(problem, options: null,
            contentType: System.Net.Mime.MediaTypeNames.Application.ProblemJson, cancellationToken: cancellationToken);
        return true;
    }
}
