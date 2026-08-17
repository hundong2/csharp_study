// 학습 흐름을 한 파일에서 따라갈 수 있게 구성했습니다. 실무에서는 책임별 파일과 프로젝트로 나누는 편이 좋습니다.

var requests = new[]
{
    new MeetingRequest("MT-1001", "개발 주간 회의", 6, new DateTime(2026, 8, 18, 10, 0, 0), TimeSpan.FromHours(1), true, "SEOUL"),
    new MeetingRequest("MT-1002", "디자인 리뷰", 3, new DateTime(2026, 8, 18, 10, 30, 0), TimeSpan.FromMinutes(30), false, null),
    new MeetingRequest("MT-1003", "", 4, new DateTime(2026, 8, 18, 14, 0, 0), TimeSpan.FromHours(1), false, "SEOUL"),
    new MeetingRequest("MT-1001", "중복 요청", 2, new DateTime(2026, 8, 18, 15, 0, 0), TimeSpan.FromHours(1), false, "BUSAN")
};

var rooms = new[]
{
    new MeetingRoom("S-201", "SEOUL", 8, true),
    new MeetingRoom("S-202", "SEOUL", 4, false),
    new MeetingRoom("B-101", "BUSAN", 6, true)
};

// Composition Root에서 구현 객체를 한 번 조립하면 업무 로직이 구체 클래스 생성 방법을 몰라도 되어 테스트 대역으로 바꾸기 쉽습니다.
IMeetingReservationRepository repository = new InMemoryMeetingReservationRepository();
IRoomSelectionStrategy strategy = new SmallestSuitableRoomStrategy();
var service = new ReserveMeetingsService(repository, strategy);

if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
{
    await SelfTests.RunAsync();
    return;
}

var batch = await service.ExecuteAsync(requests, rooms, CancellationToken.None);
Console.WriteLine($"예약 {batch.Reserved.Count}건 / 거절 {batch.Rejected.Count}건");
foreach (var reservation in batch.Reserved)
    Console.WriteLine($"{reservation.RequestId}: {reservation.RoomId}, {reservation.Start:HH:mm}-{reservation.End:HH:mm}");
foreach (var rejection in batch.Rejected)
    Console.WriteLine($"{rejection.RequestId}: 거절 - {rejection.Reason}");

// record는 생성 후 값이 바뀌지 않는 데이터 전달 객체에 알맞고, 값 기반 비교 덕분에 테스트도 읽기 쉬워집니다.
sealed record MeetingRequest(
    string Id,
    string Title,
    int AttendeeCount,
    DateTime Start,
    TimeSpan Duration,
    bool NeedsVideo,
    string? PreferredOffice);

sealed record MeetingRoom(string Id, string Office, int Capacity, bool HasVideo);
sealed record Reservation(string RequestId, string RoomId, DateTime Start, DateTime End);
sealed record Rejection(string RequestId, string Reason);
sealed record ReservationBatch(IReadOnlyList<Reservation> Reserved, IReadOnlyList<Rejection> Rejected);

// 예상 가능한 입력 실패는 Result로 반환해 호출자가 정상 흐름 안에서 처리하게 하고, 인프라 장애나 계약 위반은 예외로 구분합니다.
sealed record Result<T>(bool IsSuccess, T Value, string? Error)
{
    public static Result<T> Success(T value) => new(true, value, null);

    // 실패일 때 Value를 읽지 않는다는 계약을 default!로 표현합니다. 호출자는 반드시 IsSuccess를 먼저 확인해야 합니다.
    public static Result<T> Failure(string error) => new(false, default!, error);
}

interface IMeetingReservationRepository
{
    Task<bool> RequestExistsAsync(string requestId, CancellationToken cancellationToken);
    Task<bool> HasConflictAsync(string roomId, DateTime start, DateTime end, CancellationToken cancellationToken);
    Task SaveAsync(Reservation reservation, CancellationToken cancellationToken);
}

interface IRoomSelectionStrategy
{
    Result<MeetingRoom> Select(MeetingRequest request, IEnumerable<MeetingRoom> availableRooms);
}

// 방 선택 규칙을 Strategy로 격리하면 비용 우선, 층 우선 같은 새 정책을 Application Service 수정 없이 교체할 수 있어 OCP를 지킵니다.
sealed class SmallestSuitableRoomStrategy : IRoomSelectionStrategy
{
    public Result<MeetingRoom> Select(MeetingRequest request, IEnumerable<MeetingRoom> availableRooms)
    {
        var room = availableRooms
            .Where(candidate => candidate.Capacity >= request.AttendeeCount)
            .Where(candidate => !request.NeedsVideo || candidate.HasVideo)
            .Where(candidate => request.PreferredOffice is null ||
                candidate.Office.Equals(request.PreferredOffice, StringComparison.OrdinalIgnoreCase))
            .OrderBy(candidate => candidate.Capacity)
            .ThenBy(candidate => candidate.Id, StringComparer.Ordinal)
            .FirstOrDefault();

        return room is null
            ? Result<MeetingRoom>.Failure("조건을 만족하는 빈 회의실이 없습니다.")
            : Result<MeetingRoom>.Success(room);
    }
}

