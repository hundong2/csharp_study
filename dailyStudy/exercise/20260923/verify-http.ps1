param(
    [string]$BaseUri = 'http://127.0.0.1:5080'
)

$ErrorActionPreference = 'Stop'
$base = $BaseUri.TrimEnd('/')
$itemId = '11111111-1111-1111-1111-111111111111'
$passed = 0
$total = 27

# 실제 값과 기대값이 다르면 즉시 멈춰 어떤 HTTP 계약이 깨졌는지 알려 줍니다.
function Assert-Equal {
    param(
        [AllowNull()]
        [object]$Actual,
        [AllowNull()]
        [object]$Expected,
        [string]$Message
    )

    if ($Actual -ne $Expected) {
        throw "$Message (기대: '$Expected', 실제: '$Actual')"
    }

    $script:passed++
    Write-Host "[$script:passed/$script:total] $Message"
}

# 반드시 참이어야 하는 응답 조건을 검사하고 실패 이유를 그대로 보여 줍니다.
function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }

    $script:passed++
    Write-Host "[$script:passed/$script:total] $Message"
}

# 4xx도 PowerShell 예외로 숨기지 않고 상태·헤더·본문을 직접 검증할 수 있게 요청을 보냅니다.
function Send-ApiRequest {
    param(
        [string]$Method,
        [string]$Path,
        [hashtable]$Headers = @{},
        [AllowNull()]
        [string]$Body = $null
    )

    $request = @{
        Uri = "$base$Path"
        Method = $Method
        Headers = $Headers
        SkipHttpErrorCheck = $true
        # 학습용 400 검사에서는 일부러 잘못된 ETag도 보내야 하므로 client 쪽 사전 거절만 건너뜁니다.
        SkipHeaderValidation = $true
    }

    if ($null -ne $Body) {
        $request.ContentType = 'application/json'
        $request.Body = $Body
    }

    return Invoke-WebRequest @request
}

$health = Send-ApiRequest -Method Get -Path '/health'
Assert-Equal ([int]$health.StatusCode) 200 'health endpoint는 200을 반환한다.'

$initial = Send-ApiRequest -Method Get -Path "/catalog/$itemId"
Assert-Equal ([int]$initial.StatusCode) 200 '첫 상품 조회는 200을 반환한다.'
Assert-Equal ([string]$initial.Headers.ETag) '"v1"' '첫 조회 ETag는 "v1"이다.'
$initialBody = $initial.Content | ConvertFrom-Json
Assert-Equal ([long]$initialBody.version) 1 '첫 응답 본문의 학습용 version은 1이다.'

$notModified = Send-ApiRequest -Method Get -Path "/catalog/$itemId" -Headers @{ 'If-None-Match' = '"v1"' }
Assert-Equal ([int]$notModified.StatusCode) 304 '같은 If-None-Match는 304를 반환한다.'
Assert-True ([string]::IsNullOrEmpty($notModified.Content)) '304 응답은 JSON 본문을 보내지 않는다.'

$weakNotModified = Send-ApiRequest -Method Get -Path "/catalog/$itemId" -Headers @{ 'If-None-Match' = 'W/"v1"' }
Assert-Equal ([int]$weakNotModified.StatusCode) 304 'GET의 weak ETag 비교도 304를 반환한다.'

$invalidReadHeader = Send-ApiRequest -Method Get -Path "/catalog/$itemId" -Headers @{ 'If-None-Match' = 'v1' }
Assert-Equal ([int]$invalidReadHeader.StatusCode) 400 '따옴표 없는 If-None-Match는 400을 반환한다.'

$validBody = @{ name = '저소음 기계식 키보드'; price = 139000 } | ConvertTo-Json -Compress
$missingPrecondition = Send-ApiRequest -Method Patch -Path "/catalog/$itemId" -Body $validBody
Assert-Equal ([int]$missingPrecondition.StatusCode) 428 'If-Match 없는 수정은 428을 반환한다.'
Assert-Equal ([string]$missingPrecondition.Headers.ETag) '"v1"' '428은 client가 다시 사용할 현재 ETag를 제공한다.'

$invalidWriteHeader = Send-ApiRequest -Method Patch -Path "/catalog/$itemId" -Headers @{ 'If-Match' = 'v1' } -Body $validBody
Assert-Equal ([int]$invalidWriteHeader.StatusCode) 400 '잘못된 If-Match 문법은 400을 반환한다.'

