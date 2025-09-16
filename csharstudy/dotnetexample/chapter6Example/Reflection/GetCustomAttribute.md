# GetCustomAttributes 파라미터 설명과 올바른 사용법
리플렉션에서 특성(Attribute)을 조회할 때 쓰는 GetCustomAttributes의 파라미터 의미와 주의점을 초보자도 이해하기 쉽게 정리합니다.

## 예시 파일
[MemberInfo.GetCustomAttributes 샘플 (Microsoft/dotnet/samples)](https://github.com/dotnet/samples/blob/main/snippets/csharp/VS_Snippets_CLR_System/system.reflection/cs/program.cs)

## 답변
GetCustomAttributes의 대표적인 오버로드와 파라미터 의미:
- GetCustomAttributes(bool inherit)
  - inherit: 상속 체인(기반 타입/오버라이드 원형)을 따라 올라가며 특성을 함께 검색할지 여부.
  - 주의: 속성(Property), 이벤트(Event)에서는 inherit 값이 무시됩니다. 타입(Type), 메서드(Method), 생성자(Constructor)에서만 효과적입니다.
- GetCustomAttributes(Type attributeType, bool inherit)
  - attributeType: 필터로 사용할 특성 타입. Attribute를 상속한 타입이어야 하며, 해당 타입이거나 그 파생 타입인 특성만 반환됩니다.
  - inherit: 위와 동일. 타입/메서드/생성자에서만 상속 탐색에 의미가 있습니다.
- 반환형
  - object[] (비제네릭 API). 필요 시 캐스팅하거나 OfType<T>()로 걸러 쓰세요.
  - 제네릭 확장 메서드도 있음: CustomAttributeExtensions.GetCustomAttributes<T>(...) → IEnumerable<T> 반환.

동작 요점:
- 상속 고려: inherit=true라도, 특성이 [AttributeUsage(Inherited = true)]일 때만 파생에 “전이”됩니다. 기본값은 Inherited = true입니다.
- 무효 대상: Assembly, Module, Parameter 등에는 상속 개념이 없으므로 inherit은 의미가 없습니다.
- 존재 확인 최적화: 특성 존재만 확인할 땐 IsDefined(attributeType, inherit)을 쓰는 게 성능상 유리합니다.
- 단일 특성 받기: 하나만 필요하면 GetCustomAttribute<T>(...)를 사용하세요.

실전 예: 플러그인 엔트리 타입 찾기
- 특정 특성(PluginEntryAttribute)이 붙은 타입을 dll에서 찾는 예시로, attributeType과 inherit의 의미가 명확히 드러납니다.

````csharp
using System.Reflection;

namespace ConsoleApp;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class PluginEntryAttribute : Attribute { }

class Program
{
    private static void ProcessPlugIn(string rootPath)
    {
        foreach (var dllPath in Directory.GetFiles(rootPath, "*.dll"))
        {
            Assembly pluginDll = Assembly.LoadFrom(dllPath);
            var entry = FindEntryType(pluginDll);
            if (entry is not null)
            {
                Console.WriteLine($"Entry: {entry.FullName} in {Path.GetFileName(dllPath)}");
            }
        }
    }

    static void Main(string[] args)
    {
        var pluginFolder = @".\plugins";
        if (Directory.Exists(pluginFolder) == true)
        {
            ProcessPlugIn(pluginFolder);
        }
    }

    // 엔트리 특성이 붙은 타입을 검색
    private static Type? FindEntryType(Assembly pluginDll)
    {
        foreach (var type in pluginDll.GetTypes())
        {
            // attributeType: PluginEntryAttribute만 필터링
            // inherit: false → 상속 체인 검색 안 함(명시적 부착만 인정)
            if (type.IsDefined(typeof(PluginEntryAttribute), inherit: false))
            {
                return type;
            }
        }
        return null;
    }
}
````

요약 체크리스트:
- attributeType: 어떤 특성을 찾을지 지정(해당 타입 또는 파생 타입 매칭).
- inherit: 타입/메서드/생성자에서만 상속 검색. 속성/이벤트/어셈블리/모듈에서는 무시됨.
- 존재만 확인: IsDefined(...) 권장. 값이 필요: GetCustomAttributes(...) 또는 GetCustomAttribute<T>(...).

### 추가 자료
- [MemberInfo.GetCustomAttributes(Boolean) 문서](https://learn.microsoft.com/dotnet/api/system.reflection.memberinfo.getcustomattributes)
- [Attribute.IsDefined 문서](https://learn.microsoft.com/dotnet/api/system.attribute.isdefined)
- [CustomAttributeExtensions(GetCustomAttribute<T>) 문서](https://learn.microsoft.com/dotnet/api/system.reflection.customattributeextensions.getcustomattribute)
- [AttributeUsageAttribute 설명(Inherited/AllowMultiple)](https://learn.microsoft.com/dotnet/api/system.attributeusageattribute)