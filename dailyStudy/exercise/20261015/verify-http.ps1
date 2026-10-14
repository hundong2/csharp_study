[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = Join-Path $PSScriptRoot 'src\DeploymentSseApi'
$executablePath = Join-Path $projectRoot 'bin\Release\net10.0\DeploymentSseApi.exe'
$assemblyPath = Join-Path $projectRoot 'bin\Release\net10.0\DeploymentSseApi.dll'

if (-not (Test-Path -LiteralPath $executablePath -PathType Leaf) -or
    -not (Test-Path -LiteralPath $assemblyPath -PathType Leaf)) {
    throw "Release 실행 파일이 없습니다. 먼저 다음 명령을 실행하세요: dotnet build '$projectRoot\DeploymentSseApi.csproj' -c Release"
}

# 오래된 assembly를 실수로 검증하지 않도록 마지막 source 수정 시각과 비교합니다.
# exe는 .NET apphost이고 실제 학습 코드가 담긴 dll의 수정 시각이 freshness 판단 기준입니다.
$latestSource = Get-ChildItem -LiteralPath $projectRoot -Recurse -File |
    Where-Object {
        $_.FullName -notmatch '\\(?:bin|obj)\\' -and
        $_.Extension -in @('.cs', '.csproj', '.json')
    } |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1
$assembly = Get-Item -LiteralPath $assemblyPath
if ($null -ne $latestSource -and $latestSource.LastWriteTimeUtc -gt $assembly.LastWriteTimeUtc) {
    throw 'Release assembly가 source보다 오래되었습니다. dotnet build -c Release를 다시 실행하세요.'
}

$runId = [Guid]::NewGuid().ToString('N')
$tempDirectory = [System.IO.Path]::GetTempPath()
$stdoutPath = Join-Path $tempDirectory "deployment-sse-$runId.stdout.log"
$stderrPath = Join-Path $tempDirectory "deployment-sse-$runId.stderr.log"
$serverProcess = $null
$baseUrl = $null
$passed = 0
$httpClient = [System.Net.Http.HttpClient]::new()
$httpClient.Timeout = [System.Threading.Timeout]::InfiniteTimeSpan

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
        [ValidateSet('GET', 'POST')]
        [string]$Method,
        [Parameter(Mandatory)]
        [string]$Path,
        [AllowEmptyString()]
        [string]$Body,
        [string]$ContentType = 'application/json',
        [hashtable]$Headers,
        [int]$TimeoutSeconds = 5
    )

    $request = [System.Net.Http.HttpRequestMessage]::new(
        [System.Net.Http.HttpMethod]::new($Method),
        "$baseUrl$Path"
    )
    $cancellation = [System.Threading.CancellationTokenSource]::new(
        [TimeSpan]::FromSeconds($TimeoutSeconds)
    )
    $response = $null

    try {
        if ($PSBoundParameters.ContainsKey('Body')) {
            $request.Content = [System.Net.Http.StringContent]::new(
                $Body,
                [System.Text.Encoding]::UTF8,
                $ContentType
            )
        }

        if ($null -ne $Headers) {
            foreach ($name in $Headers.Keys) {
                $added = $request.Headers.TryAddWithoutValidation(
                    [string]$name,
                    [string]$Headers[$name]
                )
                if (-not $added) {
                    throw "요청 header를 추가할 수 없습니다: $name"
                }
            }
        }

        # SSE는 일반 웹 명령이 응답 전체를 버퍼링하면 영원히 기다릴 수 있습니다.
        # ResponseHeadersRead와 별도 취소 토큰을 함께 사용해 header 직후부터 stream을 직접 읽고,
        # terminal event가 있는데도 서버가 닫지 않으면 제한 시간 안에 명확히 실패시킵니다.
        $response = $httpClient.SendAsync(
            $request,
            [System.Net.Http.HttpCompletionOption]::ResponseHeadersRead,
            $cancellation.Token
        ).GetAwaiter().GetResult()

        $responseBody = if ($null -eq $response.Content) {
            ''
        }
        else {
            $response.Content.ReadAsStringAsync($cancellation.Token).GetAwaiter().GetResult()
        }
        $responseContentType = if (
            $null -eq $response.Content -or
            $null -eq $response.Content.Headers.ContentType
        ) {
            ''
        }
        else {
            $response.Content.Headers.ContentType.ToString()
        }
        $location = if ($null -eq $response.Headers.Location) {
            ''
        }
        else {
            $response.Headers.Location.ToString()
        }

        return [pscustomobject]@{
            StatusCode = [int]$response.StatusCode
            ContentType = $responseContentType
            Location = $location
            Body = $responseBody
        }
    }
    catch [System.OperationCanceledException] {
        throw "HTTP $Method $Path 응답이 $($TimeoutSeconds)초 안에 끝나지 않았습니다. terminal SSE stream이 닫히는지 확인하세요."
    }
    finally {
        if ($null -ne $response) {
            $response.Dispose()
        }
        $request.Dispose()
        $cancellation.Dispose()
    }
}

