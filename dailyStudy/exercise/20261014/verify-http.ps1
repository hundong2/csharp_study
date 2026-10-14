[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = Join-Path $PSScriptRoot 'src\InvitationTokenApi'
$executablePath = Join-Path $projectRoot 'bin\Release\net10.0\InvitationTokenApi.exe'
$assemblyPath = Join-Path $projectRoot 'bin\Release\net10.0\InvitationTokenApi.dll'

if (-not (Test-Path -LiteralPath $executablePath -PathType Leaf) -or
    -not (Test-Path -LiteralPath $assemblyPath -PathType Leaf)) {
    throw "Release 실행 파일이 없습니다. 먼저 다음 명령을 실행하세요: dotnet build '$projectRoot\InvitationTokenApi.csproj' -c Release"
}

# 오래된 실행 파일을 실수로 검증하지 않도록 마지막 source 수정 시각과 assembly를 비교합니다.
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

$runId = [Guid]::NewGuid().ToString('N')
$tempDirectory = [System.IO.Path]::GetTempPath()
$stdoutPath = Join-Path $tempDirectory "invitation-token-$runId.stdout.log"
$stderrPath = Join-Path $tempDirectory "invitation-token-$runId.stderr.log"
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
        [AllowNull()]
        [object]$Content
    )

    if ($null -eq $Content) {
        return ''
    }

    if ($Content -is [byte[]]) {
        return [System.Text.Encoding]::UTF8.GetString($Content)
    }

    return [string]$Content
}

function Assert-JsonContentType {
    param(
        [Parameter(Mandatory)]
        [object]$Response,
        [Parameter(Mandatory)]
        [string]$Message
    )

    $contentType = [string]($Response.Headers.'Content-Type' | Select-Object -First 1)
    Assert-True ($contentType.StartsWith('application/json', [StringComparison]::OrdinalIgnoreCase)) $Message
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

function New-IssueBody {
    param(
        [Parameter(Mandatory)]
        [string]$InvitationId,
        [Parameter(Mandatory)]
        [string]$ProjectId,
        [Parameter(Mandatory)]
        [string]$Email,
        [Parameter(Mandatory)]
        [string]$Role
    )

    return [ordered]@{
        invitationId = $InvitationId
        projectId = $ProjectId
        email = $Email
        role = $Role
    } | ConvertTo-Json -Compress
}

function New-RedeemBody {
    param(
        [Parameter(Mandatory)]
        [string]$Token
    )

    return @{ token = $Token } | ConvertTo-Json -Compress
}

function New-TamperedToken {
    param(
        [Parameter(Mandatory)]
        [string]$Token
    )

    if ($Token.Length -lt 3) {
        throw '변조 검증에 사용할 토큰이 지나치게 짧습니다.'
    }

    $index = [Math]::Floor($Token.Length / 2)
    $replacement = if ($Token[$index] -eq 'A') { 'B' } else { 'A' }
    return $Token.Substring(0, $index) + $replacement + $Token.Substring($index + 1)
}

function Assert-ProblemEnvelope {
    param(
        [Parameter(Mandatory)]
        [object]$Response,
        [Parameter(Mandatory)]
        [int]$StatusCode,
        [Parameter(Mandatory)]
        [string]$Instance
    )

    Assert-True ($Response.StatusCode -eq $StatusCode) "오류 HTTP 상태는 $StatusCode 이다."
    $contentType = [string]($Response.Headers.'Content-Type' | Select-Object -First 1)
    Assert-True ($contentType.StartsWith('application/problem+json', [StringComparison]::OrdinalIgnoreCase)) '오류 media type은 application/problem+json이다.'

    $json = Convert-ContentToString -Content $Response.Content
    $document = [System.Text.Json.JsonDocument]::Parse($json)
    try {
        $root = $document.RootElement
        Assert-True ($root.GetProperty('status').GetInt32() -eq $StatusCode) 'Problem Details status가 HTTP 상태와 같다.'
        Assert-True (-not [string]::IsNullOrWhiteSpace($root.GetProperty('title').GetString())) 'Problem Details에 사람이 읽는 title이 있다.'
        Assert-True (-not [string]::IsNullOrWhiteSpace($root.GetProperty('detail').GetString())) 'Problem Details에 내부 정보를 노출하지 않는 detail이 있다.'
        Assert-True ($root.GetProperty('instance').GetString() -eq $Instance) "Problem Details instance는 $Instance 이다."
    }
    finally {
        $document.Dispose()
    }
}

function Assert-ApplicationProblem {
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
        [string]$Instance
    )

    Assert-ProblemEnvelope -Response $Response -StatusCode $StatusCode -Instance $Instance

    $json = Convert-ContentToString -Content $Response.Content
    $document = [System.Text.Json.JsonDocument]::Parse($json)
    try {
        $root = $document.RootElement
        Assert-True ($root.GetProperty('code').GetString() -eq $Code) "안정 오류 code는 $Code 이다."
        Assert-True ($root.GetProperty('field').GetString() -eq $Field) "오류 field는 $Field 이다."
        Assert-True (-not [string]::IsNullOrWhiteSpace($root.GetProperty('traceId').GetString())) '서버 로그와 연결할 traceId가 있다.'
    }
    finally {
        $document.Dispose()
    }
}

