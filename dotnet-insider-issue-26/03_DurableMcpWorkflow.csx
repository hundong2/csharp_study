// 실행: dotnet script 03_DurableMcpWorkflow.csx
// 목적: 요청 수명과 작업 수명을 분리하고 opaque ID로 상태를 조회하는 패턴을 익힌다.

// nullable 참조 형식 주석(string?)을 활성화해 응답의 선택 필드를 명시한다.
#nullable enable

// 01. Console, Guid, DateTimeOffset 같은 형식을 가져온다.
using System;
// 02. ConcurrentDictionary는 여러 스레드가 workflow 상태를 안전하게 읽고 쓰게 한다.
using System.Collections.Concurrent;
// 03. Task와 비동기 대기를 가져온다.
using System.Threading.Tasks;

// 04. enum은 허용된 상태 집합을 정수 대신 이름으로 제한한다.
enum WorkState { Working, InputRequired, Completed, Failed, Cancelled }
// 05. record class는 참조 형식 상태 객체다. set은 모형에서 상태 전이를 허용한다.
record class Workflow(string Id, WorkState State, string? Result, DateTimeOffset UpdatedAt);
// 06. start 응답은 즉시 결과 또는 workflow ID 중 하나를 가진다.
record StartResponse(string Status, string? WorkflowId, string? Result, int PollAfterSeconds);
// 07. get 응답은 외부 계약에 필요한 값만 노출한다.
record GetResponse(string Status, string? Result, int? PollAfterSeconds);

// 08. 저장소는 thread-safe지만 프로세스 메모리이므로 교육용이다. 실제 내구성은 DB/스토리지에 checkpoint해야 한다.
ConcurrentDictionary<string, Workflow> store = new(StringComparer.Ordinal);

// 09. StartAsync는 긴 작업을 시작하되 짧은 응답 예산만 기다린다.
async Task<StartResponse> StartAsync(string request, TimeSpan responseBudget)
{
    // 10. Guid N 형식은 하이픈 없는 opaque 식별자를 만든다. 권한 검증은 별도로 필요하다.
    string id = Guid.NewGuid().ToString("N");
    // 11. 초기 상태를 저장소에 먼저 기록해 응답이 끊겨도 ID가 존재하게 한다.
    store[id] = new(id, WorkState.Working, null, DateTimeOffset.UtcNow);

    // 12. Task.Run은 CPU 작업 모형을 스레드 풀에 큐잉한다. Durable activity와 같지는 않다.
    Task worker = Task.Run(async () =>
    {
        // 13. 외부 I/O 또는 긴 계산 시간을 비동기 지연으로 흉내 낸다.
        await Task.Delay(120);
        // 14. 현재 상태를 읽고 취소되지 않았을 때만 완료 상태로 바꾼다.
        Workflow current = store[id];
        // 15. 취소는 협조적이다. worker가 상태를 확인해야 실제로 멈춘다.
        if (current.State != WorkState.Cancelled)
        {
            // 16. with 식은 record를 복사하며 지정한 속성만 바꾼다.
            store[id] = current with
            {
                State = WorkState.Completed,
                Result = $"mined:{request.ToUpperInvariant()}",
                UpdatedAt = DateTimeOffset.UtcNow
            };
        }
    });

    // 17. 응답 예산만큼 기다리는 Task를 만든다. 작업 자체 timeout이 아니다.
    Task budget = Task.Delay(responseBudget);
    // 18. 먼저 끝난 Task를 확인해 즉시 결과와 ID 응답 중 하나를 선택한다.
    Task winner = await Task.WhenAny(worker, budget);
    // 19. worker가 먼저 끝났으면 저장된 최종 결과를 바로 반환한다.
    if (winner == worker)
    {
        Workflow done = store[id];
        return new("completed", null, done.Result, 0);
    }

    // 20. 응답 연결은 끝내지만 worker는 계속 실행되고 ID로 다시 찾을 수 있다.
    return new("working", id, null, PollAfterSeconds: 1);
}

// 21. 상태 조회는 유효하지 않은 ID를 예외 대신 명시적인 not_found로 처리한다.
GetResponse Get(string id)
{
    // 22. 서버는 실제로 tenant 소유권도 검사해야 한다.
    if (!store.TryGetValue(id, out Workflow? workflow))
    {
        return new("not_found", null, null);
    }

    // 23. switch 식은 내부 enum을 외부 문자열 계약으로 매핑한다.
    return workflow.State switch
    {
        WorkState.Completed => new("completed", workflow.Result, null),
        WorkState.Failed => new("failed", workflow.Result, null),
        WorkState.Cancelled => new("cancelled", null, null),
        WorkState.InputRequired => new("input_required", null, null),
        _ => new("working", null, 1)
    };
}

// 24. 20ms만 기다리므로 120ms 작업은 대부분 ID를 반환한다.
StartResponse started = await StartAsync("frames", TimeSpan.FromMilliseconds(20));
// 25. nullable ID가 없으면 즉시 완료된 것이므로 결과를 출력하고 폴링하지 않는다.
Console.WriteLine($"start = {started.Status}, workflow_id = {started.WorkflowId ?? "<inline>"}");

// 26. 이 실습에서는 ID 응답 경로를 기대하지만 경쟁 조건을 안전하게 처리한다.
if (started.WorkflowId is string workflowId)
{
    // 27. 처음 조회는 보통 working이다.
    Console.WriteLine($"first get = {Get(workflowId).Status}");
    // 28. 실제 클라이언트는 서버가 준 poll_after_seconds와 backoff를 따라야 한다.
    await Task.Delay(150);
    // 29. 작업이 끝난 뒤 상태와 결과를 다시 읽는다.
    GetResponse final = Get(workflowId);
    // 30. 완료 상태와 결과를 출력한다.
    Console.WriteLine($"final get = {final.Status}, result = {final.Result}");
}
else
{
    // 31. 빠른 환경에서 즉시 완료됐다면 inline 결과를 출력한다.
    Console.WriteLine($"inline result = {started.Result}");
}

// CLR 관찰 메모
// - await 상태 머신은 프로세스 메모리에 있으므로 프로세스 종료를 견디지 못한다.
// - Durable Functions는 이벤트 이력/checkpoint로 상태를 저장하고 replay해 복구한다.
// - Task.Run은 스레드 풀을 사용하므로 긴 blocking I/O를 넣으면 thread starvation을 만들 수 있다.
// - ConcurrentDictionary는 프로세스 내 동시 접근만 보호하며 process crash 뒤 상태를 복구하지는 못한다.
