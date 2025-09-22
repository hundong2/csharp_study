# IEnumerable<T> 표준 쿼리 연산자 총정리
IEnumerable<T>에 대해 System.Linq가 제공하는 표준 쿼리 연산자들을 범주별로 정리했습니다. 각 연산자의 이름, 반환값, 간단 설명을 제공합니다.

## 예시 파일
[System.Linq.Enumerable 원본 코드 (모든 연산자 구현)](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Linq/src/System/Linq/Enumerable.cs)

## 답변
아래 표는 범주별로 대표 연산자를 1행씩 나열합니다(.NET 6+의 신규 연산자 표시). 대부분 지연 실행을 반환하며, 컬렉션 변환 계열만 즉시 물질화합니다.

- 생성(Generation)
| 종류 | 표준 쿼리 연산자 | 반환값 | 설명 |
|---|---|---|---|
| 생성 | Empty<T>() | IEnumerable<T> | 빈 시퀀스 생성 |
| 생성 | Range(start, count) | IEnumerable<int> | 정수 범위 시퀀스 생성 |
| 생성 | Repeat<T>(element, count) | IEnumerable<T> | 같은 요소를 반복 생성 |

- 형 변환/캐스팅/조회(Conversion/Casting)
| 종류 | 표준 쿼리 연산자 | 반환값 | 설명 |
|---|---|---|---|
| 변환 | AsEnumerable() | IEnumerable<T> | 원본을 그대로 IEnumerable로 노출 |
| 변환 | Cast<TResult>() | IEnumerable<TResult> | 각 요소를 지정 형식으로 캐스팅 |
| 변환 | OfType<TResult>() | IEnumerable<TResult> | 형식이 일치하는 요소만 필터링 |
| 변환 | ToArray() | T[] | 배열로 즉시 물질화 |
| 변환 | ToList() | List<T> | 리스트로 즉시 물질화 |
| 변환 | ToDictionary(keySel, [elemSel], [cmp]) | Dictionary<TKey,TElement> | 사전으로 변환(키 중복 시 예외) |
| 변환 | ToHashSet([cmp]) | HashSet<T> | 해시셋으로 변환(.NET Core/5+) |
| 변환 | ToLookup(keySel, [elemSel], [cmp]) | ILookup<TKey,TElement> | 다중값 맵(그룹 사전) 생성 |

- 필터링(Filtering)
| 종류 | 표준 쿼리 연산자 | 반환값 | 설명 |
|---|---|---|---|
| 필터 | Where(pred) | IEnumerable<T> | 조건에 맞는 요소만 통과(인덱스 오버로드 있음) |
| 필터 | DefaultIfEmpty([default]) | IEnumerable<T> | 비어 있으면 기본값 1개를 제공 |

- 프로젝션/평탄화(Projection/Flatten)
| 종류 | 표준 쿼리 연산자 | 반환값 | 설명 |
|---|---|---|---|
| 프로젝션 | Select(selector) | IEnumerable<TResult> | 요소를 다른 형태로 변환(인덱스 오버로드 있음) |
| 평탄화 | SelectMany(colSelector, [resultSel]) | IEnumerable<TResult> | 중첩 시퀀스를 평탄화하며 매핑 |

- 파티셔닝(Partitioning)
| 종류 | 표준 쿼리 연산자 | 반환값 | 설명 |
|---|---|---|---|
| 파티션 | Take(n) | IEnumerable<T> | 앞 n개 취함 |
| 파티션 | Skip(n) | IEnumerable<T> | 앞 n개 건너뜀 |
| 파티션 | TakeWhile(pred) | IEnumerable<T> | 조건 참인 동안 취함 |
| 파티션 | SkipWhile(pred) | IEnumerable<T> | 조건 참인 동안 건너뜀 |
| 파티션 | TakeLast(n) | IEnumerable<T> | 끝에서 n개 취함(.NET Core 3+) |
| 파티션 | SkipLast(n) | IEnumerable<T> | 끝에서 n개 건너뜀(.NET Core 3+) |
| 파티션 | Chunk(size) | IEnumerable<T[]> | 고정 크기 청크로 분할(.NET 6+) |

