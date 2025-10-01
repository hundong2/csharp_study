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


## reference 

- https://nikiforovall.blog/csharp/compcsi/dotnet/2020/10/20/awaitable-pattern.html
- 