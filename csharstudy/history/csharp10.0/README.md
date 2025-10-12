# C# 10.0

## record 형식 ( stuct에도 표현 가능 )

- 기존

```csharp
record Vector(int X, int Y);
```

- 추가 

```csharp
record struct Point(int X, int Y); 
//compile 시 : public struct Point로 변경
record class Point(int X, int Y); //명시적으로 추가 됨. struct와 구분 record == record class
```

## class 타입의 record에 ToString 메서드의 sealed 지원

- record정의 시 Equals, GetHAshCode, ToString 메서드에 대한 기본 코드 제공
- `GetHashCode`, `ToString`에 대해서 재정의 가능.  Equals의 경우 컴파일 에러

```csharp
record class Vector2D(float x, float y)
{
    public override string ToString() //재정의 가능
    {
        return $"2D({x}, {y})";
    }
    public override int GetHashCode()
    {
        return base.GetHashCode();
    }
}
```

- `ToString` Method에 상속 재정의를 막는 `sealed` 사용 가능 

```csharp
record class Vector2D(float x, float y)
{
    public sealed override string ToString()
    {
        return $"2D{x}{y}";
    }
}
```

## 구조체 개선

- 기본 생성자 지원
- 필드 초기화 지원  

### 매개변수가 없는 구조체 생성자 ( Parameterless struct constructors )

- `class`의 경우 매개변수를 갖는 생성자가 정의 되면 기본 생성자가 제거 되지만, struct는 매개변수를 갖는 생성자가 있어도 기본 생성자를 호출 할 수 있었던 관행을 그대로 유지.

```csharp
record struct Student()
{
    public string Name { get; init; } = "John";
    public int Id { get; init; } = 20;
}
```

## Global using 지시문 

- csharp project 특정 파일에 아래와 같이 선언하면 프로젝트 내의 소스코드 파일에는 네임스페이스를 별도 선언하지 않고 사용 가능 

```csharp
global using System;
global using System.Linq;
```

- C#10.0 부터 csproj 내 네임스페이스 선언 시 자동으로 전역 네임스페이스 선언을 담고 있는 c# 소스코드 파일을 자동 생성해 프로젝트와 함께 빌드.

```csharp
<Project Sdk="Microsoft.NET.Sdk">
    <ImplicitUsing>enable</ImplicitUsing>
</Project>
```

## 보간된 문자열 개선(improved interpolated strings)

- https://www.sysnet.pe.kr/2/0/12826

## source generator v2 api

- https://www.sysnet.pe.kr/2/0/12985

## Enhanced #line directives 

- https://www.sysnet.pe.kr/2/0/12812
