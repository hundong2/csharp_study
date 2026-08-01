/*
문제: 그래프에서 시작 노드 1로부터 BFS 방문 순서를 출력하세요.

기초 문법 포인트:
- Dictionary<int, List<int>>로 인접 리스트를 표현합니다.
- Queue<T>는 BFS에 쓰는 FIFO 컬렉션입니다.
- HashSet<T>로 방문 여부를 기록합니다.
*/

using System;
using System.Collections.Generic;

var graph = new Dictionary<int, List<int>>
{
    [1] = new() { 2, 3 },
    [2] = new() { 4 },
    [3] = new() { 4 },
    [4] = new()
};

var queue = new Queue<int>();
var visited = new HashSet<int>();
var order = new List<int>();

queue.Enqueue(1);
visited.Add(1);

while (queue.Count > 0)
{
    int node = queue.Dequeue();
    order.Add(node);

    foreach (int next in graph[node])
    {
        if (visited.Add(next))
        {
            queue.Enqueue(next);
        }
    }
}

Console.WriteLine($"BFS: {string.Join(" -> ", order)}");

/*
실행 결과:
BFS: 1 -> 2 -> 3 -> 4
*/

