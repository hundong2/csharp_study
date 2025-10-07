# C#의 Attribute는 내부적으로 어떻게 동작하나
Attribute는 “코드에 붙는 주석이 아닌, 메타데이터”입니다. 컴파일러가 어셈블리 메타데이터에 기록하고, 런타임/도구가 그 메타데이터를 읽어 동작을 결정합니다. 즉, Attribute는 스스로 실행되지 않고, 이를 “읽는 쪽”이 의미를 부여합니다.

## 예시 파일
[System.Attribute 원본 코드(.NET Runtime)](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Attribute.cs)

## 답변
핵심 흐름
- 선언과 적용: [MyAttr]처럼 타입/멤버/매개변수/리턴/어셈블리 등에 붙이면, 컴파일러가 “CustomAttribute” 메타데이터 항목으로 기록합니다. 기록되는 것은 “어떤 Attribute 타입의 어떤 생성자를 어떤 인자로 호출해야 하는가”와 “이름 붙은 인자(프로퍼티/필드 초기값)”입니다.
- 저장 형태(메타데이터): IL의 CustomAttribute 테이블에 “ctor 참조 + 인자 blob(고정 인자 + named 인자)”로 저장됩니다. 이건 코드가 아니라 데이터입니다.
- 조회와 인스턴스화:
  - Attribute.GetCustomAttributes(...)를 호출하면, 런타임이 메타데이터를 읽어 실제 Attribute 인스턴스를 “그때” 생성(해당 ctor 호출 + named 인자 설정)합니다.
  - 단, CustomAttributeData는 “인스턴스 생성 없이” 메타데이터만 해석해 읽습니다(성능/로딩 영향 낮음).
- 의미 부여: 프레임워크/라이브러리/사용자 코드가 “해당 Attribute를 읽어” 동작을 바꿉니다. 예: ASP.NET의 [HttpGet], 직렬화기의 [JsonPropertyName], AOT 트리머 설정 등.
- 적용 제약: [AttributeUsage(AttributeTargets.Class, AllowMultiple=true, Inherited=true)]로 적용 위치/중복/상속 여부를 “컴파일러가” 검사합니다(런타임도 조회 가능).

중요 포인트
- 수명/실행: Attribute는 붙이는 순간 실행되지 않습니다. “조회할 때” 생성되거나(또는 조회 없이도 그냥 메타데이터로 남아 있음), 특정 빌드 도구/소스 제너레이터가 컴파일 중 읽을 수 있습니다.
- 성능: 빈번한 리플렉션은 비용이 큽니다. 캐싱, IsDefined 사용, CustomAttributeData로 지연 인스턴스화를 고려하세요.
- 트리밍/AOT: 리플렉션으로 Attribute를 읽는 경우 트리머가 필요한 멤버를 제거할 수 있습니다. [DynamicallyAccessedMembers]나 Trimmer 설정으로 보존을 명시하세요.
- 적용 위치 문법: [assembly:], [module:], [return:], [param:], [type:](C# 10+) 접두로 정확한 대상 지정.

예제: 커스텀 Attribute 선언/적용/조회
````csharp
using System;
using System.Linq;
using System.Reflection;

// 1) 선언: Attribute는 보통 sealed가 아니어도 되지만, 상태만 담는 단순 클래스입니다.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class TagAttribute : Attribute
{
    public string Name { get; }
    public int Level { get; set; } // named 인자(프로퍼티/필드)로 저장됨

    public TagAttribute(string name) => Name = name; // 고정 인자(ctor 인자)
}

// 2) 적용: ctor 인자와 named 인자가 메타데이터에 기록됩니다.
[Tag("type-level", Level = 1)]
public class Service
{
    [Tag("op", Level = 2)]
    public void Run() { }
}

public static class AttributesInternalsDemo
{
    public static void Main()
    {
        // 3) 런타임 조회(인스턴스 생성됨)
        var t = typeof(Service);
        var typeTags = t.GetCustomAttributes<TagAttribute>(inherit: true).ToArray();
        Console.WriteLine($"Type tags: {typeTags.Length}, first={typeTags.First().Name}, level={typeTags.First().Level}");

        var m = t.GetMethod(nameof(Service.Run))!;
        var methodTags = m.GetCustomAttributes(typeof(TagAttribute), inherit: true);
        Console.WriteLine($"Method tags: {methodTags.Length}");

        // 4) 인스턴스 생성 없이 메타데이터만 읽기(CustomAttributeData)
        foreach (var cad in m.CustomAttributes.Where(a => a.AttributeType == typeof(TagAttribute)))
        {
            var ctorArg = cad.ConstructorArguments.First().Value; // "op"
            var level = cad.NamedArguments.FirstOrDefault(na => na.MemberName == nameof(TagAttribute.Level)).TypedValue.Value;
            Console.WriteLine($"CAD -> name={ctorArg}, level={level}");
        }
    }
}
````

확장 예시
- 매개변수/반환값에 적용:
  - void M([Tag("p")] int x) { }
  - [return: Tag("retval")] int M() => 0;
- 어셈블리 전역에 적용:
  - [assembly: Tag("assembly-scope", Level = 0)]
- 제네릭 매개변수/형식 매개변수 제약과 조합: 일반적으로 Attribute는 제약 자체엔 영향 없고, 메타데이터로만 남습니다.

베스트 프랙티스
- 빈번한 조회는 캐싱: (MemberInfo, AttributeType) 키로 결과를 메모이즈.
- CustomAttributeData로 “필터만” 먼저 하고 필요한 경우에만 생성.
- 공개 API 계약을 Attribute로 규정할 때는 “어트리뷰트를 읽는 코드”를 명확히 분리해 테스트.
- 트리밍 환경 대비: 리플렉션 경로와 필요한 타입/멤버를 보존하도록 주석/특성/링커 설정 추가.

### 추가 자료
- [Attributes 개요(문서)](https://learn.microsoft.com/dotnet/csharp/programming-guide/concepts/attributes/)
- [AttributeUsageAttribute](https://learn.microsoft.com/dotnet/api/system.attributeusageattribute)
- [CustomAttributeData](https://learn.microsoft.com/dotnet/api/system.reflection.customattributedata)
- [리플렉션 기본](https://learn.microsoft.com/dotnet/csharp/programming-guide/concepts/reflection)
- [트리밍과 리플렉션 고려사항](https://learn.microsoft.com/dotnet/core/deploying/trimming/prepare-libraries-for-trimming)