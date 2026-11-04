param(
    [string]$BaseUrl = "http://localhost:5209"
)

# Runs real HTTP checks against the running API. Successful creation checks add
# a few uniquely named records to the configured development database.
$ErrorActionPreference = "Stop"
$script:apiBaseUrl = $BaseUrl.TrimEnd('/')
$script:passCount = 0
$script:failCount = 0
$script:preferredLogPath = Join-Path $PSScriptRoot "week06-verification-report.txt"
$script:logLines = [System.Collections.Generic.List[string]]::new()

function Write-Log {
    param([Parameter(Mandatory)][AllowEmptyString()][string]$Message)
    Write-Host $Message
    $script:logLines.Add($Message)
}

function Save-Report {
    $runId = [Guid]::NewGuid().ToString("N").Substring(0, 8)
    $fallbackPath = Join-Path $PSScriptRoot "week06-verification-report-$runId.txt"
    $script:logLines.Add("Output file: $script:preferredLogPath")
    try {
        Set-Content -LiteralPath $script:preferredLogPath -Value $script:logLines -Encoding UTF8 -ErrorAction Stop
        Write-Host "Report saved to $script:preferredLogPath"
    }
    catch [System.IO.IOException] {
        $script:logLines[$script:logLines.Count - 1] = "Output file: $fallbackPath"
        Set-Content -LiteralPath $fallbackPath -Value $script:logLines -Encoding UTF8 -ErrorAction Stop
        Write-Host "Report saved to $fallbackPath (standard report file was locked)"
    }
}

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
        $requestParams.ContentType = "application/json; charset=utf-8"
        $requestParams.Body = ConvertTo-Json -InputObject $Body -Depth 10 -Compress
    }

    try {
        $response = Invoke-WebRequest @requestParams
        $status = [int]$response.StatusCode
        $content = [string]$response.Content
    }
    catch {
        $errorResponse = $_.Exception.Response
        if ($null -eq $errorResponse) {
            return [pscustomobject]@{ StatusCode = 0; Body = $null; Error = $_.Exception.Message }
        }

        $status = [int]$errorResponse.StatusCode
        $content = ""
        if ($errorResponse -is [System.Net.HttpWebResponse]) {
            $reader = [System.IO.StreamReader]::new($errorResponse.GetResponseStream())
            $content = $reader.ReadToEnd()
            $reader.Dispose()
        }
        elseif ($null -ne $errorResponse.Content) {
            $content = $errorResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        }
    }

    $bodyObject = $content
    if (-not [string]::IsNullOrWhiteSpace($content)) {
        try { $bodyObject = $content | ConvertFrom-Json }
        catch { $bodyObject = $content }
    }
    return [pscustomobject]@{ StatusCode = $status; Body = $bodyObject; Error = $null }
}

function Assert-Check {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][bool]$Condition,
        [string]$Evidence = ""
    )
    if ($Condition) {
        $script:passCount++
        $line = "  PASS  $Name"
    }
    else {
        $script:failCount++
        $line = "  FAIL  $Name"
    }
    if ($Evidence) { $line += " [$Evidence]" }
    Write-Log $line
}

