using System;
using System.Collections.Generic;
using System.Linq;

// 이 예제는 HybridCache 자체 구현이 아니라 CRDT의 핵심 아이디어를 학습하기 위한 모델입니다.
// CRDT는 Conflict-free Replicated Data Type의 약자입니다.
// 여러 리전/노드가 동시에 값을 바꿔도 나중에 병합 규칙만으로 같은 결과에 도달하게 만드는 자료구조입니다.

public sealed class GCounter
{
    // Dictionary<TKey, TValue>:
    // - key로 값을 찾는 자료구조입니다.
    // - 여기서는 replica 이름별 카운터 값을 저장합니다.
    private readonly Dictionary<string, long> _counts = new();

    // expression-bodied property:
    // - get만 있는 간단한 속성을 => 로 짧게 쓸 수 있습니다.
    // - 모든 replica의 값을 더한 것이 전체 카운터 값입니다.
    public long Value => _counts.Values.Sum();

    public void Increment(string replica, long amount = 1)
    {
        // amount = 1:
        // - 기본 매개변수입니다. 호출자가 amount를 생략하면 1이 들어갑니다.
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "GCounter only supports increments.");
        }

        // TryGetValue:
        // - key가 있으면 true와 기존 값을 돌려줍니다.
        // - key가 없으면 false이고 current는 기본값 0입니다.
        _counts.TryGetValue(replica, out long current);
        _counts[replica] = current + amount;
    }

    public void Merge(GCounter other)
    {
        // CRDT G-Counter 병합 규칙:
        // - replica별로 더 큰 값을 선택합니다.
        // - 같은 이벤트가 여러 번 병합되어도 값이 중복 증가하지 않습니다.
        foreach ((string replica, long otherValue) in other._counts)
        {
            _counts.TryGetValue(replica, out long current);
            _counts[replica] = Math.Max(current, otherValue);
        }
    }
}

var asia = new GCounter();
var us = new GCounter();

asia.Increment("asia-seoul", 3);
us.Increment("us-oregon", 5);

// 서로 다른 리전에서 동시에 증가했다고 가정합니다.
// 중앙 락을 잡지 않고 각 리전이 먼저 로컬 업데이트를 수행합니다.
asia.Merge(us);
us.Merge(asia);

Console.WriteLine($"[CRDT] Asia View: {asia.Value}");
Console.WriteLine($"[CRDT] US View: {us.Value}");
Console.WriteLine("[CRDT] Both regions converged to the same value.");
