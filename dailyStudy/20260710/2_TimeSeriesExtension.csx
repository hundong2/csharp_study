/*
문제: 시계열 데이터 포인트의 기준값 대비 차이를 계산하세요.
*/

using System;

public readonly record struct RawDataPoint(long Id, double Value);

public static class DataAnalyzerExtension
{
    // dotnet script에서는 확장 메서드 선언이 중첩 클래스로 처리될 수 있어 일반 정적 메서드로 작성합니다.
    public static double ComputeDelta(RawDataPoint point, double baseline)
        => point.Value - baseline;
}

var point = new RawDataPoint(1001, 150.75d);
Console.WriteLine($"[C# TSDB] Computed Delta directly: {DataAnalyzerExtension.ComputeDelta(point, 100.0d)}");

/*
실행 결과:
[C# TSDB] Computed Delta directly: 50.75
*/
