using System;
using System.Collections.Generic;

namespace ConsoleApp;

public class Program
{
    class Rectangle
    {
        public int X { set; get; }
        public int Y { set; get; }
        public int Width { set; get; }
        public int Height { set; get; }
        public Rectangle(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
        public void Deconstruct(out int x, out int y)
        {
            x = X;
            y = Y;
        }
        public void Deconstruct(out int x, out int y, out int width, out int height)
        {
            x = X;
            y = Y;
            width = Width;
            height = Height;
        }
    }
    static void Main(string[] args)
    {
        Rectangle rect = new Rectangle(10, 20, 100, 200);
        var (x, y) = rect;
        Console.WriteLine($"x={x}, y={y}");
        var (x1, y1, w1, h1) = rect;
        Console.WriteLine($"x1={x1}, y1={y1}, w1={w1}, h1={h1}");
        var list = new List<(int x, int y)>();
        list.Add((10, 20));
        list.Add((100, 200));
        foreach (var (x2, y2) in list)
        {
            Console.WriteLine($"x2={x2}, y2={y2}");
        }

        var (_, _) = rect; // discard
        (var _, var y3) = rect; // discard
        Console.WriteLine($"y3={y3}");

        var (_, _, _, _) = rect; // discard all
        (var _, var _, var w3, var h3) = rect; // discard
        Console.WriteLine($"w3={w3}, h3={h3}");

    }
}