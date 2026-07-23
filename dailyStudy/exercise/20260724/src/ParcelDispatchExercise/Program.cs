// 오늘 예제는 "택배 배차"라는 작은 업무를 통해 문법과 실무 구조를 한 흐름으로 연결합니다.
// 한 파일에 둔 이유는 초보자가 파일 사이를 오가기 전에 실행 순서를 먼저 눈으로 따라가기 쉽도록 하기 위해서입니다.
var services = CompositionRoot.Build();

if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
{
    await BeginnerValidation.RunAsync(services);
    return;
}

var requests = new[]
{
    new DispatchRequest("PKG-100", "서울", 3.2m, DeliverySpeed.Standard, "문 앞"),
    new DispatchRequest("PKG-101", "부산", 1.1m, DeliverySpeed.Express, null),
    new DispatchRequest("PKG-102", "서울", -1m, DeliverySpeed.Standard, "경비실")
};

foreach (var request in requests)
{
    var result = await services.Dispatcher.DispatchAsync(request, CancellationToken.None);
    Console.WriteLine(result.IsSuccess
        ? $"성공: {result.Value!.Id} / {result.Value.Carrier} / {result.Value.Fee:N0}원"
        : $"실패: {result.Error}");
}

// record는 값이 같은 데이터를 같은 것으로 비교하며 init 속성으로 생성 뒤 변경을 제한합니다.
// 요청 DTO를 불변에 가깝게 두면 여러 비동기 작업이 같은 값을 바꾸는 실수를 줄일 수 있습니다.
public sealed record DispatchRequest(
    string PackageId,
    string Destination,
    decimal WeightKg,
    DeliverySpeed Speed,
    string? DeliveryNote);

// enum은 허용되는 선택지를 문자열보다 명확하게 제한합니다.
public enum DeliverySpeed { Standard, Express }

// Domain Model은 자기 데이터의 유효성 규칙을 스스로 지켜 잘못된 상태가 퍼지지 않게 합니다.
public sealed record Parcel
{
    public required string Id { get; init; }
    public required string Destination { get; init; }
    public required decimal WeightKg { get; init; }
    public required DeliverySpeed Speed { get; init; }
    public string? DeliveryNote { get; init; }

    public static Result<Parcel> Create(DispatchRequest request)
    {
        // IsNullOrWhiteSpace는 null, 빈 문자열, 공백 문자열을 한 번에 검사합니다.
        if (string.IsNullOrWhiteSpace(request.PackageId))
            return Result<Parcel>.Failure("운송장 번호가 필요합니다.");
        if (string.IsNullOrWhiteSpace(request.Destination))
            return Result<Parcel>.Failure("배송지가 필요합니다.");
        if (request.WeightKg is <= 0 or > 30)
            return Result<Parcel>.Failure("무게는 0kg 초과 30kg 이하여야 합니다.");

        return Result<Parcel>.Success(new Parcel
        {
            Id = request.PackageId.Trim(),
            Destination = request.Destination.Trim(),
            WeightKg = request.WeightKg,
            Speed = request.Speed,
            // ?.는 왼쪽 값이 null이면 메서드를 호출하지 않고 null을 돌려줍니다.
            DeliveryNote = request.DeliveryNote?.Trim()
        });
    }
}

// Result는 입력 오류처럼 예상 가능한 실패를 호출자가 명시적으로 처리하게 합니다.
// DB 연결 끊김이나 버그처럼 예상 밖 실패는 예외로 남겨 관측·복구 정책에 맡깁니다.
public sealed record Result<T>(bool IsSuccess, T? Value, string? Error)
{
    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Failure(string error) => new(false, default, error);
}

public sealed record DispatchReceipt(string Id, string Carrier, decimal Fee);

// Strategy는 배송 방식별 규칙을 교체 가능하게 만듭니다. 새 방식 추가 시 기존 서비스 수정을 줄이는 OCP 예시입니다.
public interface IShippingStrategy
{
    bool Supports(DeliverySpeed speed);
    (string Carrier, decimal Fee) Quote(Parcel parcel);
}

public sealed class StandardShippingStrategy : IShippingStrategy
{
    public bool Supports(DeliverySpeed speed) => speed == DeliverySpeed.Standard;
    public (string Carrier, decimal Fee) Quote(Parcel parcel) =>
        ("한빛택배", 3_000m + parcel.WeightKg * 500m);
}

public sealed class ExpressShippingStrategy : IShippingStrategy
{
    public bool Supports(DeliverySpeed speed) => speed == DeliverySpeed.Express;
    public (string Carrier, decimal Fee) Quote(Parcel parcel) =>
        ("번개특송", 6_000m + parcel.WeightKg * 800m);
}

