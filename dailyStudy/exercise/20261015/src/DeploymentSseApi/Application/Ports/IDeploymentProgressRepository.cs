using DeploymentSseApi.Domain;

namespace DeploymentSseApi.Application.Ports;

/// <summary>
/// 배포 이벤트 저장과 SSE 구독을 Application 계층에서 구체 기술과 분리하는 Repository Port입니다.
/// Application Service는 메모리, 데이터베이스, 메시지 브로커 중 무엇을 쓰는지 몰라도 같은 use case를 실행할 수 있습니다.
/// </summary>
public interface IDeploymentProgressRepository
{
    /// <summary>
    /// 특정 배포에 마지막으로 저장된 불변 Event snapshot을 조회합니다.
    /// </summary>
    /// <param name="deploymentId">검증과 정규화를 마친 저장소 key입니다.</param>
    /// <param name="cancellationToken">호출자가 조회를 더 기다리지 않는다는 취소 신호입니다.</param>
    /// <returns>최신 Event 또는 아직 이벤트가 없음을 뜻하는 null이 든 ValueTask를 반환합니다.</returns>
    ValueTask<DeploymentProgressEvent?> GetLatestAsync(
        string deploymentId,
        CancellationToken cancellationToken);

    /// <summary>
    /// 읽었던 마지막 EventId가 아직 최신일 때만 다음 Event를 원자적으로 추가합니다.
    /// </summary>
    /// <param name="progressEvent">Strategy가 검증하고 다음 EventId를 부여한 불변 Event입니다.</param>
    /// <param name="expectedLastEventId">Application Service가 읽은 최신 EventId이며 첫 추가라면 0입니다.</param>
    /// <param name="cancellationToken">호출자가 저장 결과를 더 기다리지 않는다는 취소 신호입니다.</param>
    /// <returns>추가 성공 또는 다른 요청이 먼저 쓴 충돌 상태가 든 ValueTask를 반환합니다.</returns>
    ValueTask<DeploymentProgressAppendResult> TryAppendAsync(
        DeploymentProgressEvent progressEvent,
        long expectedLastEventId,
        CancellationToken cancellationToken);

    /// <summary>
    /// afterEventId 뒤의 replay snapshot을 잡는 순간 live 구독도 함께 등록하여 사이 이벤트가 빠지지 않는 stream을 엽니다.
    /// </summary>
    /// <param name="deploymentId">검증과 정규화를 마친 저장소 key입니다.</param>
    /// <param name="afterEventId">클라이언트가 이미 받은 마지막 SSE id이며 처음 연결이면 0입니다.</param>
    /// <param name="cancellationToken">HTTP 연결 종료처럼 구독을 제거해야 하는 취소 신호입니다.</param>
    /// <returns>순서가 보장된 replay+live stream, 또는 배포가 없음을 뜻하는 null이 든 ValueTask를 반환합니다.</returns>
    ValueTask<DeploymentProgressStream?> OpenStreamAsync(
        string deploymentId,
        long afterEventId,
        CancellationToken cancellationToken);
}

/// <summary>
/// 낙관적 Event 추가 시 저장소가 관찰한 결과 종류입니다.
/// </summary>
public enum DeploymentProgressAppendStatus
{
    Appended,
    Conflict
}

/// <summary>
/// Event 추가 결과를 Adapter 예외나 구현 형식에 의존하지 않는 값으로 전달합니다.
/// 괄호 안 매개변수는 주 생성자가 속성을 초기화하며, 생성자는 객체만 만들고 반환값은 없습니다.
/// </summary>
/// <param name="Status">추가 성공 또는 낙관적 동시성 충돌 상태입니다.</param>
public sealed record DeploymentProgressAppendResult(DeploymentProgressAppendStatus Status);

/// <summary>
/// 저장소가 원자적으로 구성한 과거 replay와 이후 live Event를 하나의 비동기 순서로 노출합니다.
/// 괄호 안 매개변수는 주 생성자가 속성을 초기화하며, 생성자는 객체만 만들고 반환값은 없습니다.
/// </summary>
/// <param name="Events">EventId 오름차순으로 replay를 먼저 내보낸 뒤 live Event를 기다리는 비동기 열거입니다.</param>
// IAsyncEnumerable은 모든 결과를 메모리에 모으지 않고 await foreach로 도착하는 즉시 한 건씩 소비하게 합니다.
public sealed record DeploymentProgressStream(
    IAsyncEnumerable<DeploymentProgressEvent> Events);
