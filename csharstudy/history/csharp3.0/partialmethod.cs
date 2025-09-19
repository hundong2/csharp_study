partial class MyTest
{
    partial void Log(object obj); //method signature
    public void WriteTest()
    {
        this.Log("call test!"); //it's possible to call Log method
    }
}
partial class MyTest
{
    partial void Log(object obj)
    {
        System.Console.WriteLine(obj.ToString());
    }
}

class Program
{
    public static void Main(string[] args)
    {
        MyTest t = new MyTest();
        t.WriteTest();
    }
}