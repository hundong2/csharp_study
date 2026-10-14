namespace InvitationTokenApi.Domain;

/// <summary>
/// 프로젝트에 초대받은 사용자의 권한 범위를 제한합니다.
/// </summary>
public enum ProjectRole
{
    Member,
    Admin
}

/// <summary>
/// 초대가 아직 사용 가능한지, 이미 사용됐는지를 표현합니다.
/// </summary>
public enum InvitationStatus
{
    Issued,
    Redeemed
}

/// <summary>
/// 검증과 정규화를 마친 프로젝트 초대를 불변 값으로 보관하는 Domain Model입니다.
/// </summary>
public sealed record ProjectInvitation
{
    /// <summary>
    /// 검증을 통과한 값만 초대 상태가 되도록 private 생성자로 초기화합니다.
    /// </summary>
    /// <param name="invitationId">대문자로 정규화된 초대 식별자입니다.</param>
    /// <param name="projectId">대문자로 정규화된 프로젝트 식별자입니다.</param>
    /// <param name="email">소문자로 정규화된 초대 대상 이메일입니다.</param>
    /// <param name="role">초대 후 부여할 Member 또는 Admin 권한입니다.</param>
    /// <param name="issuedAtUtc">초대를 발급한 UTC 시각입니다.</param>
    /// <param name="expiresAtUtc">토큰을 더 이상 받지 않을 UTC 시각입니다.</param>
    /// <param name="redeemedAtUtc">실제 사용 시각이며 아직 사용하지 않았으면 null입니다.</param>
    /// <returns>생성자는 새 Domain 객체를 초기화하므로 별도 반환값은 없습니다.</returns>
    private ProjectInvitation(
        string invitationId,
        string projectId,
        string email,
        ProjectRole role,
        DateTimeOffset issuedAtUtc,
        DateTimeOffset expiresAtUtc,
        DateTimeOffset? redeemedAtUtc)
    {
        InvitationId = invitationId;
        ProjectId = projectId;
        Email = email;
        Role = role;
        IssuedAtUtc = issuedAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        RedeemedAtUtc = redeemedAtUtc;
    }

    public string InvitationId { get; }

    public string ProjectId { get; }

    public string Email { get; }

    public ProjectRole Role { get; }

    public DateTimeOffset IssuedAtUtc { get; }

    public DateTimeOffset ExpiresAtUtc { get; }

    // private init은 이 record 내부의 with 전이만 값을 바꾸게 해 외부 코드가 사용 완료 시각을 위조하지 못하게 합니다.
    public DateTimeOffset? RedeemedAtUtc { get; private init; }

    // =>는 오른쪽 식 하나를 계산해 반환하는 짧은 속성 문법입니다. `조건 ? A : B`는 조건에 따라 둘 중 하나를 고르는 삼항 연산자입니다.
    // 저장된 redeemed 시각에서 상태를 파생해 별도 상태 필드와 시각이 어긋나는 일을 막습니다.
    public InvitationStatus Status => RedeemedAtUtc is null
        ? InvitationStatus.Issued
        : InvitationStatus.Redeemed;

