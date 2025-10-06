using System.Numerics;

namespace ConsoleApp;

public class Program
{
    readonly Vector v1 = new Vector();
    static void Main(string[] args)
    {
        Program pg = new();
        StructParam(pg.GetVector());
        StructParam(in pg.GetRefReadonlyVector());
    }
    private static void StructParam(in Vector v)
    {
        v.Increment(1, 1);
    }
    private Vector GetVector()
    {
        return v1; //readonly struct이므로 복사본 반환
    }
    private ref readonly Vector GetRefReadonlyVector()
    {
        return ref v1; //readonly struct이므로 참조 반환 가능
    }   
    readonly struct Vector
    {
        readonly public double X;
        readonly public double Y;

        public Vector(double x, double y)
        {
            this.X = x;
            this.Y = y;
        }
        public Vector Increment(int x, int y)
        {
            return new Vector(X + x, Y + y);
        }
    }
}