using System;
using System.IO;
using System.Text;

// 실무 패턴: IDisposable + using
// 파일, 소켓, DB 연결처럼 운영체제 리소스를 잡는 객체는 사용 후 반드시 정리해야 합니다.
// using 블록을 쓰면 예외가 나도 Dispose가 호출됩니다.

string path = Path.Combine(Path.GetTempPath(), "daily-study-disposable.txt");

// FileStream:
// 파일을 byte 단위로 읽고 쓰는 기본 스트림입니다.
// using 블록이 끝나면 stream.Dispose()가 자동 호출되어 파일 핸들이 닫힙니다.
using (FileStream stream = File.Create(path))
{
    byte[] bytes = Encoding.UTF8.GetBytes("resource cleanup matters");

    // Write:
    // byte 배열의 내용을 파일 스트림에 씁니다.
    stream.Write(bytes, 0, bytes.Length);
}

// File.ReadAllText:
// 작은 텍스트 파일을 한 번에 읽을 때 편리합니다.
// 큰 파일은 스트리밍 방식으로 읽는 것이 좋습니다.
string content = File.ReadAllText(path, Encoding.UTF8);

Console.WriteLine($"[Disposable] File Path: {path}");
Console.WriteLine($"[Disposable] Content: {content}");
