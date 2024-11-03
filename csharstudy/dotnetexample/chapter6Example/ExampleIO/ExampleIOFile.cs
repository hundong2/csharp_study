namespace ExampleIO;
using System.IO;
public class ExampleIOFile
{
    public ExampleIOFile()
    {
        Console.WriteLine("ExampleIOFile");
        string filepath = Path.Combine(Environment.CurrentDirectory, "test.txt");
        Console.WriteLine($"current file path {filepath}");
    }

    /// <summary>
    /// get Invalid path check in path
    /// https://learn.microsoft.com/ko-kr/dotnet/api/system.io.path.getinvalidpathchars?view=net-8.0
    /// </summary>
    public void ExampleInvaildPath()
    {
        string newDirName = "my<new";
        int include=newDirName.IndexOfAny(Path.GetInvalidPathChars());
        if( include != -1 )
        {
            Console.WriteLine("Invalid path character");
        }
        else
        {
            Console.WriteLine("Valid path character");
        }
    }

    public void ExampleTempFile()
    {
        string cratedTempFilePath = Path.GetTempFileName();
        Console.WriteLine($"Temp file path : {cratedTempFilePath}");

        string tempFilePath
            = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Console.WriteLine($"Temp file path : {tempFilePath}");
    }

    public void ExampleEtcFunction()
    {
        string samplePath = @"C:\temp\test.txt";
        Console.WriteLine("ChangeExatension ==> "
            + Path.ChangeExtension(samplePath, "dll"));
        Console.WriteLine("GetDirectoryName ==> " + Path.GetDirectoryName(samplePath));
        Console.WriteLine("GetExtension ==> " + Path.GetExtension(samplePath));
        Console.WriteLine("GetFileName ==> " + Path.GetFileName(samplePath));
        Console.WriteLine("GetFileNameWithoutExtension ==> "
            + Path.GetFileNameWithoutExtension(samplePath));
        Console.WriteLine("GetFullPath ==> " + Path.GetFullPath(samplePath));
        Console.WriteLine("GetPathRoot ==> " + Path.GetPathRoot(samplePath));
        
    }
}
