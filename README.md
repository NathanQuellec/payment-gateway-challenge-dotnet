# Payment gateway

[![CI](https://github.com/NathanQuellec/payment-gateway-challenge-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/NathanQuellec/payment-gateway-challenge-dotnet/actions/workflows/ci.yml)
[![CodeQL](https://github.com/NathanQuellec/payment-gateway-challenge-dotnet/actions/workflows/codeql.yml/badge.svg)](https://github.com/NathanQuellec/payment-gateway-challenge-dotnet/actions/workflows/codeql.yml)

An ASP.NET Core API that validates card payments, requests authorization from an
acquiring bank, and retrieves stored outcomes. This challenge implementation uses
an in-memory repository and a bank simulator. Use synthetic card data only.

## Start the complete application

Requires Docker with Compose v2:

```sh
docker compose up --build -d --wait
curl --fail http://localhost:5000/health/ready
```

| Service | Address |
| --- | --- |
| API | http://localhost:5000/api/payments |
| Swagger UI | http://localhost:5000/swagger |
| Grafana dashboard | http://localhost:15300/d/payment-gateway/payment-gateway |
| Prometheus query UI | http://localhost:9090 |
| Liveness / readiness | http://localhost:5000/health/live / http://localhost:5000/health/ready |
| Prometheus metrics | http://localhost:9464/metrics |
| Bank simulator | http://localhost:8080 |
| Simulator administration | http://localhost:2525 |

All published ports bind to localhost. Set `API_PORT`, `BANK_PORT`,
`BANK_ADMIN_PORT`, `METRICS_PORT`, or `OTLP_PORT` before starting Compose if a port is occupied.
The dashboard ports can also be changed with `GRAFANA_PORT` and `PROMETHEUS_PORT`.
For example, `API_PORT=5001 docker compose up --build -d` on Bash, or
`$env:API_PORT='5001'` before the same command in PowerShell.

```sh
docker compose logs -f api otel-collector
docker compose down
```

Stopping or replacing the API loses stored payments. Swagger is enabled in the
local Compose development environment and disabled in Production.

## Metrics dashboard

Open http://localhost:15300/d/payment-gateway/payment-gateway for the preconfigured
Grafana dashboard. Local anonymous access is read-only; no login is needed to view
charts. The dashboard shows payment outcomes, bank p95 latency, HTTP traffic and
latency, rate-limit rejections and collector scrape health. Send payment requests
and allow 10-15 seconds for export/scraping. Rate charts require at least two
scrapes; request counters reset when the API restarts.

To add the UI to an already running API/collector stack:

```sh
docker compose up -d --wait prometheus grafana
```

Prometheus retains metrics for up to 24 hours, subject to a 256 MB retention cap.
Named Docker volumes preserve dashboard settings and metric history across
container restarts. Dashboard JSON and data sources are provisioned from
`observability/grafana/`; edit those files to change the shared dashboard.

The UI binds to localhost. Administration uses username `admin` and the local
demo password `local-demo-change-me`; set `GRAFANA_ADMIN_PASSWORD` before the
first startup to override it. Once the Grafana volume exists, password changes
must be made through Grafana administration. Trace summaries are still available
through `docker compose logs -f otel-collector`.

## Run with the .NET SDK

The local SDK is pinned by `global.json` to .NET 8.0.205. The container uses the
serviced .NET 8 SDK and ASP.NET runtime images.

```sh
docker compose up -d bank_simulator otel-collector
dotnet run --project src/PaymentGateway.Api --urls http://localhost:5000
dotnet test PaymentGateway.sln
```

## API examples

Submit a synthetic payment (Bash; use `curl.exe` instead of PowerShell's curl alias):

```sh
curl -i http://localhost:5000/api/payments \
  -H 'Content-Type: application/json' \
  -d '{"cardNumber":"2222405343240001","expiryMonth":12,"expiryYear":2099,"currency":"GBP","amount":100,"cvv":"012"}'
```

The response is `201 Created`, with a `Location` header identifying the retrieval
URL. Both approvals and declines create a stored payment. Follow the returned
location or call `GET /api/payments/{id}`. Responses contain only the last four
card digits and omit the CVV.

| Scenario | Input | Expected result |
| --- | --- | --- |
| Approved | Card ending in 1, 3, 5, 7, or 9 | 201, `Authorized` |
| Declined | Card ending in 2, 4, 6, or 8 | 201, `Declined` |
| Bank unavailable | Card ending in 0 | 503 |
| Validation failure | Empty object `{}` | 400, `paymentStatus: Rejected` |
| Invalid JSON/type | `{"amount":"invalid"}` | 400, sanitized field errors |
| Unknown payment | GET with an unknown GUID | 404 |
| Rate limit reached | More than the configured allowance | 429 and `Retry-After` |

The card-ending rules belong to the simulator. The API always asks the bank for
the decision. Amounts are positive integers in minor currency units: `100` means
GBP 1.00 for currency GBP.

Error responses use problem details and carry `traceId`. The matching
`X-Trace-Id` response header links errors to structured application logs. Bank
errors return 502 for invalid responses, 503 for unavailability/transport failure,
and 504 for timeout. A timeout leaves the bank outcome unknown; do not blindly
retry a payment.

## Configuration

| Setting | Default / purpose |
| --- | --- |
| `Bank__BaseUrl` | `http://localhost:8080/`; Compose uses its bank service hostname |
| Bank timeout | 10 seconds |
| `RateLimiting__Payments__SubmissionPermitLimit` | 10 POST requests per client IP per window |
| `RateLimiting__Payments__LookupPermitLimit` | 60 GET requests per client IP per window |
| `RateLimiting__Payments__WindowSeconds` | 60 seconds |
| `Hosting__UseHttpsRedirection` | true; false inside the local HTTP container |
| `Hosting__KnownProxyNetworks__0` | Optional trusted proxy CIDR |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Collector URL: IDE profiles use `http://localhost:4317`, Compose uses `http://otel-collector:4317`; unset disables export |
| `OTEL_METRIC_EXPORT_INTERVAL` | Export interval in milliseconds; Compose uses 5000 |

See [IMPLEMENTATION.md](IMPLEMENTATION.md) for telemetry names, tradeoffs,
limitations and future improvements.

## Tests and CI

```sh
dotnet test PaymentGateway.sln --configuration Release
```

The .NET tests use controlled HTTP handlers and need no Docker. After starting the
Compose stack, run the real simulator smoke tests with PowerShell 7:

```sh
pwsh -File scripts/smoke-test.ps1
```

Use `-BaseUrl http://localhost:5001` when overriding the API port. The smoke script
tests approval, decline, retrieval, masking, invalid input, trace correlation and
bank unavailability. Use a fresh API instance if earlier requests exhausted its
rate limit.

GitHub Actions builds and analyzes the solution, runs tests with coverage,
security-audits dependencies, scans C# with CodeQL, and builds/runs the container
stack against the real bank simulator. Test reports are retained for 14 days.