function Close-SseConnection {
    param(
        [AllowNull()]
        [object]$Connection
    )

    if ($null -eq $Connection) {
        return
    }

    # 열린 stream을 먼저 취소해 pending ReadLineAsync를 깨운 뒤, 이 검증이 소유한 객체만 역순으로 정리합니다.
    try {
        $Connection.Cancellation.Cancel()
    }
    catch [System.ObjectDisposedException] {
        # 이미 정리된 연결을 finally에서 다시 닫아도 안전하게 끝냅니다.
    }

    $Connection.Reader.Dispose()
    $Connection.Stream.Dispose()
    $Connection.Response.Dispose()
    $Connection.Request.Dispose()
    $Connection.Cancellation.Dispose()
}

function Open-SseConnection {
    param(
        [Parameter(Mandatory)]
        [string]$Path,
        [int]$LifetimeSeconds = 20
    )

    $request = [System.Net.Http.HttpRequestMessage]::new(
        [System.Net.Http.HttpMethod]::Get,
        "$baseUrl$Path"
    )
    $added = $request.Headers.TryAddWithoutValidation('Accept', 'text/event-stream')
    if (-not $added) {
        $request.Dispose()
        throw 'SSE 요청에 Accept header를 추가할 수 없습니다.'
    }

    # 전체 연결 lifetime과 각 frame read timeout을 모두 둬서 회귀가 생겨도 verifier가 무기한 대기하지 않습니다.
    $cancellation = [System.Threading.CancellationTokenSource]::new(
        [TimeSpan]::FromSeconds($LifetimeSeconds)
    )
    $response = $null
    $stream = $null
    $reader = $null

    try {
        $response = $httpClient.SendAsync(
            $request,
            [System.Net.Http.HttpCompletionOption]::ResponseHeadersRead,
            $cancellation.Token
        ).GetAwaiter().GetResult()
        $stream = $response.Content.ReadAsStreamAsync(
            $cancellation.Token
        ).GetAwaiter().GetResult()
        $reader = [System.IO.StreamReader]::new(
            $stream,
            [System.Text.Encoding]::UTF8,
            $true,
            1024,
            $true
        )
        $contentType = if ($null -eq $response.Content.Headers.ContentType) {
            ''
        }
        else {
            $response.Content.Headers.ContentType.ToString()
        }

        return [pscustomobject]@{
            StatusCode = [int]$response.StatusCode
            ContentType = $contentType
            Request = $request
            Response = $response
            Stream = $stream
            Reader = $reader
            Cancellation = $cancellation
        }
    }
    catch {
        if ($null -ne $reader) {
            $reader.Dispose()
        }
        if ($null -ne $stream) {
            $stream.Dispose()
        }
        if ($null -ne $response) {
            $response.Dispose()
        }
        $request.Dispose()
        $cancellation.Dispose()
        throw
    }
}

function Read-SseFrame {
    param(
        [Parameter(Mandatory)]
        [object]$Connection,
        [Parameter(Mandatory)]
        [string]$Context,
        [int]$TimeoutSeconds = 5
    )

    $readCancellation =
        [System.Threading.CancellationTokenSource]::CreateLinkedTokenSource(
            $Connection.Cancellation.Token
        )
    $readCancellation.CancelAfter([TimeSpan]::FromSeconds($TimeoutSeconds))
    $lines = [System.Collections.Generic.List[string]]::new()

    try {
        while ($true) {
            $lineValueTask = $Connection.Reader.ReadLineAsync(
                $readCancellation.Token
            )
            $line = $lineValueTask.AsTask().GetAwaiter().GetResult()

            if ($null -eq $line) {
                if ($lines.Count -gt 0) {
                    throw "검증 실패: $Context SSE가 빈 줄 frame terminator 없이 EOF에 도달했습니다."
                }

                return [pscustomobject]@{
                    IsEndOfStream = $true
                    Raw = ''
                }
            }

            if ($line.Length -eq 0) {
                break
            }

            $lines.Add($line)
        }

        $lineFeed = [string][char]10
        return [pscustomobject]@{
            IsEndOfStream = $false
            Raw = [string]::Join($lineFeed, $lines) + $lineFeed + $lineFeed
        }
    }
    catch [System.OperationCanceledException] {
        throw "검증 실패: $Context SSE frame 또는 EOF가 $($TimeoutSeconds)초 안에 도착하지 않았습니다."
    }
    finally {
        $readCancellation.Dispose()
    }
}

function Assert-JsonContentType {
    param(
        [Parameter(Mandatory)]
        [object]$Response,
        [Parameter(Mandatory)]
        [string]$Message
    )

    Assert-True (
        $Response.ContentType.StartsWith(
            'application/json',
            [StringComparison]::OrdinalIgnoreCase
        )
    ) $Message
}

