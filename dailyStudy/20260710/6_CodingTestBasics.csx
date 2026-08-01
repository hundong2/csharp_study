/*
문제: 정수 목록에서 중복을 제거하고 오름차순으로 출력하세요.

기초 문법 포인트:
- List<T>는 순서 있는 컬렉션입니다.
- HashSet<T>는 중복 제거에 좋습니다.
- Sort로 리스트를 정렬합니다.
*/

using System;
using System.Collections.Generic;

var values = new List<int> { 5, 1, 3, 5, 2, 3, 1 };
var unique = new HashSet<int>(values);
var sorted = new List<int>(unique);
sorted.Sort();

Console.WriteLine($"Sorted Unique: {string.Join(", ", sorted)}");

/*
실행 결과:
Sorted Unique: 1, 2, 3, 5
*/

