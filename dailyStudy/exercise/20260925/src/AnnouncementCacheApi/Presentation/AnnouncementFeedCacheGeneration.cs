namespace AnnouncementCacheApi.Presentation;

/// <summary>
/// 성공한 공지 쓰기마다 증가해 이전 세대의 목록 캐시를 새 요청이 재사용하지 못하게 합니다.
/// </summary>
public sealed class AnnouncementFeedCacheGeneration
{
    private long _current;

    /// <summary>
    /// 현재 process에서 마지막으로 완료된 공지 쓰기 세대를 thread-safe하게 읽습니다.
    /// </summary>
    // =>는 중괄호와 return을 생략하고 식 하나의 결과를 반환하는 expression-bodied member입니다.
    // Interlocked.Read를 사용해 64비트 세대 값을 여러 요청이 동시에 읽어도 찢어진 값을 보지 않게 합니다.
    public long Current => Interlocked.Read(ref _current);

    /// <summary>
    /// 공지 저장 성공 직후 세대를 원자적으로 한 단계 올립니다.
    /// </summary>
    /// <returns>증가가 끝난 새 세대 번호를 반환합니다.</returns>
    public long Advance()
    {
        return Interlocked.Increment(ref _current);
    }
}
