```markdown
# Parallel.ForEach 로컬 상태(localInit / body / localFinally) 동작
제공한 Parallel.ForEach 오버로드는 각 작업(스레드/파티션)마다 독립적인 로컬 변수를 생성·갱신 후 마지막에 합산(집계)하는 패턴이다.
## 예시 파일
[ParallelEx.cs](./ParallelEx.cs)
## 답변
해당 호출 시그니처 (제네릭 형식):
Parallel.ForEach<TSource, TLocal>(
    IEnumerable<TSource> source,
    Func<TLocal> localInit,
    Func<TSource, ParallelLoopState, TLocal, TLocal> body,
    Action<TLocal> localFinally)

각 델리게이트 의미  
1) localInit: 파티션(작업 단위)마다 한 번 실행, 로컬 누적 변수 초기값 반환 (여기선 0).  
2) body: 컬렉션의 각 요소마다 호출.  
   - 매개변수: (현재 요소, loopState, 현재 로컬값)  
   - 반환값: 다음 반복에 전달할 새로운 로컬값 (여기선 ++localCount 결과)  
3) localFinally: 해당 파티션이 모두 끝났을 때 한 번 호출; 최종 로컬 누적값을 전역 집계 변수(fileCount)에 합산.  
   - 여러 파티션이 병렬로 호출하므로 Interlocked.Add 로 원자적 합산.  

흐름 예 (개념 순서):  
- 스레드 A: localInit → localCount=0 → 요소 여러 개 처리하면서 body 반환값으로 로컬 증가 → 끝난 후 localFinally(c=처리 수) → Interlocked.Add(ref fileCount, c)  
- 스레드 B, C … 동일 패턴 → 모든 파티션 완료 후 fileCount = 전체 합계.  

왜 이렇게 쓰나?  
- 매 반복마다 전역 공유 카운터 Interlocked.Increment 호출하면 경쟁(cost) 증가.  
- 대신 “로컬에 누적 → 마지막 한 번만 전역 합산”으로 잠금/원자 연산 횟수 최소화(분산 누적).  

loopState 용도 (예: 조기 종료):  
- loopState.Break(): 현재 인덱스보다 작은 인덱스만 계속 처리  
- loopState.Stop(): 즉시 전체 중단 시도  
(질문 코드에서는 미사용)  

제공 코드 설명(불필요 캐스팅 제거 후):
````csharp
// filepath: /Users/donghun2/workspace/csharp_study/csharstudy/TPL/ParallelForEachLocalStateEx.cs
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

public static class ParallelForEachLocalStateEx
{
    public static int ProcessFiles(string[] files, Action<string> action)
    {
        int fileCount = 0;

        Parallel.ForEach(
            files,
            () => 0, // localInit: 파티션별 로컬 카운터 초기값
            (file, loopState, localCount) =>
            {
                action(file);      // 실제 작업
                return localCount + 1; // 로컬 누적
            },
            localCount =>
            {
                // 파티션 종료 시 전역 합산 (스레드 안전)
                Interlocked.Add(ref fileCount, localCount);
            }
        );

        return fileCount;
    }
}
````

중요 포인트  
- body 의 마지막 반환값이 다음 반복의 localCount 로 전달된다.  
- localFinally 는 파티션마다 한 번, 요소마다 아님.  
- 파티션 수는 내부 파티셔너/스레드풀 상태에 따라 환경마다 다를 수 있음(= localFinally 호출 횟수 ≠ 파일 개수).  
- 순서는 보장되지 않음. 액션이 순서 의존이면 다른 접근 필요(예: PLINQ .AsOrdered, 또는 사후 정렬).  

잠재 개선/확장  
- I/O 바운드라면 Parallel.ForEach 대신 비동기 처리 + Channel<T> 고려  
- 파일 수가 매우 많고 action 이 가볍다면 파티셔너(Partitioner.Create)로 균형 개선  
- 예외 발생 시 AggregateException 으로 수집되어 throw → try/catch 필요 시 외부 감싸기  

자주 하는 실수  
| 실수 | 설명 | 해결 |
|------|------|------|
| 전역 변수 직접 ++ | 경쟁 증가 | 로컬 상태 + 마지막 합산 |
| localFinally 에서 또 Loop | 불필요 | 거기선 집계만 |
| localInit 에서 비싼 객체 생성 | 파티션마다 반복 비용 | 풀/캐시 사용 |

요약  
- 해당 오버로드는 “파티션 로컬 누적 → 마지막 전역 합산”을 지원하는 고성능 패턴.  
- Interlocked.Add 로 전역 경쟁 최소화.  
- 반환되는 fileCount 는 처리된 전체 항목 수.  

### 추가 자료
- [Parallel.ForEach 문서](https://learn.microsoft.com/dotnet/api/system.threading.tasks.parallel.foreach)
- [Interlocked 클래스](https://learn.microsoft.com/dotnet/api/system.threading.interlocked)
```