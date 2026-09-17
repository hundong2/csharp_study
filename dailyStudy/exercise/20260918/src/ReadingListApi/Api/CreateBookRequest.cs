namespace ReadingListApi.Api;

/// <summary>POST 요청의 입력값을 담습니다. title은 제목, author는 저자이며 생성자 반환값은 없습니다.</summary>
/// <param name="Title">등록할 책의 제목입니다.</param>
/// <param name="Author">등록할 책의 저자입니다.</param>
// record는 입력 데이터를 묶는 자료형이고, ?는 누락된 값이 null일 수 있음을 표시합니다.
public sealed record CreateBookRequest(string? Title, string? Author);
