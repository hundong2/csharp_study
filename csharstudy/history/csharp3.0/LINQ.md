# Linq  ( Language Integrated Query )

## SELECT * FROM <TABLE>

```csharp
var all = from person in people //foreach( var person in people )
            select person; //yield return person; IEnumerable<T>
//var all = IEnumerable<person> all
```

- linq changed that below code 

```csharp
IEnumerable<Person> all = people.Select((elem) => elem);
```

- `select` expression 확장메서드의 기능이 형변환에 있음. 

```csharp
var nameList = from person in people
                where person.Name;
//==
var nameList = people.Select((elem) => elem.Name );
```

- anonymous type(익명 타입)

```csharp
var dateList = from person in people
    select new { Name = person.Name, Year = DateTime.Now.AddYears(-person.Age).Year };
//==
var dateList = people.Select((elem) => new { Name = elem.Name, Year = DateTime.Now.AddYears(-elem.Age).Year})
```

## where, order by, group by, join

### where

```csharp
var ageOver30 = from person in people
                where person.Age > 30
                select person;
```

```csharp
var endWithS = from person in people
                where person.Name.EndsWith("s")
                select person;
```

### order by

```csharp
var ageSort = from person in people
                orderby person.Age //descending (내림차순), ascending ( default )
                select person;
```

- orderby에 올수 있는 값은 IComparable interface가 구현된 타입이면 된다.  

```csharp
var addGroup = from person in people
                group person by person.Address;
foreach(var itemGroup in addGroup )
{
    Console.WriteLine(string.Format("[{0}]", itemGroup.Key));
    foreach(var item in itemGroup )
    {
        Console.WriteLine(item);
    }
    Console.WriteLine();
}
```

- select로 가능한 형변환 작업에 대해 group에서도 사용 가능 

```csharp

var nameAgeList = from person in people
                    group new { Name = person.Name, Age = person.Age} by person.Address;

```