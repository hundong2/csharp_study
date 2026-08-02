// 읽는 순서: 맨 위 실행 흐름 → 명령/결과 자료형 → Reservation 도메인 → Strategy → Repository → Application Service → 테스트 순서로 내려가세요.
if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
{
    await SelfTests.RunAsync();
    return;
}

// Composition Root는 프로그램의 조립 지점입니다. 업무 클래스가 구체 구현 생성법을 몰라야 구현 교체와 테스트가 쉬워집니다.
var now = DateTimeOffset.Parse("2026-08-03T10:00:00+09:00");
IReservationRepository repository = new InMemoryReservationRepository(
[
    Reservation.Create("RSV-100", "customer-1", now.AddMinutes(-40), TimeSpan.FromMinutes(30)).Value!,
    Reservation.Create("RSV-101", "customer-2", now.AddMinutes(-10), TimeSpan.FromMinutes(30)).Value!,
]);
IExpiryPolicy expiryPolicy = new DeadlineExpiryPolicy();
var service = new ExpireReservationsService(repository, expiryPolicy, new ConsoleOperationLog());

var result = await service.ExecuteAsync(new ExpireReservationsCommand(now, BatchSize: 100), CancellationToken.None);
Console.WriteLine(result.IsSuccess
    ? $"처리 완료: 만료 {result.Value!.ExpiredCount}건 / 유지 {result.Value.KeptCount}건"
    : $"입력 오류: {result.Error}");

// record는 값 중심 자료형입니다. init-only 성격 덕분에 전달 중 값이 바뀌지 않아 명령과 결과에 적합합니다.
public sealed record ExpireReservationsCommand(DateTimeOffset Now, int BatchSize);
public sealed record ExpirySummary(int ScannedCount, int ExpiredCount, int KeptCount);

// 예상 가능한 검증 실패는 Result로 표현하면 호출자가 성공과 실패를 빠뜨리지 않고 분기할 수 있습니다.
public sealed record Result<T>(T? Value, string? Error)
{
    public bool IsSuccess => Error is null;
    public static Result<T> Success(T value) => new(value, null);
    public static Result<T> Failure(string error) => new(default, error);
}

public enum ReservationStatus
{
    Pending,
    Confirmed,
    Expired
}

// Domain Model은 데이터와 상태 변경 규칙을 함께 둡니다. 아무 코드나 Status를 바꾸게 두지 않아 잘못된 상태 전이를 막습니다.
public sealed record Reservation
{
    public required string Id { get; init; }
    public required string CustomerId { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public ReservationStatus Status { get; private init; }

    public static Result<Reservation> Create(string id, string customerId, DateTimeOffset createdAt, TimeSpan holdDuration)
    {
        if (string.IsNullOrWhiteSpace(id))
            return Result<Reservation>.Failure("예약 ID는 필수입니다.");
        if (string.IsNullOrWhiteSpace(customerId))
            return Result<Reservation>.Failure("고객 ID는 필수입니다.");
        if (holdDuration <= TimeSpan.Zero)
            return Result<Reservation>.Failure("보관 시간은 0보다 커야 합니다.");

        return Result<Reservation>.Success(new Reservation
        {
            Id = id.Trim(),
            CustomerId = customerId.Trim(),
            CreatedAt = createdAt,
            ExpiresAt = createdAt.Add(holdDuration),
            Status = ReservationStatus.Pending
        });
    }

    public Result<Reservation> Expire(DateTimeOffset now)
    {
        if (Status != ReservationStatus.Pending)
            return Result<Reservation>.Failure("대기 중인 예약만 만료할 수 있습니다.");
        if (now < ExpiresAt)
            return Result<Reservation>.Failure("아직 만료 시각이 지나지 않았습니다.");

        // with 식은 원본을 바꾸지 않고 새 값을 만듭니다. 불변 객체는 공유되어도 중간 상태가 노출되지 않습니다.
        return Result<Reservation>.Success(this with { Status = ReservationStatus.Expired });
    }
}

// Strategy는 '언제 만료할지' 정책을 교체 가능한 계약으로 분리합니다. 정책 추가 때 서비스 수정이 줄어 OCP에 유리합니다.
public interface IExpiryPolicy
{
    bool ShouldExpire(Reservation reservation, DateTimeOffset now);
}

public sealed class DeadlineExpiryPolicy : IExpiryPolicy
{
    public bool ShouldExpire(Reservation reservation, DateTimeOffset now) =>
        reservation.Status == ReservationStatus.Pending && now >= reservation.ExpiresAt;
}

// Repository는 저장 기술을 업무 규칙에서 숨깁니다. 실제 환경에서는 EF Core 구현으로 바꾸고 이 계약은 유지할 수 있습니다.
public interface IReservationRepository
{
    Task<IReadOnlyCollection<Reservation>> GetPendingAsync(int limit, CancellationToken cancellationToken);
    Task SaveAsync(Reservation reservation, CancellationToken cancellationToken);
}

public sealed class InMemoryReservationRepository(IEnumerable<Reservation> seed) : IReservationRepository
{
    private readonly Dictionary<string, Reservation> _items = seed.ToDictionary(item => item.Id);

