# YARP (Yet Another Reverse Proxy) - 시작하기

> 참고 문서: [Microsoft Learn - YARP 시작하기](https://learn.microsoft.com/ko-kr/aspnet/core/fundamentals/servers/yarp/getting-started?view=aspnetcore-10.0)

---

## YARP란?

**YARP**는 Microsoft에서 만든 오픈소스 리버스 프록시 라이브러리입니다.  
ASP.NET Core 위에서 동작하며, 복잡한 프록시/게이트웨이 시나리오를 코드 또는 설정 파일로 간단하게 구성할 수 있습니다.

- GitHub: https://github.com/microsoft/reverse-proxy
- NuGet: `Yarp.ReverseProxy`

---

## 1. 프로젝트 생성

```bash
dotnet new web -n YarpGettingStarted
cd YarpGettingStarted
```

---

## 2. YARP NuGet 패키지 추가

```bash
dotnet add package Yarp.ReverseProxy
```

---

## 3. Program.cs — YARP 미들웨어 등록

```csharp
var builder = WebApplication.CreateBuilder(args);

// ① YARP 리버스 프록시 서비스를 DI 컨테이너에 등록하고
//    appsettings.json 의 "ReverseProxy" 섹션에서 설정을 읽어옵니다.
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

// ② YARP 미들웨어를 파이프라인에 추가합니다.
app.MapReverseProxy();

app.Run();
```

---

## 4. appsettings.json — 라우트·클러스터 설정

```json
{
  "ReverseProxy": {
    "Routes": {
      "route1": {
        "ClusterId": "cluster1",
        "Match": {
          "Path": "{**catch-all}"
        }
      }
    },
    "Clusters": {
      "cluster1": {
        "Destinations": {
          "destination1": {
            "Address": "https://example.com/"
          }
        }
      }
    }
  }
}
```

### 핵심 개념

| 용어 | 설명 |
|------|------|
| **Route** | 들어오는 요청을 어느 클러스터로 보낼지 결정하는 규칙 |
| **Cluster** | 실제 업스트림(백엔드) 서버들의 그룹 |
| **Destination** | 클러스터 안의 개별 업스트림 서버 주소 |
| **Path** | 매칭할 URL 경로 패턴 (`{**catch-all}` = 모든 경로) |

---

## 5. 실행

```bash
dotnet run
```

브라우저에서 `http://localhost:5000` 을 열면 `https://example.com/` 으로 요청이 프록시됩니다.

---

## 6. Deep Dive — 주요 확장 포인트

### 6-1. 헤더 변환 (Transforms)

```json
"Routes": {
  "route1": {
    "ClusterId": "cluster1",
    "Match": { "Path": "{**catch-all}" },
    "Transforms": [
      { "RequestHeader": "X-Forwarded-For", "Append": "{RemoteIpAddress}" }
    ]
  }
}
```

### 6-2. 로드 밸런싱 정책

```json
"Clusters": {
  "cluster1": {
    "LoadBalancingPolicy": "RoundRobin",
    "Destinations": {
      "dest1": { "Address": "https://backend1.example.com/" },
      "dest2": { "Address": "https://backend2.example.com/" }
    }
  }
}
```

지원 정책: `FirstAlphabetical`, `Random`, `RoundRobin`, `LeastRequests`, `PowerOfTwoChoices`

### 6-3. 코드로 동적 설정 변경

`IProxyConfigProvider` 를 구현해 런타임에 라우트·클러스터를 동적으로 변경할 수 있습니다.

```csharp
// 서비스 등록 시
builder.Services
    .AddReverseProxy()
    .LoadFromMemory(GetRoutes(), GetClusters());
```

### 6-4. 미들웨어 파이프라인 커스터마이징

```csharp
app.MapReverseProxy(proxyPipeline =>
{
    proxyPipeline.Use((context, next) =>
    {
        // 요청 전처리 로직
        Console.WriteLine($"Proxying: {context.Request.Path}");
        return next();
    });
});
```

---

## 7. 프로젝트 구조

```
YarpGettingStarted/          ← 이 폴더의 실행 가능한 예제 프로젝트
├── YarpGettingStarted.csproj
├── Program.cs               ← YARP 미들웨어 설정
└── appsettings.json         ← 라우트 & 클러스터 설정
```

---

## 8. 참고 링크

- [YARP 공식 문서](https://microsoft.github.io/reverse-proxy/)
- [YARP GitHub](https://github.com/microsoft/reverse-proxy)
- [MS Learn - YARP 개요](https://learn.microsoft.com/ko-kr/aspnet/core/fundamentals/servers/yarp/overview?view=aspnetcore-10.0)
- [MS Learn - YARP 시작하기](https://learn.microsoft.com/ko-kr/aspnet/core/fundamentals/servers/yarp/getting-started?view=aspnetcore-10.0)
