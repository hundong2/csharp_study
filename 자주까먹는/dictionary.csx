using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

//dictionary 
using System.Collections.Frozen;

// .NET 8.0에서 새롭게 추가된 FrozenDictionary는 불변(immutable) 딕셔너리 컬렉션입니다.
// FrozenDictionary는 생성 시점에 모든 키-값 쌍을 고정(freeze)하여, 이후에는 추가, 삭제, 수정이 불가능합니다.
// FrozenDictionary는 내부적으로 해시 테이블을 사용하지만, 일반 Dictionary와 달리 동기화(synchronization) 비용이 없고, 읽기 전용(read-only)으로 최적화되어 있습니다.

var mathcingValue = new Dictionary<string, int>
{
    ["one"] = 1,
    ["two"] = 2,
    ["three"] = 3
};

Console.WriteLine($"matching value count: {mathcingValue.Count}");
Console.WriteLine($"matching value for 'one': {mathcingValue["one"]}");
// Result 
// matching value count: 3
// matching value for 'one': 1


