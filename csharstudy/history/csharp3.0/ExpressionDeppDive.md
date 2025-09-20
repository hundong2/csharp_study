# System.Linq.Expressions 핵심 타입/팩터리 메서드 실무 가이드
코드를 “데이터(트리)”로 표현해 해석·변환·컴파일할 수 있게 하는 식 트리 API입니다. LINQ Provider(EF Core 등)에서 쿼리 해석에 널리 쓰입니다.

## 예시 파일
[Expression Trees 샘플 Program.cs](https://github.com/dotnet/samples/blob/main/snippets/csharp/expression-trees/ExpressionTrees/Program.cs)

## 답변
아래는 실무/오픈소스에서 자주 쓰는 노드 타입과 팩터리 메서드(Expression.XXX)의 핵심과 최소 예시입니다. 식 트리는 불변이며, 필요 시 ExpressionVisitor로 새 트리를 만들어 재구성합니다.

- LambdaExpression / Expression<TDelegate>
  - 만들기: Expression.Lambda<T>(body, params)
  - 실행: Compile() → 델리게이트
  - 예시: (a, b) => a + b
````csharp
using System;
using System.Linq.Expressions;

var a = Expression.Parameter(typeof(int), "a");
var b = Expression.Parameter(typeof(int), "b");
var add = Expression.Add(a, b); // BinaryExpression
var lambda = Expression.Lambda<Func<int,int,int>>(add, a, b);
Console.WriteLine(lambda);              // (a, b) => (a + b)
Console.WriteLine(lambda.Compile()(3,5)); // 8
````

- ParameterExpression / ConstantExpression
  - Expression.Parameter(type, name), Expression.Constant(value, type?)
  - 변수/상수 노드를 정의
````csharp
var p = Expression.Parameter(typeof(string), "s");
var c = Expression.Constant("hi");
````

- BinaryExpression(산술/논리/비교)
  - Expression.Add/Subtract/Multiply/Divide
  - Expression.AndAlso/OrElse, Expression.Equal/NotEqual/GreaterThan…
````csharp
var x = Expression.Parameter(typeof(int), "x");
var body = Expression.GreaterThan(x, Expression.Constant(10)); // x > 10
var pred = Expression.Lambda<Func<int,bool>>(body, x);
Console.WriteLine(pred); // x => (x > 10)
````

- MethodCallExpression(메서드 호출)
  - Expression.Call(instanceOrNull, MethodInfo, args)
  - 인스턴스/정적 메서드 호출 모두 가능
````csharp
var s = Expression.Parameter(typeof(string), "s");
var contains = typeof(string).GetMethod(nameof(string.Contains), new[] { typeof(string) })!;
var call = Expression.Call(s, contains, Expression.Constant("abc"));
var f = Expression.Lambda<Func<string,bool>>(call, s).Compile();
Console.WriteLine(f("xyzabc")); // True
````

- MemberExpression(필드/속성 접근)
  - Expression.Property(instance, "Name"), Expression.Field(instance, fieldInfo)
````csharp
public class Person { public string Name { get; set; } = ""; }
var pParam = Expression.Parameter(typeof(Person), "p");
var nameProp = Expression.Property(pParam, nameof(Person.Name));
var toUpper = typeof(string).GetMethod(nameof(string.ToUpper), Type.EmptyTypes)!;
var upper = Expression.Call(nameProp, toUpper);
var proj = Expression.Lambda<Func<Person,string>>(upper, pParam).Compile();
Console.WriteLine(proj(new Person{ Name="kim"})); // KIM
````

- NewExpression / MemberInitExpression(객체/멤버 초기화)
  - Expression.New(ctor), Expression.MemberInit(newExpr, bindings)
  - 익명/DTO 투영 시 유용
````csharp
public class Dto { public string Name {get;set;} = ""; public int Len {get;set;} }
var sParam = Expression.Parameter(typeof(string), "s");
var newDto = Expression.MemberInit(
    Expression.New(typeof(Dto)),
    Expression.Bind(typeof(Dto).GetProperty(nameof(Dto.Name))!, sParam),
    Expression.Bind(typeof(Dto).GetProperty(nameof(Dto.Len))!, Expression.Property(sParam, nameof(string.Length)))
);
var toDto = Expression.Lambda<Func<string, Dto>>(newDto, sParam).Compile();
var dto = toDto("hello"); // { Name="hello", Len=5 }
````

- NewArrayExpression / ListInitExpression(배열/컬렉션 초기화)
  - Expression.NewArrayInit(elemType, elements)
  - Expression.ListInit(new List<T>(), Add(..) 바인딩)

- ConditionalExpression(삼항 연산자)
  - Expression.Condition(test, ifTrue, ifFalse)
````csharp
var n = Expression.Parameter(typeof(int), "n");
var abs = Expression.Condition(
    Expression.LessThan(n, Expression.Constant(0)),
    Expression.Negate(n),
    n
);
Console.WriteLine(Expression.Lambda<Func<int,int>>(abs, n).Compile()(-7)); // 7
````

- UnaryExpression(단항/변환)
  - Expression.Convert(value, targetType), Not, Negate 등
````csharp
var obj = Expression.Parameter(typeof(object), "o");
var asInt = Expression.Convert(obj, typeof(int)); // (int)o
````

- BlockExpression / Assign(문 블록과 대입)
  - Expression.Block(variables, expressions…), Expression.Assign(left, right)
  - 제한적 “문”도 표현 가능
````csharp
var v = Expression.Variable(typeof(int), "v");
var block = Expression.Block(
    new[] { v },
    Expression.Assign(v, Expression.Constant(5)),
    Expression.PostIncrementAssign(v), // v++
    v
);
Console.WriteLine(Expression.Lambda<Func<int>>(block).Compile()()); // 6
````

- InvocationExpression(식 호출)
  - Expression.Invoke(lambdaExpr, args) — 식 안에서 식 델리게이트 호출

- IndexExpression(인덱서)
  - Expression.MakeIndex(instance, indexerInfo, args) 또는 배열은 ArrayIndex

- ExpressionVisitor(식 변환/최적화)
  - 방문자 패턴으로 노드 치환/최적화
````csharp
using System.Linq.Expressions;

class AddK : ExpressionVisitor
{
    private readonly int _k;
    public AddK(int k) => _k = k;
    protected override Expression VisitBinary(BinaryExpression node)
    {
        if (node.NodeType == ExpressionType.GreaterThan &&
            node.Right is ConstantExpression c && c.Type == typeof(int))
            return Expression.GreaterThan(Visit(node.Left), Expression.Constant((int)c.Value! + _k));
        return base.VisitBinary(node);
    }
}
var p = Expression.Parameter(typeof(int), "x");
var gt10 = Expression.Lambda<Func<int,bool>>(Expression.GreaterThan(p, Expression.Constant(10)), p);
var gt15 = (Expression<Func<int,bool>>) new AddK(5).Visit(gt10)!;
Console.WriteLine(gt15); // x => (x > 15)
````

실무 패턴 1) 동적 필터 빌더(IQueryable에서 SQL 번역 가능)
- IQueryable.Where는 Expression<Func<T,bool>>를 받아 Provider(EF)가 트리를 해석해 SQL로 변환합니다.
````csharp
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