// Repository는 저장 기술을 감춘 계약입니다. Application Service가 SQL이나 DB 제품에 의존하지 않게 합니다.
public interface IParcelRepository
{
    Task<bool> ExistsAsync(string id, CancellationToken cancellationToken);
    Task SaveAsync(DispatchReceipt receipt, CancellationToken cancellationToken);
    IReadOnlyList<DispatchReceipt> GetAll();
}

public sealed class InMemoryParcelRepository : IParcelRepository
{
    private readonly List<DispatchReceipt> _items = [];

    public Task<bool> ExistsAsync(string id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_items.Any(item => item.Id == id));
    }

    public Task SaveAsync(DispatchReceipt receipt, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _items.Add(receipt);
        return Task.CompletedTask;
    }

    public IReadOnlyList<DispatchReceipt> GetAll() => _items.AsReadOnly();
}

// Application Service는 도메인 생성 → 중복 확인 → 정책 선택 → 저장이라는 유스케이스 순서를 조정합니다.
// 인터페이스에 의존하는 DIP 덕분에 메모리 저장소를 테스트 대역으로 사용할 수 있습니다.
public sealed class DispatchService(IParcelRepository repository, IEnumerable<IShippingStrategy> strategies)
{
    public async Task<Result<DispatchReceipt>> DispatchAsync(
        DispatchRequest request,
        CancellationToken cancellationToken)
    {
        var parcelResult = Parcel.Create(request);
        if (!parcelResult.IsSuccess)
            return Result<DispatchReceipt>.Failure(parcelResult.Error ?? "알 수 없는 입력 오류");

        var parcel = parcelResult.Value!;
        if (await repository.ExistsAsync(parcel.Id, cancellationToken))
            return Result<DispatchReceipt>.Failure("이미 배차된 운송장입니다.");

        // Single은 조건에 맞는 정책이 정확히 하나여야 한다는 설계 규칙도 검사합니다.
        var strategy = strategies.Single(item => item.Supports(parcel.Speed));
        var quote = strategy.Quote(parcel);
        var receipt = new DispatchReceipt(parcel.Id, quote.Carrier, quote.Fee);

        // await는 저장 완료까지 기다리되 스레드를 점유해 막지 않습니다. 취소 토큰은 중단 요청을 전달합니다.
        await repository.SaveAsync(receipt, cancellationToken);
        return Result<DispatchReceipt>.Success(receipt);
    }
}

public sealed record AppServices(DispatchService Dispatcher, IParcelRepository Repository);

// Composition Root는 실제 구현을 선택하고 객체를 조립하는 유일한 장소입니다.
// 실무에서는 DI 컨테이너로 바꿔도 각 클래스의 책임은 그대로 유지됩니다.
public static class CompositionRoot
{
    public static AppServices Build()
    {
        IParcelRepository repository = new InMemoryParcelRepository();
        IShippingStrategy[] strategies =
        [
            new StandardShippingStrategy(),
            new ExpressShippingStrategy()
        ];
        return new AppServices(new DispatchService(repository, strategies), repository);
    }
}

public static class BeginnerValidation
{
    public static async Task RunAsync(AppServices services)
    {
        var passed = 0;
        passed += Check(!Parcel.Create(new("", "서울", 1m, DeliverySpeed.Standard, null)).IsSuccess,
            "빈 운송장 번호 거부");
        passed += Check(!Parcel.Create(new("A", "서울", 0m, DeliverySpeed.Standard, null)).IsSuccess,
            "0kg 무게 거부");

        var first = await services.Dispatcher.DispatchAsync(
            new("TEST-1", "서울", 2m, DeliverySpeed.Express, null), CancellationToken.None);
        passed += Check(first.IsSuccess && first.Value?.Fee == 7_600m, "특송 요금 계산");

        var duplicate = await services.Dispatcher.DispatchAsync(
            new("TEST-1", "서울", 2m, DeliverySpeed.Express, null), CancellationToken.None);
        passed += Check(!duplicate.IsSuccess, "중복 운송장 거부");

        Console.WriteLine($"초보자 검증 통과 ({passed}/4)");
        if (passed != 4)
            throw new InvalidOperationException("검증 실패: 위 항목을 고친 뒤 다시 실행하세요.");
    }

    private static int Check(bool condition, string name)
    {
        Console.WriteLine($"{(condition ? "[통과]" : "[실패]")} {name}");
        return condition ? 1 : 0;
    }
}
