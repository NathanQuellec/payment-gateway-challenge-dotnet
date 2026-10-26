# Payment gateway

Requires the .NET 8 SDK pinned in global.json and Docker for the optional bank simulator.

## Run

```powershell
docker compose up -d
dotnet run --project src/PaymentGateway.Api --urls http://localhost:5000
dotnet test PaymentGateway.sln
```

The bank defaults to http://localhost:8080/. Override it using configuration key
Bank:BaseUrl (environment variable Bank__BaseUrl). The bank timeout is 10 seconds.
Tests use a controlled HTTP handler and do not require Docker.

## API

POST /api/payments with this JSON:

```json
{
  "cardNumber": "2222405343240001",
  "expiryMonth": 12,
  "expiryYear": 2030,
  "currency": "GBP",
  "amount": 100,
  "cvv": "012"
}
```

An authorized or declined payment returns 201 with its ID, status, last four card
digits, expiry, currency and amount. The Location header points to
GET /api/payments/{id}, which returns the same details. Unknown IDs return 404.

Invalid input returns 400 with paymentStatus "Rejected" and field errors. Rejected
requests do not call the bank and are not stored. Both malformed JSON and omitted
numeric fields are rejected.

Bank unavailability/network failure returns 503; timeout returns 504; unexpected
HTTP responses or malformed bank payloads return 502. These failures are not
declines and do not create stored payments. Caller cancellation is propagated.
An approval must include a nonblank bank authorization code; an approval without
one is treated as a malformed bank response (502). The code is not logged or
exposed in the public payment response.

The simulator in `imposters/bank_simulator.ejs` approves cards ending in an odd
digit, declines those ending in 2, 4, 6 or 8, and returns 503 for those ending in 0.
The bank client sends the request to the simulator rather than computing these
outcomes locally. Its request uses the simulator's snake_case field names and
formats the expiry date as MM/yyyy.

## Decisions and assumptions

- Cards remain valid through the end of their expiry month, using UTC.
- Currency must be uppercase GBP, USD, or EUR.
- Amount is a positive 32-bit integer in minor currency units.
- Card numbers and CVVs accept ASCII digits only; no Luhn check is required.
- Card numbers, CVVs and last four digits are strings to preserve leading zeroes.
- The service validates before calling a typed HttpClient adapter. Only HTTP I/O
  is asynchronous; validation and in-memory repository operations are synchronous.
- A singleton concurrent dictionary stores immutable Payment objects, separate
  from the PaymentResponse API contract. The service maps stored payments to
  responses. Full card
  numbers and CVVs are sent to the bank but never persisted or returned.
- Storage is local to the running process and is lost on restart.
- There are no automatic payment retries: a failed response can leave the bank
  outcome unknown, and retries require an idempotency contract.
- Authentication, merchant isolation, durable reconciliation and idempotency are
  outside this assessment's implementation.

## Verification

The GitHub Actions workflow in `.github/workflows/ci.yml` runs on pushes, pull
requests, manual dispatch and Mondays at 07:00 UTC. Independent jobs run the
tests, enforce compiler and .NET analyzer warnings as errors, audit direct and
transitive NuGet dependencies, and scan C# with CodeQL's security-extended suite.
The quality job enables build-time code style diagnostics; it does not enforce
whitespace formatting. Test results and coverage are retained for 14 days.

The dependency audit fails on known vulnerabilities of any severity or failure
to retrieve vulnerability information. Swashbuckle.AspNetCore 6.9.0,
Microsoft.NET.Test.Sdk 17.11.1, xunit 2.9.3 and its Visual Studio runner 2.8.2
resolve the previously reported Swagger UI and transitive test dependency
vulnerabilities. The full dependency audit passed after these updates.
CodeQL publishes findings to GitHub code
scanning; findings do not automatically fail the analysis job. Configure required
checks and code scanning merge protection in repository settings to enforce
merge restrictions. CodeQL is available for public repositories; private
repositories require GitHub Code Security.

Unit tests cover validation boundaries, bank decisions, rejection before bank
invocation, request mapping, malformed bank responses, transport failures and
cancellation. API integration tests exercise the real service, validator,
repository and HTTP bank adapter, including POST followed by GET, masking,
unknown IDs, invalid input and bank unavailability.
