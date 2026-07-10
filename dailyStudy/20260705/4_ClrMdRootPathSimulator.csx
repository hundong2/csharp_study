using System;
using System.Collections.Generic;

public sealed class ObjectGraph
{
    private readonly Dictionary<string, List<string>> _edges = new();

    public void AddReference(string from, string to)
    {
        if (!_edges.TryGetValue(from, out var targets))
        {
            targets = new List<string>();
            _edges[from] = targets;
        }

        targets.Add(to);
    }

    public IReadOnlyList<string> FindPath(string root, string target)
    {
        // Queue<T>:
        // - 너비 우선 탐색(BFS)에 적합한 FIFO 컬렉션입니다.
        var queue = new Queue<List<string>>();
        var visited = new HashSet<string>();

        queue.Enqueue(new List<string> { root });
        visited.Add(root);

        while (queue.Count > 0)
        {
            var path = queue.Dequeue();
            string current = path[^1];

            if (current == target)
            {
                return path;
            }

            if (!_edges.TryGetValue(current, out var nextNodes))
            {
                continue;
            }

            foreach (string next in nextNodes)
            {
                if (visited.Add(next))
                {
                    var nextPath = new List<string>(path) { next };
                    queue.Enqueue(nextPath);
                }
            }
        }

        return Array.Empty<string>();
    }
}

var graph = new ObjectGraph();
graph.AddReference("GC Root", "HybridCache");
graph.AddReference("HybridCache", "TenantEntry");
graph.AddReference("TenantEntry", "LeakedPayload");

var rootPath = graph.FindPath("GC Root", "LeakedPayload");
Console.WriteLine($"[ClrMD Sim] Root path: {string.Join(" -> ", rootPath)}");

/*
실행 결과:
[ClrMD Sim] Root path: GC Root -> HybridCache -> TenantEntry -> LeakedPayload
*/

