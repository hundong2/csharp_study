using ReadingListApi.Domain;

namespace ReadingListApi.Application;

// Application Service는 입력 검증과 사용 사례를 맡습니다. HTTP 세부 사항을 몰라 자체 검증이 쉽습니다.
public sealed class BookService
{
    private readonly IBookRepository _repository;
    private readonly IBookFilterStrategy _filter;

    /// <summary>
    /// 책 저장소와 검색 전략을 받습니다. repository는 저장 작업, filter는 목록 검색을 담당하며 생성자 반환값은 없습니다.
    /// </summary>
    public BookService(IBookRepository repository, IBookFilterStrategy filter)
    {
        _repository = repository;
        _filter = filter;
    }

    /// <summary>
    /// title과 author를 확인한 뒤 읽을 책을 등록합니다. 각 매개변수는 사용자가 입력한 제목과 저자이며 성공 시 책을 반환합니다.
    /// </summary>
    public ServiceResult<Book> Create(string? title, string? author)
    {
        // ?는 null이 들어올 수 있음을 표시합니다. 요청 값이 빠져도 프로그램이 예외 없이 검증할 수 있습니다.
        if (string.IsNullOrWhiteSpace(title))
        {
            return ServiceResult<Book>.Failure(ServiceErrorKind.Validation, "제목을 입력하세요.");
        }

        if (string.IsNullOrWhiteSpace(author))
        {
            return ServiceResult<Book>.Failure(ServiceErrorKind.Validation, "저자를 입력하세요.");
        }

        if (title.Trim().Length > 100 || author.Trim().Length > 100)
        {
            return ServiceResult<Book>.Failure(ServiceErrorKind.Validation, "제목과 저자는 각각 100자 이하여야 합니다.");
        }

        // var는 오른쪽의 new Book(...)에서 자료형 Book을 추론합니다. 형식이 분명해 중복 표기를 줄입니다.
        var book = new Book(Guid.NewGuid(), title.Trim(), author.Trim(), BookStatus.ToRead);
        _repository.Add(book);
        return ServiceResult<Book>.Success(book);
    }

    /// <summary>
    /// id와 일치하는 책을 찾습니다. id는 책의 고유 번호이며 있으면 책을, 없으면 NotFound 결과를 반환합니다.
    /// </summary>
    public ServiceResult<Book> Get(Guid id)
    {
        // ?는 책이 없을 수도 있음을 표시합니다. is null은 그 경우를 검사하고, 조건 ? 참일 때 값 : 거짓일 때 값 형태의 삼항식은 둘 중 하나를 고릅니다.
        // Repository의 '없음'을 HTTP 404와 독립적인 서비스 결과로 바꿉니다.
        Book? book = _repository.Find(id);
        return book is null
            ? ServiceResult<Book>.Failure(ServiceErrorKind.NotFound, "해당 책을 찾을 수 없습니다.")
            : ServiceResult<Book>.Success(book);
    }

    /// <summary>
    /// status 문자열에 해당하는 책을 조회합니다. status를 생략하면 전체를 반환하며 잘못된 값이면 Validation 결과를 반환합니다.
    /// </summary>
    public ServiceResult<IReadOnlyList<Book>> List(string? status)
    {
        // out은 메서드가 bool 결과와 함께 변환된 상태도 돌려주게 합니다.
        if (!TryParseStatus(status, out BookStatus? parsedStatus))
        {
            return ServiceResult<IReadOnlyList<Book>>.Failure(
                ServiceErrorKind.Validation, "status는 toRead, reading, completed 중 하나여야 합니다.");
        }

        return ServiceResult<IReadOnlyList<Book>>.Success(_filter.Filter(_repository.All(), parsedStatus));
    }

    /// <summary>
    /// text를 허용된 상태로 변환합니다. status에는 변환 결과를 담고, 성공 여부를 bool로 반환합니다.
    /// </summary>
    private static bool TryParseStatus(string? text, out BookStatus? status)
    {
        // out은 호출자에게 변환된 값을 돌려주는 매개변수입니다. 숫자 enum 입력을 막기 위해 이름을 명시적으로 검사합니다.
        status = null;
        if (text is null)
        {
            return true;
        }

        if (text.Equals("toRead", StringComparison.OrdinalIgnoreCase))
        {
            status = BookStatus.ToRead;
        }
        else if (text.Equals("reading", StringComparison.OrdinalIgnoreCase))
        {
            status = BookStatus.Reading;
        }
        else if (text.Equals("completed", StringComparison.OrdinalIgnoreCase))
        {
            status = BookStatus.Completed;
        }
        else
        {
            return false;
        }

        return true;
    }
}
