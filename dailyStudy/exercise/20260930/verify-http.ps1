[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ExecutablePath = Join-Path $PSScriptRoot 'src\OrderIntakeApi\bin\Release\net10.0\OrderIntakeApi.exe'
if (-not (Test-Path -LiteralPath $ExecutablePath -PathType Leaf)) {
    throw "Release 실행 파일이 없습니다. 먼저 README의 dotnet build 명령을 실행하세요: $ExecutablePath"
}
$baseUrl = $null

$runId = [Guid]::NewGuid().ToString('N')
$tempDirectory = [System.IO.Path]::GetTempPath()
$stdoutPath = Join-Path $tempDirectory "order-intake-$runId.stdout.log"
$stderrPath = Join-Path $tempDirectory "order-intake-$runId.stderr.log"
$serverProcess = $null
$passed = 0

function Assert-True {
    param(
        [Parameter(Mandatory)]
        [bool]$Condition,
        [Parameter(Mandatory)]
        [string]$Message
    )

    if (-not $Condition) {
        throw "검증 실패: $Message"
    }

    $script:passed++
    Write-Host "  [PASS] $Message"
}

function Invoke-Api {
    param(
        [Parameter(Mandatory)]
        [ValidateSet('GET', 'POST', 'PUT')]
        [string]$Method,
        [Parameter(Mandatory)]
        [string]$Path,
        [string]$Body
    )

    $request = @{
        Method = $Method
        Uri = "$baseUrl$Path"
        SkipHttpErrorCheck = $true
        TimeoutSec = 5
    }

    if ($PSBoundParameters.ContainsKey('Body')) {
        $request.ContentType = 'application/json'
        $request.Body = $Body
    }

    return Invoke-WebRequest @request
}

function Assert-Problem {
    param(
        [Parameter(Mandatory)]
        [object]$Response,
        [Parameter(Mandatory)]
        [int]$StatusCode,
        [Parameter(Mandatory)]
        [string]$Code,
        [Parameter(Mandatory)]
        [string]$Instance
    )

    Assert-True ($Response.StatusCode -eq $StatusCode) "오류 상태는 $StatusCode 이다."
    $contentType = [string]($Response.Headers.'Content-Type' | Select-Object -First 1)
    Assert-True ($contentType.StartsWith('application/problem+json')) '오류 media type은 application/problem+json이다.'

    $content = $Response.Content
    if ($content -is [byte[]]) {
        $content = [System.Text.Encoding]::UTF8.GetString($content)
    }

    $document = [System.Text.Json.JsonDocument]::Parse([string]$content)
    try {
        $root = $document.RootElement
        Assert-True ($root.GetProperty('status').GetInt32() -eq $StatusCode) 'JSON status가 HTTP 상태와 같다.'
        Assert-True (-not [string]::IsNullOrWhiteSpace($root.GetProperty('type').GetString())) 'type URI가 있다.'
        Assert-True (-not [string]::IsNullOrWhiteSpace($root.GetProperty('title').GetString())) 'title이 있다.'
        Assert-True (-not [string]::IsNullOrWhiteSpace($root.GetProperty('detail').GetString())) '안전한 detail이 있다.'
        Assert-True ($root.GetProperty('code').GetString() -eq $Code) "오류 code는 $Code 이다."
        Assert-True (-not [string]::IsNullOrWhiteSpace($root.GetProperty('traceId').GetString())) 'traceId가 있다.'
        Assert-True ($root.GetProperty('instance').GetString() -eq $Instance) "instance는 $Instance 이다."
    }
    finally {
        $document.Dispose()
    }
}

try {
    $arguments = @(
        '--urls', 'http://127.0.0.1:0',
        '--environment', 'Development',
        '--DemoEndpoints:Enabled=true'
    )
    $serverProcess = Start-Process `
        -FilePath $ExecutablePath `
        -ArgumentList $arguments `
        -PassThru `
        -WindowStyle Hidden `
        -RedirectStandardOutput $stdoutPath `
        -RedirectStandardError $stderrPath

    $ready = $false
    foreach ($attempt in 1..80) {
        if ($serverProcess.HasExited) {
            throw "검증 서버가 준비되기 전에 종료되었습니다. ExitCode=$($serverProcess.ExitCode)"
        }

        try {
            if (Test-Path -LiteralPath $stdoutPath) {
                $serverOutput = Get-Content -Raw -LiteralPath $stdoutPath
                $addressMatch = [regex]::Match($serverOutput, 'http://127\.0\.0\.1:\d+')
                if ($addressMatch.Success) {
                    $baseUrl = $addressMatch.Value
                    $root = Invoke-WebRequest `
                        -Uri "$baseUrl/" `
                        -SkipHttpErrorCheck `
                        -TimeoutSec 1
                    $rootDocument = [System.Text.Json.JsonDocument]::Parse([string]$root.Content)
                    try {
                        $rootElement = $rootDocument.RootElement
                        $ready = $root.StatusCode -eq 200 -and
                            $rootElement.GetProperty('service').GetString() -eq 'order-intake' -and
                            $rootElement.GetProperty('status').GetString() -eq 'ready'
                    }
                    finally {
                        $rootDocument.Dispose()
                    }

                    if ($ready) {
                        break
                    }
                }
            }
        }
        catch {
            # 서버가 포트를 열기 전 연결 실패는 짧게 기다린 뒤 다시 확인합니다.
        }

        Start-Sleep -Milliseconds 125
    }

    Assert-True $ready '실제 Kestrel 서버가 제한 시간 안에 준비된다.'

    $validBody = @'
{
  "orderId": "verify-1",
  "customerId": "customer-1",
  "items": [
    { "sku": "BOOK-CS", "quantity": 1 },
    { "sku": "MUG-DOTNET", "quantity": 1 }
  ]
}
'@
    $created = Invoke-Api -Method POST -Path '/orders' -Body $validBody
    Assert-True ($created.StatusCode -eq 201) '유효한 주문은 201 Created다.'
    $location = [string]($created.Headers.Location | Select-Object -First 1)
    Assert-True ($location -eq '/orders/VERIFY-1') '201 Location은 정규화한 주문 URL이다.'

    $read = Invoke-Api -Method GET -Path '/orders/verify-1'
    Assert-True ($read.StatusCode -eq 200) '생성한 주문은 200으로 조회된다.'

    $duplicate = Invoke-Api -Method POST -Path '/orders' -Body $validBody
    Assert-Problem $duplicate 409 'order.duplicate' '/orders'

    $invalidBody = $validBody -replace 'verify-1', 'bad id'
    $invalid = Invoke-Api -Method POST -Path '/orders' -Body $invalidBody
    Assert-Problem $invalid 400 'order.id.invalid' '/orders'

    $malformed = Invoke-Api -Method POST -Path '/orders' -Body '{'
    Assert-Problem $malformed 400 'http.bad_request' '/orders'

    $missingBody = @'
{
  "orderId": "verify-2",
  "customerId": "customer-1",
  "items": [{ "sku": "UNKNOWN-ITEM", "quantity": 1 }]
}
'@
    $missing = Invoke-Api -Method POST -Path '/orders' -Body $missingBody
    Assert-Problem $missing 404 'catalog.item.not_found' '/orders'

    $unavailableSet = Invoke-Api -Method PUT -Path '/demo/catalog/unavailable'
    Assert-True ($unavailableSet.StatusCode -eq 200) 'Development에서 일시 장애를 주입할 수 있다.'
    $unavailable = Invoke-Api -Method POST -Path '/orders' -Body ($validBody -replace 'verify-1', 'verify-3')
    Assert-Problem $unavailable 503 'catalog.unavailable' '/orders'
    $retryAfter = [string]($unavailable.Headers.'Retry-After' | Select-Object -First 1)
    Assert-True ($retryAfter -eq '5') '503에는 5초 Retry-After가 있다.'

    $bugSet = Invoke-Api -Method PUT -Path '/demo/catalog/bug'
    Assert-True ($bugSet.StatusCode -eq 200) 'Development에서 예기치 않은 버그를 주입할 수 있다.'
    $unexpected = Invoke-Api -Method POST -Path '/orders' -Body ($validBody -replace 'verify-1', 'verify-4')
    Assert-Problem $unexpected 500 'server.unexpected' '/orders'
    Assert-True (-not $unexpected.Content.Contains('InvalidOperationException')) '500 본문은 내부 예외 형식을 숨긴다.'

    $healthySet = Invoke-Api -Method PUT -Path '/demo/catalog/healthy'
    Assert-True ($healthySet.StatusCode -eq 200) '카탈로그를 정상 상태로 복구할 수 있다.'

    $routeMissing = Invoke-Api -Method GET -Path '/missing-route'
    Assert-Problem $routeMissing 404 'http.not_found' '/missing-route'

    Write-Host "HTTP VERIFY PASSED: $passed assertions"
}
catch {
    if (Test-Path -LiteralPath $stdoutPath) {
        Write-Error "--- server stdout ---`n$(Get-Content -Raw -LiteralPath $stdoutPath)" -ErrorAction Continue
    }
    if (Test-Path -LiteralPath $stderrPath) {
        Write-Error "--- server stderr ---`n$(Get-Content -Raw -LiteralPath $stderrPath)" -ErrorAction Continue
    }
    throw
}
finally {
    if ($null -ne $serverProcess -and -not $serverProcess.HasExited) {
        Stop-Process -Id $serverProcess.Id -Force
        $serverProcess.WaitForExit()
    }

    Remove-Item -LiteralPath $stdoutPath -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $stderrPath -Force -ErrorAction SilentlyContinue
}