$mixedWildcard = Send-ApiRequest -Method Patch -Path "/catalog/$itemId" -Headers @{ 'If-Match' = '*, "v1"' } -Body $validBody
Assert-Equal ([int]$mixedWildcard.StatusCode) 400 '와일드카드와 개별 ETag를 섞은 If-Match 목록은 400을 반환한다.'

$weakWrite = Send-ApiRequest -Method Patch -Path "/catalog/$itemId" -Headers @{ 'If-Match' = 'W/"v1"' } -Body $validBody
Assert-Equal ([int]$weakWrite.StatusCode) 412 'weak If-Match는 strong 쓰기 비교에 실패해 412를 반환한다.'

$updated = Send-ApiRequest -Method Patch -Path "/catalog/$itemId" -Headers @{ 'If-Match' = '"v1"' } -Body $validBody
Assert-Equal ([int]$updated.StatusCode) 200 '현재 strong ETag 수정은 200을 반환한다.'
Assert-Equal ([string]$updated.Headers.ETag) '"v2"' '성공한 수정은 ETag를 "v2"로 높인다.'
$updatedBody = $updated.Content | ConvertFrom-Json
Assert-Equal ([decimal]$updatedBody.price) 139000 '성공 응답은 새 가격을 담는다.'

$staleBody = @{ name = '오래된 화면의 상품명'; price = 120000 } | ConvertTo-Json -Compress
$stale = Send-ApiRequest -Method Patch -Path "/catalog/$itemId" -Headers @{ 'If-Match' = '"v1"' } -Body $staleBody
Assert-Equal ([int]$stale.StatusCode) 412 '오래된 ETag 수정은 412를 반환한다.'
Assert-Equal ([string]$stale.Headers.ETag) '"v2"' '412는 현재 ETag "v2"를 제공한다.'

$oldCache = Send-ApiRequest -Method Get -Path "/catalog/$itemId" -Headers @{ 'If-None-Match' = '"v1"' }
Assert-Equal ([int]$oldCache.StatusCode) 200 '오래된 cache ETag 조회는 새 본문과 200을 반환한다.'
Assert-Equal ([string]$oldCache.Headers.ETag) '"v2"' '새 본문에는 최신 ETag "v2"가 있다.'

$invalidBody = @{ name = '저소음 기계식 키보드'; price = -1 } | ConvertTo-Json -Compress
$invalidDomain = Send-ApiRequest -Method Patch -Path "/catalog/$itemId" -Headers @{ 'If-Match' = '"v2"' } -Body $invalidBody
Assert-Equal ([int]$invalidDomain.StatusCode) 400 'Domain 규칙을 어긴 가격은 400을 반환한다.'
$afterInvalid = Send-ApiRequest -Method Get -Path "/catalog/$itemId"
Assert-Equal ([string]$afterInvalid.Headers.ETag) '"v2"' '검증 실패는 버전을 올리지 않는다.'

$wildcardBody = @{ name = '텐키리스 저소음 키보드'; price = 145000 } | ConvertTo-Json -Compress
$wildcard = Send-ApiRequest -Method Patch -Path "/catalog/$itemId" -Headers @{ 'If-Match' = '*' } -Body $wildcardBody
Assert-Equal ([int]$wildcard.StatusCode) 200 '존재하는 상품의 If-Match * 수정은 성공한다.'
Assert-Equal ([string]$wildcard.Headers.ETag) '"v3"' '와일드카드 수정도 새 ETag "v3"를 반환한다.'

$listRead = Send-ApiRequest -Method Get -Path "/catalog/$itemId" -Headers @{ 'If-None-Match' = '"other", W/"v3"' }
Assert-Equal ([int]$listRead.StatusCode) 304 'ETag 목록 중 하나가 weak 비교로 맞으면 304를 반환한다.'

$missing = Send-ApiRequest -Method Get -Path "/catalog/$([Guid]::NewGuid())"
Assert-Equal ([int]$missing.StatusCode) 404 '없는 상품 조회는 404를 반환한다.'

$malformed = Send-ApiRequest -Method Patch -Path "/catalog/$itemId" -Headers @{ 'If-Match' = '"v3"' } -Body '{'
Assert-Equal ([int]$malformed.StatusCode) 400 '깨진 JSON은 endpoint 실행 전에 400을 반환한다.'

Write-Host "HTTP 자동 검증 통과: $passed/$total"
