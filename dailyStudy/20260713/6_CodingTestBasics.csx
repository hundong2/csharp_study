/*
문제: 길이가 k인 연속 부분 배열의 최대합을 구하세요.

기초 문법 포인트:
- Sliding Window는 고정 길이 구간을 한 칸씩 이동하며 합을 갱신합니다.
- 새로 들어온 값은 더하고, 창에서 빠진 값은 뺍니다.
*/

using System;

int[] numbers = [3, 1, 5, 2, 6, 4];
int k = 3;
int window = 0;

for (int i = 0; i < k; i++)
{
    window += numbers[i];
}

int best = window;

for (int right = k; right < numbers.Length; right++)
{
    window += numbers[right];
    window -= numbers[right - k];
    best = Math.Max(best, window);
}

Console.WriteLine($"Max Window Sum: {best}");

/*
실행 결과:
Max Window Sum: 13
*/

