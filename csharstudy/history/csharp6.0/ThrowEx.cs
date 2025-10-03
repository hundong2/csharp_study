namespace ConsoleApp;

public class Program
{
    static void Main(string[] args)
    {
        Person person = new Person("Hong");
        Console.WriteLine(person.Name);
        try
        {
            person.Print();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }
    class Person
    {
        public string Name { get; }
        public Person(string name) => Name = name ?? throw new ArgumentNullException(nameof(name));

        public string GetLastName() => throw new NotImplementedException();

        public void Print()
        {
            Action action = () => throw new Exception();
            action();
        }
    }
}