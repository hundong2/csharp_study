namespace Datetime;

class Program
{
    public static long Sum()
    {
        long sum = 0;
        for(int i = 0; i < 1000000; i++)
        {
            sum += i;
        }
        return sum;
    }    
    static void Example1()
    {
        DateTime now =  DateTime.Now;
        Console.WriteLine($"{now} : {now.Kind}");

        DateTime utcNow = DateTime.UtcNow;
        Console.WriteLine($"{utcNow} : {utcNow.Kind}");

        DateTime worldcup2002 = new DateTime(2002, 5, 31);
        Console.WriteLine($"{worldcup2002} : {worldcup2002.Kind}");

        worldcup2002 = new DateTime(2002, 5, 31, 0, 0, 0, DateTimeKind.Local);
        Console.WriteLine($"{worldcup2002} : {worldcup2002.Kind}");
    }
    //TimeSpan
    static void Example2()
    {
        DateTime endOfYear = new DateTime(DateTime.Now.Year, 12, 31);
        DateTime now = DateTime.Now;

        Console.WriteLine($"today's date: {now}");
        TimeSpan gap = endOfYear - now;
        Console.WriteLine($"remain date of this year : {gap.TotalDays}"); 
        //TotalHours, TotalMilliseconds, TotalMinutes, TotalSeconds 
    }

    //System.Diagnostics.Stopwatch 
    static void Example3()
    {
        System.Diagnostics.Stopwatch st = new System.Diagnostics.Stopwatch();

        st.Start();
        Sum();
        st.Stop();

        Console.WriteLine($"Total Ticks: {st.ElapsedTicks}");
        Console.WriteLine($"Millisecond: {st.ElapsedMilliseconds}");
        Console.WriteLine($"Second: {st.ElapsedMilliseconds / 1000}"); //millisecond to second 

        //Stop.Frequency attribute 
        Console.WriteLine($"Second : {st.ElapsedTicks/System.Diagnostics.Stopwatch.Frequency}");
    }
    static void Main(string[] args)
    {
        Console.WriteLine("Hello, World!");
        Example1();
        Example2();
        Example3();
    }
}
