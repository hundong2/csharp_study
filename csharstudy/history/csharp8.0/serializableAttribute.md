# [C#] Serializable 특성이란?
[Serializable]은 “형식을 런타임 직렬화 대상으로 표시”하는 특성입니다. 주로 옛 포매터(BinaryFormatter, SoapFormatter 등)에서 사용되며, 필드 단위로는 [NonSerialized]로 제외할 수 있습니다. 최신 JSON/XML 직렬화에는 필수가 아닙니다.

## 예시 파일
[SerializableAttribute 문서(공식 샘플 포함)](https://learn.microsoft.com/dotnet/api/system.serializableattribute)

## 답변
- 무엇을 의미하나
  - [Serializable]을 붙이면 런타임 포매터 기반 직렬화 대상이 됩니다(필요 시 ISerializable로 커스터마이즈).
  - 특정 필드를 직렬화에서 제외하려면 [NonSerialized]를 사용합니다.
- 언제 필요한가
  - 레거시 포매터(BinaryFormatter/SoapFormatter 등) 또는 일부 라이브러리의 런타임 직렬화가 필요할 때.
  - 최신 대안(System.Text.Json, XmlSerializer, DataContractSerializer 등)은 [Serializable] 없이도 작동하는 경우가 많습니다.
- 주의
  - BinaryFormatter는 보안상 더 이상 권장되지 않습니다(경계 간 데이터 교환 금지). 가능하면 System.Text.Json 등 대체 사용.
  - 순환 참조, 버전 변경(필드 추가/삭제) 시 호환성 전략이 필요합니다.
- 관련 특성/인터페이스
  - [NonSerialized]: 특정 필드를 직렬화에서 제외
  - [OnSerializing]/[OnSerialized]/[OnDeserializing]/[OnDeserialized]: 직렬화 전후 훅
  - ISerializable: 커스텀 직렬화 구현(특수 생성자 포함)

예제 코드(데모 목적: BinaryFormatter 경고 억제, 실제 서비스에서는 JSON 등 대체 권장)
````csharp
using System;
using System.IO;
using System.Runtime.Serialization;
#pragma warning disable SYSLIB0011 // BinaryFormatter obsolete
using System.Runtime.Serialization.Formatters.Binary;

[Serializable] // 형식 전체를 직렬화 대상으로 표시
public class Person : ISerializable
{
    public string Name { get; set; } = "";
    public int Age { get; set; }

    [NonSerialized]                  // 직렬화 제외(캐시, 핸들 등)
    private string? _cachedDisplay;

    public Person() { }

    // 역직렬화 전용 생성자 (ISerializable를 구현하면 필수)
    protected Person(SerializationInfo info, StreamingContext ctx)
    {
        Name = info.GetString(nameof(Name)) ?? "";
        Age  = info.GetInt32(nameof(Age));
    }

    // 커스텀 직렬화 로직
    public void GetObjectData(SerializationInfo info, StreamingContext ctx)
    {
        info.AddValue(nameof(Name), Name);
        info.AddValue(nameof(Age), Age);
        // _cachedDisplay는 제외됨
    }

    [OnDeserialized]
    private void OnDeserialized(StreamingContext ctx)
    {
        _cachedDisplay = $"{Name} ({Age})";
    }

    public override string ToString() => _cachedDisplay ?? $"{Name} ({Age})";
}

public static class SerializableDemo
{
    public static byte[] Serialize(Person p)
    {
        using var ms = new MemoryStream();
        new BinaryFormatter().Serialize(ms, p); // 데모용
        return ms.ToArray();
    }

    public static Person Deserialize(byte[] blob)
    {
        using var ms = new MemoryStream(blob);
        return (Person)new BinaryFormatter().Deserialize(ms);
    }

    public static void Run()
    {
        var p = new Person { Name = "Alice", Age = 30 };
        var bytes = Serialize(p);
        var back  = Deserialize(bytes);
        Console.WriteLine(back); // Alice (30)
    }
}
````

핵심 요약
- [Serializable]: 포매터 기반(레거시) 직렬화 사용 시 필요 표시.
- [NonSerialized]: 제외 필드 지정.
- ISerializable/직렬화 콜백: 커스텀 제어 지점.
- 실제 서비스에서는 System.Text.Json 등 현대적 직렬화를 우선 고려.

### 추가 자료
- [SerializableAttribute](https://learn.microsoft.com/dotnet/api/system.serializableattribute)
- [NonSerializedAttribute](https://learn.microsoft.com/dotnet/api/system.nonserializedattribute)
- [ISerializable](https://learn.microsoft.com/dotnet/api/system.runtime.serialization.iserializable)
- [Serialization callbacks(OnDeserialized 등)](https://learn.microsoft.com/dotnet/api/system.runtime.serialization.ondeserializedattribute)
- [BinaryFormatter 사용 금지 권고](https://learn.microsoft.com/dotnet/standard/serialization/binaryformatter-security-guide)
- [System.Text.Json 소개](https://learn.microsoft.com/dotnet/standard/serialization/system-text-json-overview)