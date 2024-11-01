using System;
using ExampleCollection;

class Program
{
    static void Main(string[] args)
    {
        ExampleSortedList ex = new ExampleSortedList();
        ex.Example1();
        ExampleCollectionTest.ExampleStack();
        ExampleCollectionTest.ExampleQueue();
        ExampleFileTest.ExampleFile();
        ExampleFileTest.ExampleDirectory();
    }
}
