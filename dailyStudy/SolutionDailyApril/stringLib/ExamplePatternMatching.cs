namespace UtilityLibrary;

public class ExamplePatternMatching
{
    public class Circle
    {
        public string name = "circle";
        public int x;
        public int y;
        public double radius;
    }
    public class Square
    {
        public string name = "square";
        public int x;
        public int y;
        public double side;
    }
    public Circle _circle = new Circle(){
        radius = 1.1,
        x = 0,
        y = 1
    };
    public Square _square = new Square(){
        side = 1.3,
        x = 1,
        y = 2
    };

    public ExamplePatternMatching()
    {

    }    


    public void TestPatternMatching(object value)
    {
        if( value is Circle c )
        {
            Console.WriteLine($@"name {c.name}, {c.radius}");
        }
        if(value is Square s)
        {
            Console.WriteLine($@"name {s.name}, {s.side}");
        }
    }
}
