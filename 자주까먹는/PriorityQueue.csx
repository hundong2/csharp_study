using System;
using System.Collections.Generic;

// .NET 6.0에서 새롭게 추가된 PriorityQueue<TElement, TPriority>는 우선순위 큐를 구현한 컬렉션입니다.
RunPriorityQueueExample();
void RunPriorityQueueExample()
{
    Console.WriteLine("PriorityQueue Example:");

    // 우선순위 큐 생성 (최소 힙)
    PriorityQueue<string, int> priorityQueue = new();

    // 요소 추가 (값, 우선순위)
    priorityQueue.Enqueue("Task A", 3);
    priorityQueue.Enqueue("Task B", 1);
    priorityQueue.Enqueue("Task C", 2);

    // 우선순위에 따라 요소 제거
    while (priorityQueue.Count > 0)
    {
        var item = priorityQueue.Dequeue();
        Console.WriteLine($"Dequeued: {item}");
    }
}

void RunPriorityQueueExampleWithCustomComparer()
{
    Console.WriteLine("PriorityQueue Example with Custom Comparer:");

    // 우선순위 큐 생성 (최대 힙)
    PriorityQueue<string, int> priorityQueue = new(Comparer<int>.Create((x, y) => y.CompareTo(x)));
    //Comparer<int>.Create((x, y) => y.CompareTo(x))는 우선순위 큐를 최대 힙으로 동작하도록 설정합니다.
    // 요소 추가 (값, 우선순위)
    priorityQueue.Enqueue("Task A", 3);
    priorityQueue.Enqueue("Task B", 1);
    priorityQueue.Enqueue("Task C", 2);

    // 우선순위에 따라 요소 제거
    while (priorityQueue.Count > 0)
    {
        var item = priorityQueue.Dequeue();
        Console.WriteLine($"Dequeued: {item}");
    }
}
// Result Output
// PriorityQueue Example:
// Dequeued: Task B
// Dequeued: Task C
// Dequeued: Task A

RunPriorityQueueExampleWithTuple();
void RunPriorityQueueExampleWithTuple()
{
    Console.WriteLine("PriorityQueue Example with Tuple:");

    // 우선순위 큐 생성 (최소 힙)
    PriorityQueue<(string Task, int Priority), int> priorityQueue = new();

    // 요소 추가 (값, 우선순위)
    priorityQueue.Enqueue(("Task A", 3), 3);
    priorityQueue.Enqueue(("Task B", 1), 1);
    priorityQueue.Enqueue(("Task C", 2), 2);

    // 우선순위에 따라 요소 제거
    while (priorityQueue.Count > 0)
    {
        var item = priorityQueue.Dequeue();
        Console.WriteLine($"Dequeued: {item.Task} with Priority: {item.Priority}");
    }
}