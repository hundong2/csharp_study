namespace chapter6Example;
using System;
using ExampleIO;
using ExampleThreading;
using ExampleAysnc;
class Program
{
    static void Main()
    {
        //IO Example
        /*
        ExampleIOFile exampleIOFile = new ExampleIOFile();
        Console.WriteLine("Hello World!");
        exampleIOFile.ExampleInvaildPath();
        exampleIOFile.ExampleTempFile();
        exampleIOFile.ExampleEtcFunction();
        */

        //Thread Example
        /*
        var varThreading = new ExampleThreading();
        varThreading.ExampleBasicTrhead();
        varThreading.ExampleOtherThread();
        varThreading.ExampleOtherThreadUsingJoin();
        varThreading.ExampleThreadUsingParameter();
        varThreading.ExampleThreadUsingMultiParam();
        varThreading.ExampleThreadSafetyUsingInterlocked();
        varThreading.ExampleThreadPool();
        */
        Console.WriteLine("EventHandle Example");
        ExampleEventHandle exampleEventHandle = new ExampleEventHandle();
        exampleEventHandle.ExampleEventHandling();
        exampleEventHandle.ExampleThreadpoolUsingEventHandler();
        //ExampleAysnc.SyncExample.ExampleSync.ExampleSyncDriver();



    }
}