/*
문제: 두 배열의 내적 구하기

두 정수 배열 a, b가 주어졌을 때 같은 인덱스의 값을 곱해 모두 더한 내적을 구하세요.

입력 예:
a = [1, 2, 3, 4, 5, 6, 7, 8]
b = [8, 7, 6, 5, 4, 3, 2, 1]

기초 문법 포인트:
- for 반복문으로 배열을 인덱스 기반 순회합니다.
- System.Numerics.Vector<T>로 여러 값을 한 번에 처리하는 SIMD 스타일을 연습합니다.
- Vector.IsHardwareAccelerated로 현재 환경의 벡터 가속 가능 여부를 확인합니다.
*/

using System;
using System.Numerics;

int[] a = [1, 2, 3, 4, 5, 6, 7, 8];
int[] b = [8, 7, 6, 5, 4, 3, 2, 1];

int sum = 0;
int i = 0;
int width = Vector<int>.Count;

// Vector<T>:
// - CPU가 지원하는 벡터 폭만큼 값을 묶어 한 번에 계산합니다.
// - 배열 길이가 벡터 폭보다 작거나 나머지가 있으면 일반 for 루프로 마무리합니다.
for (; i <= a.Length - width; i += width)
{
    var va = new Vector<int>(a, i);
    var vb = new Vector<int>(b, i);
    var product = va * vb;

    for (int lane = 0; lane < width; lane++)
    {
        sum += product[lane];
    }
}

for (; i < a.Length; i++)
{
    sum += a[i] * b[i];
}

Console.WriteLine($"Vector Width: {width}");
Console.WriteLine($"Hardware Accelerated: {Vector.IsHardwareAccelerated}");
Console.WriteLine($"Dot Product: {sum}");

/*
실행 결과 예시:
Vector Width: 8
Hardware Accelerated: True
Dot Product: 120

참고: Vector Width와 Hardware Accelerated 값은 실행 환경에 따라 달라질 수 있습니다.
*/

