// `namespace 이름;`은 이 파일 전체를 중괄호 없이 같은 이름 공간에 넣는 file-scoped namespace 문법입니다.
namespace OptimisticConcurrencyExercise;

// enum은 허용된 상태를 이름 있는 값으로 제한합니다. 문자열 오타를 막고 switch에서 빠진 경우를 찾기 쉽습니다.
public enum ArticleStatus
{
    Draft,
    Published,
}

// 수정 종류를 enum으로 표현하면 Application Service가 알맞은 Strategy를 안전하게 선택할 수 있습니다.
public enum RevisionAction
{
    Edit,
    Publish,
}

// positional record는 생성자와 읽기 전용 데이터 속성을 함께 만듭니다. 오류는 값으로 비교하기 좋아 record가 알맞습니다.
// CurrentVersion의 ?는 충돌처럼 현재 버전이 알려진 실패에서만 숫자가 있고, 일반 검증 실패에서는 null일 수 있음을 뜻합니다.
// Code는 분기용 안정 키, Message는 사용자용 안전한 설명, CurrentVersion은 선택적인 최신 버전입니다.
// 괄호 안 primary constructor가 세 값을 초기화하며 생성자는 객체를 만드는 역할이므로 반환값은 없습니다.
public sealed record Problem(string Code, string Message, int? CurrentVersion = null);

// Result<T>는 사용자가 고칠 수 있는 검증 실패와 동시성 충돌을 예외 대신 명시적인 값으로 전달합니다.
// T를 class로 제한하면 null을 성공값으로 허용하지 않아 "성공했지만 값 없음"이라는 모호한 상태를 막습니다.
public sealed class Result<T>
    where T : class
{
    // nullable reference의 ?는 이 필드가 상태에 따라 값이 없을 수 있음을 컴파일러와 독자에게 알립니다.
    private readonly T? _value;
    private readonly Problem? _problem;

    /// <summary>
    /// 성공값 또는 실패 정보를 한 객체에 보관합니다.
    /// value는 성공할 때의 값, problem은 실패할 때의 설명이며 둘 중 정확히 하나만 전달됩니다.
    /// 생성자는 객체를 초기화하므로 반환값은 없습니다.
    /// </summary>
    private Result(T? value, Problem? problem)
    {
        _value = value;
        _problem = problem;
    }

    // =>는 짧은 계산 속성을 표현하는 expression-bodied member입니다. is null은 null constant pattern입니다.
    public bool IsSuccess => _problem is null;

    public T Value
    {
        get
        {
            if (!IsSuccess)
            {
                throw new InvalidOperationException("실패 Result에서는 성공값을 읽을 수 없습니다.");
            }

            // !는 런타임 동작 없이 nullable 경고만 억제합니다. 성공 팩터리가 값을 항상 넣는다는 불변식을 근거로 씁니다.
            return _value!;
        }
    }

    public Problem Problem
    {
        get
        {
            if (IsSuccess)
            {
                throw new InvalidOperationException("성공 Result에서는 실패 정보를 읽을 수 없습니다.");
            }

            return _problem!;
        }
    }

    /// <summary>
    /// null이 아닌 성공값을 가진 Result를 만듭니다.
    /// value는 호출자에게 돌려줄 유효한 객체입니다.
    /// 반환값은 성공 상태의 Result입니다.
    /// </summary>
    public static Result<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Result<T>(value, problem: null);
    }

    /// <summary>
    /// 호출자가 분기하여 처리할 수 있는 실패 Result를 만듭니다.
    /// problem은 안정적인 코드와 안전한 설명을 가진 실패 정보입니다.
    /// 반환값은 실패 상태의 Result입니다.
    /// </summary>
    public static Result<T> Failure(Problem problem)
    {
        ArgumentNullException.ThrowIfNull(problem);
        return new Result<T>(value: null, problem);
    }
}

// 수정 요청은 검증을 통과한 뒤 바뀌지 않아야 하므로 get 전용 속성을 가진 불변 Domain 값으로 만듭니다.
public sealed record ArticleRevision
{
    public string ArticleId { get; }

