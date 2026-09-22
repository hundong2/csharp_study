namespace ConditionalCatalogApi.Domain;

/// <summary>
/// 상품 이름·가격·버전 규칙을 스스로 지키는 Domain Model입니다.
/// record는 값 중심 모델에 잘 맞고, get 전용 속성과 private 생성자는 생성·변경 검증을 우회하지 못하게 해 불변성을 지킵니다.
/// </summary>
public sealed record CatalogItem
{
    /// <summary>
    /// 검증을 마친 상품 상태를 만듭니다. 외부에서는 Create 또는 Revise를 통해서만 이 생성자에 도달합니다.
    /// </summary>
    /// <param name="id">상품을 구분하는 변경되지 않는 식별자입니다.</param>
    /// <param name="name">앞뒤 공백을 제거하고 검증한 표시 이름입니다.</param>
    /// <param name="price">검증한 원화 가격입니다.</param>
    /// <param name="version">상태가 바뀔 때마다 1씩 커지는 동시성 버전입니다.</param>
    /// <returns>생성자는 값을 반환하지 않고 새 CatalogItem 인스턴스를 초기화합니다.</returns>
    private CatalogItem(Guid id, string name, decimal price, long version)
    {
        Id = id;
        Name = name;
        Price = price;
        Version = version;
    }

    /// <summary>
    /// 상품의 변경되지 않는 식별자를 반환합니다.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// 검증되고 앞뒤 공백이 제거된 상품 이름을 반환합니다.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 0보다 크고 소수 둘째 자리까지인 원화 가격을 반환합니다.
    /// </summary>
    public decimal Price { get; }

    /// <summary>
    /// ETag와 원자적 저장 비교에 사용하는 양의 버전을 반환합니다.
    /// </summary>
    public long Version { get; }

    /// <summary>
    /// 새 상품의 식별자·이름·가격을 검증하고 최초 버전 1의 상품을 만듭니다.
    /// </summary>
    /// <param name="id">비어 있으면 안 되는 새 상품 식별자입니다.</param>
    /// <param name="name">null일 수 있는 사용자 입력 이름입니다.</param>
    /// <param name="price">사용자가 입력한 원화 가격입니다.</param>
    /// <returns>유효하면 새 상품, 아니면 사용자가 고칠 오류를 담은 Result를 반환합니다.</returns>
    public static Result<CatalogItem> Create(Guid id, string? name, decimal price)
    {
        if (id == Guid.Empty)
        {
            return Result<CatalogItem>.Failure(
                new DomainError("ID_EMPTY", "상품 ID는 비어 있을 수 없습니다."));
        }

        // var는 오른쪽 식으로 지역 변수 타입을 compile 시점에 추론하며, 여기서는 string?으로 고정됩니다.
        // ?.는 왼쪽 값이 null이면 메서드를 호출하지 않고 null을 돌려주는 null 조건 연산자입니다.
        // JSON에서 이름이 빠진 경계를 안전하게 처리한 뒤 Domain 내부에는 null이 들어오지 않게 합니다.
        var normalizedName = name?.Trim();
        var error = Validate(normalizedName, price);

        // is not null은 값이 실제로 존재하는지 검사하는 pattern matching 문법입니다.
        // 오류가 있을 때만 실패 Result를 만들고, 이후 정상 경로에서는 검증된 값을 사용하기 위해 분기합니다.
        if (error is not null)
        {
            return Result<CatalogItem>.Failure(error);
        }

        // ??는 왼쪽이 null이면 오른쪽 값을 사용하는 null 병합 연산자입니다.
        // 위 검증과 코드가 어긋나는 프로그래밍 오류를 숨기지 않으면서 compiler에도 이후 이름이 non-null임을 알려 줍니다.
        var validName = normalizedName
            ?? throw new InvalidOperationException("검증을 통과한 상품 이름이 null입니다.");
        return Result<CatalogItem>.Success(new CatalogItem(id, validName, price, version: 1));
    }

    /// <summary>
    /// 새 이름과 가격을 검증하고 버전을 하나 높인 새 상품 상태를 만듭니다.
    /// </summary>
    /// <param name="name">null일 수 있는 수정 요청의 상품 이름입니다.</param>
    /// <param name="price">수정 요청의 원화 가격입니다.</param>
    /// <returns>유효하면 기존 객체를 바꾸지 않은 새 버전의 상품, 아니면 Domain 오류를 반환합니다.</returns>
    public Result<CatalogItem> Revise(string? name, decimal price)
    {
        var normalizedName = name?.Trim();
        var error = Validate(normalizedName, price);
        if (error is not null)
        {
            return Result<CatalogItem>.Failure(error);
        }

        // checked는 정수 범위를 넘는 버전 증가를 조용히 뒤집지 않고 예외로 드러냅니다.
        // long.MaxValue 도달은 사용자가 고칠 입력 실패가 아니라 저장 모델의 시스템 한계이므로 Result로 숨기지 않습니다.
        var nextVersion = checked(Version + 1);
        var validName = normalizedName
            ?? throw new InvalidOperationException("검증을 통과한 상품 이름이 null입니다.");
        return Result<CatalogItem>.Success(new CatalogItem(Id, validName, price, nextVersion));
    }

    /// <summary>
    /// 이름과 가격이 모든 생성·변경 경로에서 같은 규칙을 따르는지 검사합니다.
    /// </summary>
    /// <param name="normalizedName">앞뒤 공백을 제거했거나 null인 상품 이름입니다.</param>
    /// <param name="price">검사할 원화 가격입니다.</param>
    /// <returns>규칙을 어기면 첫 Domain 오류, 모두 유효하면 null을 반환합니다.</returns>
    private static DomainError? Validate(string? normalizedName, decimal price)
    {
        // is null과 or는 pattern matching 문법입니다. null과 빈 문자열을 한 조건으로 읽기 쉽게 묶습니다.
        if (normalizedName is null or "")
        {
            return new DomainError("NAME_REQUIRED", "상품 이름을 입력하세요.");
        }

        // is < 2 or > 80은 관계 pattern 두 개를 or로 묶어 허용 범위 밖의 길이를 읽기 쉽게 표현합니다.
        if (normalizedName.Length is < 2 or > 80)
        {
            return new DomainError("NAME_LENGTH", "상품 이름은 2~80 UTF-16 코드 단위여야 합니다.");
        }

        if (price is < 0.01m or > 1_000_000_000m)
        {
            return new DomainError("PRICE_RANGE", "가격은 0.01원 이상 10억 원 이하여야 합니다.");
        }

        if (decimal.Round(price, decimals: 2, MidpointRounding.ToEven) != price)
        {
            return new DomainError("PRICE_SCALE", "가격은 소수 둘째 자리까지만 입력할 수 있습니다.");
        }

        return null;
    }
}
