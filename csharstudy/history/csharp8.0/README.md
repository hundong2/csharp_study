# csharp8.0

## class generic 제약 조건  

- class 타입이어야 한다.
- sealed 타입이 아니어야 한다. 
- System.Array, `System.Delegate`, `System.Enum`은 허용하지 않는다. 
- System.ValueType 은 허용하지 않지만 특별히 struct제약을 대신 사용할 수 있다.  또한, System.Object도 허용하지 않지만 어차피 모든 타입의 기반으로 제약 조건으로써의 의미가 없다.  

- `System.Delegate`, `System.Enum` 은 제약이 풀림. 
1. [Generic의 타입만 매개변수로 받을 수 있는 Example](./GenericEx.cs). 
2. `enum`의 경우 struct제약을 해야 했지만, System.Enum으로 명시할 수 있게 됨. 

```csharp
class EnumValueCache<TEnum> wehere TEnum : System.Enum
{
    Dictionary<TEnum, int> _enumKey = new Dictionary<TEnum, int>();
    public EnumValueCache()
    {
        int[] intValues = Enum.GetValues(typeof(TEnum)) as int [];
        TEnum[] enumValues = Enum.GetValues(typeof(TEnum)) as TEnum[];

        for(int i = 0; i < intValues.Length; i++)
        {
            _enumKey.Add(enumValues[i], intValues[i]);
        }
    }
    public int GetInteger(TEnum value)
    {
        return _enumKey[value];
    }
}
```

3. unmanaged

- [Unamanaged Generic](./GenericUnmanaged.cs). 
- 형식 매개변수에 대한 포인트 연산을 할 수 있다. 
- https://www.sysnet.pe.kr/2/0/11557. 
- https://www.sysnet.pe.kr/2/0/11558. 