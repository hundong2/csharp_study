/*
실행: dotnet script 04_CSharp15Preview.csx
선행 문서: 00-csharp-primer.md, 03-sdk-csharp.md
목표: C# 15 Preview의 extension indexer와 union을 안정 문법의 등가 모델로 익힙니다.
*/

// 01. Index(^1)와 Console을 사용합니다.
using System;
// 02. IReadOnlyList<T> abstraction을 사용합니다.
using System.Collections.Generic;

// 03. CSX submission은 선언 형식을 내부적으로 중첩하므로 실행용 helper는 일반 static method로 둡니다.
public static class ReadOnlyListCompatExtensions
{
    // 04. 일반 프로젝트에서는 첫 parameter 앞에 `this`를 붙여 extension method로 만들 수 있습니다.
    public static T At<T>(IReadOnlyList<T> list, Index index)
    {
        // 05. ^1 같은 from-end Index를 실제 0-based offset으로 변환합니다.
        int offset = index.GetOffset(list.Count);
        // 06. 기존 instance indexer this[int]로 원소를 반환합니다.
        return list[offset];
    }
}

// 07. 안정 문법에서는 closed hierarchy의 공통 base type으로 union 의미를 흉내 냅니다.
public abstract class Pet
{
    // 08. protected constructor는 이 hierarchy 밖의 직접 생성을 막습니다.
    protected Pet() { }
}

// 09. sealed case type은 더 파생되지 않아 case 의미가 단순해집니다.
public sealed class Dog : Pet
{
    // 10. constructor parameter를 읽기 전용 property에 저장합니다.
    public Dog(string name) => Name = name;
    // 11. Name은 Dog case의 payload입니다.
    public string Name { get; }
}

// 12. 두 번째 case도 같은 base를 상속합니다.
public sealed class Cat : Pet
{
    // 13. Cat case는 남은 목숨 수를 payload로 가집니다.
    public Cat(int lives) => Lives = lives;
    // 14. 읽기 전용 property입니다.
    public int Lives { get; }
}

// 15. pattern matching helper를 별도 class에 둡니다.
public static class PetFunctions
{
    // 16. switch expression은 runtime type을 검사해 case별 식을 평가합니다.
    public static string Describe(Pet pet) => pet switch
    {
        // 17. property pattern으로 Dog payload를 읽습니다.
        Dog dog => $"dog: {dog.Name}",
        // 18. Cat payload를 읽습니다.
        Cat cat => $"cat: {cat.Lives}",
        // 19. 안정 base class는 외부 case 가능성을 완전히 막지 못해 fallback이 필요합니다.
        _ => throw new ArgumentOutOfRangeException(nameof(pet))
    };
}

// 20. 배열을 IReadOnlyList<string> interface reference로 봅니다.
IReadOnlyList<string> log = new[] { "start", "work", "done" };
// 21. 안정 등가 helper At(log, ^1)은 마지막 원소를 돌려줍니다.
Console.WriteLine($"last = {ReadOnlyListCompatExtensions.At(log, ^1)}");
// 22. 두 case 값을 공통 Pet 형식 배열에 담습니다.
Pet[] pets = { new Dog("Rex"), new Cat(9) };
// 23. 각 active case를 pattern matching합니다.
foreach (Pet pet in pets)
{
    // 24. C# 15 union에서는 compiler가 union case 전환을 직접 이해합니다.
    Console.WriteLine(PetFunctions.Describe(pet));
}

/*
Preview 6 SDK + <LangVersion>preview</LangVersion>에서 일반 .cs로 옮길 실제 문법:

// P01. extension block을 담는 형식은 static class입니다.
public static class ReadOnlyListExtensions
{
    // P02. receiver list에 generic extension member를 선언합니다.
    extension<T>(IReadOnlyList<T> list)
    {
        // P03. this[Index]가 Preview 6의 extension indexer 선언입니다.
        public T this[Index index] => list[index.GetOffset(list.Count)];
    }
}

// P04. record class 두 개가 union의 가능한 case payload입니다.
public record class PreviewDog(string Name);
public record class PreviewCat(int Lives);
// P05. union 선언은 정확히 두 case 중 하나를 담습니다.
public union PreviewPet(PreviewDog, PreviewCat);

// P06. switch가 active case를 검사하고 payload를 분해합니다.
static string DescribePreview(PreviewPet pet) => pet switch
{
    // P07. Dog case의 Name을 name 지역 변수로 받습니다.
    PreviewDog(var name) => $"dog: {name}",
    // P08. Cat case의 Lives를 lives로 받습니다.
    PreviewCat(var lives) => $"cat: {lives}"
};

// P09. collection expression으로 세 문자열의 read-only list를 만듭니다.
IReadOnlyList<string> previewLog = ["start", "work", "done"];
// P10. instance indexer가 없으므로 scope의 extension indexer를 사용합니다.
Console.WriteLine(previewLog[^1]); // extension indexer
*/
