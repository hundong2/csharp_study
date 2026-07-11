using System;
using System.IO;
using System.Text;

string directory = Path.Combine(Path.GetTempPath(), "daily-study-files");
Directory.CreateDirectory(directory);

string filePath = Path.Combine(directory, "sample.txt");

File.WriteAllText(filePath, "first line\nsecond line", Encoding.UTF8);

using (FileStream stream = File.OpenRead(filePath))
using (var reader = new StreamReader(stream, Encoding.UTF8))
{
    string content = reader.ReadToEnd();
    Console.WriteLine("[File]");
    Console.WriteLine(content);
}

Console.WriteLine($"Path={filePath}");
