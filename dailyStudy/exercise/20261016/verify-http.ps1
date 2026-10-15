[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = Join-Path $PSScriptRoot 'src\WebhookReceiverApi'
$executablePath = Join-Path $projectRoot 'bin\Release\net10.0\WebhookReceiverApi.exe'
$assemblyPath = Join-Path $projectRoot 'bin\Release\net10.0\WebhookReceiverApi.dll'
$webhookSecret = 'daily-csharp-webhook-secret-20261016-change-in-production'
$utf8 = [System.Text.UTF8Encoding]::new($false, $true)
$runId = [Guid]::NewGuid().ToString('N')
$tempDirectory = [System.IO.Path]::GetTempPath()
$stdoutPath = Join-Path $tempDirectory "webhook-receiver-$runId.stdout.log"
$stderrPath = Join-Path $tempDirectory "webhook-receiver-$runId.stderr.log"
$serverProcess = $null
$baseUrl = $null
$passed = 0
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

function New-SignatureHeader {
    param(
        [Parameter(Mandatory)]
        [string]$Secret,
        [Parameter(Mandatory)]
        [string]$Timestamp,
        [Parameter(Mandatory)]
        [string]$EventId,
        [Parameter(Mandatory)]
        [byte[]]$BodyBytes
    )

    # 공급자와 수신자가 완전히 같은 byte sequence에 서명해야 합니다.
    # 구분점까지 포함한 UTF-8 prefix 뒤에 JSON을 다시 직렬화하지 않은 raw body를 그대로 이어 붙입니다.
    $prefixText = [string]::Concat($Timestamp, '.', $EventId, '.')
    $prefixBytes = ConvertTo-Utf8Bytes -Text $prefixText
    $canonicalBytes = [byte[]]::new($prefixBytes.Length + $BodyBytes.Length)
    [Buffer]::BlockCopy($prefixBytes, 0, $canonicalBytes, 0, $prefixBytes.Length)
    [Buffer]::BlockCopy($BodyBytes, 0, $canonicalBytes, $prefixBytes.Length, $BodyBytes.Length)

    $keyBytes = ConvertTo-Utf8Bytes -Text $Secret
    $hmac = [System.Security.Cryptography.HMACSHA256]::new($keyBytes)
    try {
        $digest = $hmac.ComputeHash($canonicalBytes)
        # Convert.ToHexString은 대문자를 반환하므로 외부 계약에 맞게 소문자로 정규화합니다.
        return 'v1=' + [Convert]::ToHexString($digest).ToLowerInvariant()
    }
    finally {
        $hmac.Dispose()
    }
}

function New-PaymentBody {
    param(
        [Parameter(Mandatory)]
        [AllowNull()]
        [object]$Type,
        [Parameter(Mandatory)]
        [AllowNull()]
        [object]$PaymentId,
        [Parameter(Mandatory)]
        [decimal]$Amount,
        [Parameter(Mandatory)]
        [AllowNull()]
        [object]$Currency
    )

    $payload = [ordered]@{
        type = $Type
        data = [ordered]@{
            paymentId = $PaymentId
            amount = $Amount
            currency = $Currency
        }
    }

    return ConvertTo-Utf8Bytes -Text ($payload | ConvertTo-Json -Compress -Depth 4)
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
    $wwwAuthenticate = @(
        $Response.Headers.WwwAuthenticate | ForEach-Object { $_.ToString() }
    ) -join ', '
    $cacheControl = if ($null -eq $Response.Headers.CacheControl) {
        ''
    }
    else {
        $Response.Headers.CacheControl.ToString()
    }

    return [pscustomobject]@{
        StatusCode = [int]$Response.StatusCode
        BodyBytes = $bytes
        Body = $script:utf8.GetString($bytes)
        ContentType = $contentType
        Location = $location
        WwwAuthenticate = $wwwAuthenticate
        CacheControl = $cacheControl
    }
}

function Invoke-Api {
    param(
        [Parameter(Mandatory)]
        [ValidateSet('GET', 'POST')]
        [string]$Method,
        [Parameter(Mandatory)]
        [string]$Path,
        [byte[]]$BodyBytes,
        [string]$ContentType = 'application/json',
        [hashtable]$Headers = @{},
        [switch]$UseChunked
    )

    $request = [System.Net.Http.HttpRequestMessage]::new(
        [System.Net.Http.HttpMethod]::new($Method),
        "$script:baseUrl$Path")
    try {
        $request.Version = [Version]::new(1, 1)
        foreach ($entry in $Headers.GetEnumerator()) {
            if (-not $request.Headers.TryAddWithoutValidation(
                    [string]$entry.Key,
                    [string]$entry.Value)) {
                throw "요청 header를 만들 수 없습니다: $($entry.Key)"
            }
        }

        if ($PSBoundParameters.ContainsKey('BodyBytes')) {
            $request.Content = [System.Net.Http.ByteArrayContent]::new($BodyBytes)
            $request.Content.Headers.ContentType =
                [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse($ContentType)
            if ($UseChunked) {
                # Content-Length 없이 실제 stream을 읽는 경로도 앱의 byte 상한이 지키는지 확인합니다.
                $request.Headers.TransferEncodingChunked = $true
            }
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

function Invoke-SignedWebhook {
    param(
        [Parameter(Mandatory)]
        [string]$EventId,
        [Parameter(Mandatory)]
        [string]$Timestamp,
        [Parameter(Mandatory)]
        [byte[]]$BodyBytes,
        [Parameter(Mandatory)]
        [string]$SigningSecret,
        [string]$ContentType = 'application/json',
        [string[]]$OmitHeaders = @(),
        [switch]$UseChunked,
        [AllowNull()]
        [string]$SignatureOverride
    )

    $signature = if ($PSBoundParameters.ContainsKey('SignatureOverride')) {
        $SignatureOverride
    }
    else {
        New-SignatureHeader `
            -Secret $SigningSecret `
            -Timestamp $Timestamp `
            -EventId $EventId `
            -BodyBytes $BodyBytes
    }

    $headers = @{}
    if ($OmitHeaders -notcontains 'X-Webhook-Id') {
        $headers['X-Webhook-Id'] = $EventId
    }
    if ($OmitHeaders -notcontains 'X-Webhook-Timestamp') {
        $headers['X-Webhook-Timestamp'] = $Timestamp
    }
    if ($OmitHeaders -notcontains 'X-Webhook-Signature') {
        $headers['X-Webhook-Signature'] = $signature
    }

    return Invoke-Api `
        -Method POST `
        -Path '/webhooks/payments' `
        -BodyBytes $BodyBytes `
        -ContentType $ContentType `
        -Headers $headers `
        -UseChunked:$UseChunked
}

function ConvertFrom-ChunkedBytes {
    param(
        [Parameter(Mandatory)]
        [byte[]]$Bytes
    )

    $position = 0
    $decoded = [System.IO.MemoryStream]::new()
    try {
        while ($true) {
            $lineEnd = -1
            for ($index = $position; $index -lt $Bytes.Length - 1; $index++) {
                if ($Bytes[$index] -eq 13 -and $Bytes[$index + 1] -eq 10) {
                    $lineEnd = $index
                    break
                }
            }
            if ($lineEnd -lt 0) {
                throw 'chunked 응답에서 chunk 크기 줄을 찾지 못했습니다.'
            }

            $sizeText = [Text.Encoding]::ASCII.GetString(
                $Bytes,
                $position,
                $lineEnd - $position).Split(';')[0].Trim()
            $chunkSize = [Convert]::ToInt32($sizeText, 16)
            $position = $lineEnd + 2
            if ($chunkSize -eq 0) {
                break
            }
            if ($position + $chunkSize + 2 -gt $Bytes.Length) {
                throw 'chunked 응답 본문이 선언한 크기보다 짧습니다.'
            }

            $decoded.Write($Bytes, $position, $chunkSize)
            $position += $chunkSize
            if ($Bytes[$position] -ne 13 -or $Bytes[$position + 1] -ne 10) {
                throw 'chunked 응답의 chunk 끝 CRLF가 없습니다.'
            }
            $position += 2
        }

        return ,$decoded.ToArray()
    }
    finally {
        $decoded.Dispose()
    }
}

function Invoke-RawHttpRequest {
    param(
        [Parameter(Mandatory)]
        [string]$Path,
        [Parameter(Mandatory)]
        [byte[]]$BodyBytes,
        [Parameter(Mandatory)]
        [object[]]$HeaderPairs
    )

    # HttpClient는 같은 이름의 custom header 값을 한 줄로 합칠 수 있습니다.
    # 서버의 "복수 header 거부"를 정확히 확인하려고 이 세 요청만 원시 HTTP/1.1로 보냅니다.
    $baseUri = [Uri]$script:baseUrl
    $builder = [Text.StringBuilder]::new()
    [void]$builder.Append("POST $Path HTTP/1.1`r`n")
    [void]$builder.Append("Host: $($baseUri.Host):$($baseUri.Port)`r`n")
    [void]$builder.Append("Connection: close`r`n")
    [void]$builder.Append("Content-Type: application/json`r`n")
    [void]$builder.Append("Content-Length: $($BodyBytes.Length)`r`n")
    foreach ($pair in $HeaderPairs) {
        [void]$builder.Append("$($pair.Name): $($pair.Value)`r`n")
    }
    [void]$builder.Append("`r`n")

    $headBytes = [Text.Encoding]::ASCII.GetBytes($builder.ToString())
    $requestBytes = [byte[]]::new($headBytes.Length + $BodyBytes.Length)
    [Buffer]::BlockCopy($headBytes, 0, $requestBytes, 0, $headBytes.Length)
    [Buffer]::BlockCopy($BodyBytes, 0, $requestBytes, $headBytes.Length, $BodyBytes.Length)

    $client = [System.Net.Sockets.TcpClient]::new()
    $rawResponse = [System.IO.MemoryStream]::new()
    try {
        [void]$client.ConnectAsync($baseUri.Host, $baseUri.Port).GetAwaiter().GetResult()
        $stream = $client.GetStream()
        $stream.ReadTimeout = 5000
        $stream.WriteTimeout = 5000
        $stream.Write($requestBytes, 0, $requestBytes.Length)
        $stream.Flush()

        $buffer = [byte[]]::new(8192)
        while (($count = $stream.Read($buffer, 0, $buffer.Length)) -gt 0) {
            $rawResponse.Write($buffer, 0, $count)
        }
        $allBytes = $rawResponse.ToArray()
    }
    finally {
        $rawResponse.Dispose()
        $client.Dispose()
    }

    $headerEnd = -1
    for ($index = 0; $index -le $allBytes.Length - 4; $index++) {
        if ($allBytes[$index] -eq 13 -and
            $allBytes[$index + 1] -eq 10 -and
            $allBytes[$index + 2] -eq 13 -and
            $allBytes[$index + 3] -eq 10) {
            $headerEnd = $index
            break
        }
    }
    if ($headerEnd -lt 0) {
        throw '원시 HTTP 응답에서 header 끝을 찾지 못했습니다.'
    }

    $headerText = [Text.Encoding]::ASCII.GetString($allBytes, 0, $headerEnd)
    $lines = $headerText -split "`r`n"
    if ($lines[0] -notmatch '^HTTP/\d(?:\.\d)?\s+(\d{3})') {
        throw "원시 HTTP status line을 해석할 수 없습니다: $($lines[0])"
    }
    $statusCode = [int]$Matches[1]
    $headers = @{}
    foreach ($line in $lines | Select-Object -Skip 1) {
        $separator = $line.IndexOf(':')
        if ($separator -le 0) {
            continue
        }
        $name = $line.Substring(0, $separator).Trim().ToLowerInvariant()
        $value = $line.Substring($separator + 1).Trim()
        if ($headers.ContainsKey($name)) {
            $headers[$name] = "$($headers[$name]), $value"
        }
        else {
            $headers[$name] = $value
        }
    }

    $bodyStart = $headerEnd + 4
    $wireBody = [byte[]]::new($allBytes.Length - $bodyStart)
    [Array]::Copy($allBytes, $bodyStart, $wireBody, 0, $wireBody.Length)
    $body = if ($headers.ContainsKey('transfer-encoding') -and
        $headers['transfer-encoding'] -match '(?i)chunked') {
        ConvertFrom-ChunkedBytes -Bytes $wireBody
    }
    else {
        $wireBody
    }

    return [pscustomobject]@{
        StatusCode = $statusCode
        BodyBytes = $body
        Body = $script:utf8.GetString($body)
        ContentType = if ($headers.ContainsKey('content-type')) { $headers['content-type'] } else { '' }
        Location = if ($headers.ContainsKey('location')) { $headers['location'] } else { '' }
        WwwAuthenticate = if ($headers.ContainsKey('www-authenticate')) { $headers['www-authenticate'] } else { '' }
    }
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
        [string]$ExpectedField,
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
        Assert-True (-not [string]::IsNullOrWhiteSpace($root.GetProperty('title').GetString())) "$Context Problem Details title이 있다."
        Assert-True (-not [string]::IsNullOrWhiteSpace($root.GetProperty('detail').GetString())) "$Context Problem Details detail이 있다."
        Assert-True ($root.GetProperty('instance').GetString() -ceq $Instance) "$Context Problem Details instance가 요청 경로와 같다."
        Assert-True ($root.GetProperty('code').GetString() -ceq $Code) "$Context 안정 오류 code는 $Code 이다."
        Assert-True ($root.GetProperty('field').GetString() -ceq $ExpectedField) "$Context 오류 field는 $ExpectedField 이다."
        Assert-True (-not [string]::IsNullOrWhiteSpace($root.GetProperty('traceId').GetString())) "$Context 서버 추적용 traceId가 있다."
    }
    finally {
        $document.Dispose()
    }
}

function Assert-Unauthorized {
    param(
        [Parameter(Mandatory)]
        [object]$Response,
        [Parameter(Mandatory)]
        [string]$Context
    )

    Assert-Problem `
        -Response $Response `
        -StatusCode 401 `
        -Code 'webhook.unauthorized' `
        -ExpectedField 'X-Webhook-Signature' `
        -Instance '/webhooks/payments' `
        -Context $Context
    Assert-True (
        $Response.WwwAuthenticate -ceq 'HMAC realm="webhooks"'
    ) "$Context WWW-Authenticate challenge가 정확하다."
}

if (-not (Test-Path -LiteralPath $executablePath -PathType Leaf) -or
    -not (Test-Path -LiteralPath $assemblyPath -PathType Leaf)) {
    throw "Release 실행 파일이 없습니다. 먼저 다음 명령을 실행하세요: dotnet build '$projectRoot\WebhookReceiverApi.csproj' -c Release"
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
    Assert-True (
        (ConvertTo-Utf8Bytes -Text $webhookSecret).Length -ge 32
    ) '검증용 WebhookSecurity__Secret은 UTF-8 기준 32 byte 이상이다.'

    # 자식 process가 시작되는 순간에만 secret을 상속시키고, 현재 PowerShell 환경은 즉시 원래 값으로 되돌립니다.
    $previousSecret = [Environment]::GetEnvironmentVariable(
        'WebhookSecurity__Secret',
        [EnvironmentVariableTarget]::Process)
    try {
        $env:WebhookSecurity__Secret = $webhookSecret
        $serverProcess = Start-Process `
            -FilePath $executablePath `
            -ArgumentList @(
                '--urls', 'http://127.0.0.1:0',
                '--environment', 'Production') `
            -PassThru `
            -WindowStyle Hidden `
            -RedirectStandardOutput $stdoutPath `
            -RedirectStandardError $stderrPath
    }
    finally {
        if ($null -eq $previousSecret) {
            Remove-Item Env:WebhookSecurity__Secret -ErrorAction SilentlyContinue
        }
        else {
            $env:WebhookSecurity__Secret = $previousSecret
        }
    }

    $ready = $false
    foreach ($attempt in 1..100) {
        if ($serverProcess.HasExited) {
            throw "검증 서버가 준비되기 전에 종료되었습니다. ExitCode=$($serverProcess.ExitCode)"
        }

        try {
            if (Test-Path -LiteralPath $stdoutPath) {
                $serverOutput = Get-Content -Raw -LiteralPath $stdoutPath
                $addressMatch = [regex]::Match(
                    [string]$serverOutput,
                    'http://127\.0\.0\.1:\d+')
                if ($addressMatch.Success) {
                    $baseUrl = $addressMatch.Value
                    $probe = Invoke-Api -Method GET -Path '/'
                    $ready = $probe.StatusCode -ge 200 -and $probe.StatusCode -lt 600
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
    Assert-True $ready 'WebhookReceiverApi가 Production의 동적 loopback 포트에서 준비된다.'

    $suffix = $runId.Substring(0, 10)
    $currentTimestamp = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds().ToString(
        [Globalization.CultureInfo]::InvariantCulture)
    $validEventId = "evt-$suffix"
    $validBody = New-PaymentBody `
        -Type 'payment.succeeded' `
        -PaymentId "pay-$suffix" `
        -Amount 12900 `
        -Currency 'KRW'
    $validSignature = New-SignatureHeader `
        -Secret $webhookSecret `
        -Timestamp $currentTimestamp `
        -EventId $validEventId `
        -BodyBytes $validBody
    Assert-True (
        $validSignature -cmatch '^v1=[0-9a-f]{64}$'
    ) 'HMAC-SHA256 signature는 v1= 뒤에 소문자 16진수 64자가 온다.'

    $accepted = Invoke-SignedWebhook `
        -EventId $validEventId `
        -Timestamp $currentTimestamp `
        -BodyBytes $validBody `
        -SigningSecret $webhookSecret
    Assert-True ($accepted.StatusCode -eq 202) '유효한 웹훅은 202 Accepted다.'
    Assert-True (
        $accepted.ContentType.StartsWith(
            'application/json',
            [StringComparison]::OrdinalIgnoreCase)
    ) '202 응답 본문은 JSON이다.'
    Assert-True (
        $accepted.CacheControl -ceq 'no-store'
    ) '202 결제 receipt 응답은 cache 저장을 금지한다.'

    $acceptedValues = @{}
    $acceptedDocument = [System.Text.Json.JsonDocument]::Parse($accepted.Body)
    try {
        $acceptedRoot = $acceptedDocument.RootElement
        Assert-True ($acceptedRoot.GetProperty('eventId').GetString() -ceq $validEventId) '202 본문의 eventId가 요청 header와 같다.'
        Assert-True ($acceptedRoot.GetProperty('type').GetString() -ceq 'payment.succeeded') '202 본문의 type이 검증된 외부 계약 문자열이다.'
        Assert-True ($acceptedRoot.GetProperty('paymentId').GetString() -ceq "pay-$suffix") '202 본문의 paymentId가 입력과 같다.'
        Assert-True ($acceptedRoot.GetProperty('amount').GetDecimal() -eq [decimal]12900) '202 본문의 amount가 입력과 같다.'
        Assert-True ($acceptedRoot.GetProperty('currency').GetString() -ceq 'KRW') '202 본문의 currency가 입력과 같다.'
        Assert-True ($acceptedRoot.GetProperty('status').GetString() -ceq 'accepted') '202 본문의 status는 accepted다.'

        $signedAt = [DateTimeOffset]::MinValue
        $receivedAt = [DateTimeOffset]::MinValue
        Assert-True (
            [DateTimeOffset]::TryParse(
                $acceptedRoot.GetProperty('signedAtUtc').GetString(),
                [ref]$signedAt)
        ) '202 본문의 signedAtUtc는 DateTimeOffset으로 읽을 수 있다.'
        Assert-True ($signedAt.ToUnixTimeSeconds().ToString() -ceq $currentTimestamp) 'signedAtUtc는 서명 timestamp와 같다.'
        Assert-True (
            [DateTimeOffset]::TryParse(
                $acceptedRoot.GetProperty('receivedAtUtc').GetString(),
                [ref]$receivedAt)
        ) '202 본문의 receivedAtUtc는 DateTimeOffset으로 읽을 수 있다.'
        Assert-True ($receivedAt -ge $signedAt) 'receivedAtUtc는 signedAtUtc보다 이르지 않다.'

        foreach ($propertyName in @(
                'eventId',
                'type',
                'paymentId',
                'amount',
                'currency',
                'signedAtUtc',
                'receivedAtUtc',
                'status')) {
            $acceptedValues[$propertyName] =
                $acceptedRoot.GetProperty($propertyName).ToString()
        }
    }
    finally {
        $acceptedDocument.Dispose()
    }

    $escapedEventId = [Uri]::EscapeDataString($validEventId)
    $expectedLocation = "/webhooks/receipts/$escapedEventId"
    Assert-True ($accepted.Location -ceq $expectedLocation) '202 Location은 URI escape한 eventId의 receipt 경로다.'

    $receipt = Invoke-Api -Method GET -Path $expectedLocation
    Assert-True ($receipt.StatusCode -eq 200) '접수된 eventId는 GET에서 200으로 조회된다.'
    Assert-True (
        $receipt.ContentType.StartsWith(
            'application/json',
            [StringComparison]::OrdinalIgnoreCase)
    ) 'receipt GET 성공 본문은 JSON이다.'
    Assert-True (
        $receipt.CacheControl -ceq 'no-store'
    ) 'receipt GET 응답은 cache 저장을 금지한다.'
    $receiptDocument = [System.Text.Json.JsonDocument]::Parse($receipt.Body)
    try {
        $receiptRoot = $receiptDocument.RootElement
        foreach ($propertyName in @(
                'eventId',
                'type',
                'paymentId',
                'amount',
                'currency',
                'signedAtUtc',
                'receivedAtUtc',
                'status')) {
            Assert-True (
                $receiptRoot.GetProperty($propertyName).ToString() -ceq
                $acceptedValues[$propertyName]
            ) "GET의 $propertyName 값이 202 응답과 같다."
        }
    }
    finally {
        $receiptDocument.Dispose()
    }

    $replay = Invoke-SignedWebhook `
        -EventId $validEventId `
        -Timestamp $currentTimestamp `
        -BodyBytes $validBody `
        -SigningSecret $webhookSecret
    Assert-Problem `
        -Response $replay `
        -StatusCode 409 `
        -Code 'webhook.replayed' `
        -ExpectedField 'X-Webhook-Id' `
        -Instance '/webhooks/payments' `
        -Context '동일 eventId 재전송'

    $tamperEventId = "evt-tamper-$suffix"
    $originalTamperBody = New-PaymentBody `
        -Type 'payment.succeeded' `
        -PaymentId "pay-original-$suffix" `
        -Amount 2500 `
        -Currency 'KRW'
    $tamperedBody = New-PaymentBody `
        -Type 'payment.succeeded' `
        -PaymentId "pay-tampered-$suffix" `
        -Amount 2500 `
        -Currency 'KRW'
    $originalSignature = New-SignatureHeader `
        -Secret $webhookSecret `
        -Timestamp $currentTimestamp `
        -EventId $tamperEventId `
        -BodyBytes $originalTamperBody
    $tampered = Invoke-SignedWebhook `
        -EventId $tamperEventId `
        -Timestamp $currentTimestamp `
        -BodyBytes $tamperedBody `
        -SigningSecret $webhookSecret `
        -SignatureOverride $originalSignature
    Assert-Unauthorized -Response $tampered -Context '서명 뒤 raw body 변조'

    $wrongSecretEventId = "evt-wrong-secret-$suffix"
    $wrongSecret = Invoke-SignedWebhook `
        -EventId $wrongSecretEventId `
        -Timestamp $currentTimestamp `
        -BodyBytes $validBody `
        -SigningSecret 'this-is-another-secret-with-at-least-32-bytes'
    Assert-Unauthorized -Response $wrongSecret -Context '다른 secret으로 만든 signature'

    $malformedSignature = Invoke-SignedWebhook `
        -EventId "evt-signature-format-$suffix" `
        -Timestamp $currentTimestamp `
        -BodyBytes $validBody `
        -SigningSecret $webhookSecret `
        -SignatureOverride 'v1=not-lowercase-sha256-hex'
    Assert-Unauthorized -Response $malformedSignature -Context '형식이 잘못된 signature'

    $uppercaseDigest = 'v1=' + $validSignature.Substring(3).ToUpperInvariant()
    $uppercaseSignature = Invoke-SignedWebhook `
        -EventId "evt-uppercase-signature-$suffix" `
        -Timestamp $currentTimestamp `
        -BodyBytes $validBody `
        -SigningSecret $webhookSecret `
        -SignatureOverride $uppercaseDigest
    Assert-Unauthorized -Response $uppercaseSignature -Context '대문자 hex를 사용한 signature'

    # signature가 eventId까지 묶는지 확인합니다. 원래 eventId의 유효 서명을 다른 eventId에 재사용하면 실패해야 합니다.
    $signedEventId = "evt-bound-original-$suffix"
    $substitutedEventId = "evt-bound-changed-$suffix"
    $eventBoundSignature = New-SignatureHeader `
        -Secret $webhookSecret `
        -Timestamp $currentTimestamp `
        -EventId $signedEventId `
        -BodyBytes $validBody
    $substitutedEvent = Invoke-SignedWebhook `
        -EventId $substitutedEventId `
        -Timestamp $currentTimestamp `
        -BodyBytes $validBody `
        -SigningSecret $webhookSecret `
        -SignatureOverride $eventBoundSignature
    Assert-Unauthorized -Response $substitutedEvent -Context '서명 뒤 eventId 치환'

    $changedTimestamp = ([long]$currentTimestamp + 1).ToString(
        [Globalization.CultureInfo]::InvariantCulture)
    $timestampBoundEventId = "evt-bound-timestamp-$suffix"
    $timestampBoundSignature = New-SignatureHeader `
        -Secret $webhookSecret `
        -Timestamp $currentTimestamp `
        -EventId $timestampBoundEventId `
        -BodyBytes $validBody
    $substitutedTimestamp = Invoke-SignedWebhook `
        -EventId $timestampBoundEventId `
        -Timestamp $changedTimestamp `
        -BodyBytes $validBody `
        -SigningSecret $webhookSecret `
        -SignatureOverride $timestampBoundSignature
    Assert-Unauthorized -Response $substitutedTimestamp -Context '서명 뒤 timestamp 치환'

    foreach ($missingHeader in @(
            'X-Webhook-Id',
            'X-Webhook-Timestamp',
            'X-Webhook-Signature')) {
        $missingHeaderEventId = "evt-missing-$($missingHeader.Length)-$suffix"
        $missingHeaderResponse = Invoke-SignedWebhook `
            -EventId $missingHeaderEventId `
            -Timestamp $currentTimestamp `
            -BodyBytes $validBody `
            -SigningSecret $webhookSecret `
            -OmitHeaders @($missingHeader)
        Assert-Problem `
            -Response $missingHeaderResponse `
            -StatusCode 400 `
            -Code 'webhook.header.required' `
            -ExpectedField $missingHeader `
            -Instance '/webhooks/payments' `
            -Context "$missingHeader 누락"
    }

    $duplicateCases = @(
        [pscustomobject]@{
            HeaderName = 'X-Webhook-Id'
            FirstValue = "evt-duplicate-id-$suffix"
            SecondValue = "evt-duplicate-id-other-$suffix"
        },
        [pscustomobject]@{
            HeaderName = 'X-Webhook-Timestamp'
            FirstValue = $currentTimestamp
            SecondValue = $currentTimestamp
        },
        [pscustomobject]@{
            HeaderName = 'X-Webhook-Signature'
            FirstValue = 'placeholder'
            SecondValue = 'placeholder'
        })
    foreach ($duplicateCase in $duplicateCases) {
        $duplicateEventId = if ($duplicateCase.HeaderName -ceq 'X-Webhook-Id') {
            $duplicateCase.FirstValue
        }
        else {
            "evt-duplicate-$($duplicateCase.HeaderName.Length)-$suffix"
        }
        $duplicateSignature = New-SignatureHeader `
            -Secret $webhookSecret `
            -Timestamp $currentTimestamp `
            -EventId $duplicateEventId `
            -BodyBytes $validBody
        if ($duplicateCase.HeaderName -ceq 'X-Webhook-Signature') {
            $duplicateCase.FirstValue = $duplicateSignature
            $duplicateCase.SecondValue = $duplicateSignature
        }

        $pairs = @(
            [pscustomobject]@{ Name = 'X-Webhook-Id'; Value = $duplicateEventId },
            [pscustomobject]@{ Name = 'X-Webhook-Timestamp'; Value = $currentTimestamp },
            [pscustomobject]@{ Name = 'X-Webhook-Signature'; Value = $duplicateSignature })
        $pairs = @($pairs | Where-Object { $_.Name -cne $duplicateCase.HeaderName })
        $pairs += [pscustomobject]@{
            Name = $duplicateCase.HeaderName
            Value = $duplicateCase.FirstValue
        }
        $pairs += [pscustomobject]@{
            Name = $duplicateCase.HeaderName
            Value = $duplicateCase.SecondValue
        }

        $duplicateResponse = Invoke-RawHttpRequest `
            -Path '/webhooks/payments' `
            -BodyBytes $validBody `
            -HeaderPairs $pairs
        Assert-Problem `
            -Response $duplicateResponse `
            -StatusCode 400 `
            -Code 'webhook.header.ambiguous' `
            -ExpectedField $duplicateCase.HeaderName `
            -Instance '/webhooks/payments' `
            -Context "$($duplicateCase.HeaderName) 복수 값"
    }

    $invalidTimestampEventId = "evt-invalid-time-$suffix"
    $invalidTimestamp = Invoke-SignedWebhook `
        -EventId $invalidTimestampEventId `
        -Timestamp 'not-a-unix-timestamp' `
        -BodyBytes $validBody `
        -SigningSecret $webhookSecret
    Assert-Problem `
        -Response $invalidTimestamp `
        -StatusCode 400 `
        -Code 'webhook.timestamp.invalid' `
        -ExpectedField 'X-Webhook-Timestamp' `
        -Instance '/webhooks/payments' `
        -Context '숫자가 아닌 timestamp'

    $now = [DateTimeOffset]::UtcNow
    $windowCases = @(
        [pscustomobject]@{
            Name = '오래된 timestamp'
            EventId = "evt-stale-$suffix"
            Timestamp = $now.AddHours(-1).ToUnixTimeSeconds().ToString()
        },
        [pscustomobject]@{
            Name = '너무 미래인 timestamp'
            EventId = "evt-future-$suffix"
            Timestamp = $now.AddHours(1).ToUnixTimeSeconds().ToString()
        })
    foreach ($windowCase in $windowCases) {
        $windowResponse = Invoke-SignedWebhook `
            -EventId $windowCase.EventId `
            -Timestamp $windowCase.Timestamp `
            -BodyBytes $validBody `
            -SigningSecret $webhookSecret
        Assert-Problem `
            -Response $windowResponse `
            -StatusCode 400 `
            -Code 'webhook.timestamp.out_of_window' `
            -ExpectedField 'X-Webhook-Timestamp' `
            -Instance '/webhooks/payments' `
            -Context $windowCase.Name
    }

    $malformedBody = ConvertTo-Utf8Bytes -Text '{'
    $malformed = Invoke-SignedWebhook `
        -EventId "evt-malformed-$suffix" `
        -Timestamp $currentTimestamp `
        -BodyBytes $malformedBody `
        -SigningSecret $webhookSecret
    Assert-Problem `
        -Response $malformed `
        -StatusCode 400 `
        -Code 'webhook.payload.invalid_json' `
        -ExpectedField 'request' `
        -Instance '/webhooks/payments' `
        -Context '깨진 JSON'

    $requiredFieldBodies = @(
        [pscustomobject]@{
            Name = 'type 누락'
            EventId = "evt-missing-type-$suffix"
            Bytes = ConvertTo-Utf8Bytes -Text '{"data":{"paymentId":"pay-1001","amount":12900,"currency":"KRW"}}'
        },
        [pscustomobject]@{
            Name = 'paymentId null'
            EventId = "evt-null-payment-$suffix"
            Bytes = ConvertTo-Utf8Bytes -Text '{"type":"payment.succeeded","data":{"paymentId":null,"amount":12900,"currency":"KRW"}}'
        })
    foreach ($requiredCase in $requiredFieldBodies) {
        $requiredResponse = Invoke-SignedWebhook `
            -EventId $requiredCase.EventId `
            -Timestamp $currentTimestamp `
            -BodyBytes $requiredCase.Bytes `
            -SigningSecret $webhookSecret
        Assert-Problem `
            -Response $requiredResponse `
            -StatusCode 400 `
            -Code 'webhook.payload.invalid_json' `
            -ExpectedField 'request' `
            -Instance '/webhooks/payments' `
            -Context $requiredCase.Name
    }

    $unsupportedTypeBody = New-PaymentBody `
        -Type 'payment.refunded' `
        -PaymentId "pay-unsupported-$suffix" `
        -Amount 12900 `
        -Currency 'KRW'
    $unsupportedType = Invoke-SignedWebhook `
        -EventId "evt-unsupported-$suffix" `
        -Timestamp $currentTimestamp `
        -BodyBytes $unsupportedTypeBody `
        -SigningSecret $webhookSecret
    Assert-Problem `
        -Response $unsupportedType `
        -StatusCode 400 `
        -Code 'webhook.type.unsupported' `
        -ExpectedField 'type' `
        -Instance '/webhooks/payments' `
        -Context '지원하지 않는 webhook type'

    $invalidAmountBody = New-PaymentBody `
        -Type 'payment.succeeded' `
        -PaymentId "pay-amount-$suffix" `
        -Amount 0 `
        -Currency 'KRW'
    $invalidAmount = Invoke-SignedWebhook `
        -EventId "evt-amount-$suffix" `
        -Timestamp $currentTimestamp `
        -BodyBytes $invalidAmountBody `
        -SigningSecret $webhookSecret
    Assert-Problem `
        -Response $invalidAmount `
        -StatusCode 400 `
        -Code 'payment.amount.invalid' `
        -ExpectedField 'data.amount' `
        -Instance '/webhooks/payments' `
        -Context '0원 결제 금액'

    $invalidCurrencyBody = New-PaymentBody `
        -Type 'payment.succeeded' `
        -PaymentId "pay-currency-$suffix" `
        -Amount 12900 `
        -Currency 'krw'
    $invalidCurrency = Invoke-SignedWebhook `
        -EventId "evt-currency-$suffix" `
        -Timestamp $currentTimestamp `
        -BodyBytes $invalidCurrencyBody `
        -SigningSecret $webhookSecret
    Assert-Problem `
        -Response $invalidCurrency `
        -StatusCode 400 `
        -Code 'payment.currency.invalid' `
        -ExpectedField 'data.currency' `
        -Instance '/webhooks/payments' `
        -Context '소문자 통화 코드'

    $wrongMediaType = Invoke-SignedWebhook `
        -EventId "evt-media-$suffix" `
        -Timestamp $currentTimestamp `
        -BodyBytes $validBody `
        -SigningSecret $webhookSecret `
        -ContentType 'text/plain'
    Assert-Problem `
        -Response $wrongMediaType `
        -StatusCode 415 `
        -Code 'http.unsupported_media_type' `
        -ExpectedField 'Content-Type' `
        -Instance '/webhooks/payments' `
        -Context '지원하지 않는 text/plain media type'

    $boundaryBaseBody = New-PaymentBody `
        -Type 'payment.succeeded' `
        -PaymentId "pay-boundary-$suffix" `
        -Amount 12900 `
        -Currency 'KRW'
    $boundaryPaddingLength = 16384 - $boundaryBaseBody.Length
    Assert-True ($boundaryPaddingLength -gt 0) '경계 JSON 뒤에 붙일 공백 길이는 양수다.'
    $boundaryBody = ConvertTo-Utf8Bytes -Text (
        $utf8.GetString($boundaryBaseBody) + (' ' * $boundaryPaddingLength))
    Assert-True ($boundaryBody.Length -eq 16384) '경계 검증 본문은 정확히 16,384 byte다.'
    $boundaryAccepted = Invoke-SignedWebhook `
        -EventId "evt-boundary-body-$suffix" `
        -Timestamp $currentTimestamp `
        -BodyBytes $boundaryBody `
        -SigningSecret $webhookSecret
    Assert-True (
        $boundaryAccepted.StatusCode -eq 202
    ) '정확히 16,384 byte인 유효 JSON body는 202로 접수된다.'

    $oversizedPayload = [ordered]@{
        type = 'payment.succeeded'
        data = [ordered]@{
            paymentId = "pay-large-$suffix"
            amount = 12900
            currency = 'KRW'
        }
        padding = 'x' * 17000
    } | ConvertTo-Json -Compress -Depth 4
    $oversizedBody = ConvertTo-Utf8Bytes -Text $oversizedPayload
    Assert-True ($oversizedBody.Length -gt 16384) '대용량 검증 본문은 16,384 byte 제한을 실제로 넘는다.'
    $oversized = Invoke-SignedWebhook `
        -EventId "evt-large-$suffix" `
        -Timestamp $currentTimestamp `
        -BodyBytes $oversizedBody `
        -SigningSecret $webhookSecret
    Assert-Problem `
        -Response $oversized `
        -StatusCode 413 `
        -Code 'webhook.body.too_large' `
        -ExpectedField 'request' `
        -Instance '/webhooks/payments' `
        -Context '16,384 byte를 넘는 body'

    $oversizedChunked = Invoke-SignedWebhook `
        -EventId "evt-large-chunked-$suffix" `
        -Timestamp $currentTimestamp `
        -BodyBytes $oversizedBody `
        -SigningSecret $webhookSecret `
        -UseChunked
    Assert-Problem `
        -Response $oversizedChunked `
        -StatusCode 413 `
        -Code 'webhook.body.too_large' `
        -ExpectedField 'request' `
        -Instance '/webhooks/payments' `
        -Context 'Content-Length 없는 chunked 대용량 body'

    $missingReceiptPath = "/webhooks/receipts/evt-missing-$suffix"
    $missingReceipt = Invoke-Api -Method GET -Path $missingReceiptPath
    Assert-Problem `
        -Response $missingReceipt `
        -StatusCode 404 `
        -Code 'webhook.not_found' `
        -ExpectedField 'eventId' `
        -Instance $missingReceiptPath `
        -Context '존재하지 않는 receipt'

    $unknownPath = "/not-a-real-route-$suffix"
    $unknownRoute = Invoke-Api -Method GET -Path $unknownPath
    Assert-Problem `
        -Response $unknownRoute `
        -StatusCode 404 `
        -Code 'http.not_found' `
        -ExpectedField 'request' `
        -Instance $unknownPath `
        -Context '등록되지 않은 route'

    Write-Host '검증 계약: canonical = UTF8(timestamp + "." + eventId + ".") || rawBody'
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
    # 이 script가 시작한 정확한 PID만 종료합니다. 이름 기반 종료는 사용자의 다른 서버를 해칠 수 있습니다.
    if ($null -ne $serverProcess -and -not $serverProcess.HasExited) {
        Stop-Process -Id $serverProcess.Id -Force
        $serverProcess.WaitForExit()
    }

    $httpClient.Dispose()
    Remove-Item -LiteralPath $stdoutPath -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $stderrPath -Force -ErrorAction SilentlyContinue
}
