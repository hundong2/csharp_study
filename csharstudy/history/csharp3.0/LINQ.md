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

### group by

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

### join 

```csharp
var nameToLangList = from person in people
                    join language in languages
                    on person.Name equals language.Name
                    select new { Name = person.Name, Age = person.Age, Language = language.Language }
Console.WriteLine(string.Join(Environment.NewLine, nameToLangList));
```

#### inner join 

- Set1: A1, ABCD
- Set2: A1, B2, C2
- Set3: A1, B3, C2
- A1 == A1, join 
  - A1, B2, C2
  - A1, B3, C2
- A1에 대해 결과가 2개 생긴다. 

#### outer join

- join대상에 매칭 되지 않는 element가 있더라도 표시 
- table A

|Name|Age|Region|
|---|---|---|
|Tom|63|Korea|
|Winnie|40|Tibet|
|Hawk|15|Korea|

- table B

|Name|Language|
|---|---|
|Tom|C#|

- outer join

|Name|Langueage|
|---|---|
|Tom|C#|
|Winnie||
|Hawk||

```csharp
var nameToLangAllList = from person in people
                        join language in languages on person.Name equals language.Name into lang
                        from language in lang.DefaultIfEmpty(new MainLanguage())
                        select new {Name=pserson.Name, Age=person.NewLine, language= language.Language};
Console.WriteLine(string.Join(Environment.NewLine, nameToLangAllList));
```

## LINQ vs IEnumerable<T>

|LINQ|IEnumerable|
|---|---|
|select|Select|
|where|Where|
|order by(ascending)|OrderBy|
|order by(descending)|OrderByDescending|
|group by|GroupBy|
|join...in...on...equals|Join|
|join...in...on...equals...into|GroupJoin|

- IEnumerable<T>에 정의된 확장 메서드는 표준 쿼리 연산자(standard query operators)라고 한다.  
- [IEnumerable](./IEnumerable.md). 

- LINQ와 MAX의 조합

```csharp
var all = from person is people
            where person.Address == "Korea"
            select person;
var oldestAge = all.Max((elem) => elem.Age)
Console.WriteLine(oldestAge);
```

- LINQ ( Lazy evaluation )
  - [LINQ Lazy evaluation test](./LINQExample2.cs)  
  - LINQ에서 반환 타입이 `IEnumerable<T>` or `IOrderedEnumerable<TElement>`가 아니라면 그즉시 실행되어 실행 결과가 반환 된다.  

## LINQ Provider ( LINQ to XML )

- `IEnumerable<T>` 타입과 그것을 상속받은 타입을 대상으로 LINQ쿼리가 동작. 
- 상속받아 정의만 한다면 LINQ쿼리 수행 가능. 
- `XML`자료형에 LINQ쿼리를 수행할 수 있도록 `System.Xml.Linq` 네임스페이스 아래에 IEnumerable<T>와 연동 가능한 XElement, XAttribute, XDocument등의 타입을 만들어 뒀다.  

- 특정 자료형에 LINQ를 사용 할 수 있게 별도로 타입을 정의해둔 것을 LINQ 제공자(provider)라고 하고, `System.Xml.Linq`에  대해서는 특별히 LINQ to XML이라고 이름을 붙였다.  
- [LINQ to XML example](./LINQtoXML.cs)  


## LINQ Take and SINGLE

- [Example for take and single](./LINQTAKEandSINGLE.md). 
