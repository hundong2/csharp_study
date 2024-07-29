namespace EventExample;

class PrimeCallbackArg: EventArgs
{
    public int Prime;
    public PrimeCallbackArg(int prime)
    {
        this.Prime = prime;
    }
}

class PrimeGenerator
{
    public event EventHandler PrimeGenerated;
    public void Run(int limit)
    {
        for(int i =0; i< limit; i++)
        {
            if( IsPrime(i) == true && PrimeGenerated != null)
            {
                PrimeGenerated(this, new PrimeCallbackArg(i));
            }
        }
    }
    private bool IsPrime(int candidate)
    {
        if((candidate & 1) == 0 )
        {
            return candidate == 2;
        }
        for( int i = 3; (i*i) <= candidate; i+=2)
        {
            if((candidate % i) == 0) return false;
        }
        return candidate != 1;
    }
}
class Program
{
    static void PrintPrime(object sender, EventArgs arg)
    {
        Console.WriteLine((arg as PrimeCallbackArg).Prime + ", ");
    }
    static int Sum;
    static void SumPrime(object sender, EventArgs arg)
    {
        Sum += (arg as PrimeCallbackArg).Prime;
    }
    static void Main(string[] args)
    {
        PrimeGenerator gen = new PrimeGenerator();
        gen.PrimeGenerated += PrintPrime;
        gen.PrimeGenerated += SumPrime;

        gen.Run(10);
        Console.WriteLine("");
        Console.WriteLine(Sum);
        gen.PrimeGenerated -= SumPrime;
        gen.Run(15);
        Console.WriteLine("");
        Console.WriteLine(Sum);
        Console.WriteLine("Hello, World!");
    }
}
