public class Program
{
    public static void Main(string[] args)
    {
        List<int> intList = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        IEnumerable<int> enumList = intList.Where(n => n % 2 == 0);
        Array.ForEach(enumList.ToArray(), (elem) => Console.WriteLine(elem));

        var WhereResult = intList.WhereFunc();
        Array.ForEach(WhereResult.ToArray(), (elem) => Console.WriteLine(elem));
    }
}

public static class WhereClass
{
    public static IEnumerable<int> WhereFunc(this IEnumerable<int> source)
    {
        foreach( var item in source)
        {
            if (item % 2 == 0)
                yield return item;
        }
    }

}