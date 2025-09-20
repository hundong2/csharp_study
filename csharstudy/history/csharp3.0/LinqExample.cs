namespace ConsoleApp;

class Person
{
    public string Name { get; set; }
    public int Age { get; set; }
    public string Address { get; set; }

    public override string ToString()
    {
        return $"Name: {Name}, Age: {Age}, Address: {Address}";
    }
}

class MainLanguage
{
    public string Name { get; set; }
    public string Language { get; set; }
}

public class Program
{
    public static void Main()
    {
        List<Person> people = new List<Person>
        {
            new Person { Name = "Alice", Age = 30, Address = "123 Main St" },
            new Person { Name = "Bob", Age = 25, Address = "456 Oak Ave" },
            new Person { Name = "Charlie", Age = 35, Address = "789 Pine Rd" }
        };

        // LINQ query to filter and order people
        var query = from person in people
                    where person.Age > 28
                    orderby person.Name
                    select person;

        Console.WriteLine("People older than 28:");
        foreach (var person in query)
        {
            Console.WriteLine(person);
        }

        List<MainLanguage> languages = new List<MainLanguage>
        {
            new MainLanguage { Name = "Alice", Language = "C#" },
            new MainLanguage { Name = "Bob", Language = "Java" },
            new MainLanguage { Name = "Charlie", Language = "Python" }
        };

        // LINQ query with join
        var joinQuery = from person in people
                        join lang in languages on person.Name equals lang.Name
                        select new { person.Name, person.Age, lang.Language };

        Console.WriteLine("\nPeople with their main programming languages:");
        foreach (var item in joinQuery)
        {
            Console.WriteLine($"Name: {item.Name}, Age: {item.Age}, Language: {item.Language}");
        }
    }
}