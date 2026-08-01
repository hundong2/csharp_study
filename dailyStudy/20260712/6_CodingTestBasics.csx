/*
문제: 구간 합 빠르게 구하기

배열과 여러 구간 [left, right]가 주어졌을 때 각 구간 합을 출력하세요.

기초 문법 포인트:
- prefix[i + 1] = prefix[i] + numbers[i] 형태로 누적합을 만듭니다.
- 구간 합은 prefix[right + 1] - prefix[left]로 구합니다.
*/

using System;

int[] numbers = [1, 3, 5, 7, 9];
int[] prefix = new int[numbers.Length + 1];

for (int i = 0; i < numbers.Length; i++)
{
    prefix[i + 1] = prefix[i] + numbers[i];
}

int RangeSum(int left, int right) => prefix[right + 1] - prefix[left];

Console.WriteLine($"Sum 1..3: {RangeSum(1, 3)}");
Console.WriteLine($"Sum 0..4: {RangeSum(0, 4)}");

/*
실행 결과:
Sum 1..3: 15
Sum 0..4: 25
*/

