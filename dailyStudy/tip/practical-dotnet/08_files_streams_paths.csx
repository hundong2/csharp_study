using System;
using System.IO;
using System.Text;

// Path.GetTempPath:
// 운영체제가 제공하는 임시 폴더 경로를 가져옵니다.
// Path.Combine:
// Windows와 Linux의 경로 구분자가 달라도 안전하게 경로를 조합합니다.
string directory = Path.Combine(Path.GetTempPath(), "daily-study-files");

// Directory.CreateDirectory:
// 폴더가 없으면 만들고, 이미 있으면 그냥 통과합니다.
Directory.CreateDirectory(directory);

string filePath = Path.Combine(directory, "sample.txt");

// File.WriteAllText:
// 작은 텍스트 파일을 한 번에 쓸 때 편리합니다.
// Encoding.UTF8은 한글/영문을 UTF-8 바이트로 저장하겠다는 뜻입니다.
File.WriteAllText(filePath, "first line\nsecond line", Encoding.UTF8);

// FileStream:
// 파일을 byte 흐름으로 읽고 쓰는 기본 타입입니다.
// using 블록이 끝나면 파일 핸들이 닫힙니다.
using (FileStream stream = File.OpenRead(filePath))
// StreamReader:
// byte 스트림을 문자열로 읽기 쉽게 감싸는 타입입니다.
using (var reader = new StreamReader(stream, Encoding.UTF8))
{
    // ReadToEnd:
    // 현재 위치부터 파일 끝까지 전체 문자열을 읽습니다.
    // 큰 파일에서는 한 줄씩 읽는 방식이 더 안전할 수 있습니다.
    string content = reader.ReadToEnd();
    Console.WriteLine("[File]");
    Console.WriteLine(content);
}

Console.WriteLine($"Path={filePath}");
