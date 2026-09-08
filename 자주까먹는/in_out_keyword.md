- [Return값이 IList<IList<string>>일때 팁](#return값이-ilistilist-일때)  

# C#에서 `<out T>`의 `out` 키워드는 "이 제네릭 타입 T는 오직 출력(반환)용으로만 쓰겠다"고 컴파일러와 약속하는 선언입니다. 

- 이 약속을 통해 앞서 겪었던 엄격한 타입 불일치 문제를 해결하고, 자식 타입의 컬렉션을 부모 타입의 컬렉션으로 취급할 수 있는 공변성(Covariance)을 안전하게 허용합니다.

## 읽기 전용의 마법

앞서 `IList<T>` 변환에서 컴파일 에러가 났던 근본적인 이유는, 해당 컬렉션에 **'다른 자식 타입(예: 배열)을 새로 집어넣을 수 있는(쓰기) 위험성'** 때문이었습니다. 하지만 데이터를 집어넣지 않고 오직 꺼내보기만 한다면 이 위험은 원천적으로 사라집니다.

| 인터페이스 특징 | `IList<T>` (무공변성) | `IEnumerable<out T>` (공변성) |
| --- | --- | --- |
| **데이터 흐름** | 읽기 / 쓰기(`Add`) 모두 가능 | **오직 출력(순회 및 읽기)만 가능** |
| **타입 변환** | 100% 동일한 타입만 허용 | **자식 타입 컬렉션을 부모 타입으로 변환 허용** |
| **안전성 이유** | 쓰기가 가능해 엉뚱한 타입이 들어올 위험 존재 | 쓰기 기능 자체가 존재하지 않아 충돌 위험 0% |

---

## 직관적인 코드 예시

개(`Dog`)와 고양이(`Cat`)가 동물(`Animal`)을 상속받는 상황을 가정해 보겠습니다.

```csharp
class Animal {}
class Dog : Animal {}
class Cat : Animal {}

List<Dog> dogs = new List<Dog> { new Dog(), new Dog() };

// 1. IList<T>의 경우 (에러 발생)
// IList<Animal> animals = dogs; // ❌ 컴파일 에러!
// 만약 이게 허용된다면, animals.Add(new Cat()) 코드를 통해
// '개' 전용 메모리 리스트에 '고양이'를 강제로 밀어넣는 대참사가 발생합니다.

// 2. IEnumerable<out T>의 경우 (정상 동작)
IEnumerable<Animal> safeAnimals = dogs; // ✅ 컴파일 성공!
// IEnumerable은 데이터를 꺼내서 읽는 기능만 있고 Add() 메서드가 아예 없습니다.
// '개'를 꺼내서 '동물'이라고 부르는 것은 논리적으로 100% 안전합니다.

```

인터페이스를 설계할 때 `<out T>`를 붙이면, 해당 인터페이스 안에서는 `T`를 함수의 매개변수(입력값)로 사용하는 것이 문법적으로 차단됩니다. 오직 함수의 반환값(출력값)으로만 `T`를 쓸 수 있게 강제함으로써, 컴파일러가 "이 컬렉션에는 새로운 무언가를 집어넣을 수 없으니 안심하고 부모 타입으로 캐스팅을 허락해 줘도 되겠다"라고 판단하게 만드는 훌륭한 장치입니다.

C#의 `<in T>` 키워드는 제네릭 타입을 **오직 입력(매개변수)용으로만 쓰겠다**는 선언으로, 부모 타입을 다루는 객체를 자식 타입 변수에 할당할 수 있게 해주는 반공변성(Contravariance)을 제공합니다.

**공변성(out)과 반공변성(in) 비교**

| 개념 | 키워드 | 데이터 흐름 | 타입 변환 방향 | 대표적인 C# 타입 |
| --- | --- | --- | --- | --- |
| **공변성** | `<out T>` | 출력(Return) 전용 | 자식 ➔ 부모 (`Dog` ➔ `Animal`) | `IEnumerable<T>`, `Func<T>` |
| **반공변성** | `<in T>` | 입력(Parameter) 전용 | 부모 ➔ 자식 (`Animal` ➔ `Dog`) | `IComparer<T>`, `Action<T>` |

**왜 부모를 자식에 대입하는 것이 안전할까?**

동물(`Animal`)을 치료하는 '종합 수의사'와 개(`Dog`)만 치료하는 '애견 전용 수의사'가 있다고 가정해 보겠습니다.

* 어떤 보호자가 "우리 개를 치료해 줄 애견 전용 수의사(`Action<Dog>`)를 찾습니다!"라고 합니다.
* 이때 모든 동물을 다 고칠 줄 아는 종합 수의사(`Action<Animal>`)가 지원한다면 어떨까요?
* 종합 수의사는 개(Dog)가 가진 동물의 기본 특성을 모두 알고 있으므로, 논리적으로 개를 치료하는 데 아무 문제가 없습니다.

데이터를 **입력받아 소비**하는 입장에서는, 더 포괄적인(부모) 타입을 다룰 줄 아는 로직이 더 구체적인(자식) 타입의 데이터도 완벽하게 처리할 수 있기 때문에 컴파일러가 이를 100% 안전하다고 판단합니다.

**직관적인 코드 예시**

```csharp
class Animal {}
class Dog : Animal {}

// 동물을 입력받아 처리하는 메서드
void TreatAnimal(Animal a) {
    Console.WriteLine("동물을 치료합니다.");
}

// C#의 Action<in T> 델리게이트는 반공변성을 지원합니다.
Action<Animal> animalDoctor = TreatAnimal;

// ❌ 일반적인 객체 지향 상식 (에러 발생)
// Dog dog = new Animal(); // 자식 변수에 부모 객체를 담을 수 없음

// ✅ 반공변성의 마법 (정상 동작)
// '동물을 치료하는 수의사'를 '개 전용 수의사' 변수에 대입
Action<Dog> dogDoctor = animalDoctor; 

// 애견 전용 수의사(dogDoctor)에게 개를 넘겨주면, 
// 실제로는 더 넓은 범위를 커버하는 animalDoctor가 개를 안전하게 받아 치료합니다.
dogDoctor(new Dog()); 

```

이처럼 `in` 키워드는 제네릭이 **결과를 반환하지 않고 매개변수로 받아 소비하기만 할 때**, 범용적인 처리기(부모)를 구체적인 상황(자식)에 제약 없이 재사용할 수 있도록 유연성을 부여합니다.

## Return값이 IList<IList<string>> 일때

- Return값이 IList<IList<string>> 일때, List<IList<string>> 또는, Array<IList<string>> 가 되는 이유

```
[최종 목표]: IList < IList<string> > 반환
======================================================
❌ 실패 케이스: List < List<string> > 
 ├── 1차 심사 (외부): List는 IList를 상속하므로 통과! (O)
 └── 2차 심사 (내부 T): 요구하는 T는 'IList<string>'인데 
                        들어온 T는 'List<string>'이므로 탈락! (X)

✅ 성공 케이스: List < IList<string> >
 ├── 1차 심사 (외부): List는 IList를 상속하므로 통과! (O)
 └── 2차 심사 (내부 T): 요구하는 T와 들어온 T가 
                        'IList<string>'으로 완벽 일치! (O)
```

- C#의 제네릭(Generic)시스템은 런타임 에러를 막기 위해 무공변성(Invariance)이라는 매우 엄격한 규칙을 적용. 제네릭 괄호 `< >`안에 들어가는 `내용물(T)`의 타입은 부모-자식 상속 관계와 무관하게 단 1%의 오차도 없이 완벽하게 일치해야만 컴파일러가 승인 

- `Dictionary<string, List<string>>.Values`를 IEnumerable<List<string>> 컬렉션으로 인식. 이 상태에서 `.ToList()`를 호출하면 원본 제네릭 타입 파라미터가 그대로 유지되어 메모리 상에 `List<List<string>>` 객체가 생성. 엄격한 무공변성(Invariance) 규칙에 의해 `IList<IList<string>>`으로의 암시적 변환이 완전히 차단. 

```
[요구되는 반환 규격]: IList<IList<string>> (외부: IList, 내부: IList<string>)

1. .Cast().ToList() 전략
   [생성된 객체 실체]: List<IList<string>>
    ├── 외부 컨테이너(List) ───> IList 규격 만족! (O)
    └── 내부 컨테이너(IList<string>) ───> 완벽 일치! (O)

2. 배열(.ToArray()) 전략
   [생성된 객체 실체]: IList<string>[] (1차원 배열)
    ├── 외부 컨테이너(Array) ───> IList 규격 만족! (O)
    └── 내부 컨테이너(IList<string>) ───> 완벽 일치! (O)

```