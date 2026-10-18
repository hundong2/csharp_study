[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = Join-Path $PSScriptRoot 'src\InterviewValidationApi'
$executablePath = Join-Path $projectRoot 'bin\Release\net10.0\InterviewValidationApi.exe'
$assemblyPath = Join-Path $projectRoot 'bin\Release\net10.0\InterviewValidationApi.dll'
$runId = [Guid]::NewGuid().ToString('N')
$tempDirectory = [System.IO.Path]::GetTempPath()
$stdoutPath = Join-Path $tempDirectory "interview-validation-$runId.stdout.log"
$stderrPath = Join-Path $tempDirectory "interview-validation-$runId.stderr.log"
$serverProcess = $null
$baseUrl = $null
$passed = 0
$utf8 = [System.Text.UTF8Encoding]::new($false, $true)
$httpClient = [System.Net.Http.HttpClient]::new()
$httpClient.Timeout = [TimeSpan]::FromSeconds(5)

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

function ConvertTo-Utf8Bytes {
    param(
        [Parameter(Mandatory)]
        [AllowEmptyString()]
        [string]$Text
    )

    # 앞의 쉼표는 PowerShell pipeline이 byte 배열을 byte 하나씩 펼치지 않게 합니다.
    return ,$script:utf8.GetBytes($Text)
}

function New-ResponseSnapshot {
    param(
        [Parameter(Mandatory)]
        [System.Net.Http.HttpResponseMessage]$Response
    )

    $bytes = $Response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
    $contentType = if ($null -eq $Response.Content.Headers.ContentType) {
        ''
    }
    else {
        $Response.Content.Headers.ContentType.ToString()
    }
    $location = if ($null -eq $Response.Headers.Location) {
        ''
    }
    else {
        $Response.Headers.Location.OriginalString
    }

    return [pscustomobject]@{
        StatusCode = [int]$Response.StatusCode
        BodyBytes = $bytes
        Body = $script:utf8.GetString($bytes)
        ContentType = $contentType
        Location = $location
    }
}

function Invoke-Api {
    param(
        [Parameter(Mandatory)]
        [ValidateSet('GET', 'POST')]
        [string]$Method,
        [Parameter(Mandatory)]
        [string]$Path,
        [AllowNull()]
        [byte[]]$BodyBytes,
        [string]$ContentType = 'application/json'
    )

    $request = [System.Net.Http.HttpRequestMessage]::new(
        [System.Net.Http.HttpMethod]::$Method,
        "$script:baseUrl$Path")
    try {
        if ($null -ne $BodyBytes) {
            $request.Content = [System.Net.Http.ByteArrayContent]::new($BodyBytes)
            $request.Content.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse(
                $ContentType)
        }

        $response = $script:httpClient.SendAsync($request).GetAwaiter().GetResult()
        try {
            return New-ResponseSnapshot -Response $response
        }
        finally {
            $response.Dispose()
        }
    }
    finally {
        $request.Dispose()
    }
}

function New-InterviewBody {
    param(
        [string]$CandidateName = '홍길동',
        [string]$CandidateEmail = 'candidate@example.com',
        [Parameter(Mandatory)]
        [string]$StartsAtUtc,
        [int]$DurationMinutes = 60,
        [string]$Mode = 'remote',
        [AllowNull()]
        [string]$MeetingUrl = 'https://meet.example.test/interviews/1001',
        [AllowNull()]
        [string]$Room = $null,
        [string[]]$InterviewerIds = @('DEV-01')
    )

    $payload = [ordered]@{
        candidateName = $CandidateName
        candidateEmail = $CandidateEmail
        startsAtUtc = $StartsAtUtc
        durationMinutes = $DurationMinutes
        mode = $Mode
        meetingUrl = $MeetingUrl
        room = $Room
        interviewerIds = $InterviewerIds
    }

    return ConvertTo-Utf8Bytes -Text ($payload | ConvertTo-Json -Compress -Depth 4)
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
        [string]$Context
    )

    Assert-True ($Response.StatusCode -eq $StatusCode) "$Context 상태는 $StatusCode 이다."
    Assert-True (
        $Response.ContentType.StartsWith(
            'application/problem+json',
            [StringComparison]::OrdinalIgnoreCase)
    ) "$Context 응답은 application/problem+json이다."

    $document = [System.Text.Json.JsonDocument]::Parse($Response.Body)
    try {
        $root = $document.RootElement
        Assert-True ($root.GetProperty('status').GetInt32() -eq $StatusCode) "$Context Problem Details status가 HTTP 상태와 같다."
        Assert-True (-not [string]::IsNullOrWhiteSpace($root.GetProperty('title').GetString())) "$Context title이 있다."
        Assert-True (-not [string]::IsNullOrWhiteSpace($root.GetProperty('detail').GetString())) "$Context detail이 있다."
        Assert-True ($root.GetProperty('instance').GetString() -ceq $Instance) "$Context instance가 요청 경로와 같다."
        Assert-True ($root.GetProperty('code').GetString() -ceq $Code) "$Context 안정 오류 code는 $Code 이다."
        Assert-True (-not [string]::IsNullOrWhiteSpace($root.GetProperty('traceId').GetString())) "$Context traceId가 있다."
    }
    finally {
        $document.Dispose()
    }
}

