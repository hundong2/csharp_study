# 12. Memory, Span, ArrayPool

고성능 코드에서는 불필요한 배열 할당과 복사를 줄이는 것이 중요합니다.

## 핵심 개념

- `Span<T>`: 연속 메모리 구간을 복사 없이 다룹니다.
- `ReadOnlySpan<T>`: 읽기 전용 메모리 구간입니다.
- `ArrayPool<T>`: 배열을 빌려 쓰고 반납합니다.
- `stackalloc`: 작은 임시 버퍼를 스택에 만듭니다.
- heap: 일반 객체와 배열이 만들어지는 메모리 영역입니다. GC가 관리합니다.
- stack: 메서드 호출 중 잠깐 쓰는 지역 값이 놓이는 영역입니다. 메서드가 끝나면 정리됩니다.

## 실무에서 왜 필요한가

네트워크, 파일, JSON, 바이너리 프로토콜 처리에서는 byte 배열이 많이 생깁니다. 배열을 계속 새로 만들면 GC 부담이 커집니다.

## 메모리 관점으로 이해하기

### 일반 배열

```csharp
byte[] buffer = new byte[1024];
```

`new byte[1024]`는 힙에 배열 객체를 만듭니다. 많이 반복하면 GC가 나중에 치워야 할 객체가 늘어납니다.

### `stackalloc`

```csharp
Span<byte> buffer = stackalloc byte[128];
```

`stackalloc`은 작은 버퍼를 스택에 만듭니다. GC 대상이 아니므로 빠르지만, 너무 큰 버퍼를 만들면 스택 공간을 압박합니다. 보통 작고 짧게 쓰는 임시 버퍼에 적합합니다.

### `ArrayPool<T>`

```csharp
byte[] rented = ArrayPool<byte>.Shared.Rent(4096);
ArrayPool<byte>.Shared.Return(rented);
```

`ArrayPool<T>`는 큰 배열을 매번 새로 만들지 않고 재사용합니다. 성능에는 좋지만, 반납 전에 이전 데이터가 남아 있을 수 있으므로 민감한 데이터는 `clearArray: true`로 반납합니다.

## 선택 기준

| 상황 | 추천 |
|------|------|
| 아주 작은 임시 버퍼 | `stackalloc` + `Span<T>` |
| 외부에서 받은 배열 일부만 읽기 | `ReadOnlySpan<T>` |
| 큰 배열을 반복 사용 | `ArrayPool<T>` |
| 오래 보관해야 하는 데이터 | 일반 배열 또는 `Memory<T>` |

처음에는 일반 배열로 시작하고, 실제로 GC나 복사 비용이 문제가 될 때 `Span<T>`와 `ArrayPool<T>`를 적용하는 것이 안전합니다.
