# 2026-07-18 weekly: 주간 닷넷 #36 실습 자료

기준 게시물: [주말 아침 - 주간 닷넷 #36](https://forum.dotnetdev.kr/t/topic/14744)

오늘 루프는 최신 `주말 아침 - 주간 닷넷 #36`의 주제 중 로컬에서 재현 가능한 내용을 실습으로 구성했습니다. .NET 11 Preview 기능 자체는 현재 로컬 SDK에서 바로 쓸 수 없는 항목이 있어, 같은 문제를 현재 .NET에서 이해할 수 있는 재현 코드로 풀었습니다.

## 목차

- [이번 주 핵심 주제](#이번-주-핵심-주제)
- [실행 방법](#실행-방법)
- [실습 구성](#실습-구성)
- [학습 순서](#학습-순서)
- [출처](#출처)

## 이번 주 핵심 주제

- Process API의 stdout/stderr 교착 상태 문제와 동시 드레인 패턴
- 실행 중인 .NET 프로세스에 C# REPL을 주입하는 디버깅 접근
- CPU 캐시 라인과 false sharing 때문에 느려지는 스레드별 카운터
- .NET 호스트 프로세스가 `Main()` 전에 하는 일과 hang 진단
- EF Core 감사 추적에서 대량 업데이트/삭제가 변경 추적기를 우회하는 문제
- EF Core N+1 쿼리 탐지와 쿼리 예산 테스트
- DI에 강하게 묶인 라이브러리 API가 테스트와 재사용을 막는 문제
- CQRS 데코레이터 파이프라인의 계층 순서 설계
- C# discriminated union 제안과 패턴 매칭 개선 방향
- cDAC 기반 CLR 내부 진단 접근

## 실행 방법

```powershell
cd D:\workspace\csharp_study\weekly-dotnetdev\20260718\weekly
.\run.ps1 all
```

개별 실습만 실행하려면 다음 명령을 사용합니다.

```powershell
.\run.ps1 process
.\run.ps1 false-sharing
.\run.ps1 query-budget
```

PowerShell 실행 정책 때문에 스크립트 실행이 막히면 프로젝트를 직접 실행합니다.

```powershell
dotnet run --project .\src\WeeklyDotNetDev36\WeeklyDotNetDev36.csproj -- all
```

## 실습 구성

- [실습 1: Process 출력 동시 수집](./labs/01-process-output.md)
  - stdout과 stderr를 동시에 읽어 자식 프로세스가 pipe 버퍼에서 막히지 않게 합니다.
- [실습 2: false sharing 재현](./labs/02-false-sharing.md)
  - 인접한 `long[]` 카운터와 128바이트 패딩 카운터의 실행 시간을 비교합니다.
- [실습 3: N+1 쿼리 예산 감시](./labs/03-query-budget.md)
  - EF Core 없이도 N+1 구조를 재현하고 쿼리 카운터로 예산 초과를 확인합니다.

## 학습 순서

- 먼저 `process` 실습을 실행해 I/O 교착 상태가 왜 병렬 드레인 문제인지 확인합니다.
- 다음으로 `false-sharing` 실습을 여러 번 실행해 결과 편차와 캐시 라인 영향을 관찰합니다.
- 마지막으로 `query-budget` 실습에서 쿼리 횟수를 테스트 가능한 지표로 바꾸는 방식을 확인합니다.
- 확장 과제로 감사 추적 우회, DI 결합, CQRS 데코레이터 순서 문제를 현재 프로젝트에 적용할 수 있는 코드 리뷰 체크리스트로 바꿔 봅니다.

## 출처

- 닷넷데브: [주말 아침 - 주간 닷넷 #36](https://forum.dotnetdev.kr/t/topic/14744)
- Andrew Lock: [Improvements to reading Process outputs](https://andrewlock.net/exploring-the-dotnet-11-preview-5-improvments-to-process-apis/)
- Will Fuqua: [Injecting a C# REPL into a running .NET process](https://fuqua.io/blog/2026/06/injecting-a-csharp-repl-into-a-running-net-process/)
- Vasyl's Dev Notes: [AI Wrote a Thread-Safe Counter. The CPU Made It 5x Slower.](https://dev.to/mrviduus/ai-wrote-a-thread-safe-counter-the-cpu-made-it-5x-slower-45n6)
- dotNetTips: [The .NET Host Process](https://dotnettips.com/2026/07/05/the-net-host-process-what-runs-before-main-and-why-it-sometimes-hangs/)
- Chris Woodruff: [The N+1 Query Problem in EF Core](https://www.woodruff.dev/the-n1-query-problem-in-ef-core/)
