namespace DeploymentSseApi.Domain;

/// <summary>
/// 게시와 구독이 정확히 같은 배포 식별자 규칙을 사용하도록 정규화를 한곳에 모읍니다.
/// </summary>
public static class DeploymentIdentity
{
    /// <summary>
    /// 외부 배포 ID의 앞뒤 공백, 길이, 허용 문자를 검사합니다.
    /// </summary>
    /// <param name="deploymentId">route나 요청에서 받은 아직 신뢰할 수 없는 식별자입니다.</param>
    /// <returns>앞뒤 공백을 제거한 ID 또는 deploymentId 필드 오류를 반환합니다.</returns>
    public static Result<string> Normalize(string? deploymentId)
    {
        var normalized = deploymentId?.Trim();

        // `is < 1 or > 64`는 최솟값과 최댓값 밖을 한눈에 보여 주는 관계 패턴입니다.
        // All에 method group을 넘기면 반복문 상태 없이 "모든 문자가 허용되는가"라는 의도를 직접 표현할 수 있습니다.
        if (string.IsNullOrWhiteSpace(normalized) ||
            normalized.Length is < 1 or > 64 ||
            !normalized.All(IsAllowedCharacter))
        {
            return Result<string>.Failure(
                new Error(
                    "deployment.id.invalid",
                    "deploymentId는 1~64자의 영문, 숫자, 하이픈 또는 밑줄이어야 합니다.",
                    "deploymentId"));
        }

        return Result<string>.Success(normalized);
    }

    /// <summary>
    /// 배포 ID 문자 하나가 URL과 저장소 key에 안전한 문자 범위인지 검사합니다.
    /// </summary>
    /// <param name="character">배포 ID에서 검사할 문자 하나입니다.</param>
    /// <returns>ASCII 영문·숫자·하이픈·밑줄이면 true, 아니면 false를 반환합니다.</returns>
    private static bool IsAllowedCharacter(char character)
    {
        return char.IsAsciiLetterOrDigit(character) || character is '-' or '_';
    }
}
