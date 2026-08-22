using System.Linq;

// 문자열 반복 예제
string patter = "ABC";
var patters = string.Concat(Enumerable.Repeat(patter, 3)); // "ABCABCABC"
Console.WriteLine(patters); // "ABCABCABC"
Console.WriteLine($"{patter} repeated 3 times: {patters}"); // "ABCABCABC"

// ABCABCABC
// ABC repeated 3 times: ABCABCABC
var usingStringBuilder = new StringBuilder(3).Insert(0, "ABC", 3).ToString();
Console.WriteLine(usingStringBuilder); // "ABCABCABC"

//Result Output
// ABCABCABC


Console.WriteLine($"Using Create String Result: {UsingCreateString()}"); // "ABCABCABC"
//Using Create String
string UsingCreateString()
{
    string pattern = "ABC";
    int repeatCount = 3;

    // 최종 결합되어 생성될 문자열의 전체 길이를 사전에 계산합니다. (3 * 3 = 9)
    int totalLength = pattern.Length * repeatCount;

    // string.Create<TState>는 지정된 길이의 힙 메모리를 사전에 단 한 번만 딱 맞게 할당하고,
    // 그 메모리를 직접 수정할 수 있는 쓰기 가능한 Span<char>(span)을 제공하여 문자열을 초기화합니다.
    // 튜플 (pattern, repeatCount) 상태 값은 람다의 'state' 파라미터로 직접 전달되어
    // 외부 변수를 캡처하는 클로저(Closure) 생성을 방지하여 가비지 컬렉션(GC) 부하를 제거합니다.
    return string.Create(totalLength, (pattern, repeatCount), (span, state) =>
    {
        // 원본 패턴 문자열("ABC")을 스택 상의 초고속 읽기 전용 Span 프리뷰인 ReadOnlySpan<char>로 가져옵니다.
        ReadOnlySpan<char> sourceSpan = state.pattern.AsSpan();
        
        for (int i = 0; i < state.repeatCount; i++)
        {
            //Console.WriteLine($"{i}번째 루프 시작 (복사 대상: \"{span.ToString()}\")");
            // 1. span.Slice(...)를 통해 문자열을 쓸 대상 영역의 i번째 위치 세그먼트를 떼어냅니다.
            // 2. CopyTo(...)를 호출하여 CPU 메모리 복사 레벨에서 원본 문자(sourceSpan)를 대상 세그먼트로 고속 전송합니다.
            sourceSpan.CopyTo(span.Slice(i * sourceSpan.Length, sourceSpan.Length));

            // 복사 이후 전체 span 버퍼 공간이 어떻게 채워지고 있는지 시각적으로 확인하기 위해
            // 할당되지 않은 null 문자('\0')를 언더바('_')로 임시 변경하여 출력합니다.
            char[] tempArray = span.ToArray();
            for (int k = 0; k < tempArray.Length; k++)  
            {
                if (tempArray[k] == '\0') tempArray[k] = '_';
            }
            Console.WriteLine($"  -> 복사 후 전체 버퍼 상태: [{new string(tempArray)}]");
        }
    });
}