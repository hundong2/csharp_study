using System;
using UtilityLibrary;


    public class Program
    {
        static void Main(string[] args)
        {
            var stringVariable = stringLib.GetInstance("hello world!!");
            Console.WriteLine(stringVariable.GetStringValue());


            var exampleAdvance = exampleAdvancedTrick.GetInstance();
            var value = exampleAdvance.GetCalculate(10,5);

            //Example Pattern Matching 
            var examplePattern = new ExamplePatternMatching();
            examplePattern.TestPatternMatching(examplePattern._circle);
            examplePattern.TestPatternMatching(examplePattern._square);

            //Local functions for encapsulation
            var _fibonacci = Fibonacci(5);
        }

        /// <summary>
        /// Local functions for encapsulation
        /// </summary>
        /// <param name="n"></param>
        /// <returns></returns>
        static public IEnumerable<int> Fibonacci(int n)
        {
            int Fib(int term) => term <= 2 ? 1 : Fib(term - 1) + Fib(term - 2);
            return Enumerable.Range(1, n).Select(Fib);
        }
    }
