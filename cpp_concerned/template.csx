using System;

class Wrapper<T> {
    public T Value { get; }
    public Wrapper(T v) {
        Value = v;
        Console.WriteLine($"타입: {typeof(T)}");
    }
}


// C++의 CTAD와 달리 "var"를 쓰더라도 배열 객체 그대로 추론됩니다.
// C++처럼 포인터로 Decay 되지 않으며, 길이는 객체 내부 프로퍼티(Length)로 관리됩니다.
var arr = new char[] { 'H', 'e', 'l', 'l', 'o' };
var w = new Wrapper<char[]>(arr); // 타입: System.Char[]
