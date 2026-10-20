[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = Join-Path $PSScriptRoot 'src\AuditEventApi'
$executablePath = Join-Path $projectRoot 'bin\Release\net10.0\AuditEventApi.exe'
$assemblyPath = Join-Path $projectRoot 'bin\Release\net10.0\AuditEventApi.dll'
$runId = [Guid]::NewGuid().ToString('N')
$idSuffix = $runId.Substring(0, 12).ToUpperInvariant()
$tempDirectory = [System.IO.Path]::GetTempPath()
$stdoutPath = Join-Path $tempDirectory "audit-event-$runId.stdout.log"
$stderrPath = Join-Path $tempDirectory "audit-event-$runId.stderr.log"
$serverProcess = $null
$baseUrl = $null
$passed = 0
$utf8 = [System.Text.UTF8Encoding]::new($false, $true)
$httpClient = $null

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

function ConvertTo-JsonBytes {
    param(
        [Parameter(Mandatory)]
        [object]$Value
    )

    return ConvertTo-Utf8Bytes -Text ($Value | ConvertTo-Json -Compress -Depth 5)
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

    # 오류 대체 문자를 허용하지 않는 UTF-8 decoder를 써서 깨진 한글 응답도 즉시 실패시킵니다.
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
        $request.Headers.Accept.ParseAdd('application/json')
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

function Assert-NoInternalDetails {
    param(
        [Parameter(Mandatory)]
        [string]$Body,
        [Parameter(Mandatory)]
        [string]$Context
    )

    $internalPattern = '(?i)(System\.[A-Za-z]|Exception:|stack\s*trace|\\src\\|\.cs:line\s+\d+)'
    Assert-True (-not [regex]::IsMatch($Body, $internalPattern)) "$Context 응답은 내부 예외 형식이나 source 경로를 노출하지 않는다."
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
        [string]$Field,
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
        Assert-True ($root.GetProperty('field').GetString() -ceq $Field) "$Context 안정 오류 field는 $Field 이다."
        Assert-True (-not [string]::IsNullOrWhiteSpace($root.GetProperty('traceId').GetString())) "$Context traceId가 있다."
    }
    finally {
        $document.Dispose()
    }

    Assert-NoInternalDetails -Body $Response.Body -Context $Context
}

function Assert-AuditEventResponse {
    param(
        [Parameter(Mandatory)]
        [object]$Response,
        [Parameter(Mandatory)]
        [int]$StatusCode,
        [Parameter(Mandatory)]
        [string]$EventId,
        [Parameter(Mandatory)]
        [string]$Kind,
        [Parameter(Mandatory)]
        [DateTimeOffset]$OccurredAtUtc,
        [Parameter(Mandatory)]
        [string]$ActorId,
        [Parameter(Mandatory)]
        [string[]]$SummaryFragments,
        [Parameter(Mandatory)]
        [string]$Context
    )

    Assert-True ($Response.StatusCode -eq $StatusCode) "$Context 상태는 $StatusCode 이다."
    Assert-True (
        $Response.ContentType.StartsWith('application/json', [StringComparison]::OrdinalIgnoreCase)
    ) "$Context 응답은 JSON이다."

    $document = [System.Text.Json.JsonDocument]::Parse($Response.Body)
    try {
        $root = $document.RootElement
        $properties = @($root.EnumerateObject())
        Assert-True ($properties.Count -eq 6) "$Context 본문은 AuditEventResponse의 여섯 필드를 가진다."
        Assert-True ($root.GetProperty('eventId').GetString() -ceq $EventId) "$Context eventId가 정규화된 식별자와 같다."
        Assert-True ($root.GetProperty('kind').GetString() -ceq $Kind) "$Context kind는 $Kind 이다."
        Assert-True ($root.GetProperty('occurredAtUtc').GetDateTimeOffset() -eq $OccurredAtUtc) "$Context 발생 시각을 보존한다."
        Assert-True ($root.GetProperty('actorId').GetString() -ceq $ActorId) "$Context actorId의 바깥 공백만 정리한다."

        $summary = $root.GetProperty('summary').GetString()
        Assert-True (-not [string]::IsNullOrWhiteSpace($summary)) "$Context summary가 있다."
        foreach ($fragment in $SummaryFragments) {
            Assert-True ($summary.Contains($fragment, [StringComparison]::Ordinal)) "$Context summary에 '$fragment'가 있다."
        }

        $receivedAtUtc = $root.GetProperty('receivedAtUtc').GetDateTimeOffset()
        Assert-True ($receivedAtUtc.Offset -eq [TimeSpan]::Zero) "$Context 수신 시각은 UTC다."

        return [pscustomobject]@{
            Summary = $summary
            ReceivedAtUtc = $receivedAtUtc
        }
    }
    finally {
        $document.Dispose()
    }
}

function New-UserSignedInPayload {
    param(
        [Parameter(Mandatory)]
        [AllowNull()]
        [string]$EventId,
        [Parameter(Mandatory)]
        [AllowNull()]
        [string]$OccurredAtUtc,
        [Parameter(Mandatory)]
        [AllowNull()]
        [string]$ActorId,
        [Parameter(Mandatory)]
        [AllowNull()]
        [string]$IpAddress,
        [Parameter(Mandatory)]
        [AllowNull()]
        [Nullable[bool]]$Succeeded
    )

    return [ordered]@{
        type = 'user-signed-in'
        eventId = $EventId
        occurredAtUtc = $OccurredAtUtc
        actorId = $ActorId
        ipAddress = $IpAddress
        succeeded = $Succeeded
    }
}

function New-DocumentChangedPayload {
    param(
        [Parameter(Mandatory)]
        [string]$EventId,
        [Parameter(Mandatory)]
        [string]$OccurredAtUtc,
        [Parameter(Mandatory)]
        [string]$ActorId,
        [Parameter(Mandatory)]
        [string]$DocumentId,
        [Parameter(Mandatory)]
        [string]$ChangeSummary
    )

    return [ordered]@{
        type = 'document-changed'
        eventId = $EventId
        occurredAtUtc = $OccurredAtUtc
        actorId = $ActorId
        documentId = $DocumentId
        changeSummary = $ChangeSummary
    }
}

function New-PermissionChangedPayload {
    param(
        [Parameter(Mandatory)]
        [string]$EventId,
        [Parameter(Mandatory)]
        [string]$OccurredAtUtc,
        [Parameter(Mandatory)]
        [string]$ActorId,
        [Parameter(Mandatory)]
        [string]$TargetUserId,
        [Parameter(Mandatory)]
        [string]$Permission,
        [Parameter(Mandatory)]
        [string]$Action
    )

    return [ordered]@{
        type = 'permission-changed'
        eventId = $EventId
        occurredAtUtc = $OccurredAtUtc
        actorId = $ActorId
        targetUserId = $TargetUserId
        permission = $Permission
        action = $Action
    }
}

try {
    if (-not (Test-Path -LiteralPath $executablePath -PathType Leaf) -or
        -not (Test-Path -LiteralPath $assemblyPath -PathType Leaf)) {
        throw "Release 실행 파일이 없습니다. 먼저 dotnet build '$projectRoot\AuditEventApi.csproj' -c Release를 실행하세요."
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

    $httpClient = [System.Net.Http.HttpClient]::new()
    $httpClient.Timeout = [TimeSpan]::FromSeconds(5)

    # 포트 0으로 OS에 빈 loopback 포트를 요청한 뒤 즉시 반환합니다. 서버 로그 형식이나 로그 레벨에 의존하지 않기 위한 준비입니다.
    $portProbe = [System.Net.Sockets.TcpListener]::new(
        [System.Net.IPAddress]::Loopback,
        0)
    try {
        $portProbe.Start()
        $selectedPort = ([System.Net.IPEndPoint]$portProbe.LocalEndpoint).Port
    }
    finally {
        $portProbe.Stop()
    }
    $baseUrl = "http://127.0.0.1:$selectedPort"

    $serverProcess = Start-Process `
        -FilePath $executablePath `
        -ArgumentList @(
            '--urls', $baseUrl,
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
            $probe = Invoke-Api -Method GET -Path '/'
            $ready = $probe.StatusCode -eq 200
            if ($ready) {
                break
            }
        }
        catch {
            # Kestrel이 listen을 시작하기 전의 짧은 연결 실패는 기다렸다가 다시 확인합니다.
        }

        Start-Sleep -Milliseconds 100
    }
    Assert-True $ready 'AuditEventApi가 Production의 동적 loopback 포트에서 준비된다.'

    $status = Invoke-Api -Method GET -Path '/'
    Assert-True ($status.StatusCode -eq 200) '상태 endpoint는 200이다.'
    Assert-True ($status.ContentType.StartsWith('application/json')) '상태 endpoint는 JSON을 반환한다.'
    $statusDocument = [System.Text.Json.JsonDocument]::Parse($status.Body)
    try {
        $statusRoot = $statusDocument.RootElement
        Assert-True ($statusRoot.GetProperty('service').GetString() -ceq 'AuditEventApi') '상태 응답은 서비스 이름을 공개한다.'
        Assert-True ($statusRoot.GetProperty('sourceGeneratedJson').GetBoolean()) '상태 응답은 source-generated JSON 사용을 공개한다.'
        Assert-True ($statusRoot.GetProperty('strictInput').GetBoolean()) '상태 응답은 엄격한 입력 정책 사용을 공개한다.'
        $allowedTypes = @(
            $statusRoot.GetProperty('allowedEventTypes').EnumerateArray() |
                ForEach-Object { $_.GetString() }
        )
        Assert-True ($allowedTypes.Count -eq 3) '상태 응답은 허용한 세 가지 type만 공개한다.'
        foreach ($allowedType in @('user-signed-in', 'document-changed', 'permission-changed')) {
            Assert-True ($allowedTypes -ccontains $allowedType) "상태 응답은 '$allowedType' type을 허용한다."
        }
    }
    finally {
        $statusDocument.Dispose()
    }

    $occurredAt = [DateTimeOffset]::UtcNow.AddMinutes(-10)
    $occurredAtText = $occurredAt.ToString('O', [Globalization.CultureInfo]::InvariantCulture)
    $actorSuffix = $idSuffix.ToLowerInvariant()

    $loginEventId = "LOGIN-$idSuffix"
    $loginActor = "user-$actorSuffix"
    $loginIpAddress = '203.0.113.42'
    $loginPayload = New-UserSignedInPayload `
        -EventId "  $($loginEventId.ToLowerInvariant())  " `
        -OccurredAtUtc $occurredAtText `
        -ActorId "  $loginActor  " `
        -IpAddress $loginIpAddress `
        -Succeeded $true
    $loginCreated = Invoke-Api `
        -Method POST `
        -Path '/audit-events' `
        -BodyBytes (ConvertTo-JsonBytes -Value $loginPayload)
    $loginSnapshot = Assert-AuditEventResponse `
        -Response $loginCreated `
        -StatusCode 201 `
        -EventId $loginEventId `
        -Kind 'user-signed-in' `
        -OccurredAtUtc $occurredAt `
        -ActorId $loginActor `
        -SummaryFragments @($loginActor, '로그인', '성공') `
        -Context '로그인 이벤트 생성'
    Assert-True ($loginCreated.Location -ceq "/audit-events/$loginEventId") '로그인 이벤트 Location은 정규화된 생성 자원을 가리킨다.'
    Assert-True (
        -not $loginSnapshot.Summary.Contains($loginIpAddress, [StringComparison]::Ordinal)
    ) '로그인 응답 summary는 민감한 IP 주소를 포함하지 않는다.'

    $loginFound = Invoke-Api -Method GET -Path "/audit-events/$loginEventId"
    $loginFoundSnapshot = Assert-AuditEventResponse `
        -Response $loginFound `
        -StatusCode 200 `
        -EventId $loginEventId `
        -Kind 'user-signed-in' `
        -OccurredAtUtc $occurredAt `
        -ActorId $loginActor `
        -SummaryFragments @($loginActor, '로그인', '성공') `
        -Context '로그인 이벤트 조회'
    Assert-True ($loginFoundSnapshot.ReceivedAtUtc -eq $loginSnapshot.ReceivedAtUtc) '로그인 이벤트 조회는 저장 당시 수신 시각을 보존한다.'

    $documentEventId = "DOCUMENT-$idSuffix"
    $documentActor = "editor-$actorSuffix"
    $documentId = "DOC-$idSuffix"
    $koreanChangeSummary = "제목을 '4분기 계획'으로 변경"
    $documentPayload = New-DocumentChangedPayload `
        -EventId $documentEventId.ToLowerInvariant() `
        -OccurredAtUtc $occurredAtText `
        -ActorId $documentActor `
        -DocumentId "  $documentId  " `
        -ChangeSummary "  $koreanChangeSummary  "
    $documentCreated = Invoke-Api `
        -Method POST `
        -Path '/audit-events' `
        -BodyBytes (ConvertTo-JsonBytes -Value $documentPayload)
    $documentSnapshot = Assert-AuditEventResponse `
        -Response $documentCreated `
        -StatusCode 201 `
        -EventId $documentEventId `
        -Kind 'document-changed' `
        -OccurredAtUtc $occurredAt `
        -ActorId $documentActor `
        -SummaryFragments @($documentId, $koreanChangeSummary) `
        -Context '문서 변경 이벤트 생성'
    Assert-True ($documentCreated.Location -ceq "/audit-events/$documentEventId") '문서 변경 이벤트 Location은 생성 자원을 가리킨다.'
    Assert-True ($documentSnapshot.Summary.Contains($koreanChangeSummary, [StringComparison]::Ordinal)) 'UTF-8 한글 변경 요약이 손실 없이 왕복한다.'

    $documentFound = Invoke-Api -Method GET -Path "/audit-events/$documentEventId"
    $documentFoundSnapshot = Assert-AuditEventResponse `
        -Response $documentFound `
        -StatusCode 200 `
        -EventId $documentEventId `
        -Kind 'document-changed' `
        -OccurredAtUtc $occurredAt `
        -ActorId $documentActor `
        -SummaryFragments @($documentId, $koreanChangeSummary) `
        -Context '문서 변경 이벤트 조회'
    Assert-True ($documentFoundSnapshot.ReceivedAtUtc -eq $documentSnapshot.ReceivedAtUtc) '문서 변경 이벤트 조회는 저장 당시 수신 시각을 보존한다.'

    $permissionEventId = "PERMISSION-$idSuffix"
    $permissionActor = "admin-$actorSuffix"
    $targetUser = "member-$actorSuffix"
    $permissionPayload = New-PermissionChangedPayload `
        -EventId $permissionEventId.ToLowerInvariant() `
        -OccurredAtUtc $occurredAtText `
        -ActorId $permissionActor `
        -TargetUserId "  $targetUser  " `
        -Permission '  REPORT.READ  ' `
        -Action '  GRANT  '
    $permissionCreated = Invoke-Api `
        -Method POST `
        -Path '/audit-events' `
        -BodyBytes (ConvertTo-JsonBytes -Value $permissionPayload)
    $permissionSnapshot = Assert-AuditEventResponse `
        -Response $permissionCreated `
        -StatusCode 201 `
        -EventId $permissionEventId `
        -Kind 'permission-changed' `
        -OccurredAtUtc $occurredAt `
        -ActorId $permissionActor `
        -SummaryFragments @($targetUser, 'report.read', '부여') `
        -Context '권한 변경 이벤트 생성'
    Assert-True ($permissionCreated.Location -ceq "/audit-events/$permissionEventId") '권한 변경 이벤트 Location은 생성 자원을 가리킨다.'

    $permissionFound = Invoke-Api -Method GET -Path "/audit-events/$permissionEventId"
    $permissionFoundSnapshot = Assert-AuditEventResponse `
        -Response $permissionFound `
        -StatusCode 200 `
        -EventId $permissionEventId `
        -Kind 'permission-changed' `
        -OccurredAtUtc $occurredAt `
        -ActorId $permissionActor `
        -SummaryFragments @($targetUser, 'report.read', '부여') `
        -Context '권한 변경 이벤트 조회'
    Assert-True ($permissionFoundSnapshot.ReceivedAtUtc -eq $permissionSnapshot.ReceivedAtUtc) '권한 변경 이벤트 조회는 저장 당시 수신 시각을 보존한다.'

    # JSON 객체의 속성 순서는 의미가 없으므로 type을 마지막에 보내도 본문 상한 안에서는 같은 계약으로 처리합니다.
    $typeLastEventId = "TYPE-LAST-$idSuffix"
    $typeLastIpAddress = '192.0.2.28'
    $typeLastPayload = [ordered]@{
        eventId = $typeLastEventId
        occurredAtUtc = $occurredAtText
        actorId = 'type-last-user'
        ipAddress = $typeLastIpAddress
        succeeded = $false
        type = 'user-signed-in'
    }
    $typeLastCreated = Invoke-Api `
        -Method POST `
        -Path '/audit-events' `
        -BodyBytes (ConvertTo-JsonBytes -Value $typeLastPayload)
    $typeLastSnapshot = Assert-AuditEventResponse `
        -Response $typeLastCreated `
        -StatusCode 201 `
        -EventId $typeLastEventId `
        -Kind 'user-signed-in' `
        -OccurredAtUtc $occurredAt `
        -ActorId 'type-last-user' `
        -SummaryFragments @('type-last-user', '로그인', '실패') `
        -Context '마지막 위치 type 판별자'
    Assert-True (
        -not $typeLastSnapshot.Summary.Contains($typeLastIpAddress, [StringComparison]::Ordinal)
    ) '마지막 위치 type 요청도 summary에 민감한 IP 주소를 포함하지 않는다.'

    # 같은 업무 사건을 대소문자와 바깥 공백만 달리 재전송해도 정규화된 eventId가 같으므로 중복입니다.
    $duplicatePayload = New-UserSignedInPayload `
        -EventId " $($loginEventId.ToLowerInvariant()) " `
        -OccurredAtUtc $occurredAtText `
        -ActorId $loginActor `
        -IpAddress '2001:db8::42' `
        -Succeeded $false
    $duplicate = Invoke-Api `
        -Method POST `
        -Path '/audit-events' `
        -BodyBytes (ConvertTo-JsonBytes -Value $duplicatePayload)
    Assert-Problem `
        -Response $duplicate `
        -StatusCode 409 `
        -Code 'audit.duplicate_event_id' `
        -Field 'eventId' `
        -Instance '/audit-events' `
        -Context '중복 식별자 재사용'

    $afterDuplicate = Invoke-Api -Method GET -Path "/audit-events/$loginEventId"
    $afterDuplicateSnapshot = Assert-AuditEventResponse `
        -Response $afterDuplicate `
        -StatusCode 200 `
        -EventId $loginEventId `
        -Kind 'user-signed-in' `
        -OccurredAtUtc $occurredAt `
        -ActorId $loginActor `
        -SummaryFragments @($loginActor, '로그인', '성공') `
        -Context '중복 거절 뒤 원본 조회'
    Assert-True ($afterDuplicateSnapshot.ReceivedAtUtc -eq $loginSnapshot.ReceivedAtUtc) '중복 요청은 먼저 저장된 사건을 덮어쓰지 않는다.'

    # string 형식의 PowerShell parameter는 $null을 빈 문자열로 바꾸므로, JSON null 검증은 payload를 직접 만듭니다.
    $nullEventIdPayload = [ordered]@{
        type = 'user-signed-in'
        eventId = $null
        occurredAtUtc = $occurredAtText
        actorId = 'user-null-test'
        ipAddress = '192.0.2.10'
        succeeded = $true
    }
    $nullEventId = Invoke-Api `
        -Method POST `
        -Path '/audit-events' `
        -BodyBytes (ConvertTo-JsonBytes -Value $nullEventIdPayload)
    Assert-Problem `
        -Response $nullEventId `
        -StatusCode 400 `
        -Code 'audit.invalid_event_id' `
        -Field 'eventId' `
        -Instance '/audit-events' `
        -Context 'null 업무 식별자'

    $nonUtcPayload = New-UserSignedInPayload `
        -EventId "OFFSET-$idSuffix" `
        -OccurredAtUtc $occurredAt.ToOffset([TimeSpan]::FromHours(9)).ToString('O') `
        -ActorId 'user-offset-test' `
        -IpAddress '192.0.2.11' `
        -Succeeded $true
    $nonUtc = Invoke-Api `
        -Method POST `
        -Path '/audit-events' `
        -BodyBytes (ConvertTo-JsonBytes -Value $nonUtcPayload)
    Assert-Problem `
        -Response $nonUtc `
        -StatusCode 400 `
        -Code 'audit.invalid_occurred_at' `
        -Field 'occurredAtUtc' `
        -Instance '/audit-events' `
        -Context 'UTC가 아닌 발생 시각'

    $invalidActionPayload = New-PermissionChangedPayload `
        -EventId "ACTION-$idSuffix" `
        -OccurredAtUtc $occurredAtText `
        -ActorId 'admin-action-test' `
        -TargetUserId 'target-action-test' `
        -Permission 'report.write' `
        -Action 'delegate'
    $invalidAction = Invoke-Api `
        -Method POST `
        -Path '/audit-events' `
        -BodyBytes (ConvertTo-JsonBytes -Value $invalidActionPayload)
    Assert-Problem `
        -Response $invalidAction `
        -StatusCode 400 `
        -Code 'audit.invalid_permission_action' `
        -Field 'action' `
        -Instance '/audit-events' `
        -Context '허용되지 않은 권한 동작'

    $nullSucceededPayload = New-UserSignedInPayload `
        -EventId "NULL-RESULT-$idSuffix" `
        -OccurredAtUtc $occurredAtText `
        -ActorId 'user-null-result' `
        -IpAddress '192.0.2.12' `
        -Succeeded $null
    $nullSucceeded = Invoke-Api `
        -Method POST `
        -Path '/audit-events' `
        -BodyBytes (ConvertTo-JsonBytes -Value $nullSucceededPayload)
    Assert-Problem `
        -Response $nullSucceeded `
        -StatusCode 400 `
        -Code 'audit.missing_sign_in_result' `
        -Field 'succeeded' `
        -Instance '/audit-events' `
        -Context 'null 로그인 결과'

    $unknownDiscriminator = [ordered]@{
        type = 'server-restarted'
        eventId = "UNKNOWN-TYPE-$idSuffix"
        occurredAtUtc = $occurredAtText
        actorId = 'strict-user'
    }
    $unknownType = Invoke-Api `
        -Method POST `
        -Path '/audit-events' `
        -BodyBytes (ConvertTo-JsonBytes -Value $unknownDiscriminator)
    Assert-Problem `
        -Response $unknownType `
        -StatusCode 400 `
        -Code 'http.bad_request' `
        -Field 'http' `
        -Instance '/audit-events' `
        -Context '알 수 없는 type 판별자'

    $missingDiscriminatorPayload = [ordered]@{
        eventId = "MISSING-TYPE-$idSuffix"
        occurredAtUtc = $occurredAtText
        actorId = 'strict-user'
        ipAddress = '192.0.2.24'
        succeeded = $true
    }
    $missingDiscriminator = Invoke-Api `
        -Method POST `
        -Path '/audit-events' `
        -BodyBytes (ConvertTo-JsonBytes -Value $missingDiscriminatorPayload)
    Assert-Problem `
        -Response $missingDiscriminator `
        -StatusCode 400 `
        -Code 'http.bad_request' `
        -Field 'http' `
        -Instance '/audit-events' `
        -Context '누락된 type 판별자'

    $nullDiscriminatorPayload = [ordered]@{
        type = $null
        eventId = "NULL-TYPE-$idSuffix"
        occurredAtUtc = $occurredAtText
        actorId = 'strict-user'
        ipAddress = '192.0.2.25'
        succeeded = $true
    }
    $nullDiscriminator = Invoke-Api `
        -Method POST `
        -Path '/audit-events' `
        -BodyBytes (ConvertTo-JsonBytes -Value $nullDiscriminatorPayload)
    Assert-Problem `
        -Response $nullDiscriminator `
        -StatusCode 400 `
        -Code 'http.bad_request' `
        -Field 'http' `
        -Instance '/audit-events' `
        -Context 'null type 판별자'

    $wrongTypeDiscriminatorPayload = [ordered]@{
        type = 42
        eventId = "NUMBER-TYPE-$idSuffix"
        occurredAtUtc = $occurredAtText
        actorId = 'strict-user'
        ipAddress = '192.0.2.26'
        succeeded = $true
    }
    $wrongTypeDiscriminator = Invoke-Api `
        -Method POST `
        -Path '/audit-events' `
        -BodyBytes (ConvertTo-JsonBytes -Value $wrongTypeDiscriminatorPayload)
    Assert-Problem `
        -Response $wrongTypeDiscriminator `
        -StatusCode 400 `
        -Code 'http.bad_request' `
        -Field 'http' `
        -Instance '/audit-events' `
        -Context '문자열이 아닌 type 판별자'

    $unknownPropertyPayload = [ordered]@{
        type = 'user-signed-in'
        eventId = "UNKNOWN-PROPERTY-$idSuffix"
        occurredAtUtc = $occurredAtText
        actorId = 'strict-user'
        ipAddress = '192.0.2.20'
        succeeded = $true
        unexpectedSecret = 'must-not-be-accepted'
    }
    $unknownProperty = Invoke-Api `
        -Method POST `
        -Path '/audit-events' `
        -BodyBytes (ConvertTo-JsonBytes -Value $unknownPropertyPayload)
    Assert-Problem `
        -Response $unknownProperty `
        -StatusCode 400 `
        -Code 'http.bad_request' `
        -Field 'http' `
        -Instance '/audit-events' `
        -Context '알 수 없는 JSON 속성'

    $duplicatePropertyJson = '{' +
        '"type":"user-signed-in",' +
        '"eventId":"DUPLICATE-PROPERTY-' + $idSuffix + '",' +
        '"eventId":"SHADOW-' + $idSuffix + '",' +
        '"occurredAtUtc":"' + $occurredAtText + '",' +
        '"actorId":"strict-user","ipAddress":"192.0.2.21","succeeded":true}'
    $duplicateProperty = Invoke-Api `
        -Method POST `
        -Path '/audit-events' `
        -BodyBytes (ConvertTo-Utf8Bytes -Text $duplicatePropertyJson)
    Assert-Problem `
        -Response $duplicateProperty `
        -StatusCode 400 `
        -Code 'http.bad_request' `
        -Field 'http' `
        -Instance '/audit-events' `
        -Context '중복 JSON 속성'

    $caseMismatchJson = '{' +
        '"type":"user-signed-in",' +
        '"EventId":"CASE-' + $idSuffix + '",' +
        '"occurredAtUtc":"' + $occurredAtText + '",' +
        '"actorId":"strict-user","ipAddress":"192.0.2.22","succeeded":true}'
    $caseMismatch = Invoke-Api `
        -Method POST `
        -Path '/audit-events' `
        -BodyBytes (ConvertTo-Utf8Bytes -Text $caseMismatchJson)
    Assert-Problem `
        -Response $caseMismatch `
        -StatusCode 400 `
        -Code 'http.bad_request' `
        -Field 'http' `
        -Instance '/audit-events' `
        -Context '대소문자가 다른 JSON 속성'

    $missingRequiredPayload = [ordered]@{
        type = 'user-signed-in'
        eventId = "MISSING-$idSuffix"
        occurredAtUtc = $occurredAtText
        ipAddress = '192.0.2.23'
        succeeded = $true
    }
    $missingRequired = Invoke-Api `
        -Method POST `
        -Path '/audit-events' `
        -BodyBytes (ConvertTo-JsonBytes -Value $missingRequiredPayload)
    Assert-Problem `
        -Response $missingRequired `
        -StatusCode 400 `
        -Code 'http.bad_request' `
        -Field 'http' `
        -Instance '/audit-events' `
        -Context '필수 JSON 속성 누락'

    $blankActorPayload = New-UserSignedInPayload `
        -EventId "BLANK-ACTOR-$idSuffix" `
        -OccurredAtUtc $occurredAtText `
        -ActorId '   ' `
        -IpAddress '192.0.2.27' `
        -Succeeded $true
    $blankActor = Invoke-Api `
        -Method POST `
        -Path '/audit-events' `
        -BodyBytes (ConvertTo-JsonBytes -Value $blankActorPayload)
    Assert-Problem `
        -Response $blankActor `
        -StatusCode 400 `
        -Code 'audit.invalid_actor_id' `
        -Field 'actorId' `
        -Instance '/audit-events' `
        -Context '공백뿐인 행위자 식별자'

    # endpoint parameter 자체는 nullable이 아니므로 JSON 본문 전체의 null은 framework 계약 위반입니다.
    $nullBody = Invoke-Api `
        -Method POST `
        -Path '/audit-events' `
        -BodyBytes (ConvertTo-Utf8Bytes -Text 'null')
    Assert-Problem `
        -Response $nullBody `
        -StatusCode 400 `
        -Code 'http.bad_request' `
        -Field 'http' `
        -Instance '/audit-events' `
        -Context 'null JSON 본문'

    $malformed = Invoke-Api `
        -Method POST `
        -Path '/audit-events' `
        -BodyBytes (ConvertTo-Utf8Bytes -Text '{"type":"user-signed-in","eventId":')
    Assert-Problem `
        -Response $malformed `
        -StatusCode 400 `
        -Code 'http.bad_request' `
        -Field 'http' `
        -Instance '/audit-events' `
        -Context '잘린 JSON'

    $wrongMedia = Invoke-Api `
        -Method POST `
        -Path '/audit-events' `
        -BodyBytes (ConvertTo-Utf8Bytes -Text 'type=user-signed-in') `
        -ContentType 'text/plain'
    Assert-Problem `
        -Response $wrongMedia `
        -StatusCode 415 `
        -Code 'http.unsupported_media_type' `
        -Field 'http' `
        -Instance '/audit-events' `
        -Context '지원하지 않는 media type'

    $oversizedText = '{"type":"user-signed-in","padding":"' + ('x' * (40 * 1024)) + '"}'
    $oversized = Invoke-Api `
        -Method POST `
        -Path '/audit-events' `
        -BodyBytes (ConvertTo-Utf8Bytes -Text $oversizedText)
    Assert-Problem `
        -Response $oversized `
        -StatusCode 413 `
        -Code 'http.payload_too_large' `
        -Field 'http' `
        -Instance '/audit-events' `
        -Context '32 KiB를 넘는 요청 본문'

    $unknownEventId = "NOT-FOUND-$idSuffix"
    $notFound = Invoke-Api -Method GET -Path "/audit-events/$unknownEventId"
    Assert-Problem `
        -Response $notFound `
        -StatusCode 404 `
        -Code 'audit.not_found' `
        -Field 'eventId' `
        -Instance "/audit-events/$unknownEventId" `
        -Context '없는 감사 이벤트 조회'

    Write-Host '검증 계약: strict JSON transport -> polymorphic Strategy -> Result/domain rule -> atomic duplicate rejection'
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
    if ($null -ne $serverProcess) {
        try {
            if (-not $serverProcess.HasExited) {
                Stop-Process -Id $serverProcess.Id -Force
                if (-not $serverProcess.WaitForExit(5000)) {
                    Write-Warning "검증 서버 PID $($serverProcess.Id)가 5초 안에 종료되지 않았습니다."
                }
            }
        }
        catch {
            # 종료 확인과 실제 종료 사이의 race가 나도 나머지 자원 정리는 반드시 계속합니다.
            Write-Warning "검증 서버 정리 중 오류가 발생했습니다: $($_.Exception.Message)"
        }
        finally {
            try {
                $serverProcess.Dispose()
            }
            catch {
                # Process handle 정리가 실패해도 HttpClient와 임시 로그 정리는 독립적으로 계속합니다.
                Write-Warning "검증 서버 Process 자원 해제 중 오류가 발생했습니다: $($_.Exception.Message)"
            }
        }
    }

    if ($null -ne $httpClient) {
        try {
            $httpClient.Dispose()
        }
        catch {
            # HttpClient 해제가 실패해도 임시 stdout/stderr 파일 삭제는 반드시 시도합니다.
            Write-Warning "HTTP client 자원 해제 중 오류가 발생했습니다: $($_.Exception.Message)"
        }
    }
    Remove-Item -LiteralPath $stdoutPath -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $stderrPath -Force -ErrorAction SilentlyContinue
}
