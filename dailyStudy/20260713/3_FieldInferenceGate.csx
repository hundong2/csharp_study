/*
문제: 동시에 하나의 추론 배치만 실행되도록 CAS 게이트를 구현하세요.
*/

using System;
using System.Threading;

public sealed class InferenceGate
{
    private int _active;

    public bool TryEnter() => Interlocked.CompareExchange(ref _active, 1, 0) == 0;
    public void Exit() => Interlocked.Exchange(ref _active, 0);
    public int Active => Volatile.Read(ref _active);
}

var gate = new InferenceGate();
Console.WriteLine($"[Inference Gate] Enter 1: {gate.TryEnter()} | Active: {gate.Active}");
Console.WriteLine($"[Inference Gate] Enter 2: {gate.TryEnter()} | Active: {gate.Active}");

/*
실행 결과:
[Inference Gate] Enter 1: True | Active: 1
[Inference Gate] Enter 2: False | Active: 1
*/