    public string Title { get; }

    public string Body { get; }

    public RevisionAction Action { get; }

    public int ExpectedVersion { get; }

    /// <summary>
    /// 검증을 마친 문서 수정 요청을 초기화합니다.
    /// articleId는 문서 키, title과 body는 새 내용, action은 편집 종류, expectedVersion은 편집자가 읽었던 버전입니다.
    /// 생성자는 객체를 초기화하므로 반환값은 없습니다.
    /// </summary>
    private ArticleRevision(
        string articleId,
        string title,
        string body,
        RevisionAction action,
        int expectedVersion)
    {
        ArticleId = articleId;
        Title = title;
        Body = body;
        Action = action;
        ExpectedVersion = expectedVersion;
    }

    /// <summary>
    /// 외부 입력을 검사하여 저장 흐름에 들어갈 수 있는 수정 요청을 만듭니다.
    /// articleId는 대상 문서 키, title과 body는 새 내용, action은 편집 종류, expectedVersion은 화면에 표시됐던 양수 버전입니다.
    /// 반환값은 정규화된 ArticleRevision 또는 사용자가 고칠 수 있는 검증 Problem입니다.
    /// </summary>
    public static Result<ArticleRevision> Create(
        string? articleId,
        string? title,
        string? body,
        RevisionAction action,
        int expectedVersion)
    {
        if (string.IsNullOrWhiteSpace(articleId))
        {
            return Result<ArticleRevision>.Failure(
                new Problem("article.id_required", "문서 ID는 비워 둘 수 없습니다."));
        }

        if (articleId.Trim().Length > 40)
        {
            return Result<ArticleRevision>.Failure(
                new Problem("article.id_too_long", "문서 ID는 40자 이하여야 합니다."));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            return Result<ArticleRevision>.Failure(
                new Problem("article.title_required", "제목은 비워 둘 수 없습니다."));
        }

        if (title.Trim().Length > 80)
        {
            return Result<ArticleRevision>.Failure(
                new Problem("article.title_too_long", "제목은 80자 이하여야 합니다."));
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return Result<ArticleRevision>.Failure(
                new Problem("article.body_required", "본문은 비워 둘 수 없습니다."));
        }

        if (body.Trim().Length > 2_000)
        {
            return Result<ArticleRevision>.Failure(
                new Problem("article.body_too_long", "본문은 2,000자 이하여야 합니다."));
        }

        // Enum.IsDefined는 (RevisionAction)999처럼 숫자 강제 변환으로 들어온 정의되지 않은 값까지 거부합니다.
        if (!Enum.IsDefined(action))
        {
            return Result<ArticleRevision>.Failure(
                new Problem("article.action_invalid", "지원하지 않는 수정 종류입니다."));
        }

        if (expectedVersion <= 0)
        {
            return Result<ArticleRevision>.Failure(
                new Problem("article.version_invalid", "예상 버전은 1 이상이어야 합니다."));
        }

        return Result<ArticleRevision>.Success(
            new ArticleRevision(
                articleId.Trim(),
                title.Trim(),
                body.Trim(),
                action,
                expectedVersion));
    }
}

// 문서 aggregate는 저장 뒤 과거 스냅샷이 바뀌지 않도록 record와 private init 속성으로 불변성을 지킵니다.
public sealed record KnowledgeArticle
{
    // 발행 상태의 불변식은 특정 Strategy가 아니라 aggregate에 두어 다른 수정 경로도 우회하지 못하게 합니다.
    public const int MinimumPublishedBodyLength = 40;

    public string Id { get; private init; }

    public string Title { get; private init; }

    public string Body { get; private init; }

    public ArticleStatus Status { get; private init; }

    public int Version { get; private init; }

    /// <summary>
    /// 유효한 지식 문서 스냅샷을 초기화합니다.
    /// id는 문서 키, title과 body는 내용, status는 공개 상태, version은 저장할 때마다 증가하는 동시성 토큰입니다.
    /// 생성자는 객체를 초기화하므로 반환값은 없습니다.
    /// </summary>
    private KnowledgeArticle(string id, string title, string body, ArticleStatus status, int version)
    {
        Id = id;
        Title = title;
        Body = body;
        Status = status;
        Version = version;
    }

