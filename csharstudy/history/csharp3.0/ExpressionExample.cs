using System.Linq.Expressions;
using System;
public class Program
{
    public static void Main()
    {
        Console.WriteLine("Expression Example:");
        ExpressionExample();
        Console.WriteLine("Expression Tree Example:");
        UsingExpressionTree();
    }
    public static void ExpressionExample()
    {
        Expression<Func<int, int, int>> expr = (a, b) => a + b;

        BinaryExpression opPlus = expr?.Body as BinaryExpression;
        Console.WriteLine("Operation: " + opPlus?.NodeType); // Add
        Console.WriteLine("Left: " + (opPlus?.Left as ParameterExpression)?.Name);           // a
        Console.WriteLine("Right: " + (opPlus?.Right as ParameterExpression)?.Name);         // b

        Func<int, int, int> func = expr.Compile();
        Console.WriteLine("Result of 10 + 20: " + func(10, 20)); // 30
    }

    public static void UsingExpressionTree()
    {
        ParameterExpression paramA = Expression.Parameter(typeof(int), "a");
        ParameterExpression paramB = Expression.Parameter(typeof(int), "b");
        BinaryExpression body = Expression.Add(paramA, paramB);
        var expr = Expression.Lambda<Func<int, int, int>>(body, new ParameterExpression[] { paramA, paramB });

        Console.WriteLine("Expression Tree Example:");
        Console.WriteLine("Operation :" + expr.ToString()); // (a, b) => (a + b)
        Func<int, int, int> func = expr.Compile();
        Console.WriteLine("Result of 10 + 20: " + func(10, 20)); // 30
    }
}