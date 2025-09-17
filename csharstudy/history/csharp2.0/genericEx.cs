using System.Drawing;

namespace ConsoleApp;

public class NewStack<T>
{
    T[] _objList;
    int _pos;

    public NewStack(int size)
    {
        _objList = new T[size];
    }
    public void push(T obj)
    {
        _objList[_pos] = obj;
        _pos++;
    }
    public T pop()
    {
        _pos--;
        return _objList[_pos];
    }
}

public class Program
{
    static void Main(string[] args)
    {
        NewStack<int> intStack = new NewStack<int>(5);
        intStack.push(1);
        intStack.push(2);
        intStack.push(3);
        Console.WriteLine(intStack.pop());
        Console.WriteLine(intStack.pop());
        Console.WriteLine(intStack.pop());

        NewStack<string> strStack = new NewStack<string>(5);
        strStack.push("one");
        strStack.push("two");
        strStack.push("three");
        Console.WriteLine(strStack.pop());
        Console.WriteLine(strStack.pop());
        Console.WriteLine(strStack.pop());

        NewStack<Point> pointStack = new NewStack<Point>(5);
        pointStack.push(new Point(1, 2));
        pointStack.push(new Point(3, 4));
        pointStack.push(new Point(5, 6));
        Console.WriteLine(pointStack.pop());
        Console.WriteLine(pointStack.pop());
        Console.WriteLine(pointStack.pop());
    }
}