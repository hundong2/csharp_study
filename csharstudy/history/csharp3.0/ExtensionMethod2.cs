using System.Linq;
//IEnumerable의 확장 메서드를 호출하기 위해 네임스페이스 추가 
//using System.Linq는 컴파일러가 미리 포함해 주기 때문에 생략 가능 

namespace ConsoleApp;

class Program
{
    public static void Main(string[] args)
    {
        List<string> list = new List<string>{ "one", "two", "three" };
        Console.WriteLine(list.Min());
        Console.WriteLine(list.Max());
        Console.WriteLine(list.Count());
        Console.WriteLine(list.Average(x => x.Length));
        Console.WriteLine(list.Sum(x => x.Length));
    }
}