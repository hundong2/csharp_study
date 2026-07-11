# 14. 실무에서 자주 쓰는 외부 NuGet 라이브러리 로드맵

이 문서는 실제 프로젝트에서 자주 만나는 외부 라이브러리를 정리합니다. 이 폴더의 실행 예제는 네트워크와 패키지 설치 없이 돌아가도록 표준 라이브러리 중심으로 구성했습니다. 아래 라이브러리는 프로젝트를 만들 때 단계적으로 익히면 됩니다.

## 웹/API

| 라이브러리 | 용도 | 왜 쓰는가 |
|------------|------|-----------|
| ASP.NET Core | 웹 API, MVC, Minimal API | .NET 백엔드 표준 프레임워크 |
| Swashbuckle / NSwag | Swagger/OpenAPI 문서 생성 | API 문서와 클라이언트 생성 |
| Refit | REST API 클라이언트 인터페이스화 | HTTP 호출 코드를 간결하게 유지 |

## 데이터

| 라이브러리 | 용도 | 왜 쓰는가 |
|------------|------|-----------|
| Entity Framework Core | ORM | LINQ로 DB 조회/저장 |
| Dapper | Micro ORM | SQL을 직접 쓰면서 빠른 매핑 |
| Npgsql | PostgreSQL 드라이버 | PostgreSQL 연결 |
| Microsoft.Data.SqlClient | SQL Server 드라이버 | SQL Server 연결 |
| StackExchange.Redis | Redis 클라이언트 | 캐시, 분산 락, pub/sub |

## 안정성/장애 대응

| 라이브러리 | 용도 | 왜 쓰는가 |
|------------|------|-----------|
| Polly | retry, timeout, circuit breaker | 외부 장애 전파 차단 |
| Microsoft.Extensions.Http | IHttpClientFactory | HttpClient 수명 관리 |
| Microsoft.Extensions.Resilience | 최신 .NET resilience pipeline | 표준화된 복원력 정책 |

## 로깅/관찰성

| 라이브러리 | 용도 | 왜 쓰는가 |
|------------|------|-----------|
| Serilog | 구조화 로깅 | JSON 로그와 sink 생태계 |
| NLog | 로깅 | 오래된 시스템에서 많이 사용 |
| OpenTelemetry | trace, metrics, logs | 분산 추적 표준 |
| Prometheus.NET | Prometheus metrics | 메트릭 노출 |

## 객체 매핑/검증

| 라이브러리 | 용도 | 왜 쓰는가 |
|------------|------|-----------|
| FluentValidation | 입력 검증 | 검증 규칙을 명확히 분리 |
| AutoMapper | 객체 매핑 | DTO와 Entity 변환 자동화 |
| Mapster | 객체 매핑 | 빠른 매핑과 source generation 지원 |

## 메시징/백그라운드 작업

| 라이브러리 | 용도 | 왜 쓰는가 |
|------------|------|-----------|
| MassTransit | 메시지 버스 | RabbitMQ, Azure Service Bus 추상화 |
| RabbitMQ.Client | RabbitMQ 직접 사용 | 메시지 큐 |
| Hangfire | 백그라운드 job | 대시보드와 retry 지원 |
| Quartz.NET | 스케줄러 | 복잡한 cron/job 스케줄 |

## 테스트

| 라이브러리 | 용도 | 왜 쓰는가 |
|------------|------|-----------|
| xUnit | 테스트 프레임워크 | .NET에서 널리 사용 |
| NUnit | 테스트 프레임워크 | 오래된 코드베이스에서 자주 사용 |
| FluentAssertions | 테스트 assertion | 읽기 쉬운 검증 문장 |
| Moq / NSubstitute | Mock | 의존성 대체 |
| Testcontainers | 통합 테스트용 Docker 컨테이너 | 실제 DB/Redis 기반 테스트 |

## 학습 순서 추천

1. `System.Text.Json`, `HttpClient`, `async/await`
2. ASP.NET Core Minimal API
3. Options, DI, Logging
4. EF Core 또는 Dapper
5. Polly 또는 Microsoft.Extensions.Resilience
6. Serilog, OpenTelemetry
7. xUnit, FluentAssertions, Testcontainers

외부 라이브러리는 문법보다 운영 맥락이 중요합니다. 먼저 표준 라이브러리 예제로 개념을 익힌 뒤, 실제 프로젝트에서 필요한 라이브러리를 하나씩 붙이는 방식이 가장 안정적입니다.