if (-not (Test-Path -LiteralPath $executablePath -PathType Leaf) -or
    -not (Test-Path -LiteralPath $assemblyPath -PathType Leaf)) {
    throw "Release 실행 파일이 없습니다. 먼저 dotnet build '$projectRoot\InterviewValidationApi.csproj' -c Release를 실행하세요."
}

# 오래된 binary를 검증하는 실수를 막기 위해 source와 assembly 수정 시각을 비교합니다.
$latestSource = Get-ChildItem -LiteralPath $projectRoot -Recurse -File |
    Where-Object {
        $_.FullName -notmatch '\\(?:bin|obj)\\' -and
        $_.Extension -in @('.cs', '.csproj', '.json')
    } |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1
$assembly = Get-Item -LiteralPath $assemblyPath
if ($null -ne $latestSource -and $latestSource.LastWriteTimeUtc -gt $assembly.LastWriteTimeUtc) {
    throw 'Release 실행 파일이 source보다 오래되었습니다. dotnet build -c Release를 다시 실행하세요.'
}

try {
    $serverProcess = Start-Process `
        -FilePath $executablePath `
        -ArgumentList @(
            '--urls', 'http://127.0.0.1:0',
            '--environment', 'Production') `
        -PassThru `
        -WindowStyle Hidden `
        -RedirectStandardOutput $stdoutPath `
        -RedirectStandardError $stderrPath

    $ready = $false
    foreach ($attempt in 1..100) {
        if ($serverProcess.HasExited) {
            throw "검증 서버가 준비되기 전에 종료되었습니다. ExitCode=$($serverProcess.ExitCode)"
        }

        try {
            if (Test-Path -LiteralPath $stdoutPath) {
                $serverOutput = Get-Content -Raw -LiteralPath $stdoutPath
                $addressMatch = [regex]::Match([string]$serverOutput, 'http://127\.0\.0\.1:\d+')
                if ($addressMatch.Success) {
                    $baseUrl = $addressMatch.Value
                    $probe = Invoke-Api -Method GET -Path '/'
                    $ready = $probe.StatusCode -eq 200
                    if ($ready) {
                        break
                    }
                }
            }
        }
        catch {
            # Kestrel이 listen을 시작하기 전의 짧은 연결 실패는 기다렸다가 다시 확인합니다.
        }

        Start-Sleep -Milliseconds 100
    }
    Assert-True $ready 'InterviewValidationApi가 Production의 동적 loopback 포트에서 준비된다.'

    $status = Invoke-Api -Method GET -Path '/'
    Assert-True ($status.StatusCode -eq 200) '상태 endpoint는 200이다.'
    Assert-True ($status.Body -match '"automaticValidation":true') '상태 응답은 자동 검증 사용을 공개한다.'

    $future = [DateTimeOffset]::UtcNow.AddDays(2)
    $future = [DateTimeOffset]::new(
        $future.Year,
        $future.Month,
        $future.Day,
        6,
        0,
        0,
        [TimeSpan]::Zero)
    $futureText = $future.ToString('O', [Globalization.CultureInfo]::InvariantCulture)

    $invalidPayload = [ordered]@{
        candidateName = ''
        candidateEmail = 'not-an-email'
        startsAtUtc = $future.ToOffset([TimeSpan]::FromHours(9)).ToString('O')
        durationMinutes = 47
        mode = 'remote'
        meetingUrl = 'http://unsafe.example.test/room'
        room = 'A-101'
        interviewerIds = @('DEV-01', 'dev-01')
    } | ConvertTo-Json -Compress -Depth 4
    $invalid = Invoke-Api `
        -Method POST `
        -Path '/interviews' `
        -BodyBytes (ConvertTo-Utf8Bytes -Text $invalidPayload)
    Assert-Problem `
        -Response $invalid `
        -StatusCode 400 `
        -Code 'validation.failed' `
        -Instance '/interviews' `
        -Context '.NET 10 자동 검증 실패'
    $invalidDocument = [System.Text.Json.JsonDocument]::Parse($invalid.Body)
    try {
        $errors = $invalidDocument.RootElement.GetProperty('errors')
        $errorProperties = @($errors.EnumerateObject())
        Assert-True ($errorProperties.Count -ge 3) '속성 검증은 여러 필드 오류를 한 응답에 모은다.'
    }
    finally {
        $invalidDocument.Dispose()
    }

    # 속성 Attribute가 모두 통과해야 IValidatableObject의 객체 단위 교차 검증 단계가 실행됩니다.
    $crossFieldPayload = [ordered]@{
        candidateName = '홍길동'
        candidateEmail = 'candidate@example.com'
        startsAtUtc = $futureText
        durationMinutes = 47
        mode = 'remote'
        meetingUrl = 'http://unsafe.example.test/room'
        room = 'A-101'
        interviewerIds = @('DEV-01', 'dev-01')
    } | ConvertTo-Json -Compress -Depth 4
    $crossField = Invoke-Api `
        -Method POST `
        -Path '/interviews' `
        -BodyBytes (ConvertTo-Utf8Bytes -Text $crossFieldPayload)
    Assert-Problem `
        -Response $crossField `
        -StatusCode 400 `
        -Code 'validation.failed' `
        -Instance '/interviews' `
        -Context 'IValidatableObject 교차 검증 실패'
    $crossFieldDocument = [System.Text.Json.JsonDocument]::Parse($crossField.Body)
    try {
        $crossErrors = @($crossFieldDocument.RootElement.GetProperty('errors').EnumerateObject())
        Assert-True ($crossErrors.Count -eq 4) '교차 검증은 duration, room, URL, 중복 면접관 오류를 함께 반환한다.'
    }
    finally {
        $crossFieldDocument.Dispose()
    }

    $validBody = New-InterviewBody `
        -StartsAtUtc $futureText `
        -InterviewerIds @("DEV-$($runId.Substring(0, 6))")
    $created = Invoke-Api -Method POST -Path '/interviews' -BodyBytes $validBody
    Assert-True ($created.StatusCode -eq 201) '유효한 면접 일정은 201 Created다.'
    Assert-True ($created.ContentType.StartsWith('application/json')) '201 본문은 JSON이다.'

    $createdDocument = [System.Text.Json.JsonDocument]::Parse($created.Body)
    try {
        $createdRoot = $createdDocument.RootElement
        $interviewId = $createdRoot.GetProperty('id').GetString()
        Assert-True ([Guid]::TryParse($interviewId, [ref]([Guid]::Empty))) '201 본문에 GUID 일정 ID가 있다.'
        Assert-True ($createdRoot.GetProperty('candidateEmail').GetString() -ceq 'candidate@example.com') '성공 응답 이메일이 정규화되어 있다.'
        Assert-True ($createdRoot.GetProperty('mode').GetString() -ceq 'remote') '성공 응답 mode가 remote다.'
        Assert-True ($createdRoot.GetProperty('endsAtUtc').GetDateTimeOffset() -eq $future.AddMinutes(60)) '종료 시각은 시작 시각에 duration을 더한 값이다.'
    }
    finally {
        $createdDocument.Dispose()
    }
    Assert-True ($created.Location -ceq "/interviews/$interviewId") '201 Location이 생성 자원을 가리킨다.'

    $found = Invoke-Api -Method GET -Path "/interviews/$interviewId"
    Assert-True ($found.StatusCode -eq 200) '생성한 일정은 GET으로 조회된다.'
    Assert-True ($found.Body -match [regex]::Escape($interviewId)) 'GET 응답은 같은 일정 ID를 가진다.'

    $conflictBody = New-InterviewBody `
        -CandidateEmail 'other@example.com' `
        -StartsAtUtc $futureText `
        -InterviewerIds @("DEV-$($runId.Substring(0, 6))")
    $conflict = Invoke-Api -Method POST -Path '/interviews' -BodyBytes $conflictBody
    Assert-Problem `
        -Response $conflict `
        -StatusCode 409 `
        -Code 'interview.slot_conflict' `
        -Instance '/interviews' `
        -Context '면접관 시간 충돌'

    $independentBody = New-InterviewBody `
        -CandidateEmail 'independent@example.com' `
        -StartsAtUtc $futureText `
        -InterviewerIds @("QA-$($runId.Substring(0, 6))")
    $independent = Invoke-Api -Method POST -Path '/interviews' -BodyBytes $independentBody
    Assert-True ($independent.StatusCode -eq 201) '같은 시각이어도 면접관이 다르면 201이다.'

    # DateTimeOffset binding은 -00:00의 부호를 보존하지 않고 Offset=0 값으로 정규화합니다.
    # 원문 철자까지 제한하려면 DTO를 string으로 받거나 사용자 정의 JSON converter가 필요합니다.
    $negativeZeroText = $futureText -replace '\+00:00$', '-00:00'
    $negativeZeroBody = New-InterviewBody `
        -CandidateEmail 'zero-offset@example.com' `
        -StartsAtUtc $negativeZeroText `
        -InterviewerIds @("UTC-$($runId.Substring(0, 6))")
    $negativeZero = Invoke-Api -Method POST -Path '/interviews' -BodyBytes $negativeZeroBody
    Assert-True ($negativeZero.StatusCode -eq 201) 'binding은 -00:00 표기도 0 오프셋 값으로 받아들인다.'
    $negativeZeroDocument = [System.Text.Json.JsonDocument]::Parse($negativeZero.Body)
    try {
        $normalizedStart = $negativeZeroDocument.RootElement.GetProperty('startsAtUtc').GetString()
        Assert-True (
            $negativeZeroDocument.RootElement.GetProperty('startsAtUtc').GetDateTimeOffset().Offset -eq [TimeSpan]::Zero
        ) 'binding 뒤 시작 시각의 의미는 UTC 0 오프셋이다.'
        Assert-True (-not $normalizedStart.EndsWith('-00:00')) '응답은 잃어버린 -00:00 원문 부호를 그대로 돌려주지 않는다.'
    }
    finally {
        $negativeZeroDocument.Dispose()
    }

    $tooSoonText = [DateTimeOffset]::UtcNow.AddMinutes(5).ToString(
        'O',
        [Globalization.CultureInfo]::InvariantCulture)
    $tooSoonBody = New-InterviewBody `
        -CandidateEmail 'soon@example.com' `
        -StartsAtUtc $tooSoonText `
        -InterviewerIds @("OPS-$($runId.Substring(0, 6))")
    $tooSoon = Invoke-Api -Method POST -Path '/interviews' -BodyBytes $tooSoonBody
    Assert-Problem `
        -Response $tooSoon `
        -StatusCode 400 `
        -Code 'interview.too_soon' `
        -Instance '/interviews' `
        -Context '시간 업무 규칙 실패'

    $maximumTimeBody = New-InterviewBody `
        -CandidateEmail 'maximum@example.com' `
        -StartsAtUtc ([DateTimeOffset]::MaxValue.ToString('O')) `
        -DurationMinutes 180 `
        -InterviewerIds @("MAX-$($runId.Substring(0, 6))")
    $maximumTime = Invoke-Api -Method POST -Path '/interviews' -BodyBytes $maximumTimeBody
    Assert-Problem `
        -Response $maximumTime `
        -StatusCode 400 `
        -Code 'interview.invalid_time_range' `
        -Instance '/interviews' `
        -Context '표현 불가능한 종료 시각'

    $onsiteBody = New-InterviewBody `
        -CandidateEmail 'onsite@example.com' `
        -StartsAtUtc $future.AddHours(3).ToString('O') `
        -Mode 'onsite' `
        -MeetingUrl $null `
        -Room 'B-201' `
        -InterviewerIds @("HR-$($runId.Substring(0, 6))")
    $onsite = Invoke-Api -Method POST -Path '/interviews' -BodyBytes $onsiteBody
    Assert-True ($onsite.StatusCode -eq 201) '유효한 대면 면접도 201이다.'
    Assert-True ($onsite.Body -match '"location":"B-201"') '대면 응답 위치는 회의실이다.'

    $malformed = Invoke-Api `
        -Method POST `
        -Path '/interviews' `
        -BodyBytes (ConvertTo-Utf8Bytes -Text '{"candidateName":')
    Assert-True ($malformed.StatusCode -eq 400) '잘린 JSON은 400이다.'
    Assert-True ($malformed.ContentType.StartsWith('application/problem+json')) '잘린 JSON은 Problem Details다.'

    $wrongMedia = Invoke-Api `
        -Method POST `
        -Path '/interviews' `
        -BodyBytes (ConvertTo-Utf8Bytes -Text 'candidateName=Hong') `
        -ContentType 'text/plain'
    Assert-Problem `
        -Response $wrongMedia `
        -StatusCode 415 `
        -Code 'http.unsupported_media_type' `
        -Instance '/interviews' `
        -Context '지원하지 않는 media type'

    $unknownId = [Guid]::NewGuid().ToString()
    $notFound = Invoke-Api -Method GET -Path "/interviews/$unknownId"
    Assert-Problem `
        -Response $notFound `
        -StatusCode 404 `
        -Code 'interview.not_found' `
        -Instance "/interviews/$unknownId" `
        -Context '없는 일정 조회'

    Write-Host '검증 계약: Transport shape -> Domain invariant -> Application time/conflict rule'
    Write-Host "HTTP VERIFY PASSED: $passed assertions"
}
catch {
    $lineBreak = [Environment]::NewLine
    if (Test-Path -LiteralPath $stdoutPath) {
        Write-Error ('--- server stdout ---' + $lineBreak + (Get-Content -Raw -LiteralPath $stdoutPath)) -ErrorAction Continue
    }
    if (Test-Path -LiteralPath $stderrPath) {
        Write-Error ('--- server stderr ---' + $lineBreak + (Get-Content -Raw -LiteralPath $stderrPath)) -ErrorAction Continue
    }
    throw
}
finally {
    # 이 script가 시작한 정확한 PID만 종료합니다. 이름 기반 종료는 사용자의 다른 서버를 해칠 수 있습니다.
    if ($null -ne $serverProcess -and -not $serverProcess.HasExited) {
        Stop-Process -Id $serverProcess.Id -Force
        $serverProcess.WaitForExit()
    }

    $httpClient.Dispose()
    Remove-Item -LiteralPath $stdoutPath -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $stderrPath -Force -ErrorAction SilentlyContinue
}
