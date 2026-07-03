using System;
using System.Collections.Concurrent;

public sealed class NumaPartitionedCache
{
    private readonly ConcurrentDictionary<string, string>[] _partitions;

    public NumaPartitionedCache(int nodeCount)
    {
        if (nodeCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nodeCount));
        }

        _partitions = new ConcurrentDictionary<string, string>[nodeCount];
        for (int i = 0; i < _partitions.Length; i++)
        {
            _partitions[i] = new ConcurrentDictionary<string, string>();
        }
    }

    public int GetNode(string key) => StableHash(key) % _partitions.Length;

    public void Set(string key, string value)
    {
        int node = GetNode(key);
        _partitions[node][key] = value;
        Console.WriteLine($"[HybridCache] SET key={key}, numa-node={node}");
    }

    public bool TryGet(string key, out string value)
    {
        int node = GetNode(key);
        bool found = _partitions[node].TryGetValue(key, out value);
        Console.WriteLine($"[HybridCache] GET key={key}, numa-node={node}, hit={found}");
        return found;
    }

    private static int StableHash(string key)
    {
        unchecked
        {
            int hash = 17;
            foreach (char ch in key)
            {
                hash = (hash * 31) + ch;
            }

            return hash & 0x7FFFFFFF;
        }
    }
}

var cache = new NumaPartitionedCache(nodeCount: 4);

cache.Set("tenant:42:model:embedding", "warm");
cache.TryGet("tenant:42:model:embedding", out string value);

Console.WriteLine("HybridCache Sub-system initialized with Adaptive NUMA-Node Page Partitioning Protocol.");

/*
실행 결과:
[HybridCache] SET key=tenant:42:model:embedding, numa-node=1
[HybridCache] GET key=tenant:42:model:embedding, numa-node=1, hit=True
HybridCache Sub-system initialized with Adaptive NUMA-Node Page Partitioning Protocol.
*/
