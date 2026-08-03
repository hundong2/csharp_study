using System;
using System.Collections.Generic;
static void ProcessData(ReadOnlySpan<int> data) {
    Console.WriteLine($"[C#] 배열 크기 보존됨! 길이: {data.Length}");
    
    // JIT 컴파일러는 data.Length가 명확하므로, 루프 내부의 인덱스 검사를 제거(Elision)합니다.
    // 이는 C++의 Release 모드 최적화와 결이 같습니다.
    for (int i = 0; i < data.Length; i++) {
        Console.Write(data[i] + " "); // unused-parameter 해결과 동일
    }
    Console.WriteLine();
}

    int[] arr = { 10, 20, 30, 40 };
    // 배열을 포인터로 붕괴시키지 않고, 메모리 뷰(View)를 그대로 넘김
    ProcessData(arr);


// [C#] 배열 크기 보존됨! 길이: 4
// 10 20 30 40 