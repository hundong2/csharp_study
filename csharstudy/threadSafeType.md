# C#의 Thread-Safe 타입과 List<T>의 안전성

어떤 타입이 “여러 스레드에서 동시에 접근/변경”되어도 추가 동기화 없이 올바르게 동작하는지를 설명. List<T>는 쓰기(추가/삭제/정렬 등)가 섞인 동시 접근에 대해 안전하지 않음.

## 예시 파일

[ConcurrentDictionary 샘플 (dotnet/samples)](https://github.com/dotnet/samples/blob/main/core/linq/csharp/ConcurrentDictionaryExample/Program.cs)

## 답변

핵심 요약  

- 불변(Immutable) 또는 “읽기 전용으로만 공유”하면 사실상 thread-safe.  
- List<T>는 “동시 읽기만” 안전(변경 없을 때). 읽기/쓰기 혼재 또는 동시 쓰기 시 안전하지 않음.  
- 동시 변경이 필요하면 Concurrent* 컬렉션이나 Immutable 컬렉션, 혹은 lock 사용.  

1. 실질적으로 Thread-Safe로 간주 가능한 범주  
- 완전 불변 타입: string, System.Uri, DateTime, DateTimeOffset, Guid, decimal, 대부분의 struct(필드 모두 값 형식이고 내부 변이 없음)  

- System.Collections.Immutable.* (ImmutableList<T>, ImmutableDictionary<TKey,TValue> 등)  
- 동시 컬렉션(경쟁 제어 내장):  
  - ConcurrentDictionary<TKey,TValue>  
  - ConcurrentQueue<T>, ConcurrentStack<T>, ConcurrentBag<T>  
  - BlockingCollection<T> (생산자/소비자 큐 래퍼)  
  - ConcurrentExclusiveSchedulerPair 등 TPL 스케줄러 도우미  
- 스레드 안전 설계 I/O / 동기화 타입: Channel<T>, SemaphoreSlim, ConcurrentRandom(직접 구현), ThreadSafeRandom(패턴)  
- Interlocked / Volatile API: 원시 값(참조, int, long 등)에 대한 원자적 연산 제공(타입이 아니라 도우미)  

2. 부분적으로 안전 또는 조건부  
- Dictionary<TKey,TValue>, Queue<T>, Stack<T>, List<T>: 동시 쓰기 또는 쓰기+읽기 혼합 시 위험.  
- StringBuilder: 문서상 thread-safe 아님(외부 lock 필요).  
- Lazy<T>: 생성 옵션(LazyThreadSafetyMode) 따라 안전성 결정.  
- Random(.NET 6 이전): 전역 공유 시 lock 필요(또는 ThreadLocal<Random>).  

3. List<T>가 안전하지 않은 이유  
- 내부적으로 T[] 배열과 size 인덱스를 관리. Add/Remove 시 재할당/size 증가 과정이 원자적이 아님.  
- Enumerator는 “변경 감지(version)” 기법 사용: 열거 중 변경되면 InvalidOperationException 발생(데이터 찢김 방지).  
- 동시 Add/Remove는 size 손상, 인덱스 범위 오류, 요소 손실 가능.  

4. List<T> 안전 사용 패턴  
- 읽기 전용 공유: 초기 구성 후 더 이상 변경하지 않음.  
- 복사 후 변경: var snapshot = list.ToArray(); 스냅샷을 다른 스레드에 전달.  
- 외부 lock: lock(_sync){ list.Add(item); } (열거도 lock으로 감쌈).  
- Immutable 컬렉션 전환: ImmutableList<T> 사용(변경은 새 인스턴스 반환 → 참조 스왑은 Interlocked.Exchange).  
- Channel<T>나 BlockingCollection<T>로 생산/소비 모델 구성.  

5. 대체 선택 가이드  
| 요구 | 권장 |
|------|------|
| Key/Value 동시 갱신 + 조회 많음 | ConcurrentDictionary |
| FIFO 큐 | ConcurrentQueue |
| LIFO 스택 | ConcurrentStack |
| 작업 풀/중복 허용 | ConcurrentBag |
| 다중 생산자/소비자 + 완료 시그널 | BlockingCollection 또는 Channel |
| 다수 읽기, 드문 쓰기 | ImmutableList + Interlocked 스왑 |

6. 짧은 데모: List<T> 경쟁 상황 vs ConcurrentDictionary  

````csharp
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

static class Demo
{
    public static async Task RaceList()
    {
        var list = new List<int>();
        var tasks = new List<Task>();
        for (int t = 0; t < 8; t++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (int i = 0; i < 10_000; i++)
                    list.Add(i); // 경쟁 → 예외/누락 가능(환경 따라 감지 안 될 수도)
            }));
        }
        await Task.WhenAll(tasks);
        Console.WriteLine($"List Count (예상=80000) => {list.Count}");
    }

    public static async Task SafeConcurrentDict()
    {
        var dict = new ConcurrentDictionary<int,int>();
        var tasks = new List<Task>();
        for (int t = 0; t < 8; t++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (int i = 0; i < 10_000; i++)
                    dict.AddOrUpdate(i, 1, (_, old) => old + 1);
            }));
        }
        await Task.WhenAll(tasks);
        Console.WriteLine($"ConcurrentDictionary DistinctKeys={dict.Count}");
    }
}
````

7. ImmutableList 교체 패턴(쓰기 드묾)  
````csharp
using System.Collections.Immutable;
using System.Threading;

ImmutableList<int> _data = ImmutableList<int>.Empty;

void AddItem(int x)
{
    ImmutableList<int> oldList, newList;
    do
    {
        oldList = _data;
        newList = oldList.Add(x);
    } while (Interlocked.CompareExchange(ref _data, newList, oldList) != oldList);
}

// 읽기: var snapshot = _data; (그 자체가 불변)
````

8. 확인 체크리스트  
- “동시에 변경” 필요? → Concurrent / Immutable / lock  
- “읽기만” 공유? → List<T> 가능  
- 빈번한 업데이트 + 읽기 매우 많음 + 지연 허용? → Copy-on-write(Immutable)  
- 생산자/소비자? → BlockingCollection 또는 Channel  

요약 문장  
- List<T>는 다중 스레드 동시 수정에 안전하지 않다.  
- 안전한 공유가 필요하면(1) lock, (2) concurrent 컬렉션, (3) immutable 스냅샷 전략을 선택.  

### 추가 자료
- [Thread-Safe 컬렉션(Concurrent Collections)](https://learn.microsoft.com/dotnet/standard/collections/thread-safe/)
- [Immutable Collections](https://learn.microsoft.com/dotnet/standard/collections/immutable)
- [Interlocked 클래스](https://learn.microsoft.com/dotnet/api/system.threading.interlocked)
- [BlockingCollection 사용 예](https://learn.microsoft.com/dotnet/standard/collections/thread-safe/blockingcollection-overview)