using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExampleConsequence
{
    public class ExampleDelegate
    {
        public class Calc
        {
            public static long Cumsum(int start, int end)
            {
                long sum = 0;
                for(int i = start; i <= end; i++ )
                {
                    sum += i;
                }

                return sum;
            }
        }
        public ExampleDelegate() { }
        public delegate long CalcMethod(int start, int end);

        public void Run()
        {
            CalcMethod calc = new CalcMethod(Calc.Cumsum);
            long result = calc(1, 100);
            Console.WriteLine(result);
        }
        public async void RunUsingInvoke()
        {
            CalcMethod calc = new CalcMethod(Calc.Cumsum);

            //Not Support .NET core 
            /*
            IAsyncResult ar = calc.BeginInvoke(1, 100, null, null); 
            ar.AsyncWaitHandle.WaitOne();
            long result = calc.EndInvoke(ar); 
            */
            var result = await Task.Run(() => Calc.Cumsum(1, 100));
            
            Console.WriteLine($"Using Invoke example : {result}");
        }
    }
}
