using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text.Json;

using PaymentGateway.Api.Exceptions;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Services.impl;

public sealed class BankClientService(HttpClient httpClient, ILogger<BankClientService> logger) : IBankClient
{
    public async Task<bool> AuthorizeAsync(PaymentRequest request, CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        logger.LogDebug("Sending authorization request to the acquiring bank.");
        try
        {
            return await SendAuthorizationAsync(request, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Acquiring bank authorization request timed out.");
            throw new BankException(StatusCodes.Status504GatewayTimeout);
        }
        catch (OperationCanceledException)
        {
            logger.LogDebug("Acquiring bank authorization request was canceled by the caller.");
            throw;
        }
        catch (HttpRequestException)
        {
            logger.LogWarning("Acquiring bank authorization request failed due to a transport error.");
            throw new BankException(StatusCodes.Status503ServiceUnavailable);
        }
        catch (JsonException)
        {
            logger.LogWarning("Acquiring bank returned an invalid JSON response.");
            throw new BankException(StatusCodes.Status502BadGateway);
        }
        catch (NotSupportedException)
        {
            logger.LogWarning("Acquiring bank returned an unsupported response format.");
            throw new BankException(StatusCodes.Status502BadGateway);
        }
        finally
        {
            logger.LogDebug("Acquiring bank authorization request completed in {ElapsedMilliseconds} ms.",
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
    }

    private async Task<bool> SendAuthorizationAsync(PaymentRequest request, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(
            Constants.Bank.PaymentsPath, CreateBankRequest(request), cancellationToken);

        EnsureSuccessfulResponse(response);
        return await ReadAuthorizationDecisionAsync(response, cancellationToken);
    }

    private static BankRequest CreateBankRequest(PaymentRequest request) => new()
    {
        CardNumber = request.CardNumber,
        ExpiryDate = string.Format(CultureInfo.InvariantCulture, Constants.Bank.ExpiryDateFormat,
            request.ExpiryMonth, request.ExpiryYear),
        Currency = request.Currency,
        Amount = request.Amount,
        Cvv = request.Cvv
    };

    private void EnsureSuccessfulResponse(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return;

        logger.LogWarning("Acquiring bank returned HTTP status {StatusCode}.", (int)response.StatusCode);
        throw new BankException(response.StatusCode == HttpStatusCode.ServiceUnavailable
            ? StatusCodes.Status503ServiceUnavailable
            : StatusCodes.Status502BadGateway);
    }

    private async Task<bool> ReadAuthorizationDecisionAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var result = await response.Content.ReadFromJsonAsync<BankResponse>(cancellationToken);
        if (result?.Authorized is not { } authorized)
        {
            logger.LogWarning("Acquiring bank response did not contain an authorization decision.");
            throw new BankException(StatusCodes.Status502BadGateway);
        }

        if (authorized && string.IsNullOrWhiteSpace(result.AuthorizationCode))
        {
            logger.LogWarning("Acquiring bank approved the payment without an authorization code.");
            throw new BankException(StatusCodes.Status502BadGateway);
        }

        logger.LogDebug("Acquiring bank returned authorization decision {Authorized}.", authorized);
        return authorized;
    }
}
