using System.Collections;

namespace ConsoleApp;

public class NaturalNumber : IEnumerable<int>
{
    public IEnumerator<int> GetEnumerator()
    {
        return new NaturalNumberEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return new NaturalNumberEnumerator();
    }
}

public class NaturalNumberEnumerator : IEnumerator<int>
{
    int _current;

    public int Current => _current;

    object IEnumerator.Current => _current;
    public void Dispose()
    {
    }

    public bool MoveNext()
    {
        _current++;
        return true;
    }

    public void Reset()
    {
        _current = 0;
    }
}

class YieldNaturalNumber
{
    public static IEnumerable<int> Next()
    {
        int _start = 0;
        while (true)
        {
            _start++;
            yield return _start;
        }
    }
}
class Program
{
    static void Main(string[] args)
    {
        var number = new NaturalNumber();
        foreach (var n in number)
        {
            Console.WriteLine(n);
            if (n >= 20) break; // 무한루프 방지
        }

        foreach (var n in YieldNaturalNumber.Next())
        {
            Console.WriteLine(n);
            if (n >= 20) break; // 무한루프 방지
        }
    }
}