    /// <summary>
    /// 외부 문자열과 발급 시각을 검증·정규화해 유효한 초대를 만듭니다.
    /// </summary>
    /// <param name="invitationId">클라이언트가 보낸 아직 검증하지 않은 초대 ID입니다.</param>
    /// <param name="projectId">클라이언트가 보낸 아직 검증하지 않은 프로젝트 ID입니다.</param>
    /// <param name="email">클라이언트가 보낸 아직 검증하지 않은 이메일입니다.</param>
    /// <param name="role">이미 허용 목록으로 검증한 권한입니다.</param>
    /// <param name="issuedAtUtc">TimeProvider에서 읽은 발급 시각입니다.</param>
    /// <param name="expiresAtUtc">만료 Strategy가 계산한 만료 시각입니다.</param>
    /// <returns>유효한 불변 초대 또는 어느 필드가 잘못됐는지 담은 실패를 반환합니다.</returns>
    public static Result<ProjectInvitation> Create(
        string? invitationId,
        string? projectId,
        string? email,
        ProjectRole role,
        DateTimeOffset issuedAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        // `is not (... or ...)` pattern은 숫자 cast로 만든 정의되지 않은 enum 값까지 허용 목록 밖으로 거절합니다.
        if (role is not (ProjectRole.Member or ProjectRole.Admin))
        {
            return Result<ProjectInvitation>.Failure(
                new Error(
                    "invitation.role.invalid",
                    "role은 Member 또는 Admin이어야 합니다.",
                    "role"));
        }

        var normalizedInvitationId = NormalizeIdentifier(
            invitationId,
            "invitation.id.invalid",
            "invitationId",
            "초대 ID는 3~40자의 영문, 숫자, 하이픈이어야 합니다.");
        if (!normalizedInvitationId.IsSuccess)
        {
            // postfix !는 앞의 실패 검사로 Error가 null이 아님을 증명했음을 nullable 분석기에 알리며 runtime 검사를 대신하지 않습니다.
            return Result<ProjectInvitation>.Failure(normalizedInvitationId.Error!);
        }

        var normalizedProjectId = NormalizeIdentifier(
            projectId,
            "invitation.project_id.invalid",
            "projectId",
            "프로젝트 ID는 3~40자의 영문, 숫자, 하이픈이어야 합니다.");
        if (!normalizedProjectId.IsSuccess)
        {
            return Result<ProjectInvitation>.Failure(normalizedProjectId.Error!);
        }

        var normalizedEmail = NormalizeEmail(email);
        if (!normalizedEmail.IsSuccess)
        {
            return Result<ProjectInvitation>.Failure(normalizedEmail.Error!);
        }

        // 외부 입력 오류는 Result로 돌려주지만, 내부 호출자가 만든 역전된 시간은 프로그래밍 불변식 위반이므로 예외로 즉시 드러냅니다.
        if (expiresAtUtc <= issuedAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expiresAtUtc),
                "만료 시각은 발급 시각보다 뒤여야 합니다.");
        }

        return Result<ProjectInvitation>.Success(
            new ProjectInvitation(
                normalizedInvitationId.Value!,
                normalizedProjectId.Value!,
                normalizedEmail.Value!,
                role,
                issuedAtUtc.ToUniversalTime(),
                expiresAtUtc.ToUniversalTime(),
                null));
    }

    /// <summary>
    /// HTTP 문자열 권한을 허용된 Member 또는 Admin enum 값으로 바꿉니다.
    /// </summary>
    /// <param name="role">대소문자와 앞뒤 공백이 아직 정리되지 않은 권한 문자열입니다.</param>
    /// <returns>허용된 권한 또는 role 오류를 반환합니다.</returns>
    public static Result<ProjectRole> ParseRole(string? role)
    {
        // ?.는 role이 null이면 Trim을 호출하지 않고 null을 유지합니다. out var는 파싱 결과를 새 지역 변수로 선언하며 받습니다.
        var normalized = role?.Trim();
        // pattern matching의 `is ... or ...`는 파싱된 값이 허용한 두 enum 중 하나인지 읽기 쉽게 제한합니다.
        if (Enum.TryParse<ProjectRole>(normalized, ignoreCase: true, out var parsed) &&
            parsed is ProjectRole.Member or ProjectRole.Admin)
        {
            return Result<ProjectRole>.Success(parsed);
        }

        return Result<ProjectRole>.Failure(
            new Error(
                "invitation.role.invalid",
                "role은 Member 또는 Admin이어야 합니다.",
                "role"));
    }

    /// <summary>
    /// 조회 route의 초대 ID를 저장소 key와 같은 규칙으로 정규화합니다.
    /// </summary>
    /// <param name="invitationId">route에서 받은 아직 검증하지 않은 초대 ID입니다.</param>
    /// <returns>대문자 ID 또는 invitationId 오류를 반환합니다.</returns>
    public static Result<string> NormalizeInvitationId(string? invitationId)
    {
        return NormalizeIdentifier(
            invitationId,
            "invitation.id.invalid",
            "invitationId",
            "초대 ID는 3~40자의 영문, 숫자, 하이픈이어야 합니다.");
    }

    /// <summary>
    /// 아직 사용하지 않은 초대를 지정 시각에 사용 완료한 새 불변 객체로 전환합니다.
    /// </summary>
    /// <param name="redeemedAtUtc">TimeProvider에서 한 번 읽어 전체 요청에 사용하는 UTC 시각입니다.</param>
    /// <returns>사용 완료된 새 초대, 이미 사용됨, 또는 만료 오류를 반환합니다.</returns>
    public Result<ProjectInvitation> Redeem(DateTimeOffset redeemedAtUtc)
    {
        if (RedeemedAtUtc is not null)
        {
            return Result<ProjectInvitation>.Failure(
                new Error(
                    "invitation.already_redeemed",
                    "이미 사용한 초대 토큰입니다.",
                    "token"));
        }

        if (redeemedAtUtc < IssuedAtUtc)
        {
            return Result<ProjectInvitation>.Failure(
                new Error(
                    "invitation.token.invalid_or_expired",
                    "초대 토큰이 올바르지 않거나 만료되었습니다.",
                    "token"));
        }

        if (redeemedAtUtc >= ExpiresAtUtc)
        {
            return Result<ProjectInvitation>.Failure(
                new Error(
                    "invitation.token.invalid_or_expired",
                    "초대 토큰이 올바르지 않거나 만료되었습니다.",
                    "token"));
        }

        // with는 원본 record를 바꾸지 않고 지정 속성만 달라진 새 복사본을 만들어 불변성을 지킵니다.
        return Result<ProjectInvitation>.Success(
            this with { RedeemedAtUtc = redeemedAtUtc.ToUniversalTime() });
    }

    /// <summary>
    /// ID의 공백, 길이, 허용 문자를 한 규칙으로 검사하고 대문자 key로 바꿉니다.
    /// </summary>
    /// <param name="value">검사할 외부 입력 문자열입니다.</param>
    /// <param name="code">실패 때 사용할 안정 오류 코드입니다.</param>
    /// <param name="field">실패가 연결될 입력 필드입니다.</param>
    /// <param name="message">사용자에게 보여 줄 안전한 설명입니다.</param>
    /// <returns>정규화된 ID 또는 전달받은 오류 정보를 반환합니다.</returns>
    private static Result<string> NormalizeIdentifier(
        string? value,
        string code,
        string field,
        string message)
    {
        var normalized = value?.Trim().ToUpperInvariant();
        // `is < 3 or > 40`은 범위를 읽기 쉽게 표현하는 relational pattern입니다.
        // All(IsAllowedIdentifierCharacter)은 LINQ와 method group으로 모든 문자가 규칙을 만족하는지 선언해 수동 반복문의 상태 관리를 없앱니다.
        if (string.IsNullOrWhiteSpace(normalized) ||
            normalized.Length is < 3 or > 40 ||
            !normalized.All(IsAllowedIdentifierCharacter))
        {
            return Result<string>.Failure(new Error(code, message, field));
        }

        return Result<string>.Success(normalized);
    }

    /// <summary>
    /// 초대 대상 이메일을 최소한의 학습용 규칙으로 검사하고 소문자로 정규화합니다.
    /// </summary>
    /// <param name="email">검사할 외부 이메일 문자열입니다.</param>
    /// <returns>정규화된 이메일 또는 email 오류를 반환합니다.</returns>
    private static Result<string> NormalizeEmail(string? email)
    {
        var normalized = email?.Trim().ToLowerInvariant();
        // Count의 `character => ...`는 문자마다 조건을 검사하는 lambda이고, Any(char.IsWhiteSpace)는 method group으로 공백 존재 여부를 묻습니다.
        // LINQ를 사용하면 “@가 정확히 하나이고 공백은 없어야 한다”라는 검증 의도가 반복문보다 직접 보입니다.
        if (string.IsNullOrWhiteSpace(normalized) ||
            normalized.Length > 254 ||
            normalized.Count(character => character == '@') != 1 ||
            normalized.StartsWith('@') ||
            normalized.EndsWith('@') ||
            normalized.Any(char.IsWhiteSpace))
        {
            return Result<string>.Failure(
                new Error(
                    "invitation.email.invalid",
                    "email은 하나의 @를 포함한 유효한 주소 형식이어야 합니다.",
                    "email"));
        }

        return Result<string>.Success(normalized);
    }

    /// <summary>
    /// ID 문자 하나가 영문, 숫자, 하이픈 중 하나인지 검사합니다.
    /// </summary>
    /// <param name="character">ID에서 검사할 문자 하나입니다.</param>
    /// <returns>허용 문자이면 true, 아니면 false를 반환합니다.</returns>
    private static bool IsAllowedIdentifierCharacter(char character)
    {
        return char.IsAsciiLetterOrDigit(character) || character == '-';
    }
}
