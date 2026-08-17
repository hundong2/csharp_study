// 실행: dotnet script 05_DataTransactionAndIndex.csx
// 목적: atomic Unit of Work, ESR index 선택, tenant별 model cache key를 작은 값으로 재현한다.

// 01. 기본 형식과 Console을 가져온다.
using System;
// 02. Dictionary/List로 database와 cache를 모형화한다.
using System.Collections.Generic;
// 03. LINQ로 snapshot copy와 metric을 계산한다.
using System.Linq;

// 04. 주문 DB의 최소 상태를 record class로 표현한다.
record class StoreState(int Stock, List<string> Orders, List<string> Audit);

// 05. UoW는 원본 대신 snapshot을 수정하고 commit 때 한 번에 교체한다.
sealed class InMemoryUnitOfWork
{
    // 06. 원본 참조는 commit 전까지 바뀌지 않는다.
    private readonly StoreState _original;
    // 07. Working은 transaction 안의 변경 상태다.
    public StoreState Working { get; private set; }

    // 08. 생성 시 collection도 복사해 shallow aliasing을 막는다.
    public InMemoryUnitOfWork(StoreState original)
    {
        _original = original;
        Working = new(original.Stock, new List<string>(original.Orders), new List<string>(original.Audit));
    }

    // 09. 주문·재고·감사 변경을 아직 commit되지 않은 snapshot에 적용한다.
    public void PlaceOrder(string orderId, int quantity)
    {
        // 10. business invariant를 transaction 안에서 검사한다.
        if (quantity <= 0 || Working.Stock < quantity) throw new InvalidOperationException("insufficient stock");
        // 11. record with로 새 top-level state를 만들고 같은 working list에 order를 추가한다.
        Working.Orders.Add(orderId);
        // 12. 감사 log도 같은 transaction boundary에 추가한다.
        Working.Audit.Add($"placed:{orderId}");
        // 13. Stock만 바꾼 새 record를 Working에 대입한다.
        Working = Working with { Stock = Working.Stock - quantity };
    }

    // 14. commit 결과를 caller가 durable state로 교체하도록 반환한다.
    public StoreState Commit() => Working;
    // 15. rollback은 원본을 그대로 돌려준다.
    public StoreState Rollback() => _original;
}

// 16. query plan의 핵심 지표만 record로 표현한다.
record Plan(string Stage, int Examined, int Returned, bool InMemorySort, bool Fetch);
// 17. index key를 순서가 있는 문자열로 표현한다.
record IndexDesign(string Equality, string SortAndRange);

// 18. waste ratio는 returned 0을 방어하며 scan 비효율을 보여 준다.
static double WasteRatio(Plan plan) => plan.Returned == 0 ? double.PositiveInfinity : (double)plan.Examined / plan.Returned;
// 19. ESR 설계 함수는 equality field 뒤 sort/range field를 둔다.
static IndexDesign RecommendEsr(string selectiveEquality, string otherEquality, string sortRange)
    => new($"{selectiveEquality},{otherEquality}", sortRange);
// 20. tenant schema를 model cache key에 포함한다.
static string ModelCacheKey(Type contextType, string schema, bool designTime)
    => $"{contextType.FullName}|{schema}|{designTime}";

// 21. 초기 database 상태는 재고 5, 빈 주문/감사 log다.
StoreState database = new(5, new(), new());
// 22. 첫 transaction을 시작한다.
InMemoryUnitOfWork successful = new(database);
// 23. 관련 변경을 working snapshot에 적용한다.
successful.PlaceOrder("order-1", 2);
// 24. commit을 한 번 호출해 모든 변경을 원자적으로 반영한다.
database = successful.Commit();
// 25. stock/order/audit가 함께 바뀌었는지 출력한다.
Console.WriteLine($"commit: stock={database.Stock}, orders={database.Orders.Count}, audit={database.Audit.Count}");

// 26. 두 번째 transaction을 시작한다.
InMemoryUnitOfWork failing = new(database);
// 27. 실패를 잡아 rollback path를 실행한다.
try
{
    // 28. 남은 재고보다 큰 주문은 예외를 낸다.
    failing.PlaceOrder("order-2", 99);
    // 29. 성공했다면 commit하지만 이 입력에서는 도달하지 않는다.
    database = failing.Commit();
}
catch (InvalidOperationException)
{
    // 30. 원본 상태를 복원해 partial write가 없게 한다.
    database = failing.Rollback();
}
// 31. rollback 뒤 값이 첫 commit 상태와 같은지 확인한다.
Console.WriteLine($"rollback: stock={database.Stock}, orders={database.Orders.Count}");

// 32. index 전 full collection scan 모형은 333333개를 보고 18개를 반환한다.
Plan before = new("COLLSCAN", 333_333, 18, true, true);
// 33. ESR compound index 뒤 plan 모형은 18 key로 18개를 반환한다.
Plan after = new("IXSCAN", 18, 18, false, false);
// 34. 두 waste ratio를 출력한다.
Console.WriteLine($"plan waste before/after = {WasteRatio(before):F1}/{WasteRatio(after):F1}");
// 35. customerId 선택도가 status보다 높다고 알고 있는 ESR index를 추천한다.
IndexDesign index = RecommendEsr("customerId", "status", "createdAt DESC");
// 36. field order를 사람이 확인한다.
Console.WriteLine($"ESR index = [{index.Equality}] -> [{index.SortAndRange}]");

// 37. 같은 context type이라도 schema가 다르면 cache key도 달라야 한다.
string tenantA = ModelCacheKey(typeof(InMemoryUnitOfWork), "tenant_a", false);
// 38. 두 번째 tenant key를 만든다.
string tenantB = ModelCacheKey(typeof(InMemoryUnitOfWork), "tenant_b", false);
// 39. false가 나오면 model cache가 tenant별로 격리된다.
Console.WriteLine($"tenant model keys equal = {tenantA == tenantB}");

// CLR/JIT 관찰 메모
// - 이 snapshot UoW는 교육용이며 실제 DB atomicity/WAL/locking을 구현하지 않는다.
// - record with는 top-level shallow copy이므로 List를 생성자에서 명시적으로 복사했다.
// - database index와 CLR Dictionary는 모두 lookup 구조지만 persistence/concurrency/query optimizer 의미가 다르다.
// - EF model cache key마다 model object graph가 heap에 남아 tenant 수가 많으면 memory가 증가한다.
