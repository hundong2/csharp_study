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

        Console.WriteLine("Example delegate function call");
        var delegateExample = new ExampleDelegate();
        delegateExample.Run();

        Console.WriteLine("Example delegate function call using BeginInvoke");
        delegateExample.RunUsingInvoke();
    }
}