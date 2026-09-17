using ReadingListApi.Application;
using ReadingListApi.Domain;

namespace ReadingListApi.Api;

public static class BookEndpoints
{
    /// <summary>
    /// 책 관련 URL과 처리 메서드를 연결합니다. app은 실행 중인 웹 앱이며 반환값은 없습니다.
    /// </summary>
    // this는 WebApplication에서 app.MapBookEndpoints()처럼 호출할 수 있게 하는 확장 메서드 문법입니다.
    public static void MapBookEndpoints(this WebApplication app)
    {
        // Route Group은 같은 /books 접두어를 한곳에 묶어 URL 실수를 줄입니다.
        RouteGroupBuilder books = app.MapGroup("/books");
        books.MapPost("", CreateBook);
        books.MapGet("/{id:guid}", GetBook);
        books.MapGet("", ListBooks);
    }

    /// <summary>
    /// request를 검증해 책을 등록합니다. service는 등록 규칙을 실행하며 성공 시 201, 입력 오류 시 400 응답을 반환합니다.
    /// </summary>
    private static IResult CreateBook(CreateBookRequest request, BookService service)
    {
        ServiceResult<Book> result = service.Create(request.Title, request.Author);
        if (!result.IsSuccess)
        {
            // ValidationProblem은 필드별 오류를 표준 ProblemDetails 형식으로 응답합니다.
            // ??는 왼쪽 설명이 null일 때만 오른쪽 기본 문장을 선택합니다.
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["book"] = new[] { result.ErrorMessage ?? "입력값을 확인하세요." }
            });
        }

        // !는 앞의 성공 검사로 값이 null이 아님을 컴파일러에 알립니다. 성공 분기에서만 사용합니다.
        Book book = result.Value!;
        return TypedResults.Created($"/books/{book.Id}", book);
    }

    /// <summary>
    /// id에 해당하는 책을 조회합니다. service는 검색을 수행하며 찾으면 200, 없으면 404 ProblemDetails를 반환합니다.
    /// </summary>
    private static IResult GetBook(Guid id, BookService service)
    {
        ServiceResult<Book> result = service.Get(id);
        if (!result.IsSuccess)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "책을 찾을 수 없습니다.",
                detail: result.ErrorMessage);
        }

        return TypedResults.Ok(result.Value);
    }

    /// <summary>
    /// status가 가리키는 책 목록을 조회합니다. service는 검색을 수행하며 성공 시 200, 잘못된 상태면 400 ProblemDetails를 반환합니다.
    /// </summary>
    private static IResult ListBooks(string? status, BookService service)
    {
        ServiceResult<IReadOnlyList<Book>> result = service.List(status);
        if (!result.IsSuccess)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "올바르지 않은 검색 조건입니다.",
                detail: result.ErrorMessage);
        }

        return TypedResults.Ok(result.Value);
    }
}
