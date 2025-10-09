using System.Threading.Tasks;

namespace ConsoleApp;

public class Program
{
    public static async Task Main()
    {
        //비동기 스트림을 위해 foreach문에 await 키워드 사용 
        await foreach (var number in GetNumbersAsync(10))
        {
            Console.WriteLine($"{number} {Thread.CurrentThread.ManagedThreadId}");
        }
        Console.WriteLine($"{Thread.CurrentThread.ManagedThreadId} Main End");
    }

    //IEnumerable<T> 대신 IAsyncEnumerable<T> 반환
    //비동기 메서드이므로 async 한정자 필요
    public static async IAsyncEnumerable<int> GetNumbersAsync(int count)
    {
        for (int i = 0; i < count; i++)
        {
            //Thread.Sleep(100); // 100ms 지연
            await Task.Run(() => Thread.Sleep(100)); // 작업을 Task로 변경한 후 await 호출
            yield return i;
        }
    }
}