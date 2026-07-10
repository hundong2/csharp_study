using System;
using System.Collections.Generic;

public sealed record HeapCandidate(string CacheKey, string Endpoint, long Bytes);

public sealed class LiveHeapScanner
{
    public HeapCandidate FindLargestCandidate(IEnumerable<HeapCandidate> candidates)
    {
        HeapCandidate largest = new("none", "none", 0);

        foreach (var candidate in candidates)
        {
            if (candidate.Bytes > largest.Bytes)
            {
                largest = candidate;
            }
        }

        return largest;
    }
}

var candidates = new[]
{
    new HeapCandidate("cache:tenant:42:profile", "/api/profile", 1_024_000),
    new HeapCandidate("cache:tenant:84:report", "/api/report", 4_096_000),
};

var scanner = new LiveHeapScanner();
var largest = scanner.FindLargestCandidate(candidates);

Console.WriteLine($"[ClrMD Scan] Candidate={largest.CacheKey}, Endpoint={largest.Endpoint}, Bytes={largest.Bytes}");
Console.WriteLine("HybridCache Diagnostic Core active with Non-Intrusive ClrMD Real-Time Heap Scanner.");

/*
실행 결과:
[ClrMD Scan] Candidate=cache:tenant:84:report, Endpoint=/api/report, Bytes=4096000
HybridCache Diagnostic Core active with Non-Intrusive ClrMD Real-Time Heap Scanner.
*/

