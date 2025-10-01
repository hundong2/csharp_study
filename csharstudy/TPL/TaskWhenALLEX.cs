using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;

class Program
{
    static async Task Main(string[] args)
    {
        var urls = new List<string>
        {
            "https://www.naver.com",
            "https://www.daum.net",
            "https://www.google.com"
        };

        var tasks = new List<Task<string>>();

        foreach (var url in urls)
        {
            tasks.Add(FetchDataAsync(url));
        }

        // Wait for all tasks to complete
        var results = await Task.WhenAll(tasks);

        foreach (var result in results)
        {
            Console.WriteLine(result);
        }
    }

    static async Task<string> FetchDataAsync(string url)
    {
        using (var client = new HttpClient())
        {
            return await client.GetStringAsync(url);
        }
    }
}