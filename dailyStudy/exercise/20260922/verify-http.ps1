param(
    [string]$BaseUri = 'http://127.0.0.1:5080'
)

$ErrorActionPreference = 'Stop'
$base = $BaseUri.TrimEnd('/')
$passed = 0
$total = 18

# 기대값이 다르면 바로 중단해 어떤 HTTP 계약이 깨졌는지 보여 줍니다.
function Assert-Equal {
    param(
        [object]$Actual,
        [object]$Expected,
        [string]$Message
    )

    if ($Actual -ne $Expected) {
        throw "$Message (기대: '$Expected', 실제: '$Actual')"
    }

    $script:passed++
    Write-Host "[$script:passed/$script:total] $Message"
}

# 참이어야 하는 보안·응답 조건을 검사합니다.
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

# 4xx/5xx도 예외로 숨기지 않고 상태 코드와 본문을 검증할 수 있게 HTTP 요청을 보냅니다.
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
    }

    if ($null -ne $Body) {
        $request.ContentType = 'application/json'
        $request.Body = $Body
    }

    return Invoke-WebRequest @request
}

$validJson = @{ title = '배포 점검표'; body = '승인자와 rollback 절차를 확인합니다.' } | ConvertTo-Json -Compress
$invalidJson = @{ title = ' '; body = '본문' } | ConvertTo-Json -Compress

$health = Send-ApiRequest -Method Get -Path '/health'
Assert-Equal ([int]$health.StatusCode) 200 '공개 health endpoint는 200을 반환한다.'

$anonymous = Send-ApiRequest -Method Post -Path '/documents' -Body $validJson
Assert-Equal ([int]$anonymous.StatusCode) 401 '인증 없는 생성은 401을 반환한다.'
Assert-True ([string]$anonymous.Headers.'Content-Type').StartsWith('application/problem+json') '401은 Problem Details media type을 사용한다.'
Assert-True ([string]$anonymous.Headers.'WWW-Authenticate').StartsWith('DemoHeader') '401은 DemoHeader challenge를 제공한다.'

$unknown = Send-ApiRequest -Method Post -Path '/documents' -Headers @{ 'X-Demo-User' = 'mallory' } -Body $validJson
Assert-Equal ([int]$unknown.StatusCode) 401 '알 수 없는 데모 사용자는 401을 반환한다.'

$viewer = Send-ApiRequest -Method Post -Path '/documents' -Headers @{ 'X-Demo-User' = 'bob' } -Body $validJson
Assert-Equal ([int]$viewer.StatusCode) 403 'Viewer의 생성은 403을 반환한다.'
Assert-True ([string]$viewer.Headers.'Content-Type').StartsWith('application/problem+json') '403은 Problem Details media type을 사용한다.'

$invalid = Send-ApiRequest -Method Post -Path '/documents' -Headers @{ 'X-Demo-User' = 'alice' } -Body $invalidJson
Assert-Equal ([int]$invalid.StatusCode) 400 '잘못된 Domain 입력은 400을 반환한다.'

$createdResponse = Send-ApiRequest -Method Post -Path '/documents' -Headers @{ 'X-Demo-User' = 'alice' } -Body $validJson
Assert-Equal ([int]$createdResponse.StatusCode) 201 'Editor의 유효한 생성은 201을 반환한다.'
$created = $createdResponse.Content | ConvertFrom-Json
Assert-Equal ([string]$createdResponse.Headers.Location) "/documents/$($created.id)" '201 Location은 생성된 문서 경로를 가리킨다.'
Assert-True ($null -eq $created.ownerId) '응답 DTO는 내부 권한 비교용 OwnerId를 노출하지 않는다.'

$owner = Send-ApiRequest -Method Get -Path "/documents/$($created.id)" -Headers @{ 'X-Demo-User' = 'alice' }
Assert-Equal ([int]$owner.StatusCode) 200 '문서 owner는 200으로 조회한다.'

$hidden = Send-ApiRequest -Method Get -Path "/documents/$($created.id)" -Headers @{ 'X-Demo-User' = 'bob' }
Assert-Equal ([int]$hidden.StatusCode) 404 '권한 없는 사용자는 존재를 감춘 404를 받는다.'

$admin = Send-ApiRequest -Method Get -Path "/documents/$($created.id)" -Headers @{ 'X-Demo-User' = 'admin' }
Assert-Equal ([int]$admin.StatusCode) 200 'Admin은 다른 사용자의 문서를 조회한다.'

$missing = Send-ApiRequest -Method Get -Path "/documents/$([Guid]::NewGuid())" -Headers @{ 'X-Demo-User' = 'bob' }
Assert-Equal ([int]$missing.StatusCode) 404 '존재하지 않는 문서도 404를 반환한다.'
Assert-Equal $hidden.Content $missing.Content '숨긴 문서와 없는 문서는 같은 응답 본문을 사용한다.'

$anonymousRead = Send-ApiRequest -Method Get -Path "/documents/$($created.id)"
Assert-Equal ([int]$anonymousRead.StatusCode) 401 '인증 없는 조회는 fallback policy 때문에 401을 반환한다.'

$malformed = Send-ApiRequest -Method Post -Path '/documents' -Headers @{ 'X-Demo-User' = 'alice' } -Body '{'
Assert-Equal ([int]$malformed.StatusCode) 400 '깨진 JSON은 endpoint 실행 전에 400을 반환한다.'

Write-Host "HTTP 자동 검증 통과: $passed/$total"
