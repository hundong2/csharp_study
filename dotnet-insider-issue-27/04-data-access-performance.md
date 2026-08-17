# 4. Unit of Work, SQL·DocumentDB 튜닝, 스키마별 멀티테넌시

원문:

- [How to Optimize SQL Queries: 20 Proven Best Practices](https://antondevtips.com/blog/how-to-optimize-sql-queries-20-proven-best-practices)
- [Unit of Work Pattern in .NET](https://www.nikolatech.net/blogs/unit-of-work-pattern-in-dotnet)
- [Query Performance Tuning in Azure DocumentDB](https://devblogs.microsoft.com/documentdb/query-performance-tuning-guide/)
- [Multi-Tenant .NET: Shared Database with Schema Separation](https://barretblake.dev/posts/development/2026/07/multi-tenant-part-2/)

## Unit of Work는 transaction boundary다

주문 생성, 재고 감소, 감사 log는 모두 성공하거나 모두 rollback되어야 합니다. UoW는 repository를 모아 둔 class 이름이 아니라 한 business operation의 connection/transaction/commit 경계입니다.

- EF Core `DbContext`는 Change Tracker로 변경을 모으고 `SaveChangesAsync()`에서 commit하므로 이미 UoW 역할을 합니다. `DbSet<T>`는 repository 성격을 가집니다.
- `IUnitOfWork.Products`, `Orders`로 다시 감싸는 것은 대개 기존 EF abstraction의 adapter이지 새 UoW 구현이 아닙니다.
- Dapper는 SQL을 즉시 실행하고 change tracking/UoW를 제공하지 않으므로 shared `DbConnection`과 `DbTransaction`, `Begin/Commit/Rollback`을 소유한 custom UoW가 유용합니다.
- 모든 repository가 같은 scoped UoW의 connection/transaction을 사용해야 합니다. repository가 개별 commit하면 atomicity가 깨집니다.

`await using`과 `try/catch/finally`로 rollback/dispose를 보장하고, transaction 안에서 사용자 입력이나 HTTP 호출을 기다리지 않습니다. 긴 transaction은 lock과 version을 오래 잡습니다. process crash 뒤 외부 message와 DB를 원자화하려면 단일 DB transaction만으로 부족하므로 outbox/idempotency 같은 pattern을 검토합니다.

## SQL 20개 실천 항목

기사 예시는 PostgreSQL 기준이며 다른 DB는 syntax와 planner가 다릅니다.

| 묶음 | 항목 | 핵심 |
|---|---|---|
| Sargable | 1 index, 2 WHERE column function 피하기, 3 leading wildcard 피하기, 4 type 일치 | raw column 범위와 실제 workload index |
| 적게 읽기 | 5 `SELECT *` 피하기, 6 selective filter, 7 keyset pagination, 8 watermark 증분 | I/O·network·memory 감소 |
| query shape | 9 불필요 JOIN 제거, 10 JOIN 종류 선택, 11 correlated subquery를 JOIN/CTE로, 12 EXISTS 검토 | 의미 보존 후 plan 비교 |
| read model | 13 의도적 denormalization, 14 materialized view | freshness·write 비용과 교환 |
| write | 15 batch, 16 짧은 transaction | round trip과 lock/WAL 사이 절충 |
| 측정 | 17 `EXPLAIN ANALYZE`, 18 statistics, 19 hint 최소화, 20 지속 monitoring | 추측보다 실제 plan/지표 |

중요한 함정:

- composite index는 column 순서가 중요하고 모든 index는 write/storage 비용이 있습니다.
- `EXTRACT(YEAR FROM order_date)=2025`보다 `order_date >= ... AND < ...`가 일반 index에 sargable합니다.
- `LIKE '%son'`은 B-tree prefix seek가 어려워 full-text/trigram이 맞을 수 있습니다.
- 깊은 `OFFSET`은 앞 행을 읽고 버리지만 keyset은 random page jump가 어렵습니다.
- modern planner가 predicate pushdown과 IN/EXISTS rewrite를 할 수 있으므로 “EXISTS가 항상 빠르다” 같은 규칙보다 plan을 봅니다. 특히 nullable subquery의 `NOT IN`은 semantic 함정이 있습니다.
- materialized view/summary는 빠른 대신 refresh 시점만큼 stale합니다.
- query hint는 오늘의 plan을 고정해 내일의 data distribution에 나쁠 수 있습니다.

## DocumentDB: explain과 ESR

느린 query를 diagnostic log/Log Analytics의 `VCoreMongoRequests`에서 찾고 `explain("executionStats")`로 검사합니다.

```javascript
db.orders.find({
  status: "shipped",
  customerId: "C-4821",
  createdAt: { $gte: ISODate("2024-01-01") }
}).sort({ createdAt: -1 }).explain("executionStats")
```

`COLLSCAN`, `IXSCAN`, `SORT`, `FETCH`, `totalDocsExamined`, `totalKeysExamined`, `nReturned`, `executionTimeMillis`를 봅니다. 원문 예시는 333,333 document를 scan해 18개를 돌려주던 query가 단일 `status` index 뒤에도 249K key를 읽었습니다.

**ESR = Equality → Sort → Range**입니다. `customerId`(선택도 높은 equality), `status`(equality), `createdAt:-1`(sort+range) compound index로 18 key/18 result와 index-backed sort를 얻었습니다. sort field/direction은 index와 같거나 완전 반대여야 전체 index order를 활용합니다.

projection에 필요한 field까지 index에 있으면 `FETCH` 없는 covered query가 될 수 있습니다. 그러나 원문의 81.3ms→0.053ms는 특정 cache/data/hardware의 예시이며 일반 보장이 아닙니다. 실제 write 비용과 storage도 측정합니다.

## schema-per-tenant와 EF model cache

한 DB에서 tenant마다 `guild_42.Adventurers`, `guild_43.Adventurers`처럼 별도 schema를 쓰면 shared table의 `TenantId`, global query filter, SaveChanges stamping이 isolation의 필수 요소가 아니게 됩니다. query bug로 다른 tenant row를 읽을 위험을 구조적으로 줄입니다.

하지만 `OnModelCreating`은 context type 기준 model cache 때문에 보통 한 번만 실행됩니다. 첫 tenant schema가 모든 context에 재사용되는 심각한 누출을 막으려면 `IModelCacheKeyFactory` key에 `(context type, schema, designTime)`을 포함해야 합니다.

- schema 이름은 parameter로 전달할 수 없는 identifier인 경우가 많으므로 strict allowlist/pattern과 master tenant registry로 검증합니다.
- 새 tenant provisioning과 schema별 migration이 복잡해집니다.
- schema마다 model이 cache되므로 tenant 수가 많으면 memory가 증가합니다.
- 한 DB/connection pool/backup을 공유하는 운영 이점과 noisy neighbor/isolation 수준을 비교합니다.
- EF Core model cache는 optimization cache이지 security boundary가 아니며 tenant resolution/authentication도 검증합니다.

## 실습

```powershell
dotnet script .\05_DataTransactionAndIndex.csx
```

in-memory UoW의 commit/rollback, query plan 지표, ESR index 추천, tenant model cache key를 한 번에 확인합니다.

## 다음 단계

- 이전: [NuGet 보안과 Target Framework](./03-supply-chain-targeting.md)
- 다음: [Foundry Local 음성 AI](./05-local-speech-ai.md)
- 공식 자료: [EF Core transactions](https://learn.microsoft.com/ef/core/saving/transactions), [PostgreSQL EXPLAIN](https://www.postgresql.org/docs/current/using-explain.html), [EF Core dynamic model](https://learn.microsoft.com/ef/core/modeling/dynamic-model)
