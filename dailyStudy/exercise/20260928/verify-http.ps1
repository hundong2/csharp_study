#requires -Version 7.0

<#
.SYNOPSIS
    실행 중인 ReportTimeoutApi의 성공·검증 실패·request timeout 계약을 실제 HTTP로 확인합니다.

.DESCRIPTION
    이 스크립트는 서버를 시작하거나 파일을 수정하지 않습니다. 먼저 /health가 준비될 때까지
    기다린 뒤 200, 400, 404, 504 응답과 Problem Details code를 검사합니다.
    하나라도 다르면 exit 1, 모두 통과하면 exit 0을 반환합니다.

.EXAMPLE
    pwsh ./verify-http.ps1 -BaseUrl http://127.0.0.1:5198
#>
[CmdletBinding()]
param(
    # 다른 포트로 서버를 실행했다면 이 값만 바꿉니다. 끝의 슬래시는 있어도 없어도 됩니다.
    [Parameter()]
    [uri]$BaseUrl = 'http://127.0.0.1:5198'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:NormalizedBaseUrl = $BaseUrl.AbsoluteUri.TrimEnd('/')
$script:Client = [System.Net.Http.HttpClient]::new()
$script:Client.Timeout = [TimeSpan]::FromSeconds(5)
$script:Passed = 0

# 이 함수는 상대 API 경로를 검증 가능한 절대 Uri로 바꿉니다.
# Path는 슬래시로 시작하는 경로이며, 반환값은 HttpClient에 전달할 Uri입니다.
function Get-ApiUri {
    [CmdletBinding()]
    [OutputType([uri])]
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    if (-not $Path.StartsWith('/', [StringComparison]::Ordinal)) {
        throw "API 경로는 '/'로 시작해야 합니다. 실제 값: $Path"
    }

    return [uri]::new("$script:NormalizedBaseUrl$Path")
}

# 이 함수는 한 번의 GET을 보내고 4xx/5xx도 예외 대신 검증 가능한 결과 객체로 돌려줍니다.
# Path는 상대 경로이며, 반환값에는 상태·본문·MIME 형식·Content-Disposition이 있습니다.
function Invoke-Get {
    [CmdletBinding()]
    [OutputType([object])]
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    $request = [System.Net.Http.HttpRequestMessage]::new(
        [System.Net.Http.HttpMethod]::Get,
        (Get-ApiUri -Path $Path)
    )
    $response = $null

    try {
        # GetAwaiter().GetResult()는 짧은 검증 스크립트에서 비동기 Task의 실제 예외를 그대로 보여 줍니다.
        $response = $script:Client.SendAsync($request).GetAwaiter().GetResult()
        $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        return [pscustomobject]@{
            StatusCode         = [int]$response.StatusCode
            Body               = $body
            MediaType          = [string]$response.Content.Headers.ContentType.MediaType
            ContentDisposition = [string]$response.Content.Headers.ContentDisposition
        }
    }
    finally {
        if ($null -ne $response) {
            $response.Dispose()
        }

        $request.Dispose()
    }
}

# 이 함수는 조건이 참인지 확인하고 통과 개수를 누적합니다.
# Condition은 반드시 참이어야 할 계약, Message는 사용자에게 보여 줄 설명이며 반환값은 없습니다.
function Assert-That {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [bool]$Condition,

        [Parameter(Mandatory)]
        [string]$Message
    )

    if (-not $Condition) {
        throw "검증 실패: $Message"
    }

    $script:Passed++
    Write-Host "  [PASS] $Message" -ForegroundColor Green
}

# 이 함수는 Problem Details JSON에서 필수 code 문자열을 꺼냅니다.
# Response는 Invoke-Get 결과이며, code가 없거나 JSON이 아니면 계약 위반 예외를 던집니다.
function Get-ProblemCode {
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [object]$Response
    )

    $json = $Response.Body | ConvertFrom-Json -Depth 16
    $property = $json.PSObject.Properties['code']
    if ($null -eq $property -or [string]::IsNullOrWhiteSpace([string]$property.Value)) {
        throw "Problem Details에 비어 있지 않은 code가 없습니다. 본문: $($Response.Body)"
    }

    return [string]$property.Value
}

