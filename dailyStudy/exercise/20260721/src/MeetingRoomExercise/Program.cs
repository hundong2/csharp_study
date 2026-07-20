// 읽는 순서: ① Main 실행 흐름 → ② 기본 모델/Result → ③ Strategy → ④ Repository → ⑤ Application Service → ⑥ Composition Root → ⑦ 초보 검증입니다.
// 처음에는 실행 결과와 [기초] 주석만 읽고, 두 번째에는 [설계] 주석을 따라 객체가 협력하는 이유를 살펴보세요.

if (args.Contains("--self-test"))
{
    await BeginnerValidation.RunAsync();
    return;
}

Console.WriteLine("=== 회의실 예약 연습 ===");

// [기초] var는 오른쪽 값으로 정적 타입을 추론합니다. 타입 안전성은 그대로 유지됩니다.
var service = CompositionRoot.Create();
var request = new ReservationRequest("초보 C# 스터디", 6, DateTime.Today.AddDays(1).AddHours(14), 60, "화이트보드");
Result<Reservation> result = await service.ReserveAsync(request);

// [기초] switch 식은 값의 모양에 따라 결과를 선택합니다. 성공/실패를 모두 적어 누락을 눈에 보이게 합니다.
Console.WriteLine(result switch
{
    Result<Reservation>.Success(var reservation) => $"예약 성공: {reservation.RoomName}, {reservation.StartAt:g}",
    Result<Reservation>.Failure(var error) => $"예약 실패: {error}",
    _ => "알 수 없는 결과"
});

// [기초] string?의 물음표는 null 가능성을 나타내고, ??는 null일 때 대체 값을 선택합니다.
Console.WriteLine($"요청 장비: {request.RequiredEquipment ?? "없음"}");

// [도메인 모델] record는 값 중심 데이터에 적합합니다. init 전용 속성은 생성 후 우발적 변경을 막습니다.
sealed record MeetingRoom(int Id, string Name, int Capacity, string[] Equipment);
sealed record Reservation(int Id, int RoomId, string RoomName, string Title, DateTime StartAt, int DurationMinutes);
sealed record ReservationRequest(string Title, int AttendeeCount, DateTime StartAt, int DurationMinutes, string? RequiredEquipment);

// [설계] 예상 가능한 업무 실패는 예외 대신 Result로 반환합니다. 호출자가 실패를 정상 분기로 처리할 수 있습니다.
// 반면 null 인수 같은 프로그래머 오류에는 ArgumentNullException 같은 예외가 더 알맞습니다.
abstract record Result<T>
{
    public sealed record Success(T Value) : Result<T>;
    public sealed record Failure(string Error) : Result<T>;
}

// [Strategy] 선택 규칙을 인터페이스로 분리하면 서비스 수정 없이 다른 정책을 주입할 수 있습니다(OCP, DIP).
interface IRoomSelectionStrategy
{
    MeetingRoom? Select(IReadOnlyList<MeetingRoom> rooms, ReservationRequest request);
}

sealed class SmallestSuitableRoomStrategy : IRoomSelectionStrategy
{
    public MeetingRoom? Select(IReadOnlyList<MeetingRoom> rooms, ReservationRequest request)
    {
        // [중급] Where는 조건 필터, OrderBy는 정렬, FirstOrDefault는 첫 값 또는 null을 반환하는 LINQ 연산입니다.
        // 필요한 만큼만 큰 방을 고르면 큰 회의실을 소규모 회의가 낭비하지 않습니다.
        return rooms
            .Where(room => room.Capacity >= request.AttendeeCount)
            .Where(room => request.RequiredEquipment is null || room.Equipment.Contains(request.RequiredEquipment))
            .OrderBy(room => room.Capacity)
            .FirstOrDefault();
    }
}

// [Repository] 저장 기술을 계약 뒤에 숨겨 Application Service가 DB 세부사항을 몰라도 되게 합니다(SRP, DIP).
interface IReservationRepository
{
    Task<IReadOnlyList<MeetingRoom>> GetAvailableRoomsAsync(DateTime startAt, int durationMinutes, CancellationToken cancellationToken);
    Task<Reservation> AddAsync(Reservation reservation, CancellationToken cancellationToken);
}

sealed class InMemoryReservationRepository : IReservationRepository
{
    private readonly List<MeetingRoom> _rooms =
    [
        new(1, "새싹", 4, ["모니터"]),
        new(2, "나무", 8, ["모니터", "화이트보드"]),
        new(3, "숲", 16, ["화상회의", "화이트보드"])
    ];
    private readonly List<Reservation> _reservations = [];