    /// <summary>
    /// 유효성 검사를 거쳐 버전 1의 초안 문서를 만듭니다.
    /// id는 새 문서 키, title은 제목, body는 초안 본문입니다.
    /// 반환값은 Draft 상태의 KnowledgeArticle 또는 잘못된 초기 데이터 Problem입니다.
    /// </summary>
    public static Result<KnowledgeArticle> CreateDraft(string? id, string? title, string? body)
    {
        Result<ArticleRevision> validated = ArticleRevision.Create(
            id,
            title,
            body,
            RevisionAction.Edit,
            expectedVersion: 1);

        if (!validated.IsSuccess)
        {
            return Result<KnowledgeArticle>.Failure(validated.Problem);
        }

        ArticleRevision revision = validated.Value;
        return Result<KnowledgeArticle>.Success(
            new KnowledgeArticle(revision.ArticleId, revision.Title, revision.Body, ArticleStatus.Draft, version: 1));
    }

    /// <summary>
    /// 현재 스냅샷은 그대로 두고 내용·상태·버전이 갱신된 다음 스냅샷을 만듭니다.
    /// revision은 검증된 새 내용, nextStatus는 Strategy가 결정한 다음 공개 상태입니다.
    /// 반환값은 Version이 정확히 1 증가한 새 KnowledgeArticle 또는 발행 불변식 실패입니다.
    /// </summary>
    internal Result<KnowledgeArticle> CreateNextRevision(
        ArticleRevision revision,
        ArticleStatus nextStatus)
    {
        ArgumentNullException.ThrowIfNull(revision);

        // 다른 Id·버전·enum은 검증된 내부 호출 계약이 깨진 프로그래밍 오류라 예외로 드러냅니다.
        // 반면 사용자가 고칠 수 있는 발행 본문 길이는 아래에서 실패 Result로 반환합니다.
        if (!string.Equals(Id, revision.ArticleId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("다른 문서의 수정 요청을 적용할 수 없습니다.");
        }

        if (Version != revision.ExpectedVersion)
        {
            throw new InvalidOperationException("현재 스냅샷과 수정 요청의 예상 버전이 다릅니다.");
        }

        if (!Enum.IsDefined(nextStatus))
        {
            throw new ArgumentOutOfRangeException(nameof(nextStatus), "정의되지 않은 문서 상태입니다.");
        }

        if (nextStatus == ArticleStatus.Published && revision.Body.Length < MinimumPublishedBodyLength)
        {
            return Result<KnowledgeArticle>.Failure(
                new Problem(
                    "article.body_too_short_to_publish",
                    $"발행 상태의 본문은 {MinimumPublishedBodyLength}자 이상이어야 합니다."));
        }

        // with 식은 원본 record를 변경하지 않고 지정한 속성만 바꾼 복사본을 만듭니다. 동시 요청이 같은 객체를 바꾸지 않게 합니다.
        return Result<KnowledgeArticle>.Success(
            this with
            {
                Title = revision.Title,
                Body = revision.Body,
                Status = nextStatus,
                // checked는 매우 드문 int 범위 초과도 조용히 음수로 순환시키지 않고 구성/데이터 오류로 드러냅니다.
                Version = checked(Version + 1),
            });
    }
}

// 저장 성공 뒤 화면과 로그에 필요한 최소 정보만 전달하는 불변 결과입니다.
// ArticleId는 문서 키, PreviousVersion은 비교한 버전, CurrentVersion은 저장된 버전, Status는 저장 뒤 공개 상태입니다.
// positional record의 primary constructor가 네 값을 초기화하며 생성자는 객체를 만드는 역할이므로 반환값은 없습니다.
public sealed record RevisionReceipt(
    string ArticleId,
    int PreviousVersion,
    int CurrentVersion,
    ArticleStatus Status);
