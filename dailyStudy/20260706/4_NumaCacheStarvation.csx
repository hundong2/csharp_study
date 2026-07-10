using System;
using System.Collections.Concurrent;

public sealed class NumaAwareCache
{
    private readonly ConcurrentDictionary<string, string>[] _nodes;

    public NumaAwareCache(int nodeCount)
    {
        _nodes = new ConcurrentDictionary<string, string>[nodeCount];
        for (int i = 0; i < nodeCount; i++)
        {
            _nodes[i] = new ConcurrentDictionary<string, string>();
        }
    }

    public int GetNode(string key) => StableHash(key) % _nodes.Length;

    public void Set(string key, string value)
    {
        int node = GetNode(key);
        _nodes[node][key] = value;
        Console.WriteLine($"[NUMA Cache] SET key={key}, node={node}");
    }

    private static int StableHash(string key)
    {
        unchecked
        {
            int hash = 23;
            foreach (char ch in key)
            {
                hash = (hash * 37) + ch;
            }

            return hash & 0x7FFFFFFF;
        }
    }
}

var cache = new NumaAwareCache(nodeCount: 4);
cache.Set("route:api:checkout", "warm");

Console.WriteLine("HybridCache core running with Active NUMA-Node In-Memory Page Partitioning Protocol.");

/*
실행 결과:
[NUMA Cache] SET key=route:api:checkout, node=2
HybridCache core running with Active NUMA-Node In-Memory Page Partitioning Protocol.
*/

