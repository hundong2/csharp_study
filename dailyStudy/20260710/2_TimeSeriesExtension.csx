using System;

public readonly record struct RawDataPoint(long Id, double Value);

public static class DataPointAnalyzer
{
    public static double ComputeDelta(in RawDataPoint point, double baseline)
    {
        // in 매개변수:
        // 구조체를 복사하지 않고 읽기 전용 참조로 전달합니다.
        return point.Value - baseline;
    }
}

var point = new RawDataPoint(1001, 150.75d);
double delta = DataPointAnalyzer.ComputeDelta(in point, 100.0d);

Console.WriteLine($"[TSDB] Computed Delta directly: {delta}");
