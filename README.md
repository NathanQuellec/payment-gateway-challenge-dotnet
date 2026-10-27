# Payment gateway

[![CI](https://github.com/NathanQuellec/payment-gateway-challenge-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/NathanQuellec/payment-gateway-challenge-dotnet/actions/workflows/ci.yml)
[![CodeQL](https://github.com/NathanQuellec/payment-gateway-challenge-dotnet/actions/workflows/codeql.yml/badge.svg)](https://github.com/NathanQuellec/payment-gateway-challenge-dotnet/actions/workflows/codeql.yml)

See [IMPLEMENTATION.md](IMPLEMENTATION.md) for design choices, assumptions and
future improvements.
## Run

Requires Docker with Compose v2:

```sh
docker compose up --build -d --wait
```

- [Swagger / try the API](http://localhost:15000/swagger/index.html)
- [Grafana dashboard](http://localhost:15300/d/payment-gateway/payment-gateway) — no login required; send payments and allow 10–15 seconds for charts.
- [Health check](http://localhost:15000/health)
- [OpenTelemetry metrics](http://localhost:9464/metrics)


Ports bind to localhost. If port 15000 is occupied, set `API_PORT` before startup
(for example, `$env:API_PORT='15001'` in PowerShell).

## API

Submit a payment through Swagger or with Bash:

```sh
curl -i http://localhost:15000/api/payments \
  -H 'Content-Type: application/json' \
  -d '{"cardNumber":"2222405343240001","expiryMonth":12,"expiryYear":2099,"currency":"GBP","amount":100,"cvv":"012"}'
```

Retrieve it with `GET /api/payments/{id}` using the returned ID or `Location`
header. Responses include only the last four card digits and omit the CVV.
Amounts use minor currency units: `100` GBP means £1.00.

| Scenario | Result |
| --- | --- |
| Simulator card ends in 1, 3, 5, 7 or 9 | 201, `Authorized` |
| Simulator card ends in 2, 4, 6 or 8 | 201, `Declined` |
| Simulator card ends in 0 | 503, bank unavailable |
| Invalid input | 400 |
| Unknown payment ID | 404 |
| Rate limit exceeded | 429 with `Retry-After` |
| Invalid bank response / bank timeout | 502 / 504 |

Both authorized and declined payments are stored. Validation and bank failures
are not. Errors include a `traceId` for log correlation. A timeout can leave the
bank outcome unknown; automatic payment retries are disabled.

## Local development and tests

Requires the .NET 8 SDK specified in `global.json`:

```sh
docker compose up -d bank_simulator otel-collector
dotnet run --project src/PaymentGateway.Api --urls http://localhost:15000
```

Tests run independently of Docker or a running API:

```sh
dotnet test PaymentGateway.sln --configuration Release
```

To run only integration tests, add `--filter Category=Integration`. Tests use
controlled bank responses, including a local HTTP stub. CI builds, runs tests
with coverage, audits dependencies and runs CodeQL.

## Configuration

Compose configures bank and telemetry connections automatically. Main overrides:

| Environment variable | Purpose / default |
| --- | --- |
| `Bank__BaseUrl` | Bank URL; requests time out after 10 seconds |
| `RateLimiting__Payments__SubmissionPermitLimit` | 10 submissions per IP per window |
| `RateLimiting__Payments__LookupPermitLimit` | 60 lookups per IP per window |
| `RateLimiting__Payments__WindowSeconds` | 60 seconds |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | OpenTelemetry collector endpoint |

