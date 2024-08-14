namespace CollectionExample;
using System.Collections;
class Program
{
    public class Person : IComparable
    {
        public string Name { get; set; }
        public int Age { get; set; }
        public Person()
        {
        }
        public Person(string name, int age)
        {
            Name = name;
            Age = age;
        }
        public override string ToString()
        {
            return $"Name : {this.Name}, Age : {this.Age}";
        }
        public int CompareTo(object obj)
        {
            Person target = (Person)obj;
            if( this.Age > target.Age)
            {
                return 1;
            }
            else if( this.Age < target.Age)
            {
                return -1;
            }
            else 
            {
                return 0;
            }
            return 0;
        }
    }
    static void ExampleIComparableObj()
    {
        ArrayList ar = new ArrayList();
        ar.Add(new Person("Anderson", 30));
        ar.Add(new Person("Brown", 20));
        ar.Add(new Person("Cindy", 25));
        ar.Sort();
        foreach(Person obj in ar)
        {
            Console.WriteLine(obj);
        }
    }
    static void ExampleArrayList()
    {
        ArrayList ar = new ArrayList();
        ar.Add("Hello");
        ar.Add(6);
        ar.Add("World");
        ar.Add(true);

        Console.WriteLine("Contain(6): " + ar.Contains(6));
        ar.Remove("World");
        ar[2] = false;
        Console.WriteLine();

        foreach(object obj in ar)
        {
            Console.WriteLine(obj);
        }
    }
    static void ExampleArrayListSorting()
    {
        Console.WriteLine("ExampleArrayListSorting");
        ArrayList ar = new ArrayList();
        ar.Add(5);
        ar.Add(3);
        ar.Add(7);
        ar.Add(1);
        ar.Add(9);
        ar.Sort();
        foreach(int obj in ar)
        {
            Console.WriteLine(obj);
        }
    }
    static void Main(string[] args)
    {
        Console.WriteLine("Example System.Collections");
        ExampleArrayList();
        ExampleArrayListSorting();
        ExampleIComparableObj();

    }
}
