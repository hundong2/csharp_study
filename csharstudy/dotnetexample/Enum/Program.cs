namespace Enum;

class Program
{
    enum Days
    {
        Sunday=1, Monday=2, Tuesday=4, Wednesday=8, Thursday=16, Friday=32, Saturday=64
    }
    [Flags]
    enum DaysFlag 
    {
        Sunday=1, Monday=2, Tuesday=4, Wednesday=8, Thursday=16, Friday=32, Saturday=64
    }
    static void Main(string[] args)
    {
        Days today = Days.Sunday;
        int n = (int)today;
        short s = (short)today;
        today = (Days)5;
        Console.WriteLine(today);

        DaysFlag today2 = DaysFlag.Sunday;
        today2 = (DaysFlag)5; //today2 = DaysFalg.Sunday | DaysFlag.Tuesday
        Console.WriteLine(today2);
        Console.WriteLine(today2.HasFlag(DaysFlag.Sunday));
        Console.WriteLine(today2.HasFlag(DaysFlag.Tuesday));
    }
}
