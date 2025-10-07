namespace ConsoleApp;

public class Program
{
    public static void Main()
    {
        string[] strings = new string[] { "1", "2", "3" };
        //LINQ 구문에서 out 변수 사용
        var query = from text in strings
                    where int.TryParse(text, out var result)
                    select result;
        object[] objects = new object[] { 5, "is", true };

        var texts = from text in objects
                    let t = text is string value ? value : string.Empty
                    select t;
    }
    public class BaseType
    {
        //필드 초기화 식에서 변수 사용
        private readonly bool _field = int.TryParse("5", out int result);
        //속성 초기화 식에서 변수 사용
        int Number { set; get; } = int.TryParse("5", out int result) ? result : 0;
        int Number2 { set; get; } = 5 is int value ? value : 0;
        public BaseType(int number, out bool result)
        {
            Number = number;
            result = _field;
        }
        public class Derived : BaseType
        {
            //생성자의 초기화 식에서 변수 사용
            public Derived(int i) : base(i, out var result)
            {
                Console.WriteLine(result);
            }
        }
    }
    
}