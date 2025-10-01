# Task Parallel Library 

## Parallel 

```csharp
// Sequential version
foreach (var item in sourceCollection)
{
    Process(item);
}
// Parallel equivalent
Parallel.ForEach(sourceCollection, item => Process(item));
```

- Using ConcurrentBag with Parallel ForEach
  - [Parallel Example Code](./ParallelEx.cs)  

## Task.WhenALL and Task.WhenAny

- [Task.WhenAll](./TaskWhenALLEX.cs). 

## Multiple IO Concurrently 

- [Multiple IO](./MultipleIOConcurrently.cs)  
- [Explaination](./MultipleIO.md). 

## Parallel Coding Exercise

- https://microsoftlearning.github.io/mslearn-develop-oop-csharp/Instructions/Labs/l2p2-lp5-m3-exercise-implement-async-tasks.html

# Summary 

Summary
Completed
100 XP
2 minutes
In this module, you learned about asynchronous programming techniques and their importance in enhancing application performance and responsiveness. The module explained how C# supports a simplified approach to async programming, making it easier to write, debug, and maintain asynchronous code. You also learned about the implementation of asynchronous file input and output operations in C#, and how they improve application performance, especially when dealing with large files or significant data writing. The module also covered accessing web resources asynchronously using standard web protocols like HTTP or HTTPS, and the use of the HttpClient class in C#. Lastly, you learned about parallel programming in C#, the Task Parallel Library (TPL), and its role in executing multiple tasks simultaneously.

The main takeaways from this module include understanding the benefits of asynchronous programming and how asynchronous tasks help to unblock the user interface. You learned how to create async methods using the async keyword and call them using the await keyword in C#. The module emphasized the importance of using System.IO and System.Text.Json namespaces for file operations. You also learned about the HttpClient class for making asynchronous HTTP requests to web resources. The module highlighted the importance of understanding threading concepts for effective use of the TPL and the common pitfalls to avoid when writing parallel code.

Other reading
[Asynchronous Programming with async and await (C#)](https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/async-scenarios).  
[Parallel programming in .NET: A guide to the documentation](https://learn.microsoft.com/en-us/dotnet/standard/parallel-programming/).   

## reference 

- https://nikiforovall.blog/csharp/compcsi/dotnet/2020/10/20/awaitable-pattern.html
- 