/*
문제: 가장 자주 등장한 숫자 찾기

정수 배열이 주어졌을 때 가장 많이 등장한 숫자와 등장 횟수를 출력하세요.
등장 횟수가 같은 숫자가 여러 개라면 더 작은 숫자를 선택합니다.

입력 예:
numbers = [3, 1, 2, 3, 2, 3, 1, 2]

기초 문법 포인트:
- Dictionary<TKey, TValue>로 빈도수를 저장합니다.
- foreach로 배열을 순회합니다.
- KeyValuePair를 순회하며 최댓값을 갱신합니다.
*/

using System;
using System.Collections.Generic;

int[] numbers = [3, 1, 2, 3, 2, 3, 1, 2];

// Dictionary<int, int>:
// - key에는 숫자, value에는 등장 횟수를 저장합니다.
// - 코딩 테스트에서 빈도수, 인덱스 위치, 방문 여부를 기록할 때 자주 사용합니다.
var frequency = new Dictionary<int, int>();

foreach (int number in numbers)
{
    // TryGetValue:
    // - key가 있으면 true와 기존 값을 돌려줍니다.
    // - key가 없으면 false이고 count는 int 기본값 0이 됩니다.
    frequency.TryGetValue(number, out int count);
    frequency[number] = count + 1;
}

int answerNumber = 0;
int answerCount = -1;

foreach (KeyValuePair<int, int> entry in frequency)
{
    int number = entry.Key;
    int count = entry.Value;

    // 더 많이 등장했거나, 등장 횟수가 같으면서 숫자가 더 작으면 정답을 갱신합니다.
    if (count > answerCount || (count == answerCount && number < answerNumber))
    {
        answerNumber = number;
        answerCount = count;
    }
}

Console.WriteLine($"Number: {answerNumber}, Count: {answerCount}");

/*
실행 결과:
Number: 2, Count: 3
*/

