using ReadingListApi.Domain;

namespace ReadingListApi.Application;

// Repository는 저장 방식의 '약속'입니다. 서비스는 메모리, DB 등 실제 저장소의 세부 사항에 의존하지 않습니다.
public interface IBookRepository
{
    /// <summary>책을 저장합니다. book은 저장할 책이며 반환값은 없습니다.</summary>
    void Add(Book book);

    /// <summary>id에 해당하는 책을 찾습니다. id는 고유 번호이며 없으면 null을 반환합니다.</summary>
    Book? Find(Guid id);

    /// <summary>현재 저장된 모든 책의 스냅샷을 반환합니다. 매개변수는 없습니다.</summary>
    IReadOnlyList<Book> All();
}
