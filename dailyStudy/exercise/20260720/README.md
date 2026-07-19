# 2026-07-20 C# 기초와 작업 보드 실습

작은 작업 보드를 실행하며 기본 문법과 실무에서 자주 쓰는 책임 분리를 함께 익히는 입문 자료입니다. 읽기 15분, 실행 10분, 단계별 실습 30분을 권장합니다.

## 먼저 실행하기

```powershell
cd D:\workspace\csharp_study\dailyStudy\exercise\20260720
dotnet run --project .\src\WorkBoardExercise
dotnet run --project .\src\WorkBoardExercise -- --self-test
```

마지막 줄이 `초보자 검증 통과 (4/4)`이면 시작 상태가 정상입니다.

## 기본 문법 지도

| 개념 | 코드에서 찾을 표현 | 의미 |
| --- | --- | --- |
| 변수와 타입 | `string`, `int`, `bool`, `double` | 값의 종류와 이름을 정합니다. |
| null 안전성 | `string?`, `memo ?? "없음"` | 값이 없을 가능성을 코드에 표시하고 대체값을 줍니다. |
| 조건 | `if`, `switch` 식 | 입력을 검증하거나 값에 따라 결과를 고릅니다. |
| 여러 값 | 배열, `List<T>`, `foreach` | 같은 종류의 값을 모으고 반복합니다. |
| 모델 | `enum`, `record`, `with` | 업무 개념을 이름 붙이고 변경된 복사본을 만듭니다. |
| LINQ | `Sum`, `Count`, `SingleOrDefault` | 컬렉션을 검색·집계합니다. |
| 비동기 | `Task`, `async`, `await` | 파일·DB·네트워크처럼 기다림이 있는 작업의 표준 형태입니다. |

`Program.cs`를 `SyntaxTour` → `WorkItem` → `WorkBoardService` → `InMemoryWorkItemRepository` → `CompositionRoot` 순서로 읽어 보세요.

## 실무 아키텍처 흐름

```text
Program (입력/출력)
  └─ WorkBoardService (유스케이스와 검증)
       └─ IWorkItemRepository (저장 계약)
            └─ InMemoryWorkItemRepository (현재 구현)
```

- Application Service는 “작업 추가·완료” 같은 업무 순서를 조정합니다.
- Repository 패턴은 저장 방식과 업무 로직을 분리합니다. 나중에 메모리를 DB로 바꿔도 서비스 계약은 유지할 수 있습니다.
- Dependency Injection은 서비스가 구체 저장소보다 인터페이스를 받게 합니다.
- Composition Root는 실제 구현을 고르고 객체를 연결하는 한 장소입니다. ASP.NET Core에서는 보통 `Program.cs`의 서비스 등록부가 이 역할을 합니다.

작은 예제에서는 계층이 과해 보일 수 있습니다. 여기서는 실무 구조를 학습하려고 의도적으로 분리했으며, 단순 일회성 프로그램은 더 간단해도 됩니다.

## 실습과 검증

- [EXERCISES.md](./EXERCISES.md): 문법 추가부터 저장소 교체까지 5단계
- [CHECKPOINT.md](./CHECKPOINT.md): 코드를 외우지 않고 개념을 확인하는 초보자 검증
- [Program.cs](./src/WorkBoardExercise/Program.cs): 실행 예제와 의존성 없는 자체 검증

## 버전 업데이트 (2026-07-20 확인)

- 로컬 안정판은 `.NET SDK 10.0.301`, 런타임은 `10.0.9`이며 이 예제는 `net10.0`과 기본 C# 14로 실제 빌드합니다.
- [.NET 10 발표](https://devblogs.microsoft.com/dotnet/announcing-dotnet-10/)에 따르면 .NET 10은 2025-11-11 공개된 LTS이며 2028-11-10까지 지원됩니다.
- [C# 14 새 기능](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14)은 C# 14가 최신 안정 릴리스이고 .NET 10에서 지원된다고 안내합니다. 확장 멤버, null 조건부 대입, `field` 기반 속성 등이 포함됩니다.
- [.NET 10 새 기능](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview)에는 런타임 최적화, 라이브러리와 SDK 개선이 정리되어 있습니다.
- 현재 설치 환경보다 새로운 프리뷰 언어/SDK는 이 실행 예제에 섞지 않았습니다. 프리뷰를 시험할 때는 별도 프로젝트와 프리뷰 SDK를 사용하고 제품 코드의 언어 버전을 무심코 올리지 마세요.

## 짧은 복습 체크리스트

- [ ] 일반 실행과 `--self-test`가 모두 성공한다.
- [ ] nullable, 조건, 컬렉션, record, LINQ 예제를 코드에서 찾는다.
- [ ] Service, Repository, DI, Composition Root의 책임을 한 문장씩 설명한다.
- [ ] [EXERCISES.md](./EXERCISES.md)의 1~3단계를 직접 수정하고 되돌려 본다.
- [ ] [CHECKPOINT.md](./CHECKPOINT.md)를 코드 없이 점검한다.