try {
    # 포트 0을 Kestrel에 전달하면 운영체제가 실제 listen 시점에 빈 포트를 원자적으로 선택합니다.
    # 미리 임시 포트를 열었다 닫는 방식의 TOCTOU 경쟁을 피할 수 있습니다.
    $arguments = @(
        '--urls', 'http://127.0.0.1:0',
        '--environment', 'Production'
    )
    $startProcessArguments = @{
        FilePath = $executablePath
        ArgumentList = $arguments
        PassThru = $true
        WindowStyle = 'Hidden'
        RedirectStandardOutput = $stdoutPath
        RedirectStandardError = $stderrPath
    }
    $serverProcess = Start-Process @startProcessArguments

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
                    $rootResponse = Invoke-WebRequest -Uri "$baseUrl/" -SkipHttpErrorCheck -TimeoutSec 1
                    $rootJson = Convert-ContentToString -Content $rootResponse.Content
                    $rootDocument = [System.Text.Json.JsonDocument]::Parse($rootJson)
                    try {
                        $root = $rootDocument.RootElement
                        $ready = $rootResponse.StatusCode -eq 200 -and
                            $root.GetProperty('service').GetString() -eq 'InvitationTokenApi'
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
            # Kestrel이 listen을 시작하기 전의 짧은 연결 실패는 기다렸다가 다시 확인합니다.
        }

        Start-Sleep -Milliseconds 125
    }

    Assert-True $ready 'Production Kestrel 서버가 동적 loopback 포트에서 준비된다.'

    $suffix = $runId.Substring(0, 8)
    $invitationId = "verify-$suffix"
    $projectId = "project-$suffix"
    $email = "learner-$suffix@example.com"
    $issueArguments = @{
        InvitationId = $invitationId
        ProjectId = $projectId
        Email = $email
        Role = 'Member'
    }
    $validBody = New-IssueBody @issueArguments

    $created = Invoke-Api -Method POST -Path '/invitations' -Body $validBody
    Assert-True ($created.StatusCode -eq 201) '유효한 초대 발급은 201 Created다.'
    Assert-JsonContentType -Response $created -Message '201 본문은 JSON이다.'

    $createdJson = Convert-ContentToString -Content $created.Content
    $createdDocument = [System.Text.Json.JsonDocument]::Parse($createdJson)
    try {
        $createdRoot = $createdDocument.RootElement
        $createdInvitationId = $createdRoot.GetProperty('invitationId').GetString()
        $createdProjectId = $createdRoot.GetProperty('projectId').GetString()
        $createdEmail = $createdRoot.GetProperty('email').GetString()
        $createdRole = $createdRoot.GetProperty('role').GetString()
        $token = $createdRoot.GetProperty('token').GetString()
        $expiresAtText = $createdRoot.GetProperty('expiresAtUtc').GetString()

        Assert-True (-not [string]::IsNullOrWhiteSpace($createdInvitationId)) '201 본문에 invitationId가 있다.'
        Assert-True (-not [string]::IsNullOrWhiteSpace($createdProjectId)) '201 본문에 projectId가 있다.'
        Assert-True ($createdEmail -eq $email) '201 본문에 정규화된 수신 이메일이 있다.'
        Assert-True ($createdRole -eq 'Member') '201 본문 역할은 Member다.'
        Assert-True (-not [string]::IsNullOrWhiteSpace($token) -and $token.Length -ge 32) '201 본문에 충분히 긴 불투명 토큰이 있다.'
        Assert-True (-not $token.Contains($createdInvitationId, [StringComparison]::OrdinalIgnoreCase)) '토큰은 invitationId 평문을 노출하지 않는다.'
        Assert-True (-not $token.Contains($createdProjectId, [StringComparison]::OrdinalIgnoreCase)) '토큰은 projectId 평문을 노출하지 않는다.'
        Assert-True (-not $token.Contains($createdEmail, [StringComparison]::OrdinalIgnoreCase)) '토큰은 이메일 평문을 노출하지 않는다.'

        $expiresAt = [DateTimeOffset]::MinValue
        Assert-True ([DateTimeOffset]::TryParse($expiresAtText, [ref]$expiresAt)) 'expiresAtUtc는 DateTimeOffset으로 읽을 수 있다.'
        Assert-True ($expiresAt -gt [DateTimeOffset]::UtcNow) '새 토큰의 만료 시각은 현재보다 미래다.'
    }
    finally {
        $createdDocument.Dispose()
    }

    $location = [string]($created.Headers.Location | Select-Object -First 1)
    Assert-True (-not [string]::IsNullOrWhiteSpace($location)) '201 응답에 Location header가 있다.'
    $absoluteLocation = $null
    $locationPath = if ([Uri]::TryCreate($location, [UriKind]::Absolute, [ref]$absoluteLocation)) {
        $absoluteLocation.AbsolutePath
    }
    else {
        $location
    }
    $escapedInvitationId = [Uri]::EscapeDataString($createdInvitationId)
    Assert-True ($locationPath -eq "/invitations/$escapedInvitationId") 'Location은 생성된 invitationId의 조회 URL을 가리킨다.'

    $readBefore = Invoke-Api -Method GET -Path "/invitations/$escapedInvitationId"
    Assert-True ($readBefore.StatusCode -eq 200) '생성한 초대는 GET에서 200으로 조회된다.'
    Assert-JsonContentType -Response $readBefore -Message 'GET 성공 본문은 JSON이다.'
    $readBeforeJson = Convert-ContentToString -Content $readBefore.Content
    $readBeforeDocument = [System.Text.Json.JsonDocument]::Parse($readBeforeJson)
    try {
        $readBeforeRoot = $readBeforeDocument.RootElement
        Assert-True ($readBeforeRoot.GetProperty('invitationId').GetString() -eq $createdInvitationId) 'GET invitationId가 201 응답과 같다.'
        Assert-True ($readBeforeRoot.GetProperty('projectId').GetString() -eq $createdProjectId) 'GET projectId가 201 응답과 같다.'
        Assert-True ($readBeforeRoot.GetProperty('email').GetString() -eq $createdEmail) 'GET email이 201 응답과 같다.'
        Assert-True ($readBeforeRoot.GetProperty('role').GetString() -eq 'Member') 'GET role은 Member다.'
        Assert-True ($readBeforeRoot.GetProperty('status').GetString() -ne 'Redeemed') '사용 전 초대는 Redeemed 상태가 아니다.'
        Assert-True ($readBeforeRoot.GetProperty('redeemedAtUtc').ValueKind -eq [System.Text.Json.JsonValueKind]::Null) '사용 전 redeemedAtUtc는 null이다.'
        $ignoredToken = [System.Text.Json.JsonElement]::new()
        Assert-True (-not $readBeforeRoot.TryGetProperty('token', [ref]$ignoredToken)) 'GET 응답은 bearer secret인 token을 다시 노출하지 않는다.'
    }
    finally {
        $readBeforeDocument.Dispose()
    }

    $duplicate = Invoke-Api -Method POST -Path '/invitations' -Body $validBody
    Assert-ApplicationProblem -Response $duplicate -StatusCode 409 -Code 'invitation.duplicate' -Field 'invitationId' -Instance '/invitations'

    $redeemed = Invoke-Api -Method POST -Path '/invitations/redeem' -Body (New-RedeemBody -Token $token)
    Assert-True ($redeemed.StatusCode -eq 200) '유효하고 사용 전인 토큰 교환은 200이다.'
    Assert-JsonContentType -Response $redeemed -Message '토큰 교환 성공 본문은 JSON이다.'
    $redeemedJson = Convert-ContentToString -Content $redeemed.Content
    $redeemedDocument = [System.Text.Json.JsonDocument]::Parse($redeemedJson)
    try {
        $redeemedRoot = $redeemedDocument.RootElement
        Assert-True ($redeemedRoot.GetProperty('invitationId').GetString() -eq $createdInvitationId) '교환 응답 invitationId가 원래 초대와 같다.'
        Assert-True ($redeemedRoot.GetProperty('projectId').GetString() -eq $createdProjectId) '교환 응답 projectId가 원래 초대와 같다.'
        Assert-True ($redeemedRoot.GetProperty('role').GetString() -eq 'Member') '교환 응답 role은 Member다.'
        $redeemedAtText = $redeemedRoot.GetProperty('redeemedAtUtc').GetString()
        $redeemedAt = [DateTimeOffset]::MinValue
        Assert-True ([DateTimeOffset]::TryParse($redeemedAtText, [ref]$redeemedAt)) 'redeemedAtUtc는 DateTimeOffset으로 읽을 수 있다.'
        $ignoredSecret = [System.Text.Json.JsonElement]::new()
        Assert-True (-not $redeemedRoot.TryGetProperty('token', [ref]$ignoredSecret)) '교환 응답은 token을 되돌려 주지 않는다.'
        $ignoredEmail = [System.Text.Json.JsonElement]::new()
        Assert-True (-not $redeemedRoot.TryGetProperty('email', [ref]$ignoredEmail)) '교환 응답은 email을 되돌려 주지 않는다.'
    }
    finally {
        $redeemedDocument.Dispose()
    }

    $replay = Invoke-Api -Method POST -Path '/invitations/redeem' -Body (New-RedeemBody -Token $token)
    Assert-ApplicationProblem -Response $replay -StatusCode 409 -Code 'invitation.already_redeemed' -Field 'token' -Instance '/invitations/redeem'

    $readAfter = Invoke-Api -Method GET -Path "/invitations/$escapedInvitationId"
    Assert-True ($readAfter.StatusCode -eq 200) '교환 뒤에도 초대 상태를 GET으로 조회할 수 있다.'
    $readAfterJson = Convert-ContentToString -Content $readAfter.Content
    $readAfterDocument = [System.Text.Json.JsonDocument]::Parse($readAfterJson)
    try {
        $readAfterRoot = $readAfterDocument.RootElement
        Assert-True ($readAfterRoot.GetProperty('status').GetString() -eq 'Redeemed') '교환 뒤 GET 상태는 Redeemed다.'
        Assert-True ($readAfterRoot.GetProperty('redeemedAtUtc').ValueKind -eq [System.Text.Json.JsonValueKind]::String) '교환 뒤 GET에 redeemedAtUtc가 있다.'
    }
    finally {
        $readAfterDocument.Dispose()
    }

    $tamperedToken = New-TamperedToken -Token $token
    $tampered = Invoke-Api -Method POST -Path '/invitations/redeem' -Body (New-RedeemBody -Token $tamperedToken)
    Assert-ApplicationProblem -Response $tampered -StatusCode 400 -Code 'invitation.token.invalid_or_expired' -Field 'token' -Instance '/invitations/redeem'

    $randomToken = Invoke-Api -Method POST -Path '/invitations/redeem' -Body (New-RedeemBody -Token 'not-a-protected-token')
    Assert-ApplicationProblem -Response $randomToken -StatusCode 400 -Code 'invitation.token.invalid_or_expired' -Field 'token' -Instance '/invitations/redeem'

    $invalidInvitationId = New-IssueBody -InvitationId 'x' -ProjectId $projectId -Email $email -Role 'Member'
    $invalidInvitation = Invoke-Api -Method POST -Path '/invitations' -Body $invalidInvitationId
    Assert-ApplicationProblem -Response $invalidInvitation -StatusCode 400 -Code 'invitation.id.invalid' -Field 'invitationId' -Instance '/invitations'

    $invalidProjectId = New-IssueBody -InvitationId "verify-project-$suffix" -ProjectId 'x' -Email $email -Role 'Member'
    $invalidProject = Invoke-Api -Method POST -Path '/invitations' -Body $invalidProjectId
    Assert-ApplicationProblem -Response $invalidProject -StatusCode 400 -Code 'invitation.project_id.invalid' -Field 'projectId' -Instance '/invitations'

    $invalidEmailBody = New-IssueBody -InvitationId "verify-email-$suffix" -ProjectId $projectId -Email 'not-an-email' -Role 'Member'
    $invalidEmail = Invoke-Api -Method POST -Path '/invitations' -Body $invalidEmailBody
    Assert-ApplicationProblem -Response $invalidEmail -StatusCode 400 -Code 'invitation.email.invalid' -Field 'email' -Instance '/invitations'

    $invalidRoleBody = New-IssueBody -InvitationId "verify-role-$suffix" -ProjectId $projectId -Email $email -Role 'Owner'
    $invalidRole = Invoke-Api -Method POST -Path '/invitations' -Body $invalidRoleBody
    Assert-ApplicationProblem -Response $invalidRole -StatusCode 400 -Code 'invitation.role.invalid' -Field 'role' -Instance '/invitations'

    $missing = Invoke-Api -Method GET -Path '/invitations/verify-not-found'
    Assert-ApplicationProblem -Response $missing -StatusCode 404 -Code 'invitation.not_found' -Field 'invitationId' -Instance '/invitations/verify-not-found'

    $malformed = Invoke-Api -Method POST -Path '/invitations' -Body '{'
    Assert-ProblemEnvelope -Response $malformed -StatusCode 400 -Instance '/invitations'

    $wrongMediaType = Invoke-Api -Method POST -Path '/invitations' -Body '{}' -ContentType 'text/plain'
    Assert-ProblemEnvelope -Response $wrongMediaType -StatusCode 415 -Instance '/invitations'

    Write-Host "HTTP VERIFY PASSED: $passed assertions"
}
catch {
    $lineBreak = [Environment]::NewLine
    if (Test-Path -LiteralPath $stdoutPath) {
        Write-Error ("--- server stdout ---" + $lineBreak + (Get-Content -Raw -LiteralPath $stdoutPath)) -ErrorAction Continue
    }
    if (Test-Path -LiteralPath $stderrPath) {
        Write-Error ("--- server stderr ---" + $lineBreak + (Get-Content -Raw -LiteralPath $stderrPath)) -ErrorAction Continue
    }
    throw
}
finally {
    # 이 검증이 Start-Process로 만든 정확한 PID만 종료합니다.
    # 이름으로 dotnet 프로세스를 일괄 종료하면 사용자의 다른 서버까지 멈출 수 있으므로 금지합니다.
    if ($null -ne $serverProcess -and -not $serverProcess.HasExited) {
        Stop-Process -Id $serverProcess.Id -Force
        $serverProcess.WaitForExit()
    }

    Remove-Item -LiteralPath $stdoutPath -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $stderrPath -Force -ErrorAction SilentlyContinue
}
