using System.Collections;
using System.Net.WebSockets;
namespace ExampleCollection;

public class ExampleSortedList
{
    public ExampleSortedList()
    {
        
    }
    public void Example1()
    {
        SortedList sl = new SortedList();
        sl.Add("1", "One");
        sl.Add("2", "Two");
        sl.Add("4", "Four");
        sl.Add("5", "Five");
        sl.Add("3", "Three");

        foreach ( var item in sl.GetKeyList() )
        {
            Console.WriteLine($"{item} {sl[item]}");
        }
    }
}
