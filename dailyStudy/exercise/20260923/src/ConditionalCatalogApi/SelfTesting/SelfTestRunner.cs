using ConditionalCatalogApi.Application;
using ConditionalCatalogApi.Application.Ports;
using ConditionalCatalogApi.Domain;
using ConditionalCatalogApi.Infrastructure;
using ConditionalCatalogApi.Presentation;
using Microsoft.Extensions.Primitives;

namespace ConditionalCatalogApi.SelfTesting;

/// <summary>
/// 외부 테스트 package 없이 Domain, Application, Repository, ETag Strategy의 핵심 계약을 실행해 확인합니다.
/// </summary>
public static class SelfTestRunner
{
    private const int TotalChecks = 21;

    /// <summary>
    /// 독립 저장소를 만들고 정상·검증 실패·버전 충돌·취소·ETag 경계를 차례로 검사합니다.
    /// </summary>
    /// <returns>모든 검사가 통과하면 0, 하나라도 실패하면 1을 담아 완료되는 Task를 반환합니다.</returns>
    public static async Task<int> RunAsync()
    {
        var passed = 0;

        try
        {
            var invalidName = CatalogItem.Create(Guid.NewGuid(), " ", 1_000m);
            Expect(!invalidName.IsSuccess && invalidName.Error?.Code == "NAME_REQUIRED", "빈 이름을 거절한다.");
            passed++;

            var invalidScale = CatalogItem.Create(Guid.NewGuid(), "테스트 상품", 10.123m);
            Expect(!invalidScale.IsSuccess && invalidScale.Error?.Code == "PRICE_SCALE", "소수 셋째 자리 가격을 거절한다.");
            passed++;

            var seed = SeedData.Create()[0];
            ICatalogRepository repository = new InMemoryCatalogRepository([seed]);
            var application = new CatalogApplicationService(repository);
            IEntityTagCodec codec = new VersionEntityTagCodec();

            Expect(codec.Format(1) == "\"v1\"", "버전 1을 strong ETag로 만든다.");
            passed++;

            var strongMatch = codec.Evaluate(new StringValues("\"v1\""), 1, useStrongComparison: true);
            Expect(strongMatch == EntityTagEvaluation.Match, "같은 strong ETag는 If-Match 비교에 성공한다.");
            passed++;

            var weakWrite = codec.Evaluate(new StringValues("W/\"v1\""), 1, useStrongComparison: true);
            Expect(weakWrite == EntityTagEvaluation.NoMatch, "weak ETag는 쓰기용 strong 비교에 실패한다.");
            passed++;

            var weakRead = codec.Evaluate(new StringValues("W/\"v1\""), 1, useStrongComparison: false);
            Expect(weakRead == EntityTagEvaluation.Match, "weak ETag는 GET cache 재검증 비교에 성공한다.");
            passed++;

            var listMatch = codec.Evaluate(
                new StringValues("\"other\", W/\"v1\""),
                1,
                useStrongComparison: false);
            Expect(listMatch == EntityTagEvaluation.Match, "ETag 목록은 후보 중 하나가 맞으면 성공한다.");
            passed++;

            var wildcard = codec.Evaluate(new StringValues("*"), 1, useStrongComparison: true);
            Expect(wildcard == EntityTagEvaluation.Match, "존재하는 표현의 If-Match *는 성공한다.");
            passed++;

            var invalidHeader = codec.Evaluate(new StringValues("v1"), 1, useStrongComparison: true);
            Expect(invalidHeader == EntityTagEvaluation.Invalid, "따옴표 없는 ETag 문법을 거절한다.");
            passed++;

            var invalidWildcardList = codec.Evaluate(
                new StringValues("*, \"v1\""),
                1,
                useStrongComparison: true);
            Expect(
                invalidWildcardList == EntityTagEvaluation.Invalid,
                "와일드카드와 개별 ETag를 섞은 금지된 목록을 거절한다.");
            passed++;

            var found = await application.FindAsync(seed.Id, CancellationToken.None);
            Expect(found == seed, "Application Service로 시작 상품을 조회한다.");
            passed++;

            var updated = await application.UpdateAsync(
                seed.Id,
                version => version == 1,
                new UpdateCatalogCommand("저소음 기계식 키보드", 139_000m),
                CancellationToken.None);
            Expect(
                updated.Status == UpdateCatalogStatus.Updated
                && updated.Item?.Version == 2
                && updated.Item.Price == 139_000m,
                "최신 버전의 유효한 수정은 버전 2로 저장된다.");
            passed++;

            var stale = await application.UpdateAsync(
                seed.Id,
                version => version == 1,
                new UpdateCatalogCommand("오래된 화면의 수정", 120_000m),
                CancellationToken.None);
            Expect(
                stale.Status == UpdateCatalogStatus.VersionConflict
                && stale.Item?.Version == 2,
                "오래된 버전 수정은 최신 snapshot과 함께 충돌한다.");
            passed++;

            var validation = await application.UpdateAsync(
                seed.Id,
                version => version == 2,
                new UpdateCatalogCommand("저소음 기계식 키보드", -1m),
                CancellationToken.None);
            Expect(
                validation.Status == UpdateCatalogStatus.ValidationFailed
                && validation.Error?.Code == "PRICE_RANGE",
                "현재 버전이어도 잘못된 Domain 입력은 저장하지 않는다.");
            passed++;

            var afterValidation = await application.FindAsync(seed.Id, CancellationToken.None);
            Expect(afterValidation?.Version == 2, "검증 실패 뒤 저장 버전은 증가하지 않는다.");
            passed++;

            var missing = await application.UpdateAsync(
                Guid.NewGuid(),
                version => version == 1,
                new UpdateCatalogCommand("없는 상품", 1_000m),
                CancellationToken.None);
            Expect(missing.Status == UpdateCatalogStatus.NotFound, "없는 상품 수정은 NotFound를 반환한다.");
            passed++;

            var retryApplication = new CatalogApplicationService(
                new ConflictInjectingRepository(seed, conflictsToInject: 1));
            var retried = await retryApplication.UpdateAsync(
                seed.Id,
                version => version == 1 || version == 2,
                new UpdateCatalogCommand("두 버전 허용 수정", 141_000m),
                CancellationToken.None);
            Expect(
                retried.Status == UpdateCatalogStatus.Updated
                && retried.Item?.Version == 3,
                "목록 조건이 새 버전도 허용하면 CAS 충돌 뒤 다시 검사해 저장한다.");
            passed++;

            var staleAfterRaceApplication = new CatalogApplicationService(
                new ConflictInjectingRepository(seed, conflictsToInject: 1));
            var staleAfterRace = await staleAfterRaceApplication.UpdateAsync(
                seed.Id,
                version => version == 1,
                new UpdateCatalogCommand("한 버전만 허용", 142_000m),
                CancellationToken.None);
            Expect(
                staleAfterRace.Status == UpdateCatalogStatus.VersionConflict
                && staleAfterRace.Item?.Version == 2,
                "CAS 충돌 뒤 원래 조건이 최신 버전을 허용하지 않으면 충돌로 끝낸다.");
            passed++;

            var busyApplication = new CatalogApplicationService(
                new ConflictInjectingRepository(seed, conflictsToInject: int.MaxValue));
            var busy = await busyApplication.UpdateAsync(
                seed.Id,
                version => version > 0,
                new UpdateCatalogCommand("계속 허용되는 수정", 143_000m),
                CancellationToken.None);
            Expect(
                busy.Status == UpdateCatalogStatus.ContentionLimitExceeded
                && busy.Item?.Version == 9,
                "지속 충돌은 무한 반복하지 않고 최신 snapshot과 재시도 한도 초과를 반환한다.");
            passed++;

            var exhaustedConditionApplication = new CatalogApplicationService(
                new ConflictInjectingRepository(seed, conflictsToInject: int.MaxValue));
            var exhaustedCondition = await exhaustedConditionApplication.UpdateAsync(
                seed.Id,
                version => version <= 8,
                new UpdateCatalogCommand("마지막에 만료되는 조건", 144_000m),
                CancellationToken.None);
            Expect(
                exhaustedCondition.Status == UpdateCatalogStatus.VersionConflict
                && exhaustedCondition.Item?.Version == 9,
                "마지막 충돌의 최신 버전이 원래 조건 밖이면 503 대신 버전 충돌을 반환한다.");
            passed++;

            // using var는 메서드가 끝날 때 CancellationTokenSource를 자동 Dispose하는 문법입니다.
            // 테스트가 만든 OS 자원을 빼먹지 않고 정리하기 위해 사용합니다.
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await ExpectCancellationAsync(
                () => application.FindAsync(seed.Id, cancellation.Token),
                cancellation.Token,
                "취소 토큰이 Repository까지 전달된다.");
            passed++;

            Expect(passed == TotalChecks, "선언한 자체 검사 수와 실제 실행 수가 같아야 합니다.");
            Console.WriteLine($"자체 검증 통과: {passed}/{TotalChecks}");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"자체 검증 실패 ({passed}/{TotalChecks} 통과): {exception.Message}");
            return 1;
        }
    }

    /// <summary>
    /// 비동기 작업이 원래 토큰을 보존한 OperationCanceledException으로 끝나는지 확인합니다.
    /// </summary>
    /// <param name="action">호출하면 취소되어야 하는 비동기 작업을 만드는 함수입니다.</param>
    /// <param name="expectedToken">예외가 그대로 보존해야 하는 취소 토큰입니다.</param>
    /// <param name="message">취소되지 않았을 때 보여 줄 계약 설명입니다.</param>
    /// <returns>예상한 취소를 확인하면 정상 완료되는 Task를 반환합니다.</returns>
    private static async Task ExpectCancellationAsync(
        Func<Task> action,
        CancellationToken expectedToken,
        string message)
    {
        try
        {
            await action();
        }
        catch (OperationCanceledException exception)
        {
            Expect(exception.CancellationToken == expectedToken, $"{message} 원래 토큰이 보존되지 않았습니다.");
            return;
        }

        throw new InvalidOperationException(message);
    }

    /// <summary>
    /// 조건이 거짓이면 현재 자체 검증을 즉시 실패시킵니다.
    /// </summary>
    /// <param name="condition">반드시 참이어야 하는 검사 결과입니다.</param>
    /// <param name="message">거짓일 때 어떤 계약이 깨졌는지 설명할 메시지입니다.</param>
    /// <returns>값을 반환하지 않으며 조건이 참이면 다음 검사로 진행합니다.</returns>
    private static void Expect(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>
    /// Application의 재평가 로직을 결정적으로 검사하려고 지정한 횟수만큼 경쟁 writer를 끼워 넣는 테스트 Repository입니다.
    /// 운영 Adapter가 아니라, 우연한 timing이나 Thread.Sleep 없이 CAS 충돌을 재현하는 test double입니다.
    /// </summary>
    private sealed class ConflictInjectingRepository : ICatalogRepository
    {
        private CatalogItem _current;
        private int _remainingConflicts;

        /// <summary>
        /// 시작 snapshot과 강제로 만들 충돌 횟수를 보관합니다.
        /// </summary>
        /// <param name="seed">첫 조회에서 돌려줄 검증 완료 상품입니다.</param>
        /// <param name="conflictsToInject">TryReplaceAsync보다 먼저 경쟁 수정을 저장할 남은 횟수입니다.</param>
        /// <returns>생성자는 값을 반환하지 않고 독립적인 test double 상태를 초기화합니다.</returns>
        public ConflictInjectingRepository(CatalogItem seed, int conflictsToInject)
        {
            ArgumentNullException.ThrowIfNull(seed);
            ArgumentOutOfRangeException.ThrowIfNegative(conflictsToInject);
            _current = seed;
            _remainingConflicts = conflictsToInject;
        }

        /// <summary>
        /// 현재 test double snapshot을 조회합니다.
        /// </summary>
        /// <param name="id">seed와 같은지 확인할 상품 식별자입니다.</param>
        /// <param name="cancellationToken">조회 전에 중단을 확인할 토큰입니다.</param>
        /// <returns>ID가 같으면 현재 상품, 다르면 null을 담은 완료된 Task를 반환합니다.</returns>
        public Task<CatalogItem?> FindAsync(Guid id, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CatalogItem? found = id == _current.Id ? _current : null;
            return Task.FromResult(found);
        }

        /// <summary>
        /// 설정된 동안에는 경쟁 수정이 먼저 성공한 것처럼 최신 버전을 만들고, 그 뒤에는 정상 compare-and-swap을 수행합니다.
        /// </summary>
        /// <param name="replacement">Application이 현재 snapshot에서 계산한 다음 상품입니다.</param>
        /// <param name="expectedVersion">Application이 replacement를 만들 때 읽었던 버전입니다.</param>
        /// <param name="cancellationToken">교체 전에 중단을 확인할 토큰입니다.</param>
        /// <returns>강제 충돌 또는 실제 교체 상태와 최신 snapshot을 담은 Task를 반환합니다.</returns>
        public Task<ReplaceResult> TryReplaceAsync(
            CatalogItem replacement,
            long expectedVersion,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(replacement);
            cancellationToken.ThrowIfCancellationRequested();

            if (replacement.Id != _current.Id)
            {
                return Task.FromResult(new ReplaceResult(ReplaceStatus.NotFound, Current: null));
            }

            if (_remainingConflicts > 0)
            {
                _remainingConflicts--;
                var competingRevision = _current.Revise(_current.Name, _current.Price + 1m);
                if (!competingRevision.IsSuccess)
                {
                    throw new InvalidOperationException("경쟁 수정용 test data가 Domain 검증에 실패했습니다.");
                }

                _current = competingRevision.Value;
                return Task.FromResult(new ReplaceResult(ReplaceStatus.VersionConflict, _current));
            }

            if (_current.Version != expectedVersion)
            {
                return Task.FromResult(new ReplaceResult(ReplaceStatus.VersionConflict, _current));
            }

            _current = replacement;
            return Task.FromResult(new ReplaceResult(ReplaceStatus.Updated, _current));
        }
    }
}
