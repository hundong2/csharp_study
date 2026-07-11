# Practical .NET Beginner Refresh

이 폴더는 C#/.NET을 한동안 쓰지 않았거나, 실무 코드를 다시 읽어야 하는 사람을 위한 복습 세트입니다.

목표는 최신 런타임 내부 최적화를 외우는 것이 아니라, 실무 코드에서 반복해서 만나는 문법과 표준 라이브러리 사용법을 다시 몸에 익히는 것입니다.

## 실행 방법

각 주제는 설명 문서(`.md`)와 실행 가능한 예제(`.csx`)로 구성되어 있습니다.

```powershell
cd dailyStudy/tip/practical-dotnet
dotnet script 01_language_basics.csx
```

## 학습 순서

| 순서 | 문서 | 실행 파일 | 주제 |
|------|------|-----------|------|
| 01 | [01_language_basics.md](./01_language_basics.md) | [01_language_basics.csx](./01_language_basics.csx) | 변수, 조건문, 반복문, 메서드 |
| 02 | [02_collections_linq.md](./02_collections_linq.md) | [02_collections_linq.csx](./02_collections_linq.csx) | List, Dictionary, LINQ |
| 03 | [03_nullability_records_patterns.md](./03_nullability_records_patterns.md) | [03_nullability_records_patterns.csx](./03_nullability_records_patterns.csx) | nullable, record, pattern matching |
| 04 | [04_result_exception_validation.md](./04_result_exception_validation.md) | [04_result_exception_validation.csx](./04_result_exception_validation.csx) | Result 패턴, 예외, 검증 |
| 05 | [05_async_cancellation_retry.md](./05_async_cancellation_retry.md) | [05_async_cancellation_retry.csx](./05_async_cancellation_retry.csx) | async/await, CancellationToken, retry |
| 06 | [06_httpclient_json_api.md](./06_httpclient_json_api.md) | [06_httpclient_json_api.csx](./06_httpclient_json_api.csx) | HttpClient, API 호출, JSON |
| 07 | [07_system_text_json.md](./07_system_text_json.md) | [07_system_text_json.csx](./07_system_text_json.csx) | System.Text.Json 옵션과 컨버터 |
| 08 | [08_files_streams_paths.md](./08_files_streams_paths.md) | [08_files_streams_paths.csx](./08_files_streams_paths.csx) | 파일, 경로, 스트림 |
| 09 | [09_dependency_injection_concepts.md](./09_dependency_injection_concepts.md) | [09_dependency_injection_concepts.csx](./09_dependency_injection_concepts.csx) | DI 개념, 인터페이스 의존 |
| 10 | [10_options_configuration.md](./10_options_configuration.md) | [10_options_configuration.csx](./10_options_configuration.csx) | Options 패턴, 설정 검증 |
| 11 | [11_concurrency_primitives.md](./11_concurrency_primitives.md) | [11_concurrency_primitives.csx](./11_concurrency_primitives.csx) | lock, Interlocked, SemaphoreSlim |
| 12 | [12_memory_span_arraypool.md](./12_memory_span_arraypool.md) | [12_memory_span_arraypool.csx](./12_memory_span_arraypool.csx) | Span, Memory, ArrayPool |
| 13 | [13_datetime_regex_parsing.md](./13_datetime_regex_parsing.md) | [13_datetime_regex_parsing.csx](./13_datetime_regex_parsing.csx) | DateTimeOffset, Regex, TryParse |
| 14 | [14_external_libraries_roadmap.md](./14_external_libraries_roadmap.md) | - | 실무에서 자주 쓰는 외부 NuGet 라이브러리 |
| 15 | [15_architecture_records_interfaces.md](./15_architecture_records_interfaces.md) | [15_architecture_records_interfaces.csx](./15_architecture_records_interfaces.csx) | readonly record, interface 다중 상속, 아키텍처 |

## 권장 기준

- 처음에는 `.md`를 먼저 읽습니다.
- 그 다음 `.csx` 파일을 열어 주석을 읽습니다.
- `dotnet script`로 실행합니다.
- 출력이 이해되면 값을 조금 바꿔 다시 실행합니다.
- 아키텍처와 인터페이스 감각이 부족하다면 14번 외부 라이브러리 로드맵보다 15번을 먼저 읽어도 됩니다.

기초 문법이 익숙해지면 날짜별 `dailyStudy/YYYYMMDD` 예제를 보는 부담이 줄어듭니다.
