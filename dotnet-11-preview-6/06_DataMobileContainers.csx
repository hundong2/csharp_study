/*
실행: dotnet script 06_DataMobileContainers.csx
선행 문서: 05-maui-ef-fsharp-containers.md
목표: EF FullJoin/NULLIF, MAUI 거리 필터, container 절감률을 순수 C#으로 연습합니다.
*/

#nullable enable

// 01. 기본 수학과 출력 API를 사용합니다.
using System;
// 02. generic collection을 사용합니다.
using System.Collections.Generic;
// 03. LINQ의 조회/정렬 helper를 사용합니다.
using System.Linq;

// 04. 왼쪽 customer row를 나타냅니다.
public sealed class Customer
{
    // 05. primary key 역할입니다.
    public int Id { get; init; }
    // 06. 표시 이름입니다.
    public string Name { get; init; } = "";
}

// 07. 오른쪽 order row를 나타냅니다.
public sealed class Order
{
    // 08. order key입니다.
    public int Id { get; init; }
    // 09. customer foreign-key 값이며 실제 constraint 여부와는 별개입니다.
    public int CustomerId { get; init; }
}

// 10. 위치 sample의 위도/경도를 단순 평면 좌표 meter로 표현합니다.
public sealed class Point
{
    // 11. X 좌표입니다.
    public double X { get; init; }
    // 12. Y 좌표입니다.
    public double Y { get; init; }
}

// 13. 샘플 customer 두 명을 만듭니다.
var customers = new[]
{
    // 14-1. Id 1인 customer는 아래 order 100과 일치합니다.
    new Customer { Id = 1, Name = "Ada" },
    // 14-2. Id 2인 customer는 일치하는 order가 없어 FULL JOIN에서 오른쪽이 null입니다.
    new Customer { Id = 2, Name = "Grace" }
};
// 14. customer 1의 주문과 orphan order 하나를 만듭니다.
var orders = new[]
{
    // 15-1. CustomerId 1은 Ada와 일치합니다.
    new Order { Id = 100, CustomerId = 1 },
    // 15-2. CustomerId 99는 customer가 없어 FULL JOIN에서 왼쪽이 null입니다.
    new Order { Id = 200, CustomerId = 99 }
};

// 16. Select lambda가 각 customer에서 Id만 뽑습니다.
IEnumerable<int> customerIds = customers.Select(c => c.Id);
// 17. 각 order에서는 foreign-key인 CustomerId를 뽑습니다.
IEnumerable<int> orderCustomerIds = orders.Select(o => o.CustomerId);
// 18. Union은 중복을 제거한 양쪽 key 합집합을 만듭니다.
IEnumerable<int> allIds = customerIds.Union(orderCustomerIds);
// 19. OrderBy가 출력 확인을 쉽도록 key를 오름차순 정렬합니다.
IEnumerable<int> orderedIds = allIds.OrderBy(id => id);
// 20. 지연 실행 LINQ 결과를 실제 배열로 materialize합니다.
int[] keys = orderedIds.ToArray();
// 21. 각 key에 대해 왼쪽/오른쪽 일치 row를 찾습니다.
foreach (int key in keys)
{
    // 22. 없으면 null이며 DB FULL JOIN의 unmatched left/right와 같습니다.
    Customer? customer = customers.SingleOrDefault(c => c.Id == key);
    // 23. 학습 데이터에는 key별 order 하나만 있다고 가정합니다.
    Order? order = orders.SingleOrDefault(o => o.CustomerId == key);
    // 24. null conditional/coalescing으로 빠진 쪽을 표시합니다.
    Console.WriteLine($"key={key}, customer={customer?.Name ?? "NULL"}, order={order?.Id.ToString() ?? "NULL"}");
}

// 25. SQL NULLIF(value, 0)은 두 값이 같으면 null을 돌려줍니다.
int status = 0;
// 26. C# nullable int로 동일 의미를 재현합니다.
int? normalized = status == 0 ? null : status;
// 27. EF Core Preview 6은 이 모양을 CASE보다 NULLIF로 번역할 수 있습니다.
Console.WriteLine($"NULLIF equivalent = {normalized?.ToString() ?? "NULL"}");

// 28. 위치 update sample을 만듭니다.
var points = new[]
{
    // 28-1. 최초 위치입니다.
    new Point { X = 0, Y = 0 },
    // 28-2. 최초 위치에서 5m라 threshold보다 작습니다.
    new Point { X = 3, Y = 4 },
    // 28-3. 최초 위치에서 13m라 전달 대상입니다.
    new Point { X = 12, Y = 5 }
};
// 29. MAUI Preview 6 MinimumDistance와 같은 threshold를 10m로 둡니다.
double minimumDistance = 10;
// 30. 마지막으로 전달한 위치를 보관합니다.
Point lastPublished = points[0];
// 31. 첫 sample은 항상 전달됐다고 가정합니다.
Console.WriteLine($"location published: ({lastPublished.X}, {lastPublished.Y})");
// 32. 이후 sample을 검사합니다.
foreach (Point candidate in points.Skip(1))
{
    // 33. 두 위치의 가로 차이를 구합니다.
    double deltaX = candidate.X - lastPublished.X;
    // 34. 두 위치의 세로 차이를 구합니다.
    double deltaY = candidate.Y - lastPublished.Y;
    // 35. 피타고라스 공식으로 거리를 구합니다; 실제 GPS는 지구 곡률과 정확도도 고려합니다.
    double distance = Math.Sqrt(Math.Pow(deltaX, 2) + Math.Pow(deltaY, 2));
    // 36. threshold 이상 이동했을 때만 update를 전달합니다.
    if (distance >= minimumDistance)
    {
        lastPublished = candidate;
        Console.WriteLine($"location published after {distance:F1}m");
    }
}

// 37. container image 이전/이후 MB 값을 decimal로 둡니다.
decimal beforeMb = 401.2m;
// 38. Alpine NativeAOT SDK Preview 6 크기입니다.
decimal afterMb = 277.3m;
// 39. 절대 절감량입니다.
decimal savedMb = beforeMb - afterMb;
// 40. 상대 절감률은 saved/before*100입니다.
decimal savedPercent = savedMb / beforeMb * 100m;
// 41. 공식 표의 약 123.9MB, 30.9%와 비교합니다.
Console.WriteLine($"AOT SDK image saved = {savedMb:F1} MB ({savedPercent:F1}%)");
