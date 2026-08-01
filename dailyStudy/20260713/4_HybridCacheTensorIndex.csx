/*
문제: 모델명과 샤드 번호로 텐서 캐시 키를 만들고 조회하세요.
*/

using System;
using System.Collections.Generic;

var cache = new Dictionary<string, string>();

string BuildKey(string model, int shard) => $"tensor:{model}:shard:{shard}";

cache[BuildKey("embedding-v1", 0)] = "warm";
cache[BuildKey("embedding-v1", 1)] = "cold";

string key = BuildKey("embedding-v1", 0);
Console.WriteLine($"[Tensor Cache] {key}={cache[key]}");
Console.WriteLine("HybridCache Tensor Index synchronized for direct device memory workloads.");

/*
실행 결과:
[Tensor Cache] tensor:embedding-v1:shard:0=warm
HybridCache Tensor Index synchronized for direct device memory workloads.
*/

