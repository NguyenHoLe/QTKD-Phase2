param(
    [string]$BaseUrl = "http://localhost:5209"
)

$ErrorActionPreference = "Stop"

function Get-OutputLogPath {
    param([Parameter(Mandatory)][string]$PreferredName)
    $path = Join-Path $PSScriptRoot $PreferredName
    try {
        $stream = [System.IO.File]::Open(
            $path,
            [System.IO.FileMode]::Create,
            [System.IO.FileAccess]::Write,
            [System.IO.FileShare]::None)
    }
    catch [System.IO.IOException] {
        $baseName = [System.IO.Path]::GetFileNameWithoutExtension($PreferredName)
        $extension = [System.IO.Path]::GetExtension($PreferredName)
        $runId = [Guid]::NewGuid().ToString("N").Substring(0, 8)
        $path = Join-Path $PSScriptRoot "$baseName-$runId$extension"
        $stream = [System.IO.File]::Open(
            $path,
            [System.IO.FileMode]::CreateNew,
            [System.IO.FileAccess]::Write,
            [System.IO.FileShare]::None)
    }
    $stream.Dispose()
    return $path
}

$script:logPath = Get-OutputLogPath "week06-demo-log.txt"
Set-Content -LiteralPath $script:logPath -Value "# Demo API QTKD - Week 06`r`nStarted: $(Get-Date -Format o)`r`n" -Encoding UTF8
$script:apiBaseUrl = $BaseUrl.TrimEnd('/')

function Write-Log {
    param([Parameter(Mandatory)][string]$Message)
    Write-Host $Message
    Add-Content -LiteralPath $script:logPath -Value $Message -Encoding UTF8
}
Write-Log "Output file: $script:logPath"
Write-Log "## 1. Create an Active Customer"

function Invoke-ApiRequest {
    param(
        [Parameter(Mandatory)][string]$Method,
        [Parameter(Mandatory)][string]$Path,
        [object]$Body
    )

    $requestParams = @{
        Uri             = "$script:apiBaseUrl$Path"
        Method          = $Method
        UseBasicParsing = $true
        ErrorAction     = "Stop"
    }
    if ($null -ne $Body) {
        $json = ConvertTo-Json -InputObject $Body -Depth 10 -Compress
        $requestParams.ContentType = "application/json"
        $requestParams.Body = $json
    }

    try {
        $response = Invoke-WebRequest @requestParams
        $statusCode = [int]$response.StatusCode
        $responseText = [string]$response.Content
    }
    catch {
        $errorResponse = $_.Exception.Response
        if ($null -eq $errorResponse) { throw }

        $statusCode = [int]$errorResponse.StatusCode
        if ($errorResponse -is [System.Net.HttpWebResponse]) {
            $reader = [System.IO.StreamReader]::new($errorResponse.GetResponseStream())
            $responseText = $reader.ReadToEnd()
            $reader.Dispose()
        }
        elseif ($null -ne $errorResponse.Content) {
            $responseText = $errorResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        }
        else {
            $responseText = ""
        }
    }

    $parsedBody = $responseText
    if (-not [string]::IsNullOrWhiteSpace($responseText)) {
        try { $parsedBody = $responseText | ConvertFrom-Json }
        catch { $parsedBody = $responseText }
    }

    $result = [pscustomobject]@{
        StatusCode = $statusCode
        Body       = $parsedBody
    }

    Write-Log "$Method $Path -> $($result.StatusCode)"
    if ($responseText) {
        try { $prettyResponse = $responseText | ConvertFrom-Json | ConvertTo-Json -Depth 10 }
        catch { $prettyResponse = $responseText }
        Write-Log $prettyResponse
    }
    return $result
}

function Assert-Status {
    param([int]$Actual, [int]$Expected, [string]$Step)
    if ($Actual -ne $Expected) {
        throw "$Step expected HTTP $Expected but received HTTP $Actual."
    }
}

try {
    $runId = [Guid]::NewGuid().ToString("N").Substring(0, 12).ToUpperInvariant()
    $customer = Invoke-ApiRequest -Method POST -Path "/api/customers" -Body @{
        name = "Customer Demo Week06 $runId"
        status = "Active"
    }
    Assert-Status $customer.StatusCode 201 "Create Customer"
    $customerId = [int]$customer.Body.id

    $contractNo = "HD-DEMO-$runId"
    Write-Log "## 2. Create a Contract Draft"
    $startDate = (Get-Date).ToString("yyyy-MM-dd")
    $endDate = (Get-Date).AddYears(1).ToString("yyyy-MM-dd")
    $contract = Invoke-ApiRequest -Method POST -Path "/api/contracts" -Body @{
        contractNo = $contractNo
        customerId = $customerId
        startDate = $startDate
        endDate = $endDate
    }
    Assert-Status $contract.StatusCode 201 "Create Contract Draft"
    $contractId = [int]$contract.Body.id

    $failedSubmit = Invoke-ApiRequest -Method POST -Path "/api/contracts/$contractId/submit"
    Assert-Status $failedSubmit.StatusCode 422 "Submit incomplete Contract"

    Write-Log "## 3. Complete the Contract and submit"
    $updatedContract = Invoke-ApiRequest -Method PUT -Path "/api/contracts/$contractId" -Body @{
        contractNo = $contractNo
        customerId = $customerId
        startDate = $startDate
        endDate = $endDate
        contentSummary = "Demo contract content for the Week 06 workflow."
        attachmentFileName = "$contractNo.pdf"
    }
    Assert-Status $updatedContract.StatusCode 200 "Complete Contract Draft"

    $successfulSubmit = Invoke-ApiRequest -Method POST -Path "/api/contracts/$contractId/submit"
    Assert-Status $successfulSubmit.StatusCode 200 "Submit complete Contract"

    $savedContract = Invoke-ApiRequest -Method GET -Path "/api/contracts/$contractId"
    Assert-Status $savedContract.StatusCode 200 "Read submitted Contract"
    if ($savedContract.Body.status -ne "PendingApproval") {
        throw "Contract status was expected to be PendingApproval."
    }

    $duplicateSubmit = Invoke-ApiRequest -Method POST -Path "/api/contracts/$contractId/submit"
    Assert-Status $duplicateSubmit.StatusCode 409 "Submit the same Contract again"

    Write-Log "## Result"
    Write-Log "Demo completed. CustomerId=$customerId ContractId=$contractId ContractNo=$contractNo"
    Write-Log "Output saved to $script:logPath"
}
catch {
    Write-Log "ERROR: $($_.Exception.Message)"
    throw
}
