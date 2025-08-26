using System;

Person mads = new Student { FirstName = "Mads", LastName = "Torgersen", ID = 42 };
Console.WriteLine(mads.Render());
public class Person
{
    public required string FirstName { set; get; }
    public required string LastName { set; get; }
    public virtual string Render()
    {
        return $"{FirstName} {LastName}";
    }
}
public class Student : Person
{
    public required int ID { set; get; }
    public override string Render()
    {
        return $"{FirstName} {LastName} (ID: {ID})";
    }
}