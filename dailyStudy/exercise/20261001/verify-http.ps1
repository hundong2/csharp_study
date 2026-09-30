[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$executablePath = Join-Path $PSScriptRoot 'src\WorkshopContractApi\bin\Release\net10.0\WorkshopContractApi.exe'
$assemblyPath = Join-Path $PSScriptRoot 'src\WorkshopContractApi\bin\Release\net10.0\WorkshopContractApi.dll'
if (-not (Test-Path -LiteralPath $executablePath -PathType Leaf) -or
    -not (Test-Path -LiteralPath $assemblyPath -PathType Leaf)) {
    throw "Release 실행 파일이 없습니다. 먼저 README의 dotnet build 명령을 실행하세요: $executablePath"
}

$projectRoot = Join-Path $PSScriptRoot 'src\WorkshopContractApi'
$latestSource = Get-ChildItem -LiteralPath $projectRoot -Recurse -File |
    Where-Object {
        $_.FullName -notmatch '\\(?:bin|obj)\\' -and
        $_.Extension -in @('.cs', '.csproj')
    } |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1
$assembly = Get-Item -LiteralPath $assemblyPath
if ($null -ne $latestSource -and $latestSource.LastWriteTimeUtc -gt $assembly.LastWriteTimeUtc) {
    throw "Release 실행 파일이 source보다 오래되었습니다. dotnet build -c Release를 다시 실행하세요."
}

$runId = [Guid]::NewGuid().ToString('N')
$tempDirectory = [System.IO.Path]::GetTempPath()
$stdoutPath = Join-Path $tempDirectory "workshop-contract-$runId.stdout.log"
$stderrPath = Join-Path $tempDirectory "workshop-contract-$runId.stderr.log"
$serverProcess = $null
$baseUrl = $null
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

function Convert-ContentToString {
    param(
        [Parameter(Mandatory)]
        [object]$Content
    )

    if ($Content -is [byte[]]) {
        return [System.Text.Encoding]::UTF8.GetString($Content)
    }

    return [string]$Content
}

function Invoke-Api {
    param(
        [Parameter(Mandatory)]
        [ValidateSet('GET', 'POST')]
        [string]$Method,
        [Parameter(Mandatory)]
        [string]$Path,
        [string]$Body,
        [string]$ContentType = 'application/json'
    )

    $request = @{
        Method = $Method
        Uri = "$baseUrl$Path"
        SkipHttpErrorCheck = $true
        TimeoutSec = 5
    }

    if ($PSBoundParameters.ContainsKey('Body')) {
        $request.ContentType = $ContentType
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
        [string]$Instance,
        [Parameter(Mandatory)]
        [string]$Field,
        [string]$Detail
    )

    Assert-True ($Response.StatusCode -eq $StatusCode) "오류 상태는 $StatusCode 이다."
    $contentType = [string]($Response.Headers.'Content-Type' | Select-Object -First 1)
    Assert-True ($contentType.StartsWith('application/problem+json')) '오류 media type은 application/problem+json이다.'

    $json = Convert-ContentToString -Content $Response.Content
    $document = [System.Text.Json.JsonDocument]::Parse($json)
    try {
        $root = $document.RootElement
        Assert-True ($root.GetProperty('status').GetInt32() -eq $StatusCode) 'Problem Details status가 HTTP 상태와 같다.'
        Assert-True ($root.GetProperty('code').GetString() -eq $Code) "오류 code는 $Code 이다."
        Assert-True (-not [string]::IsNullOrWhiteSpace($root.GetProperty('title').GetString())) '사람이 읽는 title이 있다.'
        Assert-True (-not [string]::IsNullOrWhiteSpace($root.GetProperty('detail').GetString())) '안전한 detail이 있다.'
        Assert-True ($root.GetProperty('instance').GetString() -eq $Instance) "오류 instance는 $Instance 이다."
        Assert-True ($root.GetProperty('field').GetString() -eq $Field) "오류 field는 $Field 이다."
        Assert-True (-not [string]::IsNullOrWhiteSpace($root.GetProperty('traceId').GetString())) '서버 로그와 연결할 traceId가 있다.'
        if ($PSBoundParameters.ContainsKey('Detail')) {
            Assert-True ($root.GetProperty('detail').GetString() -eq $Detail) '계층에 맞는 안전한 구체 detail이 보존된다.'
        }
    }
    finally {
        $document.Dispose()
    }
}

try {
    $arguments = @(
        '--urls', 'http://127.0.0.1:0',
        '--environment', 'Production'
    )
    $serverProcess = Start-Process `
        -FilePath $executablePath `
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
                    $rootResponse = Invoke-WebRequest `
                        -Uri "$baseUrl/" `
                        -SkipHttpErrorCheck `
                        -TimeoutSec 1
                    $rootJson = Convert-ContentToString -Content $rootResponse.Content
                    $rootDocument = [System.Text.Json.JsonDocument]::Parse($rootJson)
                    try {
                        $root = $rootDocument.RootElement
                        $ready = $rootResponse.StatusCode -eq 200 -and
                            $root.GetProperty('service').GetString() -eq 'workshop-contract' -and
                            $root.GetProperty('status').GetString() -eq 'ready'
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
            # Kestrel이 포트를 열기 전의 짧은 연결 실패는 기다렸다가 다시 확인합니다.
        }

        Start-Sleep -Milliseconds 125
    }

    Assert-True $ready '실제 Kestrel 서버가 제한 시간 안에 준비된다.'

    $openApiResponse = Invoke-Api -Method GET -Path '/openapi/v1.json'
    Assert-True ($openApiResponse.StatusCode -eq 200) 'OpenAPI JSON endpoint는 200이다.'
    $openApiJson = Convert-ContentToString -Content $openApiResponse.Content
    $openApiDocument = [System.Text.Json.JsonDocument]::Parse($openApiJson)
    try {
        $openApiRoot = $openApiDocument.RootElement
        Assert-True ($openApiRoot.GetProperty('openapi').GetString() -eq '3.1.1') '문서는 OpenAPI 3.1.1이다.'
        Assert-True ($openApiRoot.GetProperty('info').GetProperty('title').GetString() -eq '워크숍 등록 계약 API') 'transformer 제목이 문서에 있다.'
        Assert-True ($openApiRoot.GetProperty('info').GetProperty('version').GetString() -eq 'v1') 'v1 문서 이름이 Info version에 있다.'
        $paths = $openApiRoot.GetProperty('paths')
        Assert-True (@($paths.EnumerateObject()).Count -eq 2) '업무 path 두 개만 문서화된다.'
        $post = $paths.GetProperty('/registrations').GetProperty('post')
        Assert-True ($post.GetProperty('operationId').GetString() -eq 'CreateWorkshopRegistration') 'POST operationId가 안정적이다.'
        $postResponses = $post.GetProperty('responses')
        foreach ($status in @('201', '400', '404', '409', '415')) {
            $ignored = [System.Text.Json.JsonElement]::new()
            Assert-True ($postResponses.TryGetProperty($status, [ref]$ignored)) "POST 문서가 $status 응답을 선언한다."
        }

        $locationHeader = $postResponses.GetProperty('201').GetProperty('headers').GetProperty('Location')
        Assert-True ($locationHeader.GetProperty('required').GetBoolean()) '201 Location header는 필수로 문서화된다.'
        Assert-True ($locationHeader.GetProperty('schema').GetProperty('format').GetString() -eq 'uri-reference') 'Location schema는 uri-reference 형식이다.'

        $schemas = $openApiRoot.GetProperty('components').GetProperty('schemas')
        $requestSchema = $schemas.GetProperty('CreateRegistrationRequest')
        $requiredFields = @(
            $requestSchema.GetProperty('required').EnumerateArray() |
                ForEach-Object { $_.GetString() }
        )
        $expectedFields = @('registrationId', 'sessionId', 'attendeeId', 'seatNumber', 'attendeeTier')
        Assert-True (@(Compare-Object $requiredFields $expectedFields).Count -eq 0) '요청 schema의 다섯 필드는 모두 required다.'
        Assert-True ($requestSchema.GetProperty('properties').GetProperty('seatNumber').GetProperty('type').GetString() -eq 'integer') 'seatNumber schema는 JSON integer다.'
        Assert-True ($requestSchema.GetProperty('properties').GetProperty('registrationId').GetProperty('pattern').GetString() -eq '^\s*[A-Za-z0-9-]{3,40}\s*$') '요청 ID schema가 trim 전후 공백 규칙을 표현한다.'

        $problemProperties = $schemas.GetProperty('ApiProblemResponse').GetProperty('properties')
        $ignoredProperty = [System.Text.Json.JsonElement]::new()
        Assert-True ($problemProperties.TryGetProperty('code', [ref]$ignoredProperty)) '오류 schema에 code가 명시된다.'
        $ignoredProperty = [System.Text.Json.JsonElement]::new()
        Assert-True ($problemProperties.TryGetProperty('field', [ref]$ignoredProperty)) '오류 schema에 field가 명시된다.'
        $ignoredProperty = [System.Text.Json.JsonElement]::new()
        Assert-True ($problemProperties.TryGetProperty('traceId', [ref]$ignoredProperty)) '오류 schema에 traceId가 명시된다.'
        $problemReference = $postResponses.GetProperty('400').GetProperty('content').GetProperty('application/problem+json').GetProperty('schema').GetProperty('$ref').GetString()
        Assert-True ($problemReference -eq '#/components/schemas/ApiProblemResponse') '400 응답은 전용 오류 schema를 참조한다.'

        $get = $paths.GetProperty('/registrations/{registrationId}').GetProperty('get')
        Assert-True ($get.GetProperty('operationId').GetString() -eq 'GetWorkshopRegistration') 'GET operationId가 안정적이다.'
        $registrationIdParameter = @(
            $get.GetProperty('parameters').EnumerateArray() |
                Where-Object { $_.GetProperty('name').GetString() -eq 'registrationId' }
        )
        Assert-True ($registrationIdParameter.Count -eq 1) 'GET registrationId parameter가 정확히 하나다.'
        Assert-True ($registrationIdParameter[0].GetProperty('schema').GetProperty('pattern').GetString() -eq '^\s*[A-Za-z0-9-]{3,40}\s*$') 'GET ID schema가 Domain 정규화 규칙을 표현한다.'
    }
    finally {
        $openApiDocument.Dispose()
    }

    $validBody = @'
{
  "registrationId": "verify-1",
  "sessionId": "csharp-101",
  "attendeeId": "student-1",
  "seatNumber": 1,
  "attendeeTier": "Premium"
}
'@
    $created = Invoke-Api -Method POST -Path '/registrations' -Body $validBody
    Assert-True ($created.StatusCode -eq 201) '유효한 예약은 201 Created다.'
    $location = [string]($created.Headers.Location | Select-Object -First 1)
    Assert-True ($location -eq '/registrations/VERIFY-1') '201 Location은 정규화된 조회 URL이다.'
    $createdJson = Convert-ContentToString -Content $created.Content
    $createdDocument = [System.Text.Json.JsonDocument]::Parse($createdJson)
    try {
        $createdRoot = $createdDocument.RootElement
        Assert-True ($createdRoot.GetProperty('registrationId').GetString() -eq 'VERIFY-1') '201 본문은 정규화된 예약 ID를 담는다.'
        Assert-True ($createdRoot.GetProperty('priceWon').GetInt32() -eq 40000) 'Premium 가격은 40,000원이다.'
    }
    finally {
        $createdDocument.Dispose()
    }

    $read = Invoke-Api -Method GET -Path '/registrations/verify-1'
    Assert-True ($read.StatusCode -eq 200) '생성한 예약은 200으로 조회된다.'

    $whitespaceBody = @'
{
  "registrationId": " verify-space ",
  "sessionId": " csharp-101 ",
  "attendeeId": " student-space ",
  "seatNumber": 2,
  "attendeeTier": " premium "
}
'@
    $normalized = Invoke-Api -Method POST -Path '/registrations' -Body $whitespaceBody
    Assert-True ($normalized.StatusCode -eq 201) 'schema가 허용한 앞뒤 공백 입력은 runtime에서도 201이다.'
    $normalizedLocation = [string]($normalized.Headers.Location | Select-Object -First 1)
    Assert-True ($normalizedLocation -eq '/registrations/VERIFY-SPACE') '공백 입력의 Location은 정규화된 ID다.'
    $normalizedJson = Convert-ContentToString -Content $normalized.Content
    $normalizedDocument = [System.Text.Json.JsonDocument]::Parse($normalizedJson)
    try {
        Assert-True ($normalizedDocument.RootElement.GetProperty('attendeeId').GetString() -eq 'STUDENT-SPACE') '공백 입력 본문도 정규화된다.'
    }
    finally {
        $normalizedDocument.Dispose()
    }

    $invalid = Invoke-Api -Method POST -Path '/registrations' -Body ($validBody -replace 'verify-1', 'bad/id')
    Assert-Problem $invalid 400 'registration.id.invalid' '/registrations' 'registrationId' 'registrationId은(는) 3~40자의 영문, 숫자, 하이픈만 사용할 수 있습니다.'

    $missing = Invoke-Api -Method POST -Path '/registrations' -Body (($validBody -replace 'verify-1', 'verify-2') -replace 'csharp-101', 'missing-session')
    Assert-Problem $missing 404 'workshop.session.not_found' '/registrations' 'sessionId'

    $conflict = Invoke-Api -Method POST -Path '/registrations' -Body ($validBody -replace 'verify-1', 'verify-3')
    Assert-Problem $conflict 409 'registration.seat.taken' '/registrations' 'seatNumber'

    $duplicate = Invoke-Api -Method POST -Path '/registrations' -Body ($validBody -replace '"seatNumber": 1', '"seatNumber": 2')
    Assert-Problem $duplicate 409 'registration.id.duplicate' '/registrations' 'registrationId'

    $absent = Invoke-Api -Method GET -Path '/registrations/VERIFY-NOT-FOUND'
    Assert-Problem $absent 404 'registration.not_found' '/registrations/VERIFY-NOT-FOUND' 'registrationId'

    $invalidRouteId = Invoke-Api -Method GET -Path '/registrations/x'
    Assert-Problem $invalidRouteId 400 'registration.id.invalid' '/registrations/x' 'registrationId'

    $malformed = Invoke-Api -Method POST -Path '/registrations' -Body '{'
    Assert-Problem $malformed 400 'http.bad_request' '/registrations' 'request' '요청 JSON 형식과 값의 자료형을 확인하세요.'

    $nullJson = Invoke-Api -Method POST -Path '/registrations' -Body 'null'
    Assert-Problem $nullJson 400 'http.bad_request' '/registrations' 'request'

    $quotedSeat = Invoke-Api -Method POST -Path '/registrations' -Body ($validBody -replace '"seatNumber": 1', '"seatNumber": "2"')
    Assert-Problem $quotedSeat 400 'http.bad_request' '/registrations' 'request'

    $wrongMediaType = Invoke-Api -Method POST -Path '/registrations' -Body '{}' -ContentType 'text/plain'
    Assert-Problem $wrongMediaType 415 'http.unsupported_media_type' '/registrations' 'request'

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
