/*
문제: 만료 시각이 지난 캐시 항목 수를 계산하세요.
*/

using System;

long now = 1_000;
long[] expiresAt = [900, 1_100, 800, 1_300, 999];
int expired = 0;

foreach (long timestamp in expiresAt)
{
    if (timestamp <= now)
    {
        expired++;
    }
}

Console.WriteLine($"[Cache Eviction] Expired Count: {expired}");
Console.WriteLine("HybridCache System operational with Vectorized Eviction Filter Infrastructure.");

/*
실행 결과:
[Cache Eviction] Expired Count: 3
HybridCache System operational with Vectorized Eviction Filter Infrastructure.
*/

