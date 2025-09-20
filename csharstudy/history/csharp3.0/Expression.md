# C#의 Expression(Expression<TDelegate>)란?
코드를 “데이터(트리)”로 표현하는 식 트리 API입니다. 람다를 실행 가능한 델리게이트로만 쓰는 게 아니라, 구조(연산자·메서드 호출·멤버 접근 등)를 노드로 가진 트리로 만들고, 해석·변환·컴파일할 수 있습니다. LINQ Provider(예: EF Core)가 쿼리를 분석·번역할 때 핵심적으로 사용합니다.

## 예시 파일
[Expression Trees 샘플 코드 (dotnet/samples)](https://github.com/dotnet/samples/tree/main/snippets/csharp/expression-trees)

## 답변
핵심 개념
- 네임스페이스: System.Linq.Expressions
- 주요 타입
  - Expression: 모든 식 노드의 추상 기반
  - Expression<TDelegate>: 람다 식 트리(예: Expression<Func<int,int>>)
  - 각 노드: ParameterExpression, ConstantExpression, BinaryExpression(Add 등), MethodCallExpression, MemberExpression(Property/Field), LambdaExpression 등
- 주요 기능
  - 구성: Expression.Parameter/Constant/Add/Call/Property 등으로 트리 생성
  - 해석/변환: ExpressionVisitor로 식을 순회·치환·최적화
  - 컴파일: Expression<T>.Compile() → 실행 가능한 델리게이트로 JIT 컴파일

예제 1) 식 트리 만들기/컴파일하기: x => x*x + 2
````csharp
using System;
using System.Linq.Expressions;

ParameterExpression x = Expression.Parameter(typeof(int), "x");
Expression body = Expression.Add(
    Expression.Multiply(x, x),              // x * x
    Expression.Constant(2)                  // + 2
);

var lambda = Expression.Lambda<Func<int, int>>(body, x);
Func<int, int> f = lambda.Compile();

Console.WriteLine(lambda); // x => ((x * x) + 2)
Console.WriteLine(f(5));   // 27
````

예제 2) 문자열 기반 동적 필터 만들기: Where에 쓸 Predicate 생성
````csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

static Func<T, bool> BuildEqualsPredicate<T>(string propertyName, object value)
{
    var p = Expression.Parameter(typeof(T), "e");
    var prop = Expression.Property(p, propertyName);                // e.Property
    var constant = Expression.Constant(value, prop.Type);           // 상수(타입 일치)
    var eq = Expression.Equal(prop, constant);                      // e.Property == value
    var lambda = Expression.Lambda<Func<T, bool>>(eq, p);
    return lambda.Compile();
}

// 사용
var data = new List<(string Name, int Age)> { ("A",20), ("B",30), ("C",20) };
var pred = BuildEqualsPredicate<(string Name, int Age)>(nameof(ValueTuple<string,int>.Item2), 20);
var result = data.Where(pred).ToList();  // ("A",20), ("C",20)
Console.WriteLine(string.Join(", ", result));
````

예제 3) ExpressionVisitor로 식 변환: x > 10을 x > (10 + delta)로 치환
````csharp
using System;
using System.Linq.Expressions;

class ThresholdAdder : ExpressionVisitor
{
    private readonly int _delta;
    public ThresholdAdder(int delta) => _delta = delta;

    protected override Expression VisitBinary(BinaryExpression node)
    {
        if (node.NodeType == ExpressionType.GreaterThan &&
            node.Right is ConstantExpression c && c.Type == typeof(int))
        {
            // x > C  →  x > (C + delta)
            var newRight = Expression.Constant((int)c.Value! + _delta);
            return Expression.GreaterThan(Visit(node.Left), newRight);
        }
        return base.VisitBinary(node);
    }
}

var p = Expression.Parameter(typeof(int), "x");
var body = Expression.GreaterThan(p, Expression.Constant(10)); // x > 10
var pred = Expression.Lambda<Func<int,bool>>(body, p);

var rewritten = (Expression<Func<int,bool>>) new ThresholdAdder(5).Visit(pred)!;
var f = rewritten.Compile();

Console.WriteLine(rewritten); // x => (x > 15)
Console.WriteLine(f(16));     // True
````

어디에 쓰나
- ORM/쿼리(예: Entity Framework Core): Expression을 분석해 SQL로 번역
- 동적 쿼리/필터 빌더: 조건을 런타임에 조립
- 규칙 엔진/DSL: 도메인 규칙을 트리로 표현·최적화
- 성능 최적화: 리플렉션을 대체해 “한 번 빌드·컴파일해 재사용”하는 접근

주의 사항
- 식 트리는 “식(Expression)” 중심입니다. 일반 문(statement)은 제한적으로만 표현(Block, Loop 등) 가능
- 트리는 불변입니다. 변경은 보통 Visitor로 새 트리를 만들어 반환
- Compile 비용이 있으니 캐싱해 재사용하는 것이 좋습니다

### 추가 자료
- 