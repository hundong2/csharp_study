#requires -Version 7.0

<#
.SYNOPSIS
    이미 실행 중인 AnnouncementCacheApi의 Output Cache 동작을 HTTP 경계에서 검증합니다.

.DESCRIPTION
    이 스크립트는 서버를 시작하거나 데이터를 삭제하지 않습니다. 매 실행마다 고유한
    유효 category를 만들어 이전 실행의 캐시와 겹치지 않는 cold key를 확보한 뒤,
    cache hit, vary, Cache-Control 요청 헤더 무시, resource locking, generation 갱신 경로를 차례로
    검증합니다. 하나라도 기대와 다르면 exit 1, 모두 통과하면 exit 0을 반환합니다.

.EXAMPLE
    pwsh ./verify-http.ps1

.EXAMPLE
    pwsh ./verify-http.ps1 -BaseUrl http://127.0.0.1:5196
#>
[CmdletBinding()]
param(
    # 다른 포트로 서버를 실행했다면 이 값만 바꾸면 됩니다. 끝의 슬래시는 있어도 없어도 됩니다.
    [Parameter()]
    [uri]$BaseUrl = 'http://127.0.0.1:5196'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# BaseUrl과 상대 경로를 단순히 이어 붙일 수 있도록 끝의 슬래시를 한 번만 제거합니다.
$script:NormalizedBaseUrl = $BaseUrl.AbsoluteUri.TrimEnd('/')
$script:HttpClient = [System.Net.Http.HttpClient]::new()
$script:HttpClient.Timeout = [TimeSpan]::FromSeconds(15)

# 이 함수는 API 상대 경로를 절대 URI로 바꿉니다.
# Path는 '/announcements'처럼 슬래시로 시작해야 하며, 반환값은 HttpClient가 사용할 Uri입니다.
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

# 이 함수는 조건이 참인지 검사합니다.
# Condition은 확인할 조건, Message는 성공 시에도 보여 줄 학습용 설명이며, 실패하면 즉시 예외를 던집니다.
# 반환값은 없고, 예외가 없다는 사실 자체가 검증 통과를 뜻합니다.
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

    Write-Host "  [PASS] $Message" -ForegroundColor Green
}

# 이 함수는 JSON 객체에서 반드시 있어야 하는 속성을 안전하게 꺼냅니다.
# JsonObject는 ConvertFrom-Json 결과, Name은 속성 이름, Context는 오류 메시지에 넣을 요청 설명입니다.
# 속성이 있으면 그 값을 반환하고, 객체나 속성이 없으면 계약 위반으로 예외를 던집니다.
function Get-RequiredJsonProperty {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [AllowNull()]
        [object]$JsonObject,

        [Parameter(Mandatory)]
        [string]$Name,

        [Parameter(Mandatory)]
        [string]$Context
    )

    if ($null -eq $JsonObject) {
        throw "$Context 응답 본문이 유효한 JSON 객체가 아닙니다."
    }

    $property = $JsonObject.PSObject.Properties[$Name]
    if ($null -eq $property) {
        throw "$Context 응답 JSON에 필수 속성 '$Name'이(가) 없습니다."
    }

    return $property.Value
}