function Invoke-StatusCheck {
    param(
        [string]$Name,
        [string]$Method,
        [string]$Path,
        [int]$ExpectedStatus,
        [object]$Body
    )
    $response = Invoke-ApiRequest -Method $Method -Path $Path -Body $Body
    Assert-Check -Name $Name -Condition ($response.StatusCode -eq $ExpectedStatus) `
        -Evidence "expected HTTP $ExpectedStatus, received $($response.StatusCode)"
    return $response
}

Write-Log "# QTKD Week 06 - API verification report"
Write-Log "Started: $(Get-Date -Format o)"
Write-Log "Base URL: $script:apiBaseUrl"
Write-Log ""

$runId = [Guid]::NewGuid().ToString("N").Substring(0, 12).ToUpperInvariant()
$today = (Get-Date).ToString("yyyy-MM-dd")
$nextYear = (Get-Date).AddYears(1).ToString("yyyy-MM-dd")
$missingId = 2147000000

try {
    Write-Log "## 0. Week 05 foundation and API availability"
    $customers = Invoke-StatusCheck "GET customer collection" GET "/api/customers" 200 $null
    Assert-Check "Customer collection is a JSON array" ($customers.Body -is [array])
    $contracts = Invoke-StatusCheck "GET contract collection" GET "/api/contracts" 200 $null
    Assert-Check "Contract collection is a JSON array" ($contracts.Body -is [array])
    $priceLists = Invoke-StatusCheck "GET price-list collection" GET "/api/price-lists" 200 $null
    Assert-Check "Price-list collection is a JSON array" ($priceLists.Body -is [array])
    $missingCustomer = Invoke-StatusCheck "Unknown Customer returns 404" GET "/api/customers/$missingId" 404 $null
    $missingContract = Invoke-StatusCheck "Unknown Contract returns 404" GET "/api/contracts/$missingId" 404 $null
    $missingPriceList = Invoke-StatusCheck "Unknown PriceList returns 404" GET "/api/price-lists/$missingId" 404 $null

    Write-Log ""
    Write-Log "## 1. Customer - required fields, status validation, create/read/update"
    $active = Invoke-StatusCheck "Create Active Customer" POST "/api/customers" 201 @{
        name = "Week06 Verify Active $runId"; status = "Active"
    }
    $customerId = [int]$active.Body.id
    Assert-Check "Created Customer has a positive id" ($customerId -gt 0)
    Assert-Check "Created Customer is Active" ($active.Body.status -eq "Active")

    $defaultStatus = Invoke-StatusCheck "Omitted status uses Active default" POST "/api/customers" 201 @{
        name = "Week06 Verify Default $runId"
    }
    Assert-Check "Default status persisted as Active" ($defaultStatus.Body.status -eq "Active")
    $inactive = Invoke-StatusCheck "Create Inactive Customer" POST "/api/customers" 201 @{
        name = "Week06 Verify Inactive $runId"; status = "Inactive"
    }
    $inactiveCustomerId = [int]$inactive.Body.id
    $caseInsensitiveStatus = Invoke-StatusCheck "Customer status accepts case-insensitive Active" POST "/api/customers" 201 @{
        name = "Week06 Verify Case $runId"; status = "active"
    }
    Assert-Check "Case-insensitive status is normalized to Active" ($caseInsensitiveStatus.Body.status -eq "Active")

    $detailCustomer = Invoke-StatusCheck "Read created Customer" GET "/api/customers/$customerId" 200 $null
    Assert-Check "Customer detail returns the created name" ($detailCustomer.Body.name -eq "Week06 Verify Active $runId")
    $customerCollection = Invoke-ApiRequest GET "/api/customers" $null
    Assert-Check "Customer collection contains created Customer" (@($customerCollection.Body | Where-Object id -eq $customerId).Count -eq 1)
    $updatedCustomer = Invoke-StatusCheck "Update Customer" PUT "/api/customers/$customerId" 200 @{
        name = "Week06 Verify Renamed $runId"; status = "Active"
    }
    Assert-Check "Customer update returns changed name" ($updatedCustomer.Body.name -eq "Week06 Verify Renamed $runId")
    Invoke-StatusCheck "Customer update rejects invalid status" PUT "/api/customers/$customerId" 400 @{
        name = "Should Not Be Saved"; status = "Pending"
    } | Out-Null
    $customerAfterInvalidUpdate = Invoke-StatusCheck "Read Customer after rejected update" GET "/api/customers/$customerId" 200 $null
    Assert-Check "Rejected Customer update leaves saved name unchanged" ($customerAfterInvalidUpdate.Body.name -eq "Week06 Verify Renamed $runId")
    Invoke-StatusCheck "Update unknown Customer returns 404" PUT "/api/customers/$missingId" 404 @{
        name = "No such customer"; status = "Active"
    } | Out-Null
    Invoke-StatusCheck "Create Customer without name returns 400" POST "/api/customers" 400 @{ status = "Active" } | Out-Null
    Invoke-StatusCheck "Create Customer with blank name returns 400" POST "/api/customers" 400 @{
        name = "   "; status = "Active"
    } | Out-Null
    Invoke-StatusCheck "Create Customer with 201-character name returns 400" POST "/api/customers" 400 @{
        name = ("N" * 201); status = "Active"
    } | Out-Null
    foreach ($badStatus in @("", "Unknown")) {
        Invoke-StatusCheck "Reject Customer status '$badStatus'" POST "/api/customers" 400 @{
            name = "Week06 invalid status $runId"; status = $badStatus
        } | Out-Null
    }

    Write-Log ""
    Write-Log "## 2. Contract - validation, Draft-only editing, submit workflow"
    $contractNo = "HD-VFY-$runId"
    $draft = Invoke-StatusCheck "Create Contract Draft" POST "/api/contracts" 201 @{
        contractNo = $contractNo; customerId = $customerId; startDate = $today; endDate = $nextYear
        status = "Approved" # unknown request property must not set workflow status
    }
    $contractId = [int]$draft.Body.id
    Assert-Check "New Contract starts in Draft despite supplied status" ($draft.Body.status -eq "Draft")
    Assert-Check "Contract response includes linked Customer name" ($draft.Body.customerName -eq "Week06 Verify Renamed $runId")
    $contractRead = Invoke-StatusCheck "Read created Contract" GET "/api/contracts/$contractId" 200 $null
    Assert-Check "Contract detail retains ContractNo" ($contractRead.Body.contractNo -eq $contractNo)
    $contractCollection = Invoke-ApiRequest GET "/api/contracts" $null
    Assert-Check "Contract collection contains created Contract" (@($contractCollection.Body | Where-Object id -eq $contractId).Count -eq 1)

    $badContractRequests = @(
        @{ label = "missing ContractNo"; body = @{ customerId = $customerId; startDate = $today; endDate = $nextYear } }
        @{ label = "blank ContractNo"; body = @{ contractNo = " "; customerId = $customerId; startDate = $today; endDate = $nextYear } }
        @{ label = "ContractNo over 50 chars"; body = @{ contractNo = ("C" * 51); customerId = $customerId; startDate = $today; endDate = $nextYear } }
        @{ label = "nonexistent Customer"; body = @{ contractNo = "HD-BAD-$runId-01"; customerId = $missingId; startDate = $today; endDate = $nextYear } }
        @{ label = "inactive Customer"; body = @{ contractNo = "HD-BAD-$runId-02"; customerId = $inactiveCustomerId; startDate = $today; endDate = $nextYear } }
        @{ label = "zero Customer id"; body = @{ contractNo = "HD-BAD-$runId-03"; customerId = 0; startDate = $today; endDate = $nextYear } }
        @{ label = "negative Customer id"; body = @{ contractNo = "HD-BAD-$runId-04"; customerId = -1; startDate = $today; endDate = $nextYear } }
        @{ label = "missing StartDate"; body = @{ contractNo = "HD-BAD-$runId-05"; customerId = $customerId; endDate = $nextYear } }
        @{ label = "missing EndDate"; body = @{ contractNo = "HD-BAD-$runId-06"; customerId = $customerId; startDate = $today } }
        @{ label = "EndDate before StartDate"; body = @{ contractNo = "HD-BAD-$runId-07"; customerId = $customerId; startDate = $nextYear; endDate = $today } }
        @{ label = "ContentSummary over 4000 chars"; body = @{ contractNo = "HD-BAD-$runId-08"; customerId = $customerId; startDate = $today; endDate = $nextYear; contentSummary = ("X" * 4001) } }
        @{ label = "AttachmentFileName over 255 chars"; body = @{ contractNo = "HD-BAD-$runId-09"; customerId = $customerId; startDate = $today; endDate = $nextYear; attachmentFileName = (("F" * 252) + ".pdf") } }
        @{ label = "duplicate ContractNo"; body = @{ contractNo = $contractNo; customerId = $customerId; startDate = $today; endDate = $nextYear } }
    )
    foreach ($case in $badContractRequests) {
        Invoke-StatusCheck "Reject Contract: $($case.label)" POST "/api/contracts" 400 $case.body | Out-Null
    }

    $incomplete = Invoke-StatusCheck "Submit incomplete Draft returns 422" POST "/api/contracts/$contractId/submit" 422 $null
    Assert-Check "Incomplete submit returns expected validation code" ($incomplete.Body.code -eq "submission_validation_failed")
    Assert-Check "Incomplete submit identifies missing requirements" (@($incomplete.Body.details).Count -ge 2)
    $stillDraft = Invoke-StatusCheck "Read after rejected submit" GET "/api/contracts/$contractId" 200 $null
    Assert-Check "Rejected submit leaves Contract in Draft" ($stillDraft.Body.status -eq "Draft")
    Assert-Check "Rejected submit leaves submission timestamp empty" ($null -eq $stillDraft.Body.submittedAtUtc)

    Invoke-StatusCheck "Update Draft with content but no attachment" PUT "/api/contracts/$contractId" 200 @{
        contractNo = $contractNo; customerId = $customerId; startDate = $today; endDate = $nextYear
        contentSummary = "Content supplied, attachment still missing."
    } | Out-Null
    $missingAttachment = Invoke-StatusCheck "Submit without attachment returns 422" POST "/api/contracts/$contractId/submit" 422 $null
    Assert-Check "Attachment-only validation names missing attachment" (($missingAttachment.Body.details -join " ") -match "AttachmentFileName")

    Invoke-StatusCheck "Update Draft with attachment but no content" PUT "/api/contracts/$contractId" 200 @{
        contractNo = $contractNo; customerId = $customerId; startDate = $today; endDate = $nextYear
        attachmentFileName = "$contractNo.pdf"
    } | Out-Null
    $missingContent = Invoke-StatusCheck "Submit without content returns 422" POST "/api/contracts/$contractId/submit" 422 $null
    Assert-Check "Content-only validation names missing content" (($missingContent.Body.details -join " ") -match "ContentSummary")

    $completed = Invoke-StatusCheck "Update Draft with content and attachment metadata" PUT "/api/contracts/$contractId" 200 @{
        contractNo = $contractNo; customerId = $customerId; startDate = $today; endDate = $nextYear
        contentSummary = "Week 06 verification contract content."; attachmentFileName = "$contractNo.pdf"
    }
    Assert-Check "Completed Contract remains Draft before submit" ($completed.Body.status -eq "Draft")
    $submitted = Invoke-StatusCheck "Submit eligible Draft" POST "/api/contracts/$contractId/submit" 200 $null
    Assert-Check "Successful submit changes status to PendingApproval" ($submitted.Body.status -eq "PendingApproval")
    Assert-Check "Successful submit returns UTC timestamp" ($null -ne $submitted.Body.submittedAtUtc)
    $submittedRead = Invoke-StatusCheck "Read submitted Contract" GET "/api/contracts/$contractId" 200 $null
    Assert-Check "GET confirms PendingApproval persisted" ($submittedRead.Body.status -eq "PendingApproval")
    Invoke-StatusCheck "Duplicate submit returns 409" POST "/api/contracts/$contractId/submit" 409 $null | Out-Null
    Invoke-StatusCheck "Update after submit returns 409" PUT "/api/contracts/$contractId" 409 @{
        contractNo = $contractNo; customerId = $customerId; startDate = $today; endDate = $nextYear
        contentSummary = "Attempt after submission."; attachmentFileName = "$contractNo.pdf"
    } | Out-Null
    Invoke-StatusCheck "Submit unknown Contract returns 404" POST "/api/contracts/$missingId/submit" 404 $null | Out-Null

    Write-Log ""
    Write-Log "## 3. Pricing - PriceList and PriceItem validation / duplicate prevention"
    $priceListName = "Week06 Verify PriceList $runId"
    $newPriceList = Invoke-StatusCheck "Create PriceList" POST "/api/price-lists" 201 @{
        name = $priceListName; effectiveDate = $today
    }
    $priceListId = [int]$newPriceList.Body.id
    Assert-Check "Created PriceList has positive id" ($priceListId -gt 0)
    $readPriceList = Invoke-StatusCheck "Read created PriceList" GET "/api/price-lists/$priceListId" 200 $null
    Assert-Check "PriceList detail returns its name" ($readPriceList.Body.name -eq $priceListName)
    $priceListCollection = Invoke-ApiRequest GET "/api/price-lists" $null
    Assert-Check "PriceList collection contains created PriceList" (@($priceListCollection.Body | Where-Object id -eq $priceListId).Count -eq 1)
    Invoke-StatusCheck "PriceList without name returns 400" POST "/api/price-lists" 400 @{ effectiveDate = $today } | Out-Null
    Invoke-StatusCheck "PriceList with blank name returns 400" POST "/api/price-lists" 400 @{
        name = " "; effectiveDate = $today
    } | Out-Null
    Invoke-StatusCheck "PriceList name over 200 chars returns 400" POST "/api/price-lists" 400 @{
        name = ("P" * 201); effectiveDate = $today
    } | Out-Null
    Invoke-StatusCheck "PriceList with missing date returns 400" POST "/api/price-lists" 400 @{
        name = "Week06 Missing Date $runId"
    } | Out-Null

    Invoke-StatusCheck "Read items from unknown PriceList returns 404" GET "/api/price-lists/$missingId/items" 404 $null | Out-Null
    Invoke-StatusCheck "Add item to unknown PriceList returns 404" POST "/api/price-lists/$missingId/items" 404 @{
        serviceCode = "MISSING-$runId"; unitPrice = 100
    } | Out-Null
    foreach ($case in @(
        @{ label = "blank ServiceCode"; body = @{ serviceCode = " "; unitPrice = 100 } }
        @{ label = "missing ServiceCode"; body = @{ unitPrice = 100 } }
        @{ label = "ServiceCode over 100 chars"; body = @{ serviceCode = ("S" * 101); unitPrice = 100 } }
        @{ label = "zero UnitPrice"; body = @{ serviceCode = "ZERO-$runId"; unitPrice = 0 } }
        @{ label = "negative UnitPrice"; body = @{ serviceCode = "NEG-$runId"; unitPrice = -1 } }
    )) {
        Invoke-StatusCheck "Reject PriceItem: $($case.label)" POST "/api/price-lists/$priceListId/items" 400 $case.body | Out-Null
    }
    $serviceCode = "SVC-VFY-$runId"
    $item = Invoke-StatusCheck "Create valid PriceItem" POST "/api/price-lists/$priceListId/items" 201 @{
        serviceCode = $serviceCode; unitPrice = 12345.67
    }
    Assert-Check "PriceItem response preserves ServiceCode" ($item.Body.serviceCode -eq $serviceCode)
    Assert-Check "PriceItem response preserves positive UnitPrice" ([decimal]$item.Body.unitPrice -eq [decimal]12345.67)
    Invoke-StatusCheck "Duplicate ServiceCode in same PriceList returns 409" POST "/api/price-lists/$priceListId/items" 409 @{
        serviceCode = $serviceCode; unitPrice = 12345.67
    } | Out-Null
    $items = Invoke-StatusCheck "Read PriceList items" GET "/api/price-lists/$priceListId/items" 200 $null
    Assert-Check "PriceItem appears in read-back list" (@($items.Body | Where-Object serviceCode -eq $serviceCode).Count -eq 1)

    Write-Log ""
    Write-Log "## 4. Summary"
    Write-Log ""
    Write-Log "## Result"
    Write-Log "PASS: $script:passCount"
    Write-Log "FAIL: $script:failCount"
    Write-Log "Total checks: $($script:passCount + $script:failCount)"
    if ($script:failCount -gt 0) {
        Write-Log "OVERALL: FAIL"
        Save-Report
        exit 1
    }
    Write-Log "OVERALL: PASS"
    Save-Report
}
catch {
    $script:failCount++
    Write-Log "FAIL: Script could not complete: $($_.Exception.Message)"
    Write-Log "PASS: $script:passCount"
    Write-Log "FAIL: $script:failCount"
    Write-Log "OVERALL: FAIL"
    Save-Report
    exit 1
}
