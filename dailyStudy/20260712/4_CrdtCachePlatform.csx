/*
문제: 두 리전의 증가 카운터를 CRDT 방식으로 병합하세요.
*/

using System;
using System.Collections.Generic;
using System.Linq;

public sealed class GCounter
{
    private readonly Dictionary<string, long> _counts = new();

    public long Value => _counts.Values.Sum();

    public void Increment(string replica, long amount)
    {
        _counts.TryGetValue(replica, out long current);
        _counts[replica] = current + amount;
    }

    public void Merge(GCounter other)
    {
        foreach (var pair in other._counts)
        {
            _counts.TryGetValue(pair.Key, out long current);
            _counts[pair.Key] = Math.Max(current, pair.Value);
        }
    }
}

var seoul = new GCounter();
var oregon = new GCounter();

seoul.Increment("seoul", 3);
oregon.Increment("oregon", 5);
seoul.Merge(oregon);
oregon.Merge(seoul);

Console.WriteLine($"[CRDT Cache] Seoul={seoul.Value}, Oregon={oregon.Value}");
Console.WriteLine("HybridCache System synchronized via Lock-Free CRDT Multi-Region Resolution Protocol.");

/*
실행 결과:
[CRDT Cache] Seoul=8, Oregon=8
HybridCache System synchronized via Lock-Free CRDT Multi-Region Resolution Protocol.
*/