    public Task<IReadOnlyCollection<Reservation>> GetPendingAsync(int limit, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // LINQ는 필터→정렬→개수 제한이라는 의도를 반복문보다 읽기 쉽게 표현합니다.
        IReadOnlyCollection<Reservation> result = _items.Values
            .Where(item => item.Status == ReservationStatus.Pending)
            .OrderBy(item => item.ExpiresAt)
            .Take(limit)
            .ToArray();
        return Task.FromResult(result);
    }

    public Task SaveAsync(Reservation reservation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _items[reservation.Id] = reservation;
        return Task.CompletedTask;
    }
}

public interface IOperationLog
{
    void ReservationExpired(Reservation reservation);
}

public sealed class ConsoleOperationLog : IOperationLog
{
    public void ReservationExpired(Reservation reservation) =>
        Console.WriteLine($"운영 로그: reservation={reservation.Id}, expiredAt={reservation.ExpiresAt:O}");
}

// Application Service는 조회→정책 판단→도메인 변경→저장 순서를 조정하고, 각 세부 규칙은 협력 객체에 맡겨 SRP를 지킵니다.
public sealed class ExpireReservationsService(
    IReservationRepository repository,
    IExpiryPolicy expiryPolicy,
    IOperationLog operationLog)
{
    public async Task<Result<ExpirySummary>> ExecuteAsync(
        ExpireReservationsCommand command,
        CancellationToken cancellationToken)
    {
        if (command.BatchSize is < 1 or > 1_000)
            return Result<ExpirySummary>.Failure("BatchSize는 1~1000이어야 합니다.");

        try
        {
            // await는 DB 같은 I/O가 끝날 때까지 스레드를 붙잡지 않습니다. CancellationToken은 종료 요청을 아래 계층에 전달합니다.
            var pending = await repository.GetPendingAsync(command.BatchSize, cancellationToken);
            var expiredCount = 0;

            foreach (var reservation in pending)
            {
                if (!expiryPolicy.ShouldExpire(reservation, command.Now))
                    continue;

                var expiry = reservation.Expire(command.Now);
                if (!expiry.IsSuccess)
                    continue; // 정책과 도메인 규칙이 엇갈려도 잘못된 값을 저장하지 않는 방어선입니다.

                await repository.SaveAsync(expiry.Value!, cancellationToken);
                operationLog.ReservationExpired(expiry.Value!);
                expiredCount++;
            }

            return Result<ExpirySummary>.Success(
                new ExpirySummary(pending.Count, expiredCount, pending.Count - expiredCount));
        }
        catch (OperationCanceledException)
        {
            throw; // 취소는 정상적인 제어 신호이므로 일반 장애로 감싸지 않고 호출자에게 그대로 전달합니다.
        }
        catch (Exception exception)
        {
            // 저장소 장애처럼 예상 밖 실패는 예외로 전파해 재시도·알림 정책을 상위 계층에서 적용하게 합니다.
            throw new InvalidOperationException("예약 만료 처리 중 저장소 오류가 발생했습니다.", exception);
        }
    }
}

public static class SelfTests
{
    public static async Task RunAsync()
    {
        var passed = 0;
        passed += Check(!Reservation.Create("", "c", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1)).IsSuccess, "빈 ID 거부");
        passed += Check(!Reservation.Create("r", "c", DateTimeOffset.UtcNow, TimeSpan.Zero).IsSuccess, "잘못된 보관 시간 거부");

        var now = DateTimeOffset.Parse("2026-08-03T01:00:00Z");
        var old = Reservation.Create("old", "c", now.AddMinutes(-31), TimeSpan.FromMinutes(30)).Value!;
        var fresh = Reservation.Create("fresh", "c", now.AddMinutes(-10), TimeSpan.FromMinutes(30)).Value!;
        var repository = new InMemoryReservationRepository([old, fresh]);
        var service = new ExpireReservationsService(repository, new DeadlineExpiryPolicy(), new SilentOperationLog());
        var result = await service.ExecuteAsync(new(now, 10), CancellationToken.None);

        passed += Check(result.Value?.ExpiredCount == 1, "기한이 지난 예약만 만료");
        passed += Check(result.Value?.KeptCount == 1, "기한 전 예약 유지");

        Console.WriteLine($"self-test: {passed}/4 통과");
        if (passed != 4) Environment.ExitCode = 1;
    }

    private static int Check(bool condition, string name)
    {
        Console.WriteLine($"{(condition ? "PASS" : "FAIL")}: {name}");
        return condition ? 1 : 0;
    }

    // 테스트에서는 출력 부수 효과를 제거해 업무 결과만 빠르고 결정적으로 확인합니다.
    private sealed class SilentOperationLog : IOperationLog
    {
        public void ReservationExpired(Reservation reservation) { }
    }
}