- 정렬(Sorting)
| 종류 | 표준 쿼리 연산자 | 반환값 | 설명 |
|---|---|---|---|
| 정렬 | OrderBy(keySel, [cmp]) | IOrderedEnumerable<T> | 키 기준 오름차순 정렬 |
| 정렬 | OrderByDescending(keySel, [cmp]) | IOrderedEnumerable<T> | 키 기준 내림차순 정렬 |
| 정렬 | ThenBy(keySel, [cmp]) | IOrderedEnumerable<T> | 보조 키 오름차순 |
| 정렬 | ThenByDescending(keySel, [cmp]) | IOrderedEnumerable<T> | 보조 키 내림차순 |
| 정렬 | Reverse() | IEnumerable<T> | 역순 나열(정렬 아님) |
| 극값 | MinBy(keySel, [cmp]) | TSource | 키 기준 최소 요소(.NET 6+) |
| 극값 | MaxBy(keySel, [cmp]) | TSource | 키 기준 최대 요소(.NET 6+) |

- 그룹화/조인(Grouping/Join)
| 종류 | 표준 쿼리 연산자 | 반환값 | 설명 |
|---|---|---|---|
| 그룹 | GroupBy(keySel, [elemSel], [resultSel], [cmp]) | IEnumerable<IGrouping<TKey,TElement>> 또는 IEnumerable<TResult> | 키 기준 그룹화 |
| 조인 | Join(inner, outerKey, innerKey, resultSel, [cmp]) | IEnumerable<TResult> | 내부 조인 |
| 그룹조인 | GroupJoin(inner, outerKey, innerKey, resultSel, [cmp]) | IEnumerable<TResult> | 그룹 단위 조인(Left-join 구성에 활용) |

- 집합(Set)
| 종류 | 표준 쿼리 연산자 | 반환값 | 설명 |
|---|---|---|---|
| 집합 | Distinct([cmp]) | IEnumerable<T> | 중복 제거 |
| 집합 | Union(second, [cmp]) | IEnumerable<T> | 합집합(중복 제거) |
| 집합 | Intersect(second, [cmp]) | IEnumerable<T> | 교집합 |
| 집합 | Except(second, [cmp]) | IEnumerable<T> | 차집합 |
| 집합 | Concat(second) | IEnumerable<T> | 단순 연결(중복 유지) |
| 집합 | Append(item) | IEnumerable<T> | 끝에 1개 추가(.NET Core 3+) |
| 집합 | Prepend(item) | IEnumerable<T> | 앞에 1개 추가(.NET Core 3+) |
| 집합 | DistinctBy(keySel, [cmp]) | IEnumerable<T> | 키 기준 중복 제거(.NET 6+) |
| 집합 | UnionBy(second, keySel, [cmp]) | IEnumerable<T> | 키 기준 합집합(.NET 6+) |
| 집합 | IntersectBy(second, keySel, [cmp]) | IEnumerable<T> | 키 기준 교집합(.NET 6+) |
| 집합 | ExceptBy(second, keySel, [cmp]) | IEnumerable<T> | 키 기준 차집합(.NET 6+) |

- 요소(Element)
| 종류 | 표준 쿼리 연산자 | 반환값 | 설명 |
|---|---|---|---|
| 요소 | First([pred]) | T | 첫 요소(없으면 예외) |
| 요소 | FirstOrDefault([pred], [default]) | T | 첫 요소 또는 기본값 |
| 요소 | Last([pred]) | T | 마지막 요소(없으면 예외) |
| 요소 | LastOrDefault([pred], [default]) | T | 마지막 요소 또는 기본값 |
| 요소 | Single([pred]) | T | 정확히 1개(아니면 예외) |
| 요소 | SingleOrDefault([pred], [default]) | T | 0 또는 1개 허용 |
| 요소 | ElementAt(index) | T | 인덱스의 요소(범위 밖 예외) |
| 요소 | ElementAtOrDefault(index) | T | 범위 밖이면 기본값 |
| 요소 | DefaultIfEmpty([default]) | IEnumerable<T> | 비었을 때 기본값 1개 제공 |

