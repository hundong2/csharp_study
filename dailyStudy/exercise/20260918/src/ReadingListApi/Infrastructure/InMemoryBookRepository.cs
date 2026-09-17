using System.Collections.Concurrent;
using ReadingListApi.Application;
using ReadingListApi.Domain;

namespace ReadingListApi.Infrastructure;

// Adapter는 Repository 약속을 실제 저장 방식으로 구현합니다. ConcurrentDictionary는 여러 요청의 동시 접근을 처리합니다.
public sealed class InMemoryBookRepository : IBookRepository
{
    // new()는 왼쪽 변수 형식이 분명할 때 생성자 형식 이름을 생략합니다.
    private readonly ConcurrentDictionary<Guid, Book> _books = new();

    /// <summary>
    /// book을 메모리에 저장합니다. book은 새 책이며 반환값은 없습니다. 중복 id는 개발 오류로 취급합니다.
    /// </summary>
    public void Add(Book book)
    {
        if (!_books.TryAdd(book.Id, book))
        {
            throw new InvalidOperationException("이미 존재하는 책 ID입니다.");
        }
    }

    /// <summary>
    /// id에 해당하는 책을 찾습니다. id는 고유 번호이며 없으면 null을 반환합니다.
    /// </summary>
    public Book? Find(Guid id)
    {
        // out은 찾은 책을 book 변수에 담습니다. TryGetValue는 키가 없을 때 예외 대신 false를 줍니다.
        return _books.TryGetValue(id, out Book? book) ? book : null;
    }

    /// <summary>
    /// 호출 시점에 저장된 모든 책의 복사본을 반환합니다. 매개변수는 없으며 이후 저장소 변경은 이 목록에 반영되지 않습니다.
    /// </summary>
    // =>는 한 식의 반환값을 그대로 돌려주는 짧은 메서드 문법입니다.
    public IReadOnlyList<Book> All() => _books.Values.ToArray();
}