function Assert-ProblemResponse {
    param(
        [Parameter(Mandatory)]
        [object]$Response,
        [Parameter(Mandatory)]
        [int]$StatusCode,
        [Parameter(Mandatory)]
        [string]$Context,
        [string]$ExpectedCode
    )

    Assert-True ($Response.StatusCode -eq $StatusCode) "$Context 응답은 HTTP $StatusCode 이다."
    Assert-True (
        $Response.ContentType.StartsWith(
            'application/problem+json',
            [StringComparison]::OrdinalIgnoreCase
        )
    ) "$Context 응답 media type은 application/problem+json이다."
    Assert-True (-not [string]::IsNullOrWhiteSpace($Response.Body)) "$Context Problem Details 본문이 있다."

    $document = [System.Text.Json.JsonDocument]::Parse($Response.Body)
    try {
        $root = $document.RootElement
        Assert-True (
            $root.ValueKind -eq [System.Text.Json.JsonValueKind]::Object
        ) "$Context Problem Details는 JSON object다."

        $statusElement = [System.Text.Json.JsonElement]::new()
        Assert-True (
            $root.TryGetProperty('status', [ref]$statusElement)
        ) "$Context Problem Details에 status가 있다."
        Assert-True (
            $statusElement.ValueKind -eq [System.Text.Json.JsonValueKind]::Number -and
            $statusElement.GetInt32() -eq $StatusCode
        ) "$Context Problem Details status가 HTTP 상태와 같다."

        $titleElement = [System.Text.Json.JsonElement]::new()
        Assert-True (
            $root.TryGetProperty('title', [ref]$titleElement) -and
            $titleElement.ValueKind -eq [System.Text.Json.JsonValueKind]::String -and
            -not [string]::IsNullOrWhiteSpace($titleElement.GetString())
        ) "$Context Problem Details에 사람이 읽을 수 있는 title이 있다."

        if ($PSBoundParameters.ContainsKey('ExpectedCode')) {
            $codeElement = [System.Text.Json.JsonElement]::new()
            Assert-True (
                $root.TryGetProperty('code', [ref]$codeElement) -and
                $codeElement.ValueKind -eq [System.Text.Json.JsonValueKind]::String
            ) "$Context Problem Details에 안정적인 code가 있다."
            Assert-True (
                $codeElement.GetString() -ceq $ExpectedCode
            ) "$Context Problem Details code는 $ExpectedCode 이다."
        }
    }
    finally {
        $document.Dispose()
    }
}

function New-EventBody {
    param(
        [Parameter(Mandatory)]
        [string]$Stage,
        [Parameter(Mandatory)]
        [int]$Percent,
        [Parameter(Mandatory)]
        [string]$Message
    )

    return [ordered]@{
        stage = $Stage
        percent = $Percent
        message = $Message
    } | ConvertTo-Json -Compress
}

function Read-DeploymentEvent {
    param(
        [Parameter(Mandatory)]
        [string]$Json,
        [Parameter(Mandatory)]
        [string]$ExpectedDeploymentId,
        [Parameter(Mandatory)]
        [string]$ExpectedStage,
        [Parameter(Mandatory)]
        [int]$ExpectedPercent,
        [Parameter(Mandatory)]
        [string]$ExpectedMessage,
        [Parameter(Mandatory)]
        [bool]$ExpectedIsTerminal,
        [Parameter(Mandatory)]
        [string]$Context
    )

    $document = [System.Text.Json.JsonDocument]::Parse($Json)
    try {
        $root = $document.RootElement
        Assert-True (
            $root.ValueKind -eq [System.Text.Json.JsonValueKind]::Object
        ) "$Context event payload는 JSON object다."

        # JSON object 자체의 의미는 property 순서와 무관하지만, 이 학습 예제에서는
        # wire format을 눈으로 비교하기 쉽도록 DTO의 의도한 직렬화 순서까지 고정해 검증합니다.
        $propertyNames = @(
            $root.EnumerateObject() | ForEach-Object { $_.Name }
        )
        $actualPropertyOrder = [string]::Join(',', [string[]]$propertyNames)
        $expectedPropertyOrder = 'eventId,deploymentId,stage,percent,message,occurredAtUtc,isTerminal'
        Assert-True (
            $actualPropertyOrder -ceq $expectedPropertyOrder
        ) "$Context field 순서와 목록이 eventId,deploymentId,stage,percent,message,occurredAtUtc,isTerminal이다."

        $eventIdElement = $root.GetProperty('eventId')
        Assert-True (
            $eventIdElement.ValueKind -eq [System.Text.Json.JsonValueKind]::Number
        ) "$Context eventId는 JSON number다."
        $eventId = $eventIdElement.GetInt64()
        Assert-True ($eventId -gt 0) "$Context eventId는 양의 정수다."

        $deploymentId = $root.GetProperty('deploymentId').GetString()
        $stage = $root.GetProperty('stage').GetString()
        $percent = $root.GetProperty('percent').GetInt32()
        $message = $root.GetProperty('message').GetString()
        $occurredAtUtcText = $root.GetProperty('occurredAtUtc').GetString()
        $isTerminal = $root.GetProperty('isTerminal').GetBoolean()

        Assert-True (
            $deploymentId -ceq $ExpectedDeploymentId
        ) "$Context deploymentId가 요청 경로와 같다."
        Assert-True ($stage -ceq $ExpectedStage) "$Context stage는 $ExpectedStage 이다."
        Assert-True ($percent -eq $ExpectedPercent) "$Context percent는 $ExpectedPercent 이다."
        Assert-True ($message -ceq $ExpectedMessage) "$Context message가 요청 본문과 같다."

        $occurredAtUtc = [DateTimeOffset]::MinValue
        Assert-True (
            [DateTimeOffset]::TryParse($occurredAtUtcText, [ref]$occurredAtUtc)
        ) "$Context occurredAtUtc를 DateTimeOffset으로 읽을 수 있다."
        Assert-True (
            $occurredAtUtc.Offset -eq [TimeSpan]::Zero
        ) "$Context occurredAtUtc의 offset은 UTC(+00:00)다."
        Assert-True (
            $isTerminal -eq $ExpectedIsTerminal
        ) "$Context isTerminal 값이 stage의 terminal 여부와 같다."

        return [pscustomobject]@{
            EventId = $eventId
            DeploymentId = $deploymentId
            Stage = $stage
            Percent = $percent
            Message = $message
            OccurredAtUtcText = $occurredAtUtcText
            OccurredAtUtc = $occurredAtUtc
            IsTerminal = $isTerminal
            RawJson = $Json
        }
    }
    finally {
        $document.Dispose()
    }
}

