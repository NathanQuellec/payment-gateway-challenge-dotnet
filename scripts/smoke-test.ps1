param([string]$BaseUrl = 'http://localhost:5000')
$ErrorActionPreference = 'Stop'
function Get-ResponseText($response) {
    if ($response.Content -is [byte[]]) { return [System.Text.Encoding]::UTF8.GetString($response.Content) }
    return [string]$response.Content
}
Invoke-RestMethod "$BaseUrl/health/ready" | Out-Null
foreach ($scenario in @(@{ ending = '1'; expected = 'Authorized' }, @{ ending = '2'; expected = 'Declined' })) {
    $body = @{
        cardNumber = '222240534324000' + $scenario.ending
        expiryMonth = 12; expiryYear = 2099; currency = 'GBP'; amount = 100; cvv = '012'
    } | ConvertTo-Json
    $response = Invoke-WebRequest "$BaseUrl/api/payments" -Method Post -ContentType 'application/json' -Body $body
    $responseText = Get-ResponseText $response
    $payment = $responseText | ConvertFrom-Json
    if ($response.StatusCode -ne 201 -or $payment.status -ne $scenario.expected) { throw 'Unexpected payment outcome.' }
    $stored = Invoke-RestMethod "$BaseUrl/api/payments/$($payment.id)"
    if ($stored.status -ne $scenario.expected -or $stored.cardNumberLastFour -ne ('000' + $scenario.ending)) { throw 'Payment retrieval failed.' }
    if ($responseText.Contains('222240534324000') -or $responseText.Contains('"cvv"')) { throw 'Sensitive data returned.' }
}
$invalid = Invoke-WebRequest "$BaseUrl/api/payments" -Method Post -ContentType 'application/json' -Body '{"cardNumber":2222405343240001}' -SkipHttpErrorCheck
$invalidText = Get-ResponseText $invalid
if ($invalid.StatusCode -ne 400 -or $invalidText.Contains('2222405343240001')) { throw 'Invalid request was not safely rejected.' }
$problem = $invalidText | ConvertFrom-Json
if ($problem.traceId -ne ($invalid.Headers['X-Trace-Id'] -join '')) { throw 'Error trace identifier does not match the response header.' }
$unavailable = @{
    cardNumber = '2222405343240000'; expiryMonth = 12; expiryYear = 2099; currency = 'GBP'; amount = 100; cvv = '012'
} | ConvertTo-Json
$failure = Invoke-WebRequest "$BaseUrl/api/payments" -Method Post -ContentType 'application/json' -Body $unavailable -SkipHttpErrorCheck
if ($failure.StatusCode -ne 503) { throw 'Bank unavailability was not propagated as HTTP 503.' }
Write-Output 'Container smoke tests passed: health, authorization, decline, retrieval, masking, validation, tracing and bank unavailability.'
