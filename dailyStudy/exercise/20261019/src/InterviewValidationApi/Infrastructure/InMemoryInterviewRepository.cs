using InterviewValidationApi.Application.Ports;
using InterviewValidationApi.Domain;

namespace InterviewValidationApi.Infrastructure;

/// <summary>
/// 한 프로세스 안에서 lock으로 충돌 확인과 저장을 원자화하는 학습용 Repository Adapter입니다.
/// </summary>
public sealed class InMemoryInterviewRepository : IInterviewRepository
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, ScheduledEntry> _entries = [];

    /// <summary>
    /// 검증과 동시성 자체 테스트에서 실제 저장 건수를 확인하게 합니다.
    /// </summary>
    /// <returns>현재 메모리에 저장된 일정 수를 반환합니다.</returns>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count;
            }
        }
    }

    /// <summary>
    /// 같은 면접관의 유효 시간대가 겹치는지 검사하고, 겹치지 않을 때만 같은 lock 안에서 저장합니다.
    /// </summary>
    /// <param name="interview">저장하려는 면접 일정입니다.</param>
    /// <param name="buffer">새 일정의 앞뒤 준비 시간입니다.</param>
    /// <param name="cancellationToken">lock에 들어가기 전에 취소를 관찰할 신호입니다.</param>
    /// <returns>저장 성공이면 true, 충돌이면 false인 이미 완료된 ValueTask를 반환합니다.</returns>
    public ValueTask<bool> TryScheduleAsync(
        Interview interview,
        InterviewBuffer buffer,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(interview);
        ArgumentNullException.ThrowIfNull(buffer);
        cancellationToken.ThrowIfCancellationRequested();

        // HashSet은 포함 여부를 빠르게 확인하며 Ordinal 비교로 문화권에 따른 ID 비교 차이를 없앱니다.
        var proposedInterviewers = interview.InterviewerIds.ToHashSet(StringComparer.Ordinal);
        lock (_gate)
        {
            var hasConflict = _entries.Values.Any(
                existing => SharesInterviewer(existing.Interview, proposedInterviewers)
                    && TimeWindowsOverlap(existing, interview, buffer));

            if (hasConflict)
            {
                return ValueTask.FromResult(false);
            }

            _entries.Add(interview.Id, new ScheduledEntry(interview, buffer));
            return ValueTask.FromResult(true);
        }
    }

    /// <summary>
    /// lock 안에서 식별자에 맞는 면접 일정을 조회합니다.
    /// </summary>
    /// <param name="id">조회할 면접 일정 식별자입니다.</param>
    /// <param name="cancellationToken">조회 전에 취소를 확인할 신호입니다.</param>
    /// <returns>찾은 일정 또는 null을 담은 이미 완료된 ValueTask를 반환합니다.</returns>
    public ValueTask<Interview?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            // out var는 TryGetValue가 찾은 값을 새 지역 변수 entry에 안전하게 넣는 문법입니다.
            return ValueTask.FromResult(_entries.TryGetValue(id, out var entry) ? entry.Interview : null);
        }
    }

    /// <summary>
    /// 기존 일정과 새 일정에 같은 면접관이 한 명이라도 있는지 확인합니다.
    /// </summary>
    /// <param name="existing">이미 저장된 면접 일정입니다.</param>
    /// <param name="proposedInterviewers">새 일정의 면접관 ID 집합입니다.</param>
    /// <returns>공통 면접관이 있으면 true를 반환합니다.</returns>
    private static bool SharesInterviewer(Interview existing, IReadOnlySet<string> proposedInterviewers)
    {
        return existing.InterviewerIds.Any(proposedInterviewers.Contains);
    }

    /// <summary>
    /// 기존 일정과 새 일정의 앞뒤 buffer를 포함한 반열린 시간 구간이 겹치는지 계산합니다.
    /// </summary>
    /// <param name="existing">기존 일정과 그 일정의 buffer입니다.</param>
    /// <param name="proposed">새 면접 일정입니다.</param>
    /// <param name="proposedBuffer">새 일정의 buffer입니다.</param>
    /// <returns>두 유효 시간 구간이 한 순간이라도 겹치면 true를 반환합니다.</returns>
    private static bool TimeWindowsOverlap(
        ScheduledEntry existing,
        Interview proposed,
        InterviewBuffer proposedBuffer)
    {
        var existingStart = existing.Interview.StartsAtUtc - existing.Buffer.Before;
        var existingEnd = existing.Interview.EndsAtUtc + existing.Buffer.After;
        var proposedStart = proposed.StartsAtUtc - proposedBuffer.Before;
        var proposedEnd = proposed.EndsAtUtc + proposedBuffer.After;

        // [start, end) 반열린 구간이라 한 일정의 end와 다음 start가 같으면 겹치지 않습니다.
        return proposedStart < existingEnd && existingStart < proposedEnd;
    }

    /// <summary>
    /// 저장된 일정과 당시 적용한 buffer를 함께 보존합니다.
    /// </summary>
    /// <param name="Interview">예약된 면접 일정입니다.</param>
    /// <param name="Buffer">예약할 때 적용된 앞뒤 여유 시간입니다.</param>
    private sealed record ScheduledEntry(Interview Interview, InterviewBuffer Buffer);
}