# 이 함수는 HttpClient로 HTTP 요청을 한 번 보내고 검증하기 쉬운 결과 객체로 바꿉니다.
# Method는 HTTP 메서드, Path는 상대 경로, Headers는 추가 헤더, JsonBody는 JSON으로 보낼 선택 입력입니다.
# 상태 코드, 원문 본문, 파싱된 JSON, Location을 가진 객체를 반환하며 HTTP 4xx도 정상 결과로 돌려줍니다.
function Invoke-ApiRequest {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [System.Net.Http.HttpMethod]$Method,

        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter()]
        [hashtable]$Headers = @{},

        [Parameter()]
        [AllowNull()]
        [object]$JsonBody = $null
    )

    $request = [System.Net.Http.HttpRequestMessage]::new($Method, (Get-ApiUri -Path $Path))
    $response = $null

    try {
        foreach ($entry in $Headers.GetEnumerator()) {
            # TryAddWithoutValidation은 Accept-Language의 q 값처럼 구조가 있는 값을 원문 그대로 보냅니다.
            # 서버가 이를 ko/en 의미 값으로 정규화하는지 확인하려고 HttpClient의 엄격한 parser를 우회합니다.
            if (-not $request.Headers.TryAddWithoutValidation([string]$entry.Key, [string]$entry.Value)) {
                throw "요청 헤더 '$($entry.Key)'을(를) 추가하지 못했습니다."
            }
        }

        if ($null -ne $JsonBody) {
            $jsonText = $JsonBody | ConvertTo-Json -Depth 10 -Compress
            $request.Content = [System.Net.Http.StringContent]::new(
                $jsonText,
                [System.Text.Encoding]::UTF8,
                'application/json'
            )
        }

        # GetAwaiter().GetResult()는 비동기 Task가 끝날 때까지 기다리고, 내부 예외를 그대로 보여 줍니다.
        # 짧은 검증 스크립트에서는 async 함수를 별도로 만들지 않고도 HttpClient를 안전하게 사용할 수 있습니다.
        $response = $script:HttpClient.SendAsync($request).GetAwaiter().GetResult()
        $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()

        $parsedJson = $null
        if (-not [string]::IsNullOrWhiteSpace($body)) {
            try {
                $parsedJson = $body | ConvertFrom-Json -Depth 32
            }
            catch {
                # 일부 실패 응답은 JSON이 아닐 수도 있으므로 원문은 보존합니다.
                # JSON 필수 검증이 필요한 단계에서 Get-RequiredJsonProperty가 더 구체적으로 실패합니다.
                $parsedJson = $null
            }
        }

        $location = $null
        if ($null -ne $response.Headers.Location) {
            $location = $response.Headers.Location.OriginalString
        }

        return [pscustomobject]@{
            StatusCode = [int]$response.StatusCode
            Body       = $body
            Json       = $parsedJson
            Location   = $location
        }
    }
    finally {
        if ($null -ne $response) {
            $response.Dispose()
        }

        $request.Dispose()
    }
}

