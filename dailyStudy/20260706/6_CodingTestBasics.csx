/*
문제: 괄호 문자열 검증하기

문자열에 들어 있는 (), {}, [] 괄호가 올바르게 닫히는지 검사하세요.

입력 예:
text = "{[()()]}"

기초 문법 포인트:
- Stack<T>로 마지막에 열린 괄호를 먼저 닫는 LIFO 구조를 구현합니다.
- switch 식으로 닫는 괄호에 대응하는 여는 괄호를 구합니다.
- ContainsKey로 Dictionary에 key가 있는지 확인합니다.
*/

using System;
using System.Collections.Generic;

string text = "{[()()]}";

var pairs = new Dictionary<char, char>
{
    [')'] = '(',
    ['}'] = '{',
    [']'] = '['
};

var stack = new Stack<char>();
bool valid = true;

foreach (char ch in text)
{
    if (ch == '(' || ch == '{' || ch == '[')
    {
        // Push:
        // - 스택 맨 위에 값을 추가합니다.
        stack.Push(ch);
    }
    else if (pairs.ContainsKey(ch))
    {
        // Count:
        // - 스택에 남아 있는 원소 개수입니다.
        // Pop:
        // - 스택 맨 위 값을 꺼내면서 제거합니다.
        if (stack.Count == 0 || stack.Pop() != pairs[ch])
        {
            valid = false;
            break;
        }
    }
}

if (stack.Count > 0)
{
    valid = false;
}

Console.WriteLine($"Valid: {valid}");

/*
실행 결과:
Valid: True
*/