function ConvertFrom-SseBody {
    param(
        [Parameter(Mandatory)]
        [string]$Raw,
        [Parameter(Mandatory)]
        [string]$Context
    )

    $carriageReturn = [string][char]13
    $lineFeed = [string][char]10
    $crlf = $carriageReturn + $lineFeed
    $frameSeparator = $lineFeed + $lineFeed
    $normalized = $Raw.Replace($crlf, $lineFeed)

    Assert-True (
        -not $normalized.Contains($carriageReturn)
    ) "$Context SSE line ending은 일관된 LF 또는 CRLF다."
    Assert-True (
        $normalized.EndsWith($frameSeparator, [StringComparison]::Ordinal)
    ) "$Context SSE 마지막 event도 빈 줄로 끝난다."

    $blocks = @(
        [regex]::Split($normalized, [regex]::Escape($frameSeparator)) |
            Where-Object { $_.Length -gt 0 }
    )
    $frames = @()

    foreach ($block in $blocks) {
        $lines = @(
            [regex]::Split($block, [regex]::Escape($lineFeed))
        )
        Assert-True (
            $lines.Count -in @(3, 4)
        ) "$Context SSE event는 필수 id/event/data와 선택 retry만 가진다."

        # .NET formatter가 SSE field를 쓰는 물리적 순서는 runtime 구현 세부 사항입니다.
        # 순서에 기대지 않고 각 field가 정확히 한 번 있는지 확인하면 framing 계약은 엄격히 지키면서도 안전합니다.
        $idLines = @($lines | Where-Object { $_.StartsWith('id:', [StringComparison]::Ordinal) })
        $eventLines = @($lines | Where-Object { $_.StartsWith('event:', [StringComparison]::Ordinal) })
        $dataLines = @($lines | Where-Object { $_.StartsWith('data:', [StringComparison]::Ordinal) })
        $retryLines = @($lines | Where-Object { $_.StartsWith('retry:', [StringComparison]::Ordinal) })

        Assert-True ($idLines.Count -eq 1) "$Context SSE id field가 정확히 한 번 있다."
        $idMatched = $idLines[0] -match '^id: ([1-9][0-9]*)$'
        Assert-True $idMatched "$Context SSE id는 양의 10진 정수다."
        $frameId = if ($idMatched) { [long]$Matches[1] } else { 0L }

        Assert-True ($eventLines.Count -eq 1) "$Context SSE event field가 정확히 한 번 있다."
        Assert-True (
            $eventLines[0] -ceq 'event: deployment-progress'
        ) "$Context SSE event 값은 deployment-progress 이다."

        Assert-True ($dataLines.Count -eq 1) "$Context SSE data field가 정확히 한 번 있다."
        $dataMatched = $dataLines[0] -match '^data: (.+)$'
        Assert-True $dataMatched "$Context SSE data는 비어 있지 않은 한 줄 JSON이다."
        $data = if ($dataMatched) { $Matches[1] } else { '' }

        Assert-True (
            $retryLines.Count -eq 0 -or
            ($retryLines.Count -eq 1 -and $retryLines[0] -ceq 'retry: 2000')
        ) "$Context SSE retry가 있으면 정확히 한 번, 2000ms 값이다."
        $recognizedLineCount = $idLines.Count + $eventLines.Count + $dataLines.Count + $retryLines.Count
        Assert-True (
            $recognizedLineCount -eq $lines.Count
        ) "$Context SSE event에는 알 수 없는 field나 comment가 없다."

        $frames += [pscustomobject]@{
            Id = $frameId
            Event = 'deployment-progress'
            Data = $data
        }
    }

    return $frames
}

