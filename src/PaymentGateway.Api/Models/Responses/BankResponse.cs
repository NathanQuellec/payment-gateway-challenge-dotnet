using System.Text.Json.Serialization;

namespace PaymentGateway.Api.Models.Responses;

internal sealed class BankResponse
{
    [JsonPropertyName("authorized")]
    public bool? Authorized { get; init; }

    [JsonPropertyName("authorization_code")]
    public string? AuthorizationCode { get; init; }
}