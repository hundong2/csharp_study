/*
문제: Two Sum

정수 배열 nums와 target이 주어졌을 때, 합이 target이 되는 두 인덱스를 찾으세요.

기초 문법 포인트:
- Dictionary<int, int>에 값과 인덱스를 저장합니다.
- ContainsKey 또는 TryGetValue로 보수(complement)를 O(1)에 찾습니다.
*/

using System;
using System.Collections.Generic;

int[] nums = [2, 7, 11, 15];
int target = 9;
var indexByValue = new Dictionary<int, int>();

for (int i = 0; i < nums.Length; i++)
{
    int complement = target - nums[i];

    if (indexByValue.TryGetValue(complement, out int previousIndex))
    {
        Console.WriteLine($"Answer: {previousIndex}, {i}");
        break;
    }

    indexByValue[nums[i]] = i;
}

/*
실행 결과:
Answer: 0, 1
*/

