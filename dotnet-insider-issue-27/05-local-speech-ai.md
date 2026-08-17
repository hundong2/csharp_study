# 5. Foundry Local과 C# 실시간 Speech-to-Text

원문: [Beyond Chat: live Speech-to-Text with Foundry Local and C#](https://devblogs.microsoft.com/dotnet/foundry-local-live-speech-to-text-csharp/)

## 무엇을 만드는가

.NET 10 Windows console app이 NVIDIA `nemotron-speech-streaming-en-0.6b` 영어 ASR model을 local에서 실행합니다.

```text
Foundry Local 초기화
  → catalog에서 model alias 해석
  → hardware execution provider 다운로드/등록
  → model download/cache/load
  → 16kHz·16-bit·mono PCM microphone capture
  → bounded Channel<byte[]>
  → live session AppendAsync
  → async result stream의 interim/final text
  → Stop/Unload/Dispose
```

`Microsoft.AI.Foundry.Local.WinML`과 Windows audio API를 쓰는 `NAudio.WaveInEvent` 때문에 sample은 Windows-only입니다. 첫 실행은 model과 execution provider를 download하고 cache합니다. 이후 재사용할 수 있으며 API key와 inference network round trip이 없습니다.

## abstraction과 provider SDK

`Microsoft.Extensions.AI.IChatClient`처럼 공통 abstraction이 scenario를 표현하면 portability를 얻습니다. 하지만 live transcription session, raw PCM streaming, interim result는 Foundry Local 고유 capability이므로 native `AudioClient`를 사용합니다. “항상 abstraction” 또는 “항상 provider SDK”가 아니라 필요한 capability와 교체 가능성을 비교합니다.

## audio format

- sample rate 16,000Hz: 초당 16,000 sample
- bit depth 16-bit: sample 하나당 2 byte
- channel 1: mono
- 이론 raw rate: `16,000 × 2 × 1 = 32,000 bytes/second`

capture format과 model session 설정이 다르면 속도·pitch·인식이 깨질 수 있습니다. PCM byte order와 signed encoding도 SDK 계약에 맞춰야 합니다.

## 왜 bounded Channel인가

NAudio callback은 synchronous인데 `session.AppendAsync()`는 asynchronous입니다. callback마다 fire-and-forget Task를 무한히 만들면 consumer보다 producer가 빠를 때 memory와 latency가 끝없이 증가하고 exception도 잃습니다.

`Channel.CreateBounded<byte[]>(50)`은 최대 backlog를 정합니다. 기사 sample은 `DropOldest`로 가장 오래된 audio를 버려 실시간성을 우선합니다.

| full mode | 선택 기준 |
|---|---|
| Wait | 손실을 피하지만 capture thread/blocking adapter 설계 주의 |
| DropOldest | 최신 live audio 우선, 이전 말 일부 손실 |
| DropNewest/DropWrite | 이미 queue된 연속성을 우선 |

producer/consumer rate, dropped chunk 수, end-to-end latency를 관찰해야 합니다. bounded queue는 overload를 없애지 않고 **명시적으로 처리**하게 합니다.

## async stream과 lifecycle

`await foreach (var result in session.GetStream())`은 결과가 올 때마다 비동기로 열거합니다. append task와 result reader를 분리해 서로 막지 않습니다. interim text는 같은 발화가 수정될 수 있으므로 append-only 저장하면 중복됩니다. final result만 영속 저장하거나 utterance ID로 교체합니다.

```text
LoadAsync
 try StartAsync
   try capture + append + read
   finally StopAsync
 finally UnloadAsync
```

`Console.CancelKeyPress`에서 cancellation을 요청하고 channel writer를 complete한 뒤 append task/result reader를 기다려 finally cleanup을 통과합니다. microphone permission, device 없음, model download 실패, disk 부족, unsupported hardware도 처리합니다.

## privacy와 security

local inference는 microphone audio가 cloud로 가지 않는 이점이 있지만 자동으로 안전한 것은 아닙니다.

- model download source/hash/license와 cache directory 권한
- transcript·diagnostic log에 개인정보 저장 여부
- microphone 사용 표시·명시적 동의·보존 기간
- 다른 local process의 memory/file 접근
- model 제거 `RemoveFromCacheAsync()`와 offline deployment 정책

## CLR/JIT 내부

- callback에서 생성한 `byte[]`가 빠르게 쌓이면 Gen 0 allocation rate와 GC pause가 커집니다. `ArrayPool<byte>`는 ownership을 정확히 관리할 때만 사용합니다.
- bounded Channel 내부 synchronization은 producer/consumer 사이 happens-before를 제공합니다.
- `await foreach`는 `IAsyncEnumerator<T>.MoveNextAsync()`를 반복하는 state machine으로 compile됩니다.
- model inference는 native WinML/execution provider에서 수행될 수 있어 managed CPU profile만 보면 전체 비용을 놓칩니다.
- `Dispose`와 `UnloadAsync`는 GC finalizer에 맡기지 않고 deterministic cleanup합니다.

## 실습

```powershell
dotnet script .\06_BoundedAudioPipeline.csx
```

실제 microphone/model 대신 작은 bounded channel에서 audio chunk를 생산·소비하고 interim/final result를 async stream으로 만듭니다.

## 다음 단계

- 이전: [데이터 접근과 성능](./04-data-access-performance.md)
- 다음: [C# record·Fetch Metadata·.NET 11 성능](./06-csharp-security-performance.md)
- 공식 자료: [Foundry Local live transcription](https://learn.microsoft.com/azure/foundry-local/how-to/how-to-live-transcribe-audio), [System.Threading.Channels](https://learn.microsoft.com/dotnet/core/extensions/channels)
