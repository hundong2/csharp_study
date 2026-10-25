using System.Collections.Immutable;

namespace TelemetryStreamExercise.Domain;

/// <summary>
/// stream 전체를 처리한 결과를 변경 불가능한 snapshot으로 보관합니다.
/// protocol 오류는 report 대신 실패 Result가 되며, 이 report에는 계속 처리할 수 있었던 business 오류만 들어갑니다.
/// </summary>
public sealed record IngestReport
{
    /// <summary>
    /// Application Service가 계산한 수와 오류 snapshot을 보관합니다.
    /// </summary>
    /// <param name="acceptedCount">새로 저장한 telemetry 개수입니다.</param>
    /// <param name="duplicateCount">내용까지 같은 안전한 재전송 개수입니다.</param>
    /// <param name="businessErrors">거절했지만 다음 frame 처리를 막지 않은 business 오류들입니다.</param>
    private IngestReport(
        int acceptedCount,
        int duplicateCount,
        ImmutableArray<Error> businessErrors)
    {
        AcceptedCount = acceptedCount;
        DuplicateCount = duplicateCount;
        BusinessErrors = businessErrors;
    }

    /// <summary>
    /// repository에 처음 저장된 event 수입니다.
    /// </summary>
    public int AcceptedCount { get; }

    /// <summary>
    /// 같은 event ID와 같은 내용이라 쓰기를 생략한 재전송 수입니다.
    /// </summary>
    public int DuplicateCount { get; }

    /// <summary>
    /// JSON은 읽었지만 Domain 규칙이나 idempotency 충돌 때문에 거절한 오류 snapshot입니다.
    /// </summary>
    public ImmutableArray<Error> BusinessErrors { get; }

    /// <summary>
    /// 처리 중 모은 mutable 오류 목록을 불변 배열로 복사해 이후 변경과 report를 분리합니다.
    /// </summary>
    /// <param name="acceptedCount">새로 저장한 frame 개수입니다.</param>
    /// <param name="duplicateCount">안전하게 무시한 동일 재전송 개수입니다.</param>
    /// <param name="businessErrors">Domain 또는 idempotency 규칙으로 거절한 오류 sequence입니다.</param>
    /// <returns>결정된 수와 불변 오류 snapshot을 가진 report를 반환합니다.</returns>
    public static IngestReport Create(
        int acceptedCount,
        int duplicateCount,
        IEnumerable<Error> businessErrors)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(acceptedCount);
        ArgumentOutOfRangeException.ThrowIfNegative(duplicateCount);
        ArgumentNullException.ThrowIfNull(businessErrors);

        var errorSnapshot = businessErrors.ToImmutableArray();
        // lambda `error => ...`는 각 오류가 Business가 아닌지 검사하는 이름 없는 작은 함수입니다.
        if (errorSnapshot.Any(error => error.Kind != ErrorKind.Business))
        {
            throw new ArgumentException(
                "IngestReport에는 Business 오류만 담을 수 있습니다.",
                nameof(businessErrors));
        }

        return new IngestReport(acceptedCount, duplicateCount, errorSnapshot);
    }
}
