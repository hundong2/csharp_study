namespace ConsoleApp;

public class Program
{
    public class Vector
    {
        double x, y;
        //tuple, lambda 식으로 생성자의 초기화 코드 대체 
        public Vector(double x, double y) => (this.x, this.y) = (x, y);
        //tuple, labmda 식으로 Deconstruct의 분해 코드 대체 
        public void Deconstruct(out double x, out double y) => (x, y) = (this.x, this.y);
        public double X => x;
        public double Y
        {
            get => y;
            set => y = value;
        }
        public double Magnitude => Math.Sqrt(x * x + y * y);

        public double this[int index]
        {
            get => (index == 0) ? x : y;
            set => _ = (index == 0) ? x = value : y = value;
        }
        ~Vector() => Console.WriteLine("Destructor called"); //종료자 정의 가능 

        private EventHandler positionChanged;
        public event EventHandler PositionChanged
        {
            add => positionChanged += value;
            remove => positionChanged -= value;
        }
        public Vector Move(double dx, double dy)
        {
            x += dx;
            y += dy;
            positionChanged?.Invoke(this, EventArgs.Empty);
            return this;
        }
        public void PrintIt() => Console.WriteLine($"X={x}, Y={y}");
        public override string ToString() => $"X={x}, Y={y}";
    }
    
    static void Main(string[] args)
    {

    }
}