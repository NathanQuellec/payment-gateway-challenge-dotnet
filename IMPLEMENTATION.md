# Implementation notes

## Main choices

- **Simple layered design:** controller → payment service → validator, bank client and repository. Validation happens before any bank call.
- **Bank integration:** a typed `HttpClient` calls the bank simulator with a 10-second timeout. Authorized and declined payments are stored; invalid requests and bank failures are not.
- **In-memory storage:** a concurrent dictionary stores immutable payments. Full card numbers and CVVs are never stored or returned; responses expose only the last four digits.
- **Synchronous lookups:** in-memory reads need no async I/O. With a real database, use async queries through the repository, service and controller to free request threads while waiting.
- **Consistent errors:** centralized problem responses distinguish validation (400), unavailable bank (503), timeout (504) and invalid bank responses (502). Errors include a trace ID.
- **No automatic retries:** a lost bank response can leave the outcome unknown. Safe retries need an agreed idempotency contract.
- **Rate limiter:** configurable per-IP limits protect payment submissions and lookups, returning 429 with `Retry-After` when exceeded.
- **OpenTelemetry:** request, bank-call and payment metrics/traces are exported over OTLP to the collector.
- **Prometheus:** scrapes metrics exposed by the collector and stores them for querying.
- **Grafana:** a provisioned dashboard visualizes Prometheus metrics for API traffic and payment processing.

## Assumptions and limits

- Amounts are positive 32-bit integers in minor currency units; supported currencies are uppercase GBP, USD and EUR.
- Cards remain valid through the end of their expiry month in UTC. Card numbers and CVVs use ASCII digits.
- Storage is per process and lost on restart. Authentication, merchant isolation, reconciliation and idempotency are outside this implementation.
- Rate limits are per IP and API process: 10 submissions and 60 lookups per minute by default. Clients sharing an IP share a quota.
- Health checks confirm API responsiveness, not bank availability.

## Key improvements implemented

- Input validation, masked payment responses and sensitive-data suppression in logs and errors.
- Centralized error handling, trace correlation, structured logs and OpenTelemetry metrics/traces.
- Configurable rate limiting with 429 responses and `Retry-After`.
- Non-root Docker packaging, local Compose stack, health checks and a Grafana metrics dashboard.
- Unit and API integration tests with controlled bank responses; CI builds, tests, audits dependencies and runs CodeQL.

## C# integration tests

`Integration/PaymentFlowTests.cs` runs the real bank HTTP client against a local
Kestrel stub on an automatically assigned loopback port. The API uses its real
controller, validator, payment service, error handler and in-memory repository.
Tests verify the bank request contract, every public payment field, all three
currencies, independent retrieval of multiple payments, validation before bank
calls, bank failures and an actual HTTP timeout. Each test disposes its API host
and bank stub. No packages or Docker services are required beyond the existing
test dependencies. The API is hosted in-process; the bank connection uses real
HTTP. This does not verify the supplied bank simulator or Docker packaging.

Run only this group with `dotnet test PaymentGateway.sln --filter Category=Integration`.

## Next improvements

- Durable storage and reconciliation for pending or unknown bank outcomes.
- End-to-end idempotency before introducing retries.
- Authentication, merchant ownership checks and secrets management.
- AWS deployment through Terraform and GitHub Actions.
- Retained traces, alerts and service-level objectives.
- Shared rate limits and realistic load testing across API instances.

See [README.md](README.md) for setup, commands and API examples.
