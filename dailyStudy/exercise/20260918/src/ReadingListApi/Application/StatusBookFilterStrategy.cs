using ReadingListApi.Domain;

namespace ReadingListApi.Application;

public sealed class StatusBookFilterStrategy : IBookFilterStrategy
{
    /// <summary>
    /// books에서 status와 일치하는 책을 고르고 제목순으로 정렬합니다. status가 null이면 전체를 반환합니다.
    /// </summary>
    public IReadOnlyList<Book> Filter(IReadOnlyList<Book> books, BookStatus? status)
    {
        // LINQ의 Where는 조건을 만족한 항목만 고릅니다. =>는 책 하나를 받아 참/거짓을 내는 짧은 함수를 뜻합니다.
        // is null은 상태 필터가 생략되었는지 확인하는 패턴이며, 생략되면 모든 책을 포함합니다.
        // OrderBy와 ThenBy는 결과 순서를 일정하게 만들어 같은 요청의 결과를 예측하기 쉽게 합니다.
        return books
            .Where(book => status is null || book.Status == status)
            .OrderBy(book => book.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(book => book.Id)
            .ToArray();
    }
}
