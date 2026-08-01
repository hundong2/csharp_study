/*
문제: TelemetryMetric의 RawValue를 화면 좌표로 변환하세요.
*/

using System;

public readonly record struct TelemetryMetric(long SequenceId, double RawValue);

public static class MetricUiExtension
{
    public static double GetScreenCoordinate(TelemetryMetric metric, double scale)
        => metric.RawValue * scale;
}

var metric = new TelemetryMetric(44102, 120.5d);
Console.WriteLine($"[C# UI] Screen X/Y Coordinate calculated directly: {MetricUiExtension.GetScreenCoordinate(metric, 1.5d)}");

/*
실행 결과:
[C# UI] Screen X/Y Coordinate calculated directly: 180.75
*/