# 이 함수는 서버가 시작되는 동안 /health를 반복 확인합니다.
# TimeoutSeconds는 반복 시작의 대략적 deadline입니다. 진행 중인 HTTP 호출의 최대 5초만큼 조금 더 걸릴 수 있습니다.
# 준비되면 반환값 없이 끝나고 아니면 마지막 문제를 포함한 예외를 던집니다.
function Wait-ApiReady {
    [CmdletBinding()]
    param(
        [Parameter()]
        [ValidateRange(1, 60)]
        [int]$TimeoutSeconds = 20
    )

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    $lastProblem = '아직 응답을 받지 못했습니다.'

    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        try {
            $health = Invoke-Get -Path '/health'
            if ($health.StatusCode -eq 200) {
                return
            }

            $lastProblem = "/health 상태가 $($health.StatusCode)입니다."
        }
        catch {
            $lastProblem = $_.Exception.Message
        }

        Start-Sleep -Milliseconds 250
    }

    throw "${TimeoutSeconds}초 안에 API가 준비되지 않았습니다. 마지막 문제: $lastProblem"
}

try {
    Write-Host "ReportTimeoutApi HTTP 검증 시작: $script:NormalizedBaseUrl" -ForegroundColor Cyan
    Wait-ApiReady

    Write-Host "`n[1/5] 제한 시간 안의 CSV 보고서" -ForegroundColor Yellow
    $success = Invoke-Get -Path '/reports/CUST-100?format=csv&simulateMs=10'
    Assert-That ($success.StatusCode -eq 200) "짧은 요청은 HTTP 200이다."
    Assert-That ($success.MediaType -ceq 'text/csv') "성공 MIME 형식은 text/csv다."
    Assert-That ($success.ContentDisposition.StartsWith('attachment;', [StringComparison]::OrdinalIgnoreCase)) "성공 응답은 다운로드용 attachment다."
    Assert-That ($success.ContentDisposition.Contains('cust-100', [StringComparison]::Ordinal)) "다운로드 파일 이름에 고객 번호가 있다."
    Assert-That ($success.Body.Contains('INV-1001', [StringComparison]::Ordinal)) "CSV에 첫 청구 번호가 있다."

    Write-Host "`n[2/5] 입력 검증 실패" -ForegroundColor Yellow
    $invalid = Invoke-Get -Path '/reports/AB?format=csv&simulateMs=0'
    Assert-That ($invalid.StatusCode -eq 400) "짧은 고객 번호는 HTTP 400이다."
    Assert-That ((Get-ProblemCode -Response $invalid) -ceq 'report.customer.length') "400 code는 report.customer.length다."

    $invalidDelay = Invoke-Get -Path '/reports/CUST-100?format=csv&simulateMs=oops'
    Assert-That ($invalidDelay.StatusCode -eq 400) "정수가 아닌 simulateMs도 HTTP 400이다."
    Assert-That ((Get-ProblemCode -Response $invalidDelay) -ceq 'report.delay.invalid') "query 변환 실패도 report.delay.invalid code를 준다."

    Write-Host "`n[3/5] 존재하지 않는 고객" -ForegroundColor Yellow
    $missing = Invoke-Get -Path '/reports/CUST-999?format=json&simulateMs=0'
    Assert-That ($missing.StatusCode -eq 404) "없는 고객은 HTTP 404다."
    Assert-That ((Get-ProblemCode -Response $missing) -ceq 'report.customer.not_found') "404 code는 report.customer.not_found다."

    Write-Host "`n[4/5] 이름 있는 150ms request timeout" -ForegroundColor Yellow
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $timedOut = Invoke-Get -Path '/reports/CUST-100?format=json&simulateMs=600'
    $stopwatch.Stop()
    Assert-That ($timedOut.StatusCode -eq 504) "긴 요청은 HTTP 504다."
    Assert-That ($timedOut.MediaType -ceq 'application/problem+json') "504 MIME 형식은 application/problem+json이다."
    Assert-That ((Get-ProblemCode -Response $timedOut) -ceq 'request.timeout') "504 code는 request.timeout이다."
    Assert-That ($stopwatch.Elapsed -lt [TimeSpan]::FromSeconds(2)) "600ms 원본 작업을 끝까지 기다리지 않고 2초 안에 취소한다."

    Write-Host "`n[5/5] timeout 제외 health" -ForegroundColor Yellow
    $health = Invoke-Get -Path '/health'
    Assert-That ($health.StatusCode -eq 200) "health endpoint는 HTTP 200이다."

    Write-Host "`nHTTP 검증 통과: $script:Passed assertions" -ForegroundColor Cyan
    exit 0
}
catch {
    [Console]::Error.WriteLine("HTTP 검증이 중단되었습니다: $($_.Exception.Message)")
    exit 1
}
finally {
    $script:Client.Dispose()
}
