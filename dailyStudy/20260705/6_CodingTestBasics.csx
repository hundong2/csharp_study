/*
문제: 연속 부분 배열의 합 구하기

정수 리스트와 목표 합이 주어졌을 때, 합이 목표값이 되는 연속 부분 배열의 시작/끝 인덱스를 찾으세요.
모든 숫자는 양수라고 가정합니다.

입력 예:
numbers = [1, 2, 3, 4, 2, 5]
target = 6

기초 문법 포인트:
- List<T>로 순서 있는 데이터를 다룹니다.
- 투 포인터(left/right)로 O(n)에 탐색합니다.
- while로 조건이 깨질 때까지 왼쪽 포인터를 이동합니다.
*/

using System;
using System.Collections.Generic;

var numbers = new List<int> { 1, 2, 3, 4, 2, 5 };
int target = 6;

int left = 0;
int sum = 0;
int answerLeft = -1;
int answerRight = -1;

for (int right = 0; right < numbers.Count; right++)
{
    // List<T>[index]:
    // - 배열처럼 인덱스로 값에 접근합니다.
    sum += numbers[right];

    // 현재 합이 목표보다 크면 왼쪽 값을 빼면서 창을 줄입니다.
    while (sum > target && left <= right)
    {
        sum -= numbers[left];
        left++;
    }

    if (sum == target)
    {
        answerLeft = left;
        answerRight = right;
        break;
    }
}

Console.WriteLine($"Range: {answerLeft}..{answerRight}");
Console.WriteLine($"Values: {string.Join(", ", numbers.GetRange(answerLeft, answerRight - answerLeft + 1))}");

/*
실행 결과:
Range: 0..2
Values: 1, 2, 3
*/

