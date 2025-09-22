namespace ConsoleApp;

class Person
{
    public string Name { get; set; }
    public int Age { get; set; }
    public string Address { get; set; }

    public override string ToString()
    {
        return string.Format("Name: {0}, Age: {1}, Address: {2}", Name, Age, Address);
    }
}

class MainLanguage
{
    public string Name { get; set; }
    public string IDE { get; set; }
}

class Program
{
    static void Main(string[] args)
    {
        List<Person> people = new List<Person>
        {
            new Person { Name = "Alice", Age = 30, Address = "Korea" },
            new Person { Name = "Bob", Age = 25, Address = "Korea" },
            new Person { Name = "Charlie", Age = 35, Address = "Korea" },
            new Person { Name = "David", Age = 28, Address = "Japan" },
            new Person { Name = "Eve", Age = 22, Address = "Korea" },
            new Person { Name = "Frank", Age = 40, Address = "USA" },
            new Person { Name = "Grace", Age = 27, Address = "Korea" }
        };

        //LINQ query directly executed
        Console.WriteLine("ToList() Example:");
        var inKorea = (
            from person in people
            where IsEqual(person.Address, "Korea")
            select person
        ).ToList();

        Console.ReadLine();
        Console.WriteLine("IEnumerable<T> Where/Select evaluated");
        //IEnemerable<T>를 반환하므로 LINQ 쿼리가 평가만 되고 실행 되지 않음

        var inKorea2 =
            from person in people
            where IsEqual(person.Address, "Korea")
            select person;
        Console.ReadLine();
        var firstPeople = inKorea2.Take(1);

        foreach (var item in firstPeople)
        {
            Console.WriteLine(item);
        }

        Console.WriteLine("Single(): " + firstPeople.Single());
    }
    public static bool IsEqual(string arg1, string arg2)
    {
        Console.WriteLine("IsEqual called");
        return arg1 == arg2;
    }
}