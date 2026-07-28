/*
실행: dotnet script 02_LibrariesPreview6.csx
선행 문서: 00-csharp-primer.md, 02-libraries-runtime.md
목표: Preview 6 library API의 원리를 안정 API로 재현합니다.
새 API 이름은 주석에 있고, 현재 설치된 .NET 10에서도 실행됩니다.
*/

#nullable enable

// 01. 기본 형식과 예외를 사용합니다.
using System;
// 02. 안정 버전 DataAnnotations로 동기 규칙의 기초를 확인합니다.
using System.ComponentModel.DataAnnotations;
// 03. ActivitySource로 분산 추적 span의 기본 구조를 만듭니다.
using System.Diagnostics;
// 04. MemoryStream과 StreamReader를 사용합니다.
using System.IO;
// 05. UTF8 encoding을 사용합니다.
using System.Text;
// 06. JSON 직렬화를 사용합니다.
using System.Text.Json;

// 07. 검증 대상 모델을 reference type으로 선언합니다.
public sealed class Registration
{
    // 08. Required는 null/빈 값 금지라는 동기 attribute 규칙입니다.
    [Required]
    // 09. EmailAddress는 문자열 모양만 검사하며 실제 계정 중복 I/O는 확인하지 않습니다.
    [EmailAddress]
    // 10. init accessor는 object 초기화 때 값을 정하고 이후 변경을 제한합니다.
    public string Email { get; init; } = "";
}

// 11. JSON으로 보낼 간단한 데이터 형식입니다.
public sealed class Message
{
    // 12. property는 serializer가 기본적으로 public getter/setter를 통해 읽고 씁니다.
    public string Kind { get; set; } = "";
    // 13. payload도 문자열 property입니다.
    public string Payload { get; set; } = "";
}

// 14. 원본 string은 UTF-16 char sequence입니다.
string yaml = "name: preview6";
// 15. 안정 API에서는 먼저 UTF-8 byte 배열을 만들며 이 중간 할당을 Preview 6 StringStream이 피합니다.
byte[] utf8 = Encoding.UTF8.GetBytes(yaml);
// 16. MemoryStream은 byte[]를 Stream 계약으로 감쌉니다.
using (var stream = new MemoryStream(utf8, writable: false))
// 17. StreamReader는 byte stream을 encoding으로 decode해 char/text를 돌려줍니다.
using (var reader = new StreamReader(stream, Encoding.UTF8))
{
    // 18. Preview 6에서는 `new StringStream(yaml, Encoding.UTF8)`을 Stream API에 바로 줄 수 있습니다.
    Console.WriteLine($"stream text = {reader.ReadToEnd()}");
}

// 19. 모델 인스턴스를 object initializer로 만듭니다.
var model = new Registration { Email = "learner@example.com" };
// 20. ValidationContext는 대상과 service provider/items를 담습니다.
var context = new ValidationContext(model);
// 21. 안정 버전 동기 API로 모든 property attribute를 검사합니다.
Validator.ValidateObject(model, context, validateAllProperties: true);
// 22. Preview 6의 `ValidateObjectAsync`는 I/O 규칙을 thread blocking 없이 await합니다.
Console.WriteLine("동기 형식 검증 통과; Preview 6에서는 중복 확인을 async 규칙으로 추가");

// 23. 직렬화할 managed object를 만듭니다.
var message = new Message { Kind = "text", Payload = "hello" };
// 24. JsonSerializer는 metadata/reflection 또는 source-generated contract로 JSON을 만듭니다.
string json = JsonSerializer.Serialize(message);
// 25. Preview 6 union serializer는 wrapper가 아니라 active case를 직접 쓸 수 있습니다.
Console.WriteLine($"json = {json}");

// 26. ActivitySource 이름은 추적 규칙과 exporter가 source를 구분하는 key입니다.
using (var source = new ActivitySource("Study.Preview6"))
{
    // 27. listener가 없으면 StartActivity는 allocation을 피하고 null일 수 있습니다.
    using Activity? activity = source.StartActivity("LibraryDemo");
    // 28. null conditional은 activity가 실제 생성됐을 때만 tag를 추가합니다.
    activity?.SetTag("course", "dotnet-11-preview-6");
    // 29. listener가 없으므로 null은 실패가 아니라 불필요한 tracing allocation을 피한 결과입니다.
    Console.WriteLine($"listener가 없어 Activity allocation 생략 = {activity is null}");
}

// 30. 현재 PID는 항상 존재하는 process lookup 예제로 사용합니다.
int pid = Environment.ProcessId;
// 31. 안정 API GetProcessById는 실패 시 예외이지만 Preview 6 TryGetProcessById는 bool을 반환합니다.
using (Process process = Process.GetProcessById(pid))
{
    // 32. ProcessName은 OS가 제공하는 process metadata입니다.
    Console.WriteLine($"process {pid} = {process.ProcessName}");
}

/*
Preview 6 전용 형태:

using Stream text = new StringStream(yaml, Encoding.UTF8);
ReadOnlyMemory<byte> payload = utf8;
using Stream body = new ReadOnlyMemoryStream(payload);
await Validator.ValidateObjectAsync(model, context, true);
bool found = Process.TryGetProcessById(pid, out Process? candidate);
*/
