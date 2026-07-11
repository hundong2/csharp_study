using System;
using System.Collections.Generic;

// 실무 패턴: Batching
// DB insert, 파일 쓰기, 네트워크 전송은 하나씩 처리하는 것보다 묶어서 처리하는 편이 효율적일 때가 많습니다.

public static IEnumerable<List<T>> Batch<T>(IEnumerable<T> source, int size)
{
    var bucket = new List<T>(capacity: size);

    foreach (T item in source)
    {
        bucket.Add(item);

        if (bucket.Count == size)
        {
            yield return bucket;
            bucket = new List<T>(capacity: size);
        }
    }

    if (bucket.Count > 0)
    {
        yield return bucket;
    }
}

int[] sensorValues = [10, 11, 12, 13, 14, 15, 16];

foreach (List<int> batch in Batch(sensorValues, size: 3))
{
    Console.WriteLine($"[Batch] Send {batch.Count} items: {string.Join(", ", batch)}");
}
