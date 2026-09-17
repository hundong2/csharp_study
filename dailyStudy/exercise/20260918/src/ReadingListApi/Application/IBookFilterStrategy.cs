using ReadingListApi.Domain;

namespace ReadingListApi.Application;

// Strategy는 목록을 고르는 규칙을 교체할 수 있게 합니다. 저장소의 역할과 검색 규칙을 분리합니다.
public interface IBookFilterStrategy
{
    /// <summary>
    /// books에서 status에 맞는 책을 고릅니다. status가 null이면 전체를 고르며 새 목록을 반환합니다.
    /// </summary>
    IReadOnlyList<Book> Filter(IReadOnlyList<Book> books, BookStatus? status);
}
