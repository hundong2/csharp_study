namespace Member;

public class Scheduler
{
    readonly int second = 1;
    readonly string name;

    public Scheduler()
    {
        this.name = "sceduling"; //readonly field using creator 
    }
    public void Run()
    {
        //this.second = 5; //compile error occur!!
    }
}
class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Hello, World!");
    }
}
