/*
문제: 정렬된 배열에서 target의 인덱스를 이진 탐색으로 찾으세요.

기초 문법 포인트:
- while 루프로 left <= right 범위를 유지합니다.
- mid 계산은 left + (right - left) / 2 형태가 안전합니다.
*/

using System;

int[] sorted = [1, 3, 5, 7, 9, 11, 13];
int target = 9;
int left = 0;
int right = sorted.Length - 1;
int answer = -1;

while (left <= right)
{
    int mid = left + (right - left) / 2;

    if (sorted[mid] == target)
    {
        answer = mid;
        break;
    }

    if (sorted[mid] < target)
    {
        left = mid + 1;
    }
    else
    {
        right = mid - 1;
    }
}

Console.WriteLine($"Index: {answer}");

/*
실행 결과:
Index: 4
*/

