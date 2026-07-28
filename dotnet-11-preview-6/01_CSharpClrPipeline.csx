/*
실행: dotnet script 01_CSharpClrPipeline.csx
선행 문서: 00-csharp-primer.md, 01-foundations-clr-jit.md
목표: 기본 문법이 IL, JIT, boxing, generic, GC와 어떻게 이어지는지 봅니다.
*/

// 01. 기본 BCL 형식을 사용합니다.
using System;
// 02. List<T>라는 generic collection을 사용합니다.
using System.Collections.Generic;
// 03. MethodInfo와 IL byte를 읽기 위해 reflection namespace를 엽니다.
using System.Reflection;
// 04. JIT hint attribute를 사용합니다.
using System.Runtime.CompilerServices;

// 05. class는 reference type을 선언하며 CLR은 이 형식의 MethodTable을 만듭니다.
public sealed class Calculator
{
    // 06. AggressiveInlining은 JIT에게 주는 hint이고 강제 명령이 아닙니다.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    // 07. static method에는 instance를 가리키는 숨은 `this` 인수가 없습니다.
    public static int Add(int left, int right)
    {
        // 08. IL에서는 두 인수를 evaluation stack에 올리고 add 후 반환하는 모양이 됩니다.
        return left + right;
    }

    // 09. generic method의 T는 호출 시 구체 형식으로 닫힙니다.
    public static T Echo<T>(T value)
    {
        // 10. 같은 값을 반환하며 참조 형식 instantiation은 기계 코드를 공유할 수 있습니다.
        return value;
    }
}

// 11. int는 32-bit value type이고 지역 변수의 물리 위치는 JIT가 정합니다.
int count = 3;
// 12. string은 immutable reference type이며 literal은 intern될 수 있습니다.
string runtimeName = "CLR";
// 13. method call의 IL은 첫 실행 시 JIT가 현재 CPU용 코드로 바꿉니다.
int total = Calculator.Add(count, 4);
// 14. 보간 문자열은 formatting code로 lowering됩니다.
Console.WriteLine($"{runtimeName} 계산 결과 = {total}");

// 15. List<int>는 T가 int로 닫힌 generic type입니다.
var numbers = new List<int> { 1, 2, 3 };
// 16. foreach는 GetEnumerator/MoveNext/Current 패턴으로 lowering됩니다.
foreach (int number in numbers)
{
    // 17. 각 요소를 generic method에 전달해 compile-time 형식 안전성을 유지합니다.
    Console.WriteLine($"Echo<int>({number}) = {Calculator.Echo(number)}");
}

// 18. object 변수에 int를 넣으면 value를 object로 감싸는 boxing이 일어납니다.
object boxed = total;
// 19. unboxing은 object가 실제로 boxed int인지 검사하고 값을 꺼냅니다.
int unboxed = (int)boxed;
// 20. 잘못된 형식으로 unbox하면 InvalidCastException이 발생합니다.
Console.WriteLine($"boxing 왕복 = {unboxed}");

// 21. reflection으로 Add metadata를 얻고, 끝의 `!`로 null이 아님을 compiler에 알립니다.
MethodInfo addMethod = typeof(Calculator).GetMethod(nameof(Calculator.Add))!;
// 22. 각 `!`는 nullable 경고만 억제하며 runtime null 검사나 객체 생성을 하지 않습니다.
byte[] il = addMethod.GetMethodBody()!.GetILAsByteArray()!;
// 23. MethodBody의 byte 배열은 native code가 아니라 CIL opcode stream이며 BitConverter로 표시합니다.
Console.WriteLine($"Add IL bytes = {BitConverter.ToString(il)}");

// 24. 새 byte 배열은 managed heap에 할당되고 길이는 object metadata와 별도입니다.
byte[] buffer = new byte[1_024];
// 25. 배열 첫 원소에 쓰면 bounds check가 있으며 JIT가 안전함을 증명하면 제거할 수 있습니다.
buffer[0] = 42;
// 26. 객체가 현재 어느 GC generation에 있는지 관찰합니다.
Console.WriteLine($"buffer generation = {GC.GetGeneration(buffer)}");
// 27. 참조를 마지막으로 사용한 뒤에는 JIT liveness에 따라 수집 가능 시점이 소스 scope보다 빠를 수 있습니다.
Console.WriteLine($"buffer[0] = {buffer[0]}");

// 28. 같은 method를 반복 호출해 tiering/call counting의 후보가 되게 합니다.
int checksum = 0;
// 29. loop의 비교와 index 갱신은 JIT IR 최적화 대상입니다.
for (int i = 0; i < 100_000; i++)
{
    // 30. hot call은 Tier 1과 Dynamic PGO 최적화의 후보가 됩니다.
    checksum = Calculator.Add(checksum, i & 1);
}
// 31. 결과를 사용해 JIT가 loop 전체를 불필요하다고 제거하지 못하게 합니다.
Console.WriteLine($"hot-loop checksum = {checksum}");