function Assert-SseSequence {
    param(
        [Parameter(Mandatory)]
        [object[]]$Frames,
        [Parameter(Mandatory)]
        [object[]]$ExpectedEvents,
        [Parameter(Mandatory)]
        [string]$Context
    )

    Assert-True (
        $Frames.Count -eq $ExpectedEvents.Count
    ) "$Context SSE event 개수가 예상과 같다."

    for ($index = 0; $index -lt $ExpectedEvents.Count; $index++) {
        $frame = $Frames[$index]
        $expected = $ExpectedEvents[$index]
        $ordinal = $index + 1

        Assert-True (
            $frame.Id -eq $expected.EventId
        ) "$Context $ordinal 번째 id가 저장 순서와 같다."

        $payload = Read-DeploymentEvent `
            -Json $frame.Data `
            -ExpectedDeploymentId $expected.DeploymentId `
            -ExpectedStage $expected.Stage `
            -ExpectedPercent $expected.Percent `
            -ExpectedMessage $expected.Message `
            -ExpectedIsTerminal $expected.IsTerminal `
            -Context "$Context $ordinal 번째 data"

        Assert-True (
            $payload.EventId -eq $frame.Id
        ) "$Context $ordinal 번째 id field와 data.eventId가 같다."
        Assert-True (
            $payload.OccurredAtUtcText -ceq $expected.OccurredAtUtcText
        ) "$Context $ordinal 번째 data의 occurredAtUtc가 POST 응답과 같다."
    }
}

function Publish-Sequence {
    param(
        [Parameter(Mandatory)]
        [string]$DeploymentId,
        [Parameter(Mandatory)]
        [object[]]$ExpectedSequence,
        [Parameter(Mandatory)]
        [string]$Context
    )

    $escapedDeploymentId = [Uri]::EscapeDataString($DeploymentId)
    $path = "/deployments/$escapedDeploymentId/events"
    $events = @()

    foreach ($expected in $ExpectedSequence) {
        $body = New-EventBody `
            -Stage $expected.Stage `
            -Percent $expected.Percent `
            -Message $expected.Message
        $response = Invoke-Api -Method POST -Path $path -Body $body

        Assert-True (
            $response.StatusCode -eq 201
        ) "$Context $($expected.Stage) event 생성은 201 Created다."
        Assert-JsonContentType `
            -Response $response `
            -Message "$Context $($expected.Stage) 201 본문은 JSON이다."

        $created = Read-DeploymentEvent `
            -Json $response.Body `
            -ExpectedDeploymentId $DeploymentId `
            -ExpectedStage $expected.Stage `
            -ExpectedPercent $expected.Percent `
            -ExpectedMessage $expected.Message `
            -ExpectedIsTerminal $expected.IsTerminal `
            -Context "$Context $($expected.Stage) 201 본문"

        if ($events.Count -gt 0) {
            $previous = $events[$events.Count - 1]
            Assert-True (
                $created.EventId -gt $previous.EventId
            ) "$Context eventId는 저장 순서대로 엄격히 증가한다."
            Assert-True (
                $created.OccurredAtUtc -ge $previous.OccurredAtUtc
            ) "$Context occurredAtUtc는 저장 순서를 거스르지 않는다."
        }

        Assert-True (
            -not [string]::IsNullOrWhiteSpace($response.Location)
        ) "$Context $($expected.Stage) 201 응답에 Location header가 있다."
        $locationUri = [Uri]::new([Uri]$baseUrl, $response.Location)
        # 개별 event GET endpoint는 없으므로 Location은 존재하지 않는 item URI가 아니라
        # 이 event를 읽을 수 있는 deployment별 collection/stream URI를 가리킵니다.
        $expectedLocationPath = $path
        Assert-True (
            $locationUri.AbsolutePath -ceq $expectedLocationPath
        ) "$Context Location은 deployment event collection/stream을 가리킨다."

        $events += $created
    }

    return $events
}

try {
    # 포트 0을 Kestrel에 전달하면 운영체제가 실제 listen 시점에 빈 포트를 원자적으로 선택합니다.
    # 미리 포트를 열었다 닫는 방식의 TOCTOU 경쟁 없이 exact executable을 Production으로 실행합니다.
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
                $addressMatch = [regex]::Match(
                    [string]$serverOutput,
                    'http://127\.0\.0\.1:\d+'
                )
                if ($addressMatch.Success) {
                    $baseUrl = $addressMatch.Value
                    $rootResponse = Invoke-Api `
                        -Method GET `
                        -Path '/' `
                        -TimeoutSeconds 1
                    if ($rootResponse.StatusCode -eq 200) {
                        $rootDocument = [System.Text.Json.JsonDocument]::Parse(
                            $rootResponse.Body
                        )
                        try {
                            $serviceElement = [System.Text.Json.JsonElement]::new()
                            $ready = $rootDocument.RootElement.TryGetProperty(
                                'service',
                                [ref]$serviceElement
                            ) -and
                                $serviceElement.GetString() -ceq 'DeploymentSseApi'
                        }
                        finally {
                            $rootDocument.Dispose()
                        }
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

    Assert-True $ready 'DeploymentSseApi가 Production의 동적 loopback 포트에서 준비된다.'

    $suffix = $runId.Substring(0, 8)
    $deploymentId = "verify-completed-$suffix"
    $escapedDeploymentId = [Uri]::EscapeDataString($deploymentId)
    $eventPath = "/deployments/$escapedDeploymentId/events"
    $successfulSequence = @(
        [pscustomobject]@{
            Stage = 'Queued'
            Percent = 0
            Message = '배포 요청이 대기열에 등록되었습니다.'
            IsTerminal = $false
        },
        [pscustomobject]@{
            Stage = 'Building'
            Percent = 69
            Message = 'Release artifact 빌드가 완료되었습니다.'
            IsTerminal = $false
        },
        [pscustomobject]@{
            Stage = 'Deploying'
            Percent = 99
            Message = '새 버전을 마지막 instance에 반영하고 있습니다.'
            IsTerminal = $false
        },
        [pscustomobject]@{
            Stage = 'Completed'
            Percent = 100
            Message = '모든 instance 배포가 완료되었습니다.'
            IsTerminal = $true
        }
    )
    $createdEvents = @(
        Publish-Sequence `
            -DeploymentId $deploymentId `
            -ExpectedSequence $successfulSequence `
            -Context '성공 sequence'
    )

    $streamResponse = Invoke-Api `
        -Method GET `
        -Path $eventPath `
        -Headers @{ Accept = 'text/event-stream' }
    Assert-True ($streamResponse.StatusCode -eq 200) 'terminal 성공 sequence 조회는 200이다.'
    Assert-True (
        $streamResponse.ContentType.StartsWith(
            'text/event-stream',
            [StringComparison]::OrdinalIgnoreCase
        )
    ) 'SSE 응답 media type은 text/event-stream이다.'
    $allFrames = @(
        ConvertFrom-SseBody `
            -Raw $streamResponse.Body `
            -Context '전체 replay'
    )
    Assert-SseSequence `
        -Frames $allFrames `
        -ExpectedEvents $createdEvents `
        -Context '전체 replay'
    Assert-True $true 'terminal event가 이미 있으므로 전체 replay SSE 연결이 제한 시간 안에 닫힌다.'

    $resumeAfter = $createdEvents[1].EventId
    $laterExpected = @($createdEvents[2], $createdEvents[3])
    $resumeResponse = Invoke-Api `
        -Method GET `
        -Path $eventPath `
        -Headers @{
            Accept = 'text/event-stream'
            'Last-Event-ID' = [string]$resumeAfter
        }
    Assert-True ($resumeResponse.StatusCode -eq 200) 'Last-Event-ID 재연결은 200이다.'
    Assert-True (
        $resumeResponse.ContentType.StartsWith(
            'text/event-stream',
            [StringComparison]::OrdinalIgnoreCase
        )
    ) 'Last-Event-ID 재연결 media type은 text/event-stream이다.'
    $laterFrames = @(
        ConvertFrom-SseBody `
            -Raw $resumeResponse.Body `
            -Context '재연결 replay'
    )
    Assert-SseSequence `
        -Frames $laterFrames `
        -ExpectedEvents $laterExpected `
        -Context '재연결 replay'
    foreach ($frame in $laterFrames) {
        Assert-True (
            $frame.Id -gt $resumeAfter
        ) '재연결 replay에는 Last-Event-ID보다 엄격히 큰 event만 있다.'
    }
    Assert-True $true 'terminal event가 포함된 재연결 SSE도 제한 시간 안에 닫힌다.'

    # Failed도 terminal이라는 별도 분기를 검증해 Completed에만 맞춘 구현을 잡아냅니다.
    $failedDeploymentId = "verify-failed-$suffix"
    $failedPath = "/deployments/$([Uri]::EscapeDataString($failedDeploymentId))/events"
    $failedSequence = @(
        [pscustomobject]@{
            Stage = 'Queued'
            Percent = 0
            Message = '실패 시나리오가 대기열에 등록되었습니다.'
            IsTerminal = $false
        },
        [pscustomobject]@{
            Stage = 'Building'
            Percent = 1
            Message = '실패 재현용 빌드를 시작했습니다.'
            IsTerminal = $false
        },
        [pscustomobject]@{
            Stage = 'Failed'
            Percent = 99
            Message = '배포 전 검사에서 정책 위반을 발견했습니다.'
            IsTerminal = $true
        }
    )
    $failedEvents = @(
        Publish-Sequence `
            -DeploymentId $failedDeploymentId `
            -ExpectedSequence $failedSequence `
            -Context '실패 sequence'
    )
    $failedStream = Invoke-Api `
        -Method GET `
        -Path $failedPath `
        -Headers @{ Accept = 'text/event-stream' }
    Assert-True ($failedStream.StatusCode -eq 200) 'Failed terminal sequence 조회는 200이다.'
    Assert-True (
        $failedStream.ContentType.StartsWith(
            'text/event-stream',
            [StringComparison]::OrdinalIgnoreCase
        )
    ) 'Failed terminal sequence 응답 media type은 text/event-stream이다.'
    $failedFrames = @(
        ConvertFrom-SseBody `
            -Raw $failedStream.Body `
            -Context 'Failed replay'
    )
    Assert-SseSequence `
        -Frames $failedFrames `
        -ExpectedEvents $failedEvents `
        -Context 'Failed replay'
    Assert-True $true 'Failed terminal event 뒤에도 SSE 연결이 제한 시간 안에 닫힌다.'

    # 저장된 Queued replay를 먼저 읽고 연결을 유지한 채, 이후 POST가 같은 stream에 live로 도착하는지 검증합니다.
    # terminal을 게시하기 전에 Building frame read가 끝나야 replay-only 구현도 확실히 잡아낼 수 있습니다.
    $liveDeploymentId = "verify-live-$suffix"
    $livePath = "/deployments/$([Uri]::EscapeDataString($liveDeploymentId))/events"
    $liveQueuedDefinition = [pscustomobject]@{
        Stage = 'Queued'
        Percent = 0
        Message = 'live stream 연결 전에 저장한 replay event입니다.'
        IsTerminal = $false
    }
    $liveQueuedEvents = @(
        Publish-Sequence `
            -DeploymentId $liveDeploymentId `
            -ExpectedSequence @($liveQueuedDefinition) `
            -Context 'live stream 초기 replay'
    )

    $liveConnection = $null
    try {
        $liveConnection = Open-SseConnection -Path $livePath
        Assert-True (
            $liveConnection.StatusCode -eq 200
        ) '열린 live SSE 연결은 200이다.'
        Assert-True (
            $liveConnection.ContentType.StartsWith(
                'text/event-stream',
                [StringComparison]::OrdinalIgnoreCase
            )
        ) '열린 live SSE 연결 media type은 text/event-stream이다.'

        $queuedRead = Read-SseFrame `
            -Connection $liveConnection `
            -Context 'live stream Queued replay'
        Assert-True (
            -not $queuedRead.IsEndOfStream
        ) '열린 SSE 연결이 기존 Queued event를 먼저 replay한다.'
        $queuedFrames = @(
            ConvertFrom-SseBody `
                -Raw $queuedRead.Raw `
                -Context 'live stream Queued replay'
        )
        Assert-SseSequence `
            -Frames $queuedFrames `
            -ExpectedEvents $liveQueuedEvents `
            -Context 'live stream Queued replay'

        $liveBuildingDefinition = [pscustomobject]@{
            Stage = 'Building'
            Percent = 25
            Message = '연결을 닫지 않고 받은 live Building event입니다.'
            IsTerminal = $false
        }
        $liveBuildingEvents = @(
            Publish-Sequence `
                -DeploymentId $liveDeploymentId `
                -ExpectedSequence @($liveBuildingDefinition) `
                -Context 'live stream Building 게시'
        )
        Assert-True (
            $liveBuildingEvents[0].EventId -gt $liveQueuedEvents[0].EventId
        ) 'live Building eventId는 replay한 Queued eventId보다 크다.'

        $buildingRead = Read-SseFrame `
            -Connection $liveConnection `
            -Context 'live stream Building 수신'
        Assert-True (
            -not $buildingRead.IsEndOfStream
        ) 'terminal 게시 전에 같은 열린 연결로 live Building event가 도착한다.'
        $buildingFrames = @(
            ConvertFrom-SseBody `
                -Raw $buildingRead.Raw `
                -Context 'live stream Building 수신'
        )
        Assert-SseSequence `
            -Frames $buildingFrames `
            -ExpectedEvents $liveBuildingEvents `
            -Context 'live stream Building 수신'

        $liveFailedDefinition = [pscustomobject]@{
            Stage = 'Failed'
            Percent = 25
            Message = 'live stream 종료를 확인하는 terminal event입니다.'
            IsTerminal = $true
        }
        $liveFailedEvents = @(
            Publish-Sequence `
                -DeploymentId $liveDeploymentId `
                -ExpectedSequence @($liveFailedDefinition) `
                -Context 'live stream Failed 게시'
        )
        Assert-True (
            $liveFailedEvents[0].EventId -gt $liveBuildingEvents[0].EventId
        ) 'live Failed eventId는 Building eventId보다 크다.'

        $failedLiveRead = Read-SseFrame `
            -Connection $liveConnection `
            -Context 'live stream Failed 수신'
        Assert-True (
            -not $failedLiveRead.IsEndOfStream
        ) '같은 열린 연결로 terminal Failed event가 도착한다.'
        $failedLiveFrames = @(
            ConvertFrom-SseBody `
                -Raw $failedLiveRead.Raw `
                -Context 'live stream Failed 수신'
        )
        Assert-SseSequence `
            -Frames $failedLiveFrames `
            -ExpectedEvents $liveFailedEvents `
            -Context 'live stream Failed 수신'

        $endOfLiveStream = Read-SseFrame `
            -Connection $liveConnection `
            -Context 'live stream terminal 뒤 EOF'
        Assert-True (
            $endOfLiveStream.IsEndOfStream -and
            $endOfLiveStream.Raw.Length -eq 0
        ) 'terminal Failed event 다음에는 같은 SSE 연결에서 EOF가 온다.'
    }
    finally {
        Close-SseConnection -Connection $liveConnection
    }

    $invalidTransition = Invoke-Api `
        -Method POST `
        -Path $eventPath `
        -Body (
            New-EventBody `
                -Stage 'Building' `
                -Percent 50 `
                -Message 'terminal 뒤에는 되돌아갈 수 없습니다.'
        )
    Assert-ProblemResponse `
        -Response $invalidTransition `
        -StatusCode 409 `
        -Context 'terminal 뒤 잘못된 상태 전이'

    # Enum.TryParse는 Flags attribute가 없어도 쉼표로 연결한 이름을 숫자 조합으로 받아들일 수 있습니다.
    # 정의되지 않은 composite가 Completed 값처럼 해석되는 회귀를 막기 위해 terminal 검사보다 stage 검증이 먼저여야 합니다.
    $compositeStage = Invoke-Api `
        -Method POST `
        -Path $eventPath `
        -Body (
            New-EventBody `
                -Stage 'Building,Deploying' `
                -Percent 100 `
                -Message '정의되지 않은 composite enum 이름입니다.'
        )
    Assert-ProblemResponse `
        -Response $compositeStage `
        -StatusCode 400 `
        -Context '쉼표로 연결한 composite stage' `
        -ExpectedCode 'deployment.stage.invalid'

    $invalidLastEventId = Invoke-Api `
        -Method GET `
        -Path $eventPath `
        -Headers @{
            Accept = 'text/event-stream'
            'Last-Event-ID' = 'not-a-positive-event-id'
        }
    Assert-ProblemResponse `
        -Response $invalidLastEventId `
        -StatusCode 400 `
        -Context '잘못된 Last-Event-ID'

    $missingPath = "/deployments/verify-missing-$suffix/events"
    $missing = Invoke-Api `
        -Method GET `
        -Path $missingPath `
        -Headers @{ Accept = 'text/event-stream' }
    Assert-ProblemResponse `
        -Response $missing `
        -StatusCode 404 `
        -Context '존재하지 않는 deployment'

    $malformedPath = "/deployments/verify-malformed-$suffix/events"
    $malformed = Invoke-Api `
        -Method POST `
        -Path $malformedPath `
        -Body '{'
    Assert-ProblemResponse `
        -Response $malformed `
        -StatusCode 400 `
        -Context '깨진 JSON'

    # 누락된 non-optional 생성자 인수가 int 기본값 0으로 보정되지 않는지 실제 JSON binding 경계에서 확인합니다.
    $missingPercentPath = "/deployments/verify-missing-percent-$suffix/events"
    $missingPercent = Invoke-Api `
        -Method POST `
        -Path $missingPercentPath `
        -Body '{"stage":"Queued","message":"percent를 일부러 생략했습니다."}'
    Assert-ProblemResponse `
        -Response $missingPercent `
        -StatusCode 400 `
        -Context 'percent 필드 누락'

    $wrongMediaTypePath = "/deployments/verify-media-$suffix/events"
    $wrongMediaType = Invoke-Api `
        -Method POST `
        -Path $wrongMediaTypePath `
        -Body '{"stage":"Queued","percent":0,"message":"wrong media type"}' `
        -ContentType 'text/plain'
    Assert-ProblemResponse `
        -Response $wrongMediaType `
        -StatusCode 415 `
        -Context '지원하지 않는 text/plain'

    Write-Host '검증 가정: Location=/deployments/{deploymentId}/events, SSE event=deployment-progress, eventId=양의 10진 정수.'
    Write-Host '검증 가정: success JSON field 순서는 명세에 적힌 순서를 따르며 Completed와 Failed 모두 terminal이다.'
    Write-Host "HTTP VERIFY PASSED: $passed assertions"
}
catch {
    $lineBreak = [Environment]::NewLine
    if (Test-Path -LiteralPath $stdoutPath) {
        Write-Error (
            '--- server stdout ---' +
            $lineBreak +
            (Get-Content -Raw -LiteralPath $stdoutPath)
        ) -ErrorAction Continue
    }
    if (Test-Path -LiteralPath $stderrPath) {
        Write-Error (
            '--- server stderr ---' +
            $lineBreak +
            (Get-Content -Raw -LiteralPath $stderrPath)
        ) -ErrorAction Continue
    }
    throw
}
finally {
    # 이 검증이 Start-Process로 만든 정확한 PID만 종료합니다.
    # 이름으로 프로세스를 일괄 종료하면 사용자의 다른 서버까지 멈출 수 있으므로 금지합니다.
    if ($null -ne $serverProcess -and -not $serverProcess.HasExited) {
        Stop-Process -Id $serverProcess.Id -Force
        $serverProcess.WaitForExit()
    }

    $httpClient.Dispose()
    Remove-Item -LiteralPath $stdoutPath -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $stderrPath -Force -ErrorAction SilentlyContinue
}
