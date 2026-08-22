using System;
using System.Collections.Generic;

Execute();

void Execute()
{
    Console.WriteLine("Using span, slice");
    string dataString = "2026-08-19 14:30:00";

    ReadOnlySpan<char> dataSpan = dataString.AsSpan();
    ReadOnlySpan<char> datePart = dataSpan.Slice(0, 10); // "2026-08-19"
    ReadOnlySpan<char> timePart = dataSpan.Slice(11, 8); // "14:30:00"

    int year = int.Parse(datePart.Slice(0, 4));

    Console.WriteLine($"year: {year}");
    Console.WriteLine($"year: {year}".GetType().Name); // Int32
}

// 트래픽이 몰리는 웹 서버(ASP.NET Core)의 미들웨어에서 이러한 처리는 서버의 응답 속도를 비약적으로 상승시킵니다.
void UsingStackAllocate()
{
    Console.WriteLine("Using stackalloc to allocate a buffer for Guid formatting:");
    Span<char> stackBuffer = stackalloc char[36];
    // Guid를 할당 없이 스택 버퍼에 바로 기록
    if (Guid.NewGuid().TryFormat(stackBuffer, out int charsWritten))
    {
        // 스택에 기록된 데이터를 기반으로 무언가를 처리
        Console.WriteLine(stackBuffer.Slice(0, charsWritten).ToString());
    }
}

// //Result Output 
// Using span, slice
// year: 2026

void HybridSpanExample()
{
   char[] pooledArray = null; //임시 버퍼를 저장할 힙 배열 참조 

   int length = 2048; // 예시 길이
   Span<char> buffer = length <= 1024
         ? stackalloc char[length] // 길이가 1024 이하이면 스택에 할당
         : (pooledArray = ArrayPool<char>.Shared.Rent(length)); // 길이가 1024 초과이면 힙에서 임시 배열을 빌림

    // 사용이 끝난 후 힙에서 빌린 배열을 반환
    if (pooledArray != null)
    {
        ArrayPool<char>.Shared.Return(pooledArray);
    }
    //ArrayPool을 사용하면 힙 할당을 최소화하면서도 큰 버퍼를 효율적으로 재사용할 수 있습니다.
    // 이 패턴은 특히 고성능 서버 애플리케이션에서 유용합니다.
    //CommunityToolkit.HighPerformance
    
}