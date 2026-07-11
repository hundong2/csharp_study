using System;
using System.Collections.Generic;

// 변수:
// 값을 담아두는 이름 있는 공간입니다.
// 왼쪽은 타입, 오른쪽은 실제 값입니다.
string serviceName = "OrderApi";
int requestCount = 5;
double latencyMs = 34.7;
bool isHealthy = latencyMs < 100;

Console.WriteLine($"Service={serviceName}, Requests={requestCount}, Healthy={isHealthy}");

// if:
// 조건이 true이면 첫 번째 블록을 실행하고, false이면 else 블록을 실행합니다.
if (isHealthy)
{
    Console.WriteLine("The service is healthy.");
}
else
{
    Console.WriteLine("The service is slow.");
}

// List<string>:
// 문자열 여러 개를 담는 목록입니다.
var routes = new List<string> { "/orders", "/payments", "/health" };

// foreach:
// 컬렉션 안의 값을 하나씩 꺼냅니다.
foreach (string route in routes)
{
    Console.WriteLine($"Route={route}");
}

// 메서드:
// 자주 쓰는 계산이나 동작에 이름을 붙입니다.
static double CalculateAverage(double total, int count)
{
    if (count == 0)
    {
        return 0;
    }

    return total / count;
}

double average = CalculateAverage(total: 173.5, count: requestCount);
Console.WriteLine($"Average latency={average}ms");