# 이 함수는 선택적인 /health와 필수 진단 API를 반복 확인해 서버 준비를 기다립니다.
# TimeoutSeconds는 기다릴 최대 시간이며, 준비되면 어떤 경로로 확인했는지 문자열을 반환합니다.
# /health가 없는 예제도 고려해 404이면 /diagnostics/repository-reads만으로 준비 여부를 판단합니다.
function Wait-ApiReady {
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter()]
        [ValidateRange(1, 300)]
        [int]$TimeoutSeconds = 30
    )

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    $healthIsAbsent = $false
    $healthIsReady = $false
    $lastProblem = '아직 응답을 받지 못했습니다.'

    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        try {
            if (-not $healthIsAbsent -and -not $healthIsReady) {
                $health = Invoke-ApiRequest -Method ([System.Net.Http.HttpMethod]::Get) -Path '/health'

                if ($health.StatusCode -eq 404) {
                    $healthIsAbsent = $true
                }
                elseif ($health.StatusCode -eq 200) {
                    $status = Get-RequiredJsonProperty -JsonObject $health.Json -Name 'status' -Context 'GET /health'
                    if ([string]$status -ieq 'ok') {
                        $healthIsReady = $true
                    }
                    else {
                        $lastProblem = "/health status가 'ok'가 아닙니다. 실제 값: $status"
                    }
                }
                else {
                    $lastProblem = "/health가 HTTP $($health.StatusCode)을(를) 반환했습니다."
                }
            }

            if ($healthIsAbsent -or $healthIsReady) {
                $diagnostics = Invoke-ApiRequest `
                    -Method ([System.Net.Http.HttpMethod]::Get) `
                    -Path '/diagnostics/repository-reads'

                if ($diagnostics.StatusCode -eq 200) {
                    $readCount = Get-RequiredJsonProperty `
                        -JsonObject $diagnostics.Json `
                        -Name 'readCount' `
                        -Context 'GET /diagnostics/repository-reads'

                    # 캐스팅이 성공해야 readCount가 단순 문자열이 아니라 정수로 해석 가능한 계약임을 알 수 있습니다.
                    $null = [long]$readCount
                    if ($healthIsAbsent) {
                        return 'diagnostics fallback (/health 미제공)'
                    }

                    return '/health + diagnostics'
                }

                $lastProblem = "/diagnostics/repository-reads가 HTTP $($diagnostics.StatusCode)을(를) 반환했습니다."
            }
        }
        catch {
            # 서버가 아직 포트를 열지 않은 경우도 준비 대기 중에는 즉시 실패시키지 않고 재시도합니다.
            $lastProblem = $_.Exception.Message
        }

        Start-Sleep -Milliseconds 300
    }

    throw "${TimeoutSeconds}초 동안 API가 준비되지 않았습니다. 서버를 먼저 실행했는지 확인하세요. 마지막 문제: $lastProblem"
}

# 이 함수는 진단 API에서 현재 Repository 조회 횟수를 읽습니다.
# 파라미터는 없고 long 정수를 반환합니다. 진단 API 자체는 Repository를 읽지 않아야 합니다.
function Get-RepositoryReadCount {
    [CmdletBinding()]
    [OutputType([long])]
    param()

    $response = Invoke-ApiRequest `
        -Method ([System.Net.Http.HttpMethod]::Get) `
        -Path '/diagnostics/repository-reads'

    if ($response.StatusCode -ne 200) {
        throw "진단 API의 기대 상태 코드는 200이지만 실제 값은 $($response.StatusCode)입니다."
    }

    $value = Get-RequiredJsonProperty `
        -JsonObject $response.Json `
        -Name 'readCount' `
        -Context 'GET /diagnostics/repository-reads'

    return [long]$value
}

# 이 함수는 목록 응답에서 Repository가 실제로 읽은 순번을 꺼냅니다.
# Response는 Invoke-ApiRequest 결과, Context는 오류 설명이며, originReadNumber를 long으로 반환합니다.
function Get-OriginReadNumber {
    [CmdletBinding()]
    [OutputType([long])]
    param(
        [Parameter(Mandatory)]
        [object]$Response,

        [Parameter(Mandatory)]
        [string]$Context
    )

    $value = Get-RequiredJsonProperty `
        -JsonObject $Response.Json `
        -Name 'originReadNumber' `
        -Context $Context

    return [long]$value
}

# 이 함수는 같은 cold key로 여러 GET을 거의 동시에 출발시킵니다.
# Path와 Headers는 모든 요청에 공통으로 쓰고, Count는 동시 요청 수이며, 각 HTTP 결과 객체 배열을 반환합니다.
# Task를 먼저 전부 만든 뒤 기다리므로, 결과를 순서대로 읽더라도 실제 네트워크 요청은 병렬로 진행됩니다.
function Invoke-ParallelGetRequests {
    [CmdletBinding()]
    [OutputType([object[]])]
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [hashtable]$Headers,

        [Parameter()]
        [ValidateRange(2, 100)]
        [int]$Count = 12
    )

    $pending = [System.Collections.Generic.List[object]]::new()
    $results = [System.Collections.Generic.List[object]]::new()

    for ($index = 0; $index -lt $Count; $index++) {
        $request = [System.Net.Http.HttpRequestMessage]::new(
            [System.Net.Http.HttpMethod]::Get,
            (Get-ApiUri -Path $Path)
        )

        foreach ($entry in $Headers.GetEnumerator()) {
            if (-not $request.Headers.TryAddWithoutValidation([string]$entry.Key, [string]$entry.Value)) {
                $request.Dispose()
                throw "병렬 요청 헤더 '$($entry.Key)'을(를) 추가하지 못했습니다."
            }
        }

        # SendAsync를 호출하는 순간 요청이 시작됩니다. 아직 기다리지 않고 Task들을 먼저 모으는 것이 핵심입니다.
        $task = $script:HttpClient.SendAsync($request)
        [void]$pending.Add([pscustomobject]@{
            Request   = $request
            Task      = $task
            Processed = $false
        })
    }

    try {
        foreach ($item in $pending) {
            $response = $null
            try {
                $response = $item.Task.GetAwaiter().GetResult()
                $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
                $parsedJson = $null

                if (-not [string]::IsNullOrWhiteSpace($body)) {
                    try {
                        $parsedJson = $body | ConvertFrom-Json -Depth 32
                    }
                    catch {
                        $parsedJson = $null
                    }
                }

                [void]$results.Add([pscustomobject]@{
                    StatusCode = [int]$response.StatusCode
                    Body       = $body
                    Json       = $parsedJson
                    Location   = $null
                })
            }
            finally {
                if ($null -ne $response) {
                    $response.Dispose()
                }

                $item.Request.Dispose()
                $item.Processed = $true
            }
        }
    }
    finally {
        # 중간 요청이 실패해도 아직 처리하지 못한 Task를 회수해 소켓과 요청 객체가 남지 않게 정리합니다.
        foreach ($item in $pending) {
            if (-not $item.Processed) {
                try {
                    $unfinishedResponse = $item.Task.GetAwaiter().GetResult()
                    if ($null -ne $unfinishedResponse) {
                        $unfinishedResponse.Dispose()
                    }
                }
                catch {
                    # 원래 예외가 가장 유용하므로 정리 중 생긴 두 번째 예외는 덮어쓰지 않습니다.
                }
                finally {
                    $item.Request.Dispose()
                    $item.Processed = $true
                }
            }
        }
    }

    return $results.ToArray()
}

try {
    Write-Host "AnnouncementCacheApi HTTP 검증 시작: $script:NormalizedBaseUrl" -ForegroundColor Cyan
    $readyBy = Wait-ApiReady -TimeoutSeconds 30
    Write-Host "[준비 완료] $readyBy" -ForegroundColor Cyan

    # 고유하면서 Domain 규칙에도 맞는 category를 사용해 서버 재시작 없이 반복해도 첫 요청을 cold miss로 만듭니다.
    # 언어 헤더는 원문 전체가 아니라 ko/en 의미 값으로 정규화되므로 고유 key 용도로 사용하지 않습니다.
    $runToken = [Guid]::NewGuid().ToString('N').Substring(0, 8)
    $runCategory = "verify-$runToken"
    $englishHeaders = @{
        Accept            = 'application/json'
        'Accept-Language' = "en-US,en;q=0.9,x-$runToken;q=0.1"
    }
    $koreanHeaders = @{
        Accept            = 'application/json'
        'Accept-Language' = "ko-KR,ko;q=0.9,x-$runToken;q=0.1"
    }
    $releasePath = "/announcements?category=$runCategory"

    Write-Host "`n[1/7] 첫 GET과 같은 GET의 cache hit" -ForegroundColor Yellow
    $readsBeforeFirst = Get-RepositoryReadCount
    $first = Invoke-ApiRequest `
        -Method ([System.Net.Http.HttpMethod]::Get) `
        -Path $releasePath `
        -Headers $englishHeaders
    $readsAfterFirst = Get-RepositoryReadCount

    Assert-That -Condition ($first.StatusCode -eq 200) `
        -Message "첫 목록 GET은 HTTP 200이다. 실제 값: $($first.StatusCode)"
    Assert-That -Condition ($readsAfterFirst -eq ($readsBeforeFirst + 1)) `
        -Message "cold GET은 Repository를 정확히 1회 읽는다. 전=$readsBeforeFirst, 후=$readsAfterFirst"

    $firstOrigin = Get-OriginReadNumber -Response $first -Context '첫 목록 GET'
    $firstLanguage = [string](Get-RequiredJsonProperty `
        -JsonObject $first.Json `
        -Name 'language' `
        -Context '첫 목록 GET')
    Assert-That -Condition ($firstLanguage -ceq 'en') `
        -Message "en-US 요청의 응답 language는 정확히 'en'이다. 실제 값: $firstLanguage"

    $same = Invoke-ApiRequest `
        -Method ([System.Net.Http.HttpMethod]::Get) `
        -Path $releasePath `
        -Headers $englishHeaders
    $readsAfterSame = Get-RepositoryReadCount
    $sameOrigin = Get-OriginReadNumber -Response $same -Context '같은 목록 GET'

    Assert-That -Condition ($same.StatusCode -eq 200) `
        -Message "같은 목록 GET은 HTTP 200이다. 실제 값: $($same.StatusCode)"
    Assert-That -Condition ($readsAfterSame -eq $readsAfterFirst) `
        -Message "같은 cache key의 두 번째 GET은 Repository를 다시 읽지 않는다. 기대/실제=$readsAfterFirst"
    Assert-That -Condition ($sameOrigin -eq $firstOrigin) `
        -Message "cache hit의 originReadNumber는 첫 응답과 같다. 기대/실제=$firstOrigin"
    Assert-That -Condition ($same.Body -ceq $first.Body) `
        -Message 'cache hit은 첫 응답과 바이트 기준으로 같은 JSON 본문을 돌려준다.'

    $canonicalCategory = [Uri]::EscapeDataString(" $($runCategory.ToUpperInvariant()) ")
    $canonicalHeaders = @{
        Accept            = 'application/json'
        'Accept-Language' = 'en;q=1,ko;q=0.5'
    }
    $canonical = Invoke-ApiRequest `
        -Method ([System.Net.Http.HttpMethod]::Get) `
        -Path "/announcements?category=$canonicalCategory" `
        -Headers $canonicalHeaders
    $readsAfterCanonical = Get-RepositoryReadCount
    $canonicalOrigin = Get-OriginReadNumber -Response $canonical -Context '정규화 동등 목록 GET'

    Assert-That -Condition ($canonical.StatusCode -eq 200) `
        -Message "표현만 다른 category/언어 GET은 HTTP 200이다. 실제 값: $($canonical.StatusCode)"
    Assert-That -Condition ($readsAfterCanonical -eq $readsAfterSame) `
        -Message "대소문자·공백·언어 원문이 달라도 의미가 같으면 cache hit다. 기대/실제=$readsAfterSame"
    Assert-That -Condition ($canonicalOrigin -eq $firstOrigin) `
        -Message "정규화 동등 요청의 originReadNumber가 같다. 기대/실제=$firstOrigin"
    Assert-That -Condition ($canonical.Body -ceq $first.Body) `
        -Message '정규화 동등 요청은 첫 응답과 바이트 기준으로 같은 본문을 돌려준다.'

    Write-Host "`n[2/7] 의미 기반 Accept-Language/category cache vary" -ForegroundColor Yellow
    $byLanguage = Invoke-ApiRequest `
        -Method ([System.Net.Http.HttpMethod]::Get) `
        -Path $releasePath `
        -Headers $koreanHeaders
    $readsAfterLanguage = Get-RepositoryReadCount
    $languageOrigin = Get-OriginReadNumber -Response $byLanguage -Context '언어 변경 목록 GET'
    $responseLanguage = [string](Get-RequiredJsonProperty `
        -JsonObject $byLanguage.Json `
        -Name 'language' `
        -Context '언어 변경 목록 GET')

    Assert-That -Condition ($byLanguage.StatusCode -eq 200) `
        -Message "Accept-Language 변경 GET은 HTTP 200이다. 실제 값: $($byLanguage.StatusCode)"
    Assert-That -Condition ($readsAfterLanguage -eq ($readsAfterSame + 1)) `
        -Message "Accept-Language가 달라지면 별도 cache key로 정확히 1회 읽는다. 전=$readsAfterSame, 후=$readsAfterLanguage"
    Assert-That -Condition ($languageOrigin -ne $firstOrigin) `
        -Message "언어별 응답은 서로 다른 originReadNumber를 가진다. en=$firstOrigin, ko=$languageOrigin"
    Assert-That -Condition ($responseLanguage -ceq 'ko') `
        -Message "ko-KR 요청의 응답 language는 정확히 'ko'이다. 실제 값: $responseLanguage"

    $otherCategory = "other-$runToken"
    $otherPath = "/announcements?category=$otherCategory"
    $byCategory = Invoke-ApiRequest `
        -Method ([System.Net.Http.HttpMethod]::Get) `
        -Path $otherPath `
        -Headers $englishHeaders
    $readsAfterCategory = Get-RepositoryReadCount
    $categoryOrigin = Get-OriginReadNumber -Response $byCategory -Context 'category 변경 목록 GET'
    $categoryItems = @(Get-RequiredJsonProperty `
        -JsonObject $byCategory.Json `
        -Name 'items' `
        -Context 'category 변경 목록 GET')
    $wrongCategoryItems = @($categoryItems | Where-Object { [string]$_.category -cne $otherCategory })

    Assert-That -Condition ($byCategory.StatusCode -eq 200) `
        -Message "category 변경 GET은 HTTP 200이다. 실제 값: $($byCategory.StatusCode)"
    Assert-That -Condition ($readsAfterCategory -eq ($readsAfterLanguage + 1)) `
        -Message "category가 달라지면 별도 cache key로 정확히 1회 읽는다. 전=$readsAfterLanguage, 후=$readsAfterCategory"
    Assert-That -Condition ($categoryOrigin -ne $firstOrigin) `
        -Message "category별 응답은 서로 다른 originReadNumber를 가진다. $runCategory=$firstOrigin, $otherCategory=$categoryOrigin"
    Assert-That -Condition ($wrongCategoryItems.Count -eq 0) `
        -Message '고유 category 목록에는 다른 category 항목이 섞이지 않는다.'

    Write-Host "`n[3/7] 요청 Cache-Control: no-cache와 서버 Output Cache" -ForegroundColor Yellow
    $noCacheHeaders = $englishHeaders.Clone()
    $noCacheHeaders['Cache-Control'] = 'no-cache'
    $noCache = Invoke-ApiRequest `
        -Method ([System.Net.Http.HttpMethod]::Get) `
        -Path $releasePath `
        -Headers $noCacheHeaders
    $readsAfterNoCache = Get-RepositoryReadCount
    $noCacheOrigin = Get-OriginReadNumber -Response $noCache -Context 'Cache-Control: no-cache 목록 GET'

    Assert-That -Condition ($noCache.StatusCode -eq 200) `
        -Message "Cache-Control: no-cache 요청은 HTTP 200이다. 실제 값: $($noCache.StatusCode)"
    Assert-That -Condition ($readsAfterNoCache -eq $readsAfterCategory) `
        -Message "클라이언트 no-cache 헤더가 서버 Output Cache를 우회하지 않는다. 기대/실제=$readsAfterCategory"
    Assert-That -Condition ($noCacheOrigin -eq $firstOrigin) `
        -Message "no-cache 요청도 캐시된 originReadNumber를 받는다. 기대/실제=$firstOrigin"
    Assert-That -Condition ($noCache.Body -ceq $first.Body) `
        -Message 'no-cache 요청도 서버가 저장한 원문과 같은 본문을 받는다.'

    Write-Host "`n[4/7] 같은 cold key 병렬 요청의 resource locking" -ForegroundColor Yellow
    $parallelToken = [Guid]::NewGuid().ToString('N').Substring(0, 8)
    $parallelHeaders = @{
        Accept            = 'application/json'
        'Accept-Language' = "en-US,en;q=0.9,x-$parallelToken;q=0.1"
    }
    $parallelPath = "/announcements?category=parallel-$parallelToken"
    $readsBeforeParallel = Get-RepositoryReadCount
    $parallelResponses = @(Invoke-ParallelGetRequests `
        -Path $parallelPath `
        -Headers $parallelHeaders `
        -Count 12)
    $readsAfterParallel = Get-RepositoryReadCount

    $parallelFailures = @($parallelResponses | Where-Object { $_.StatusCode -ne 200 })
    $referenceParallelBody = $parallelResponses[0].Body
    $differentParallelBodies = @($parallelResponses | Where-Object { $_.Body -cne $referenceParallelBody })
    $parallelOrigins = @($parallelResponses | ForEach-Object {
        Get-OriginReadNumber -Response $_ -Context '병렬 목록 GET'
    } | Sort-Object -Unique)

    Assert-That -Condition ($parallelResponses.Count -eq 12) `
        -Message "병렬 요청 12개의 응답을 모두 회수했다. 실제 값: $($parallelResponses.Count)"
    Assert-That -Condition ($parallelFailures.Count -eq 0) `
        -Message "병렬 요청 12개가 모두 HTTP 200이다. 실패 개수: $($parallelFailures.Count)"
    Assert-That -Condition ($readsAfterParallel -eq ($readsBeforeParallel + 1)) `
        -Message "resource locking으로 같은 cold key는 Repository를 정확히 1회만 읽는다. 전=$readsBeforeParallel, 후=$readsAfterParallel"
    Assert-That -Condition ($differentParallelBodies.Count -eq 0) `
        -Message '병렬 요청 12개는 바이트 기준으로 같은 응답 본문을 받는다.'
    Assert-That -Condition ($parallelOrigins.Count -eq 1) `
        -Message "병렬 요청 12개의 originReadNumber가 모두 같다. 고유값 개수: $($parallelOrigins.Count)"

    Write-Host "`n[5/7] 유효한 POST와 generation 전환/tag cleanup" -ForegroundColor Yellow
    $evictionToken = [Guid]::NewGuid().ToString('N').Substring(0, 8)
    $evictionHeaders = @{
        Accept            = 'application/json'
        'Accept-Language' = "ko-KR,ko;q=0.9,x-$evictionToken;q=0.1"
    }

    # POST 전에 같은 목록을 캐시에 넣어 두고, 성공 뒤 새 generation key가 최신 원본을 읽는지 검증합니다.
    # black-box HTTP만으로 tag cleanup 호출 자체를 분리해 증명할 수는 없으며, 그 호출은 소스와 로그로 확인합니다.
    $warmBeforePost = Invoke-ApiRequest `
        -Method ([System.Net.Http.HttpMethod]::Get) `
        -Path $releasePath `
        -Headers $evictionHeaders
    Assert-That -Condition ($warmBeforePost.StatusCode -eq 200) `
        -Message "POST 전 캐시 준비 GET은 HTTP 200이다. 실제 값: $($warmBeforePost.StatusCode)"
    $warmOrigin = Get-OriginReadNumber -Response $warmBeforePost -Context 'POST 전 캐시 준비 GET'

    $createdTitle = "Output Cache 검증 공지-$runToken-$(Get-Date -Format 'HHmmssfff')"
    $validPostBody = @{
        title    = $createdTitle
        body     = '유효한 POST 뒤 generation 전환과 cache 정리 경로를 확인하는 자동 검증 데이터입니다.'
        category = $runCategory
    }
    $created = Invoke-ApiRequest `
        -Method ([System.Net.Http.HttpMethod]::Post) `
        -Path '/announcements' `
        -Headers $evictionHeaders `
        -JsonBody $validPostBody

    Assert-That -Condition ($created.StatusCode -eq 201) `
        -Message "유효한 POST는 HTTP 201 Created이다. 실제 값: $($created.StatusCode)"
    Assert-That -Condition (-not [string]::IsNullOrWhiteSpace([string]$created.Location)) `
        -Message "201 응답에 새 리소스를 가리키는 Location 헤더가 있다. 실제 값: $($created.Location)"

    $createdIdText = [string](Get-RequiredJsonProperty `
        -JsonObject $created.Json `
        -Name 'id' `
        -Context '유효한 POST')
    $createdResponseTitle = [string](Get-RequiredJsonProperty `
        -JsonObject $created.Json `
        -Name 'title' `
        -Context '유효한 POST')
    [guid]$createdId = [guid]::Empty
    $createdIdIsGuid = [guid]::TryParse($createdIdText, [ref]$createdId)
    Assert-That -Condition $createdIdIsGuid `
        -Message "POST 응답 id는 유효한 GUID이다. 실제 값: $createdIdText"
    Assert-That -Condition ($createdResponseTitle -ceq $createdTitle) `
        -Message 'POST 응답 title은 요청한 title과 정확히 같다.'

    $locationUri = [uri]::new(([uri]("$script:NormalizedBaseUrl/")), [string]$created.Location)
    $expectedLocationPath = "/announcements/$createdId"
    Assert-That -Condition ($locationUri.AbsolutePath -ceq $expectedLocationPath) `
        -Message "Location은 생성된 id의 상세 경로다. 기대=$expectedLocationPath, 실제=$($locationUri.AbsolutePath)"

    $readsAfterPost = Get-RepositoryReadCount
    $freshAfterPost = Invoke-ApiRequest `
        -Method ([System.Net.Http.HttpMethod]::Get) `
        -Path $releasePath `
        -Headers $evictionHeaders
    $readsAfterFresh = Get-RepositoryReadCount
    $freshOrigin = Get-OriginReadNumber -Response $freshAfterPost -Context 'POST 뒤 목록 GET'
    $freshItems = @(Get-RequiredJsonProperty `
        -JsonObject $freshAfterPost.Json `
        -Name 'items' `
        -Context 'POST 뒤 목록 GET')
    $createdMatches = @($freshItems | Where-Object { [string]$_.title -ceq $createdTitle })

    Assert-That -Condition ($freshAfterPost.StatusCode -eq 200) `
        -Message "POST 뒤 목록 GET은 HTTP 200이다. 실제 값: $($freshAfterPost.StatusCode)"
    Assert-That -Condition ($readsAfterFresh -eq ($readsAfterPost + 1)) `
        -Message "성공한 POST의 generation 전환 뒤 목록은 Repository를 정확히 1회 새로 읽는다. 전=$readsAfterPost, 후=$readsAfterFresh"
    Assert-That -Condition ($freshOrigin -ne $warmOrigin) `
        -Message "generation 전환 뒤 originReadNumber가 갱신된다. 전=$warmOrigin, 후=$freshOrigin"
    Assert-That -Condition ($createdMatches.Count -eq 1) `
        -Message "새 목록에 방금 만든 title이 정확히 1개 있다. 실제 개수: $($createdMatches.Count)"

    Write-Host "`n[6/7] 갱신된 목록 hit와 잘못된 POST의 세대 보존" -ForegroundColor Yellow
    $repeatAfterPost = Invoke-ApiRequest `
        -Method ([System.Net.Http.HttpMethod]::Get) `
        -Path $releasePath `
        -Headers $evictionHeaders
    $readsAfterRepeat = Get-RepositoryReadCount
    $repeatOrigin = Get-OriginReadNumber -Response $repeatAfterPost -Context 'POST 뒤 반복 목록 GET'

    Assert-That -Condition ($repeatAfterPost.StatusCode -eq 200) `
        -Message "POST 뒤 반복 목록 GET은 HTTP 200이다. 실제 값: $($repeatAfterPost.StatusCode)"
    Assert-That -Condition ($readsAfterRepeat -eq $readsAfterFresh) `
        -Message "갱신된 목록의 반복 GET은 cache hit라서 다시 읽지 않는다. 기대/실제=$readsAfterFresh"
    Assert-That -Condition ($repeatOrigin -eq $freshOrigin) `
        -Message "갱신된 cache hit의 originReadNumber가 같다. 기대/실제=$freshOrigin"
    Assert-That -Condition ($repeatAfterPost.Body -ceq $freshAfterPost.Body) `
        -Message '갱신된 목록의 반복 GET 본문은 직전 본문과 정확히 같다.'

    $invalidPostBody = @{
        title    = '   '
        body     = 'title이 공백뿐이므로 저장되면 안 됩니다.'
        category = $runCategory
    }
    $invalid = Invoke-ApiRequest `
        -Method ([System.Net.Http.HttpMethod]::Post) `
        -Path '/announcements' `
        -Headers $evictionHeaders `
        -JsonBody $invalidPostBody
    Assert-That -Condition ($invalid.StatusCode -eq 400) `
        -Message "잘못된 POST는 HTTP 400 Bad Request이다. 실제 값: $($invalid.StatusCode)"

    $readsAfterInvalid = Get-RepositoryReadCount
    $afterInvalidGet = Invoke-ApiRequest `
        -Method ([System.Net.Http.HttpMethod]::Get) `
        -Path $releasePath `
        -Headers $evictionHeaders
    $readsAfterInvalidGet = Get-RepositoryReadCount
    $afterInvalidOrigin = Get-OriginReadNumber -Response $afterInvalidGet -Context '잘못된 POST 뒤 목록 GET'

    Assert-That -Condition ($afterInvalidGet.StatusCode -eq 200) `
        -Message "잘못된 POST 뒤 목록 GET은 HTTP 200이다. 실제 값: $($afterInvalidGet.StatusCode)"
    Assert-That -Condition ($readsAfterInvalidGet -eq $readsAfterInvalid) `
        -Message "잘못된 POST는 generation/tag를 바꾸지 않아 다음 GET이 cache hit다. 기대/실제=$readsAfterInvalid"
    Assert-That -Condition ($afterInvalidOrigin -eq $freshOrigin) `
        -Message "잘못된 POST 뒤 originReadNumber가 유지된다. 기대/실제=$freshOrigin"
    Assert-That -Condition ($afterInvalidGet.Body -ceq $freshAfterPost.Body) `
        -Message '잘못된 POST 뒤에도 캐시된 목록 본문이 그대로 유지된다.'

    Write-Host "`n[7/7] 존재하지 않는 id의 404" -ForegroundColor Yellow
    $missingId = [Guid]::NewGuid()
    $missing = Invoke-ApiRequest `
        -Method ([System.Net.Http.HttpMethod]::Get) `
        -Path "/announcements/$missingId" `
        -Headers @{ Accept = 'application/json'; 'Accept-Language' = 'ko-KR' }

    Assert-That -Condition ($missing.StatusCode -eq 404) `
        -Message "존재하지 않는 id의 상세 GET은 HTTP 404 Not Found이다. 실제 값: $($missing.StatusCode)"
    $missingCode = [string](Get-RequiredJsonProperty `
        -JsonObject $missing.Json `
        -Name 'code' `
        -Context '존재하지 않는 id 상세 GET')
    $missingMessage = [string](Get-RequiredJsonProperty `
        -JsonObject $missing.Json `
        -Name 'message' `
        -Context '존재하지 않는 id 상세 GET')
    Assert-That -Condition (-not [string]::IsNullOrWhiteSpace($missingCode)) `
        -Message "404 JSON에는 비어 있지 않은 code가 있다. 실제 값: $missingCode"
    Assert-That -Condition (-not [string]::IsNullOrWhiteSpace($missingMessage)) `
        -Message '404 JSON에는 사용자가 이해할 수 있는 message가 있다.'

    Write-Host "`n모든 HTTP/Output Cache 검증을 통과했습니다." -ForegroundColor Cyan
    exit 0
}
catch {
    # ErrorActionPreference가 Stop이어도 실패 메시지를 쓰는 동작 자체가 새 예외를 만들지 않도록 stderr를 직접 사용합니다.
    [Console]::Error.WriteLine("HTTP 검증이 중단되었습니다: $($_.Exception.Message)")
    exit 1
}
finally {
    $script:HttpClient.Dispose()
}
