# C# 리플렉션(Reflection) 이해와 활용
리플렉션은 런타임에 타입(클래스, 메서드, 속성 등)의 메타데이터를 읽고, 객체를 동적으로 생성하거나 메서드를 호출하는 기능입니다. 프레임워크/툴링, 플러그인 시스템, 직렬화, DI 컨테이너 등에서 핵심적으로 사용됩니다.

`c#코드가 빌드 되어 어셈블리에 포함되는 경우 그에 대한 모든 정보를 조회할 수 있는 기술`

## 예시 파일
[Program.cs (System.Reflection 샘플)](https://github.com/dotnet/samples/blob/main/snippets/csharp/VS_Snippets_CLR_System/system.reflection/cs/program.cs)

## 답변
리플렉션이란:
- 의미: 코드가 자기 자신(타입, 멤버, 특성(Attributes), 어셈블리)을 “데이터처럼” 살펴보고 조작하는 기법.
- 핵심 기능:
  - 메타데이터 조회: Type, MethodInfo, PropertyInfo, FieldInfo, Attribute 등으로 구조 확인
  - 동적 동작: Activator.CreateInstance, MethodInfo.Invoke로 인스턴스 생성/메서드 호출
  - 어셈블리 탐색: Assembly 로드/탐색으로 플러그인 검색·확장
  - 특성 읽기: 커스텀 Attribute를 읽어 동작을 구성
- 주로 유용한 곳:
  - DI 컨테이너, ORM, 직렬화/역직렬화(Json/XML), 테스트 프레임워크(특성 기반), 플러그인/모듈 시스템, 코드 생성/메타프로그래밍 도구

아래는 현재 파일에 실용 예제를 추가하는 개선안입니다. 기존 “현재 도메인의 어셈블리 나열”은 유지하고, 타입 탐색, 동적 생성/호출, 비공개 멤버 접근(학습용), 커스텀 특성 읽기 예시를 더합니다.

````csharp
using System;
using System.Linq;
using System.Reflection;

class Program
{
    static void Main(string[] args)
    {
        // ...existing code...
        AppDomain currentDomain = AppDomain.CurrentDomain;
        Console.WriteLine("Current Domain: " + currentDomain.FriendlyName);
        foreach( var asm in currentDomain.GetAssemblies())
        {
            Console.WriteLine("Assembly: " + asm.FullName);
        }

        // 1) 타입 메타데이터 열람
        Console.WriteLine("\n[1] 타입 메타데이터 열람");
        Type t = typeof(Demo);
        Console.WriteLine($"Type: {t.FullName}");
        Console.WriteLine("- 생성자:");
        foreach (var ctor in t.GetConstructors())
            Console.WriteLine($"  {ctor}");
        Console.WriteLine("- 메서드:");
        foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            Console.WriteLine($"  {m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})");
        Console.WriteLine("- 속성:");
        foreach (var p in t.GetProperties())
            Console.WriteLine($"  {p.PropertyType.Name} {p.Name}");

        // 2) 인스턴스 생성 및 메서드 호출
        Console.WriteLine("\n[2] 인스턴스 생성 및 메서드 호출");
        object demo = Activator.CreateInstance(typeof(Demo), 10); // 생성자에 seed=10 전달
        MethodInfo addMethod = t.GetMethod("Add", new[] { typeof(int), typeof(int) });
        object sum = addMethod!.Invoke(demo, new object[] { 3, 5 });
        Console.WriteLine($"Demo.Add(3,5) => {sum}");

        // 3) 비공개 멤버 접근 (학습용: 실제 서비스 코드에선 지양)
        Console.WriteLine("\n[3] 비공개 멤버 접근 (학습용)");
        MethodInfo? secret = t.GetMethod("Secret", BindingFlags.Instance | BindingFlags.NonPublic);
        string secretMsg = (string)secret!.Invoke(demo, null)!;
        Console.WriteLine($"Secret() => {secretMsg}");

        // 4) 커스텀 특성 읽기
        Console.WriteLine("\n[4] 커스텀 특성 읽기");
        var typeAttr = t.GetCustomAttribute<MyDocAttribute>();
        Console.WriteLine($"[Type Attribute] {typeAttr?.Description}");
        var methodAttr = addMethod.GetCustomAttribute<MyDocAttribute>();
        Console.WriteLine($"[Method Attribute] {methodAttr?.Description}");

        // 5) 현재 어셈블리에서 공개 타입 나열
        Console.WriteLine("\n[5] 현재 어셈블리의 공개 타입");
        Assembly thisAsm = Assembly.GetExecutingAssembly();
        foreach (var type in thisAsm.GetExportedTypes())
        {
            Console.WriteLine($"- {type.FullName}");
        }

        // 참고: 성능 민감 구간에서는 리플렉션 남용을 피하고, 캐싱(예: MethodInfo 캐시) 또는 소스생성/표현식 트리/델리게이트 변환을 고려하세요.
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class MyDocAttribute : Attribute
{
    public string Description { get; }
    public MyDocAttribute(string description) => Description = description;
}

[MyDoc("데모 타입: 생성자 seed를 더해주는 Add 메서드 포함")]
public class Demo
{
    public int Seed { get; }
    public Demo(int seed) => Seed = seed;

    [MyDoc("두 수를 더한 뒤 Seed를 더합니다.")]
    public int Add(int a, int b) => a + b + Seed;

    // 비공개 메서드 (학습용)
    [MyDoc("내부 상태를 반환하는 비공개 메서드")]
    private string Secret() => $"Seed={Seed}";
}
````

실행 방법(맥):
- VS Code 터미널에서 프로젝트 디렉터리로 이동 후 dotnet run 실행
  - 예: cd /Users/donghun2/workspace/csharp_study && dotnet run -v minimal
  - 또는 csproj 위치에서 dotnet run

핵심 팁:
- 적절한 사용처: 플러그인 검색, 테스트/도구, 직렬화, 코드 분석/생성
- 주의사항: 성능(Invoke 비용 높음)·안전성(비공개 접근 지양). 빈번 호출은 MethodInfo/ConstructorInfo/Delegate 캐싱으로 최적화.

### 추가 자료
- [C# 리플렉션 개요 (Microsoft Learn)](https://learn.microsoft.com/dotnet/csharp/programming-guide/concepts/reflection)
- [특성(Attributes) 사용 (Microsoft Learn)](https://learn.microsoft.com/dotnet/csharp/programming-guide/concepts/attributes/)