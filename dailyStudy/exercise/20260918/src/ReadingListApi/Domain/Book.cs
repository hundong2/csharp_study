namespace ReadingListApi.Domain;

// enum은 가능한 상태를 이름으로 한정합니다. 자유로운 문자열보다 잘못된 상태가 저장될 가능성이 작습니다.
public enum BookStatus
{
    ToRead,
    Reading,
    Completed
}

/// <summary>
/// 읽을 책 한 권의 현재 정보를 나타냅니다. record는 값을 담는 자료형이고, 새 값을 만들 때 기존 값을 바꾸지 않아 안전합니다.
/// </summary>
/// <param name="Id">책을 다른 책과 구분하는 고유 번호입니다.</param>
/// <param name="Title">책 제목입니다.</param>
/// <param name="Author">저자 이름입니다.</param>
/// <param name="Status">읽기 진행 상태입니다.</param>
// 이 생성자는 새 Book 값을 만들며 별도 반환값은 없습니다. init 전용 record의 불변성 덕분에 저장 후 값이 몰래 바뀌지 않습니다.
public sealed record Book(Guid Id, string Title, string Author, BookStatus Status);
