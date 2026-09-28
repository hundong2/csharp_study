[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectPath = Join-Path $PSScriptRoot 'src\ServiceHealthApi\ServiceHealthApi.csproj'
$portReservation = [System.Net.Sockets.TcpListener]::new(
    [System.Net.IPAddress]::Loopback,
    0)
$portReservation.Start()
$port = ([System.Net.IPEndPoint]$portReservation.LocalEndpoint).Port
$portReservation.Stop()
$baseUrl = "http://127.0.0.1:$port"
$server = $null
$client = [System.Net.Http.HttpClient]::new()
$client.Timeout = [TimeSpan]::FromSeconds(3)
$passed = 0

function Assert-Contract {
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

function Invoke-Request {
    param(
        [Parameter(Mandatory)]
        [System.Net.Http.HttpMethod]$Method,

        [Parameter(Mandatory)]
        [string]$Path
    )

    $request = [System.Net.Http.HttpRequestMessage]::new($Method, "$baseUrl$Path")
    $response = $null
    try {
        $response = $client.SendAsync($request).GetAwaiter().GetResult()
        $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        $cacheControl = if ($null -eq $response.Headers.CacheControl) {
            ''
        }
        else {
            $response.Headers.CacheControl.ToString()
        }

        return [pscustomobject]@{
            StatusCode = [int]$response.StatusCode
            ContentType = $response.Content.Headers.ContentType.MediaType
            CacheControl = $cacheControl
            Body = $body
            Json = if ([string]::IsNullOrWhiteSpace($body)) { $null } else { $body | ConvertFrom-Json }
        }
    }
    finally {
        $request.Dispose()
        if ($null -ne $response) {
            $response.Dispose()
        }
    }
}

function Wait-ForStatus {
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [int]$ExpectedStatus,

        [Parameter(Mandatory)]
        [int]$TimeoutMilliseconds
    )

    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    while ($watch.ElapsedMilliseconds -lt $TimeoutMilliseconds) {
        try {
            $result = Invoke-Request -Method ([System.Net.Http.HttpMethod]::Get) -Path $Path
            if ($result.StatusCode -eq $ExpectedStatus) {
                return $result
            }
        }
        catch [System.Net.Http.HttpRequestException] {
            # Kestrel이 아직 socket을 열기 전인 짧은 구간은 기다렸다가 다시 확인합니다.
        }

        Start-Sleep -Milliseconds 75
    }

    throw "$Path 가 ${TimeoutMilliseconds}ms 안에 HTTP $ExpectedStatus 상태가 되지 않았습니다."
}

function Set-Dependency {
    param(
        [Parameter(Mandatory)]
        [string]$Name,

        [Parameter(Mandatory)]
        [ValidateSet('available', 'unavailable')]
        [string]$Condition,

        [int]$DelayMilliseconds = 0
    )

    $result = Invoke-Request `
        -Method ([System.Net.Http.HttpMethod]::Put) `
        -Path "/demo/dependencies/$Name/$Condition`?delayMs=$DelayMilliseconds"
    Assert-Contract ($result.StatusCode -eq 200) "$Name probe를 $Condition/$DelayMilliseconds ms로 설정한다."
}

try {
    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = 'dotnet'
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.Environment['DOTNET_ENVIRONMENT'] = 'Development'
    $startInfo.Environment['StartupWarmup__DelayMilliseconds'] = '4000'
    $startInfo.Environment['DemoDependencies__Enabled'] = 'true'
    $startInfo.ArgumentList.Add('run')
    $startInfo.ArgumentList.Add('--project')
    $startInfo.ArgumentList.Add($projectPath)
    $startInfo.ArgumentList.Add('-c')
    $startInfo.ArgumentList.Add('Release')
    $startInfo.ArgumentList.Add('--no-build')
    $startInfo.ArgumentList.Add('--')
    $startInfo.ArgumentList.Add('--urls')
    $startInfo.ArgumentList.Add($baseUrl)

    $server = [System.Diagnostics.Process]::Start($startInfo)
    if ($null -eq $server) {
        throw '검증용 Kestrel 프로세스를 시작하지 못했습니다.'
    }

    $live = Wait-ForStatus -Path '/health/live' -ExpectedStatus 200 -TimeoutMilliseconds 5000
    Assert-Contract ($live.Json.status -eq 'Healthy') '부팅 중 liveness 상태는 Healthy다.'
    Assert-Contract ($live.Json.checks.Count -eq 0) 'liveness는 dependency probe를 실행하지 않는다.'
    Assert-Contract ($live.ContentType -eq 'application/json') 'health 응답은 application/json이다.'
    Assert-Contract ($live.CacheControl -match 'no-store') 'health 응답은 no-store로 캐시를 금지한다.'

    $startupBefore = Invoke-Request -Method ([System.Net.Http.HttpMethod]::Get) -Path '/health/startup'
    Assert-Contract ($startupBefore.StatusCode -eq 503) 'warm-up 전 startup은 HTTP 503이다.'
    Assert-Contract ($startupBefore.Json.status -eq 'Unhealthy') 'warm-up 전 startup 상태는 Unhealthy다.'

    $readyBefore = Invoke-Request -Method ([System.Net.Http.HttpMethod]::Get) -Path '/health/ready'
    Assert-Contract ($readyBefore.StatusCode -eq 503) 'warm-up 전 readiness는 HTTP 503이다.'

    $startupAfter = Wait-ForStatus -Path '/health/startup' -ExpectedStatus 200 -TimeoutMilliseconds 6000
    Assert-Contract ($startupAfter.Json.status -eq 'Healthy') 'warm-up 완료 후 startup 상태는 Healthy다.'

    $readyHealthy = Invoke-Request -Method ([System.Net.Http.HttpMethod]::Get) -Path '/health/ready'
    Assert-Contract ($readyHealthy.StatusCode -eq 200) '모든 dependency 정상 시 readiness는 HTTP 200이다.'
    Assert-Contract ($readyHealthy.Json.status -eq 'Healthy') '모든 dependency 정상 시 readiness 상태는 Healthy다.'

    $countersBefore = Invoke-Request -Method ([System.Net.Http.HttpMethod]::Get) -Path '/demo/probes'
    $countBeforeLive = ($countersBefore.Json | Measure-Object -Property probeCount -Sum).Sum
    $null = Invoke-Request -Method ([System.Net.Http.HttpMethod]::Get) -Path '/health/live'
    $countersAfter = Invoke-Request -Method ([System.Net.Http.HttpMethod]::Get) -Path '/demo/probes'
    $countAfterLive = ($countersAfter.Json | Measure-Object -Property probeCount -Sum).Sum
    Assert-Contract ($countBeforeLive -eq $countAfterLive) 'liveness 호출은 dependency probe counter를 늘리지 않는다.'

    Set-Dependency -Name 'recommendations' -Condition 'unavailable'
    $degraded = Invoke-Request -Method ([System.Net.Http.HttpMethod]::Get) -Path '/health/ready'
    Assert-Contract ($degraded.StatusCode -eq 200) '선택 dependency 장애 시 readiness는 HTTP 200이다.'
    Assert-Contract ($degraded.Json.status -eq 'Degraded') '선택 dependency 장애 시 readiness 상태는 Degraded다.'

    Set-Dependency -Name 'inventory' -Condition 'unavailable'
    $unhealthy = Invoke-Request -Method ([System.Net.Http.HttpMethod]::Get) -Path '/health/ready'
    Assert-Contract ($unhealthy.StatusCode -eq 503) '필수 dependency 장애 시 readiness는 HTTP 503이다.'
    Assert-Contract ($unhealthy.Json.status -eq 'Unhealthy') '필수 dependency 장애 시 readiness 상태는 Unhealthy다.'

    $liveDuringOutage = Invoke-Request -Method ([System.Net.Http.HttpMethod]::Get) -Path '/health/live'
    Assert-Contract ($liveDuringOutage.StatusCode -eq 200) '필수 dependency 장애 중에도 liveness는 HTTP 200이다.'

    Set-Dependency -Name 'inventory' -Condition 'available'
    Set-Dependency -Name 'recommendations' -Condition 'available' -DelayMilliseconds 500
    $optionalTimeoutWatch = [System.Diagnostics.Stopwatch]::StartNew()
    $optionalTimedOut = Invoke-Request -Method ([System.Net.Http.HttpMethod]::Get) -Path '/health/ready'
    $optionalTimeoutWatch.Stop()
    Assert-Contract ($optionalTimedOut.StatusCode -eq 200) '느린 선택 probe는 HTTP 200을 유지한다.'
    Assert-Contract ($optionalTimedOut.Json.status -eq 'Degraded') '선택 probe timeout은 Degraded다.'
    Assert-Contract ($optionalTimeoutWatch.ElapsedMilliseconds -lt 1000) '500ms 선택 probe가 1초 이내에 제한된다.'

    Set-Dependency -Name 'recommendations' -Condition 'available'
    Set-Dependency -Name 'inventory' -Condition 'available' -DelayMilliseconds 500
    $timeoutWatch = [System.Diagnostics.Stopwatch]::StartNew()
    $timedOut = Invoke-Request -Method ([System.Net.Http.HttpMethod]::Get) -Path '/health/ready'
    $timeoutWatch.Stop()
    Assert-Contract ($timedOut.StatusCode -eq 503) '느린 필수 probe는 개별 timeout으로 HTTP 503이다.'
    Assert-Contract ($timeoutWatch.ElapsedMilliseconds -lt 1000) '500ms 필수 probe가 1초 이내에 제한된다.'
    Assert-Contract ($timedOut.Body -notmatch 'Exception|connection string') 'health JSON은 내부 예외와 연결 문자열을 노출하지 않는다.'

    Set-Dependency -Name 'inventory' -Condition 'available'
    $recovered = Invoke-Request -Method ([System.Net.Http.HttpMethod]::Get) -Path '/health/ready'
    Assert-Contract ($recovered.StatusCode -eq 200) 'dependency 복구 후 readiness는 HTTP 200이다.'
    Assert-Contract ($recovered.Json.status -eq 'Healthy') 'dependency 복구 후 readiness 상태는 Healthy다.'

    $invalidDelay = Invoke-Request `
        -Method ([System.Net.Http.HttpMethod]::Put) `
        -Path '/demo/dependencies/inventory/available?delayMs=abc'
    Assert-Contract ($invalidDelay.StatusCode -eq 400) '정수가 아닌 delayMs는 HTTP 400이다.'
    Assert-Contract ($invalidDelay.Json.code -eq 'dependency.delay.invalid') '잘못된 delayMs는 안정적인 Problem Details code를 준다.'

    Write-Host "HTTP VERIFY PASSED: $passed assertions"
}
finally {
    $client.Dispose()

    if ($null -ne $server) {
        if (-not $server.HasExited) {
            $server.Kill($true)
            $null = $server.WaitForExit(5000)
        }

        $server.Dispose()
    }
}
