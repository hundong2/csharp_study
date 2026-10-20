using AuditEventApi.Domain;

namespace AuditEventApi.Application.Ports;

/// <summary>
/// 한 이벤트 종류의 Application 입력을 올바른 Domain 팩터리로 연결하는 Strategy Port입니다.
/// </summary>
/// <remarks>
/// 서비스의 switch 문을 계속 늘리는 대신 Strategy를 추가하게 하므로 기존 유스케이스를 덜 수정하는 개방-폐쇄 원칙을 연습할 수 있습니다.
/// </remarks>
public interface IAuditEventMapper
{
    /// <summary>
    /// 이 Strategy가 처리하는 정규화된 이벤트 종류를 가져옵니다.
    /// </summary>
    /// <returns>서비스의 Strategy 선택 키로 사용할 종류 문자열입니다.</returns>
    string SupportedKind { get; }

    /// <summary>
    /// 안정적인 Application command에서 필요한 필드를 골라 Domain 정규화와 검증을 실행합니다.
    /// </summary>
    /// <param name="command">Presentation 형식을 알지 못하는 공통 유스케이스 입력입니다.</param>
    /// <returns>검증 완료 AuditEventDraft 또는 구체적인 Domain 오류를 반환합니다.</returns>
    Result<AuditEventDraft> Map(IngestAuditEventCommand command);
}