// Application Service는 검증, 조회, 전략 실행, 저장의 순서만 조정합니다. 도메인 규칙과 저장 세부 구현을 섞지 않는 SRP 설계입니다.
sealed class ReserveMeetingsService(
    IMeetingReservationRepository repository,
    IRoomSelectionStrategy strategy)
{
    public async Task<ReservationBatch> ExecuteAsync(
        IEnumerable<MeetingRequest> requests,
        IEnumerable<MeetingRoom> rooms,
        CancellationToken cancellationToken)
    {
        var reserved = new List<Reservation>();
        var rejected = new List<Rejection>();

        // LINQ 정렬로 요청 처리 순서를 결정적으로 만들어 실행 결과, 로그, 테스트가 매번 같게 합니다.
        foreach (var request in requests.OrderBy(item => item.Start).ThenBy(item => item.Id, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var validation = Validate(request);
            if (!validation.IsSuccess)
            {
                rejected.Add(new(request.Id, validation.Error ?? "알 수 없는 검증 실패"));
                continue;
            }

            if (await repository.RequestExistsAsync(request.Id, cancellationToken))
            {
                rejected.Add(new(request.Id, "이미 처리된 요청입니다."));
                continue;
            }

            var end = request.Start + request.Duration;
            var availableRooms = new List<MeetingRoom>();
            foreach (var room in rooms)
            {
                if (!await repository.HasConflictAsync(room.Id, request.Start, end, cancellationToken))
                    availableRooms.Add(room);
            }

            var selection = strategy.Select(request, availableRooms);
            if (!selection.IsSuccess)
            {
                rejected.Add(new(request.Id, selection.Error ?? "회의실 선택 실패"));
                continue;
            }

            var reservation = new Reservation(request.Id, selection.Value.Id, request.Start, end);
            await repository.SaveAsync(reservation, cancellationToken);
            reserved.Add(reservation);
        }

        return new(reserved, rejected);
    }

    private static Result<bool> Validate(MeetingRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Id) || string.IsNullOrWhiteSpace(request.Title))
            return Result<bool>.Failure("요청 ID와 제목은 필수입니다.");
        if (request.AttendeeCount <= 0)
            return Result<bool>.Failure("참석 인원은 1명 이상이어야 합니다.");
        if (request.Duration <= TimeSpan.Zero || request.Duration > TimeSpan.FromHours(4))
            return Result<bool>.Failure("회의 시간은 0분 초과 4시간 이하여야 합니다.");

        return Result<bool>.Success(true);
    }
}

// 메모리 Repository는 학습용 저장소입니다. 인터페이스에 의존하므로 서비스 코드를 바꾸지 않고 실제 DB 구현으로 교체할 수 있습니다(DIP).
sealed class InMemoryMeetingReservationRepository : IMeetingReservationRepository
{
    private readonly List<Reservation> _reservations = [];

    public Task<bool> RequestExistsAsync(string requestId, CancellationToken cancellationToken)
        => Task.FromResult(_reservations.Any(item => item.RequestId == requestId));

    public Task<bool> HasConflictAsync(string roomId, DateTime start, DateTime end, CancellationToken cancellationToken)
        // 두 구간은 기존 시작 < 새 종료이고 새 시작 < 기존 종료일 때 겹칩니다. 끝 시각과 다음 시작 시각이 같으면 충돌이 아닙니다.
        => Task.FromResult(_reservations.Any(item => item.RoomId == roomId && item.Start < end && start < item.End));

    public Task SaveAsync(Reservation reservation, CancellationToken cancellationToken)
    {
        _reservations.Add(reservation);
        return Task.CompletedTask;
    }
}

static class SelfTests
{
    public static async Task RunAsync()
    {
        var passed = 0;
        var strategy = new SmallestSuitableRoomStrategy();
        var rooms = new[] { new MeetingRoom("R1", "SEOUL", 4, false), new MeetingRoom("R2", "SEOUL", 8, true) };
        var videoRequest = new MeetingRequest("T1", "영상 회의", 3, DateTime.Today.AddHours(10), TimeSpan.FromHours(1), true, null);
        Check(strategy.Select(videoRequest, rooms).Value.Id == "R2", "영상 장비 방 선택");
        passed++;

        var repository = new InMemoryMeetingReservationRepository();
        await repository.SaveAsync(new("OLD", "R1", DateTime.Today.AddHours(10), DateTime.Today.AddHours(11)), CancellationToken.None);
        Check(await repository.HasConflictAsync("R1", DateTime.Today.AddHours(10.5), DateTime.Today.AddHours(11.5), CancellationToken.None), "시간 충돌 감지");
        passed++;
        Check(!await repository.HasConflictAsync("R1", DateTime.Today.AddHours(11), DateTime.Today.AddHours(12), CancellationToken.None), "연속 예약 허용");
        passed++;

        var service = new ReserveMeetingsService(new InMemoryMeetingReservationRepository(), strategy);
        var request = new MeetingRequest("T4", "반복 요청", 2, DateTime.Today.AddHours(13), TimeSpan.FromHours(1), false, null);
        await service.ExecuteAsync([request], rooms, CancellationToken.None);
        var repeated = await service.ExecuteAsync([request], rooms, CancellationToken.None);
        Check(repeated.Rejected.Single().Reason.Contains("이미 처리", StringComparison.Ordinal), "중복 요청 거절");
        passed++;

        Console.WriteLine($"self-test 통과: {passed}/4");
    }

    private static void Check(bool condition, string name)
    {
        // 테스트 실패는 예상 업무 분기가 아니라 코드 계약 위반이므로 예외로 즉시 드러내 원인을 숨기지 않습니다.
        if (!condition) throw new InvalidOperationException($"테스트 실패: {name}");
    }
}