- 수량자(Quantifiers)
| 종류 | 표준 쿼리 연산자 | 반환값 | 설명 |
|---|---|---|---|
| 수량 | Any([pred]) | bool | 하나라도 존재하는가 |
| 수량 | All(pred) | bool | 모두 조건을 만족하는가 |
| 수량 | Contains(value, [cmp]) | bool | 값 포함 여부 |
| 수량 | SequenceEqual(second, [cmp]) | bool | 두 시퀀스가 동일한가 |

- 집계(Aggregation)
| 종류 | 표준 쿼리 연산자 | 반환값 | 설명 |
|---|---|---|---|
| 집계 | Count([pred]) | int | 요소 개수 |
| 집계 | LongCount([pred]) | long | 요소 개수(대용량) |
| 집계 | Sum(selector) | 수치 | 합계(오버로드 다수) |
| 집계 | Average(selector) | 수치 | 평균(오버로드 다수) |
| 집계 | Min([selector]) | T 또는 수치 | 최소값 |
| 집계 | Max([selector]) | T 또는 수치 | 최대값 |
| 집계 | Aggregate(seed, func, [resultSel]) | TAccumulate/ TResult | 누산(폴드/Reduce) |

- 결합/기타(Combination/Misc)
| 종류 | 표준 쿼리 연산자 | 반환값 | 설명 |
|---|---|---|---|
| 결합 | Zip(second, [resultSel]) | IEnumerable<TResult> | 2개 시퀀스 요소쌍 결합 |
| 기타 | TryGetNonEnumeratedCount(out count) | bool | 열거 없이 개수 추정(.NET 6+) |

작은 예제: 지연 vs 즉시, 그리고 키 기준 집합 연산
````csharp
using System;
using System.Linq;
using System.Collections.Generic;

var data = new List<int> { 1, 2, 3 };
var eager = data.Where(n => n > 1).ToList();     // 즉시 물질화
var deferred = data.Where(n => n > 1);           // 지연 실행

data.Add(100);
Console.WriteLine(string.Join(",", eager));      // 2,3
Console.WriteLine(string.Join(",", deferred));   // 2,3,100

// 키 기준 중복 제거(.NET 6+)
var people = new[] {
    new { Name = "Kim", Age = 20 },
    new { Name = "Lee", Age = 20 },
    new { Name = "Kim", Age = 30 },
};
var distinctByName = people.DistinctBy(p => p.Name);
Console.WriteLine(string.Join(", ", distinctByName.Select(p => $"{p.Name}:{p.Age}")));
// Kim:20, Lee:20
````

Tip
- 대부분 연산자는 지연 실행(IEnumerable<T> 반환). ToList/ToArray/ToDictionary/ToHashSet/ToLookup 등은 즉시 실행.
- OrderBy 계열은 IOrderedEnumerable<T>를 반환하며, ThenBy 계열과 함께 사용.
- Left Outer Join은 GroupJoin + SelectMany + DefaultIfEmpty를 조합.

### 추가 자료
- [표준 쿼리 연산자 개요(문서)](https://learn.microsoft.com/dotnet/csharp/programming-guide/concepts/linq/standard-query-operators-overview)
- [Enumerable API 전체 목록](https://learn.microsoft.com/dotnet/api/system.linq.enumerable)
- [LINQ 지연 실행과 즉시 실행](https://learn.microsoft.com/dotnet/csharp/linq/deferred-execution)
- [101 LINQ 샘플(공식)](https://github.com/dotnet/try-samples/tree/main/101-linq-samples)