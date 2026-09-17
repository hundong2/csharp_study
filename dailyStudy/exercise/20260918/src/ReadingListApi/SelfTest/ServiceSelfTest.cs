using ReadingListApi.Application;
using ReadingListApi.Domain;
using ReadingListApi.Infrastructure;

namespace ReadingListApi.SelfTest;

public static class ServiceSelfTest
{
    /// <summary>
    /// 저장, 조회, 필터, 오류 규칙을 검사합니다. 매개변수는 없으며 모두 통과하면 0을 반환하고 실패하면 예외를 던집니다.
    /// </summary>
    public static int Run()
    {
        // var는 오른쪽에서 만들어지는 BookService 형식을 컴파일러가 추론합니다.
        var service = new BookService(new InMemoryBookRepository(), new StatusBookFilterStrategy());

        Assert(!service.Create(" ", "저자").IsSuccess, "빈 제목 거절");
        Assert(!service.Create("제목", null).IsSuccess, "누락된 저자 거절");
        Assert(!service.Create(new string('가', 101), "저자").IsSuccess, "긴 제목 거절");

        ServiceResult<Book> created = service.Create("  C# 첫걸음  ", "  홍길동  ");
        // is not null은 값이 실제로 있는지 확인하는 패턴입니다. 뒤에서 안전하게 책 정보를 읽기 위해 검사합니다.
        Assert(created.IsSuccess && created.Value is not null, "책 등록");
        // !는 위의 성공 검사가 끝났으므로 Value가 null이 아님을 컴파일러에 알려줍니다.
        Book book = created.Value!;
        Assert(book.Title == "C# 첫걸음" && book.Author == "홍길동", "앞뒤 공백 정리");
        Assert(book.Status == BookStatus.ToRead, "초기 상태");
        Assert(service.Get(book.Id).Value == book, "등록한 책 조회");
        Assert(service.Get(Guid.NewGuid()).ErrorKind == ServiceErrorKind.NotFound, "없는 책 구분");
        Assert(service.List(null).Value?.Count == 1, "전체 목록");
        Assert(service.List("toRead").Value?.Count == 1, "상태 필터");
        Assert(service.List("completed").Value?.Count == 0, "일치하지 않는 필터");
        Assert(service.List("7").ErrorKind == ServiceErrorKind.Validation, "숫자 상태 거절");

        Console.WriteLine("PASS: 등록, 검증, 조회, 상태 필터, 오류 처리 (12개 검사)");
        return 0;
    }

    /// <summary>
    /// condition을 확인합니다. name은 실패 시 표시할 검사 이름이며 성공하면 반환값이 없고 실패하면 예외를 던집니다.
    /// </summary>
    private static void Assert(bool condition, string name)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"자체 검증 실패: {name}");
        }
    }
}