public static Func<T, bool> UnsafeFallback<T>(string name, object value) // IEnumerable용
{
    var p = Expression.Parameter(typeof(T), "e");
    var prop = Expression.Property(p, name); // e.Name
    var c = Expression.Constant(Convert.ChangeType(value, prop.Type), prop.Type);
    var eq = Expression.Equal(prop, c);
    return Expression.Lambda<Func<T,bool>>(eq, p).Compile();
}

public static Expression<Func<T,bool>> BuildEq<T>(string name, object value) // IQueryable용
{
    var p = Expression.Parameter(typeof(T), "e");
    var prop = Expression.Property(p, name);
    var c = Expression.Constant(Convert.ChangeType(value, prop.Type), prop.Type);
    var eq = Expression.Equal(prop, c);
    return Expression.Lambda<Func<T,bool>>(eq, p);
}
````

실무 패턴 2) Predicate 결합(AndAlso/OrElse)
- 서로 다른 파라미터를 하나로 맞춘 뒤 결합합니다.
````csharp
using System.Linq.Expressions;

public static class PredicateBuilder
{
    public static Expression<Func<T,bool>> And<T>(this Expression<Func<T,bool>> a, Expression<Func<T,bool>> b)
    {
        var param = a.Parameters[0];
        var replacedB = new ParameterReplacer(b.Parameters[0], param).Visit(b.Body)!;
        return Expression.Lambda<Func<T,bool>>(Expression.AndAlso(a.Body, replacedB), param);
    }
    private sealed class ParameterReplacer : ExpressionVisitor
    {
        private readonly ParameterExpression _from, _to;
        public ParameterReplacer(ParameterExpression from, ParameterExpression to) => (_from, _to) = (from, to);
        protected override Expression VisitParameter(ParameterExpression node) => node == _from ? _to : node;
    }
}
````

베스트 프랙티스
- 성능: Compile 비용 큼 → 캐시/재사용. 가능한 Reflection 정보(MethodInfo/PropertyInfo)도 캐시.
- Provider 제약: IQueryable 상에서는 “번역 가능한 노드”만 사용(EF Core 번역 가이드 참고).
- 안전성: 외부 입력으로 동적 식을 만들 땐 타입/이름 검증 필수.
- 유지보수: ExpressionVisitor로 변환 로직을 모듈화하고 단위 테스트 작성.

### 추가 자료
- [Expression 트리 개요(문서)](https://learn.microsoft.com/dotnet/csharp/expression-trees)
- [System.Linq.Expressions API 브라우저](https://learn.microsoft.com/dotnet/api/system.linq.expressions)
- [ExpressionVisitor 문서](https://learn.microsoft.com/dotnet/api/system.linq.expressions.expressionvisitor)
- [EF Core에서 식 번역 가능성](https://learn.microsoft.com/ef/core/querying/complex-query-operators)
- [System.Linq.Dynamic.Core(동적 LINQ)](https://github.com/StefH/System.Linq.Dynamic.Core)
- [LinqKit(PredicateBuilder)](https://github.com/scottksmith95/LINQKit)