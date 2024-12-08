using ExampleConsequence;
class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Sync File Read");
        ExampleFileControl.ExampleSyncGetFileInformation();
        Console.WriteLine("Async File Read");
        var expfctl = new ExampleFileControl();
        expfctl.ExampleAsyncGetFileInformation();

        Console.WriteLine("Read WorkItem Queue");
        expfctl.ExampleUsingQueueUserWorkItem();
    }
}