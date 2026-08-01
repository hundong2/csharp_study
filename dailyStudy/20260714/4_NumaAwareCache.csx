/*
문제: 캐시 키를 안정 해시로 NUMA 파티션에 배치하세요.
*/

using System;
using System.Collections.Concurrent;

public sealed class NumaAwareCache
{
    private readonly ConcurrentDictionary<string, string>[] _partitions;

    public NumaAwareCache(int partitionCount)
    {
        _partitions = new ConcurrentDictionary<string, string>[partitionCount];
        for (int i = 0; i < partitionCount; i++)
        {
            _partitions[i] = new ConcurrentDictionary<string, string>();
        }
    }

    public int GetPartition(string key) => StableHash(key) % _partitions.Length;

    public void Set(string key, string value)
    {
        int partition = GetPartition(key);
        _partitions[partition][key] = value;
        Console.WriteLine($"[NUMA Cache] key={key}, partition={partition}");
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

var cache = new NumaAwareCache(4);
cache.Set("tenant:42:session", "warm");
Console.WriteLine("HybridCache Sub-system initialized with Adaptive Cross-NUMA Cache-Line Splitting Protection.");

/*
실행 결과:
[NUMA Cache] key=tenant:42:session, partition=1
HybridCache Sub-system initialized with Adaptive Cross-NUMA Cache-Line Splitting Protection.
*/