    public Task<IReadOnlyList<MeetingRoom>> GetAvailableRoomsAsync(DateTime startAt, int durationMinutes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DateTime endAt = startAt.AddMinutes(durationMinutes);

        // [중급] Any는 하나라도 조건을 만족하는지 검사합니다. 겹침 공식은 기존 시작 < 새 종료 && 새 시작 < 기존 종료입니다.
        IReadOnlyList<MeetingRoom> available = _rooms
            .Where(room => !_reservations.Any(saved => saved.RoomId == room.Id
                && saved.StartAt < endAt
                && startAt < saved.StartAt.AddMinutes(saved.DurationMinutes)))
            .ToList();
        return Task.FromResult(available);
    }

    public Task<Reservation> AddAsync(Reservation reservation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _reservations.Add(reservation);
        return Task.FromResult(reservation);
    }
}

// [Application Service] 입력 검증, 조회, 정책 실행, 저장이라는 유스케이스 순서를 조정합니다.
// 생성자 주입은 의존성을 명시하고 가짜 구현으로 교체할 수 있게 하므로 테스트하기 쉽습니다.
sealed class ReservationService(IReservationRepository repository, IRoomSelectionStrategy strategy)
{
    public async Task<Result<Reservation>> ReserveAsync(ReservationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Title))
            return new Result<Reservation>.Failure("회의 제목을 입력하세요.");
        if (request.AttendeeCount <= 0 || request.DurationMinutes is < 15 or > 480)
            return new Result<Reservation>.Failure("참석자는 1명 이상, 시간은 15~480분이어야 합니다.");

        // [비동기] await는 I/O 완료를 기다리는 동안 스레드를 붙잡지 않는 계약입니다. CancellationToken은 취소 요청을 아래 계층까지 전달합니다.
        IReadOnlyList<MeetingRoom> rooms = await repository.GetAvailableRoomsAsync(request.StartAt, request.DurationMinutes, cancellationToken);
        MeetingRoom? room = strategy.Select(rooms, request);
        if (room is null)
            return new Result<Reservation>.Failure("조건에 맞는 빈 회의실이 없습니다.");

        var reservation = new Reservation(0, room.Id, room.Name, request.Title.Trim(), request.StartAt, request.DurationMinutes);
        Reservation saved = await repository.AddAsync(reservation, cancellationToken);
        return new Result<Reservation>.Success(saved);
    }
}

static class CompositionRoot
{
    // [Composition Root] 구체 구현 선택과 객체 연결을 한곳에 모읍니다. 실제 ASP.NET Core에서는 서비스 등록부가 이 역할을 합니다.
    public static ReservationService Create() =>
        new(new InMemoryReservationRepository(), new SmallestSuitableRoomStrategy());
}

static class BeginnerValidation
{
    public static async Task RunAsync()
    {
        // [검증] 외부 테스트 패키지 없이도 핵심 규칙을 확인합니다. 실패하면 종료 코드 1로 CI도 실패를 알 수 있습니다.
        var service = CompositionRoot.Create();
        DateTime start = new(2030, 1, 2, 10, 0, 0);
        Result<Reservation> first = await service.ReserveAsync(new("팀 회의", 5, start, 60, "화이트보드"));
        Result<Reservation> conflict = await service.ReserveAsync(new("겹친 회의", 5, start, 60, "화이트보드"));
        Result<Reservation> invalid = await service.ReserveAsync(new("", 2, start, 60, null));

        var checks = new (string Name, bool Passed)[]
        {
            ("조건에 맞는 가장 작은 방 선택", first is Result<Reservation>.Success { Value.RoomName: "나무" }),
            ("같은 방의 겹친 시간 제외", conflict is Result<Reservation>.Success { Value.RoomName: "숲" }),
            ("빈 제목을 Result 실패로 반환", invalid is Result<Reservation>.Failure),
            ("nullable 장비 없이도 요청 가능", await CanReserveWithoutEquipmentAsync())
        };

        foreach (var (name, passed) in checks)
            Console.WriteLine($"{(passed ? "[통과]" : "[실패]")} {name}");

        int passedCount = checks.Count(check => check.Passed);
        Console.WriteLine($"초보자 검증 통과 ({passedCount}/{checks.Length})");
        if (passedCount != checks.Length) Environment.ExitCode = 1;
    }

    private static async Task<bool> CanReserveWithoutEquipmentAsync()
    {
        Result<Reservation> result = await CompositionRoot.Create()
            .ReserveAsync(new("1:1", 2, new DateTime(2030, 1, 3, 9, 0, 0), 30, null));
        return result is Result<Reservation>.Success { Value.RoomName: "새싹" };
    }
}
