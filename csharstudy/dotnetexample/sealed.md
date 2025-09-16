# C#에서 sealed 키워드는 무엇인가?
상속(또는 재정의)을 막아 “더 이상 확장되지 않도록” 봉인하는 키워드입니다. 클래스에 쓰면 상속 금지, override 멤버에 쓰면 추가 재정의 금지입니다.

## 예시 파일
SystemInfo.cs

## 답변
핵심 요약:
- sealed class: 이 클래스를 더 이상 상속할 수 없음.
- sealed override: 이미 override한 멤버를 파생 타입에서 더 이상 override 못 함.
- 장점: 의도 명확화(확장 금지), 버전 관리 용이, 성능 최적화 가능(JIT의 devirtualization), 보안/불변 설계 강화.
- 주의:
  - abstract와 sealed는 클래스에 동시에 쓸 수 없음.
  - static class는 본질적으로 sealed이므로 별도 sealed 불필요.
  - struct는 상속 불가(암시적으로 sealed와 유사).

예제 1) 클래스를 봉인(상속 금지)
````csharp
namespace Example1;

public sealed class FinalService
{
    public string Ping() => "pong";
}

// 아래 코드는 컴파일 오류(상속 금지):
// public class MyService : FinalService { } // error CS0509: cannot derive from sealed type
````

예제 2) 재정의 봉인(더 이상 override 금지)
````csharp
namespace Example1;

public class Base
{
    public virtual string Name() => "Base";
}

public class Mid : Base
{
    public sealed override string Name() => "Mid"; // 여기서 재정의 봉인
}

// 아래 코드는 컴파일 오류(더 이상 override 불가):
// public class Leaf : Mid
// {
//     public override string Name() => "Leaf"; // error CS0239: cannot override sealed member
// }
````

추가 팁(현재 코드와 연계):
- 플러그인 엔트리 타입(예: ExpandModule.SystemInfo)은 외부가 상속해서 규약을 깨면 곤란하다면 sealed로 봉인하는 것이 안전합니다.
- 특성 클래스(PluginAttribute, StartUpAttribute)는 보통 sealed로 두는 것이 관례이며, 이미 그렇게 잘 작성되었습니다.

### 추가 자료
- [sealed 키워드(C# 참조)](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/sealed)
- [override와 sealed 조합](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/override)
- [가상 호출 최적화(Devirtualization) 개요](https://learn.microsoft.com/dotnet/core/deploying/readytorun#devirtualization)