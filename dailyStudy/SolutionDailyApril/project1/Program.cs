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
        }
    }
