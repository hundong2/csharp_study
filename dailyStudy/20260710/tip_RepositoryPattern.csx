#nullable enable

using System;
using System.Collections.Generic;

// 실무 패턴: Repository
// 저장소 접근 코드를 비즈니스 로직에서 분리하는 패턴입니다.
// 실제 서비스에서는 DB, Redis, 파일 저장소 등을 이 인터페이스 뒤로 숨깁니다.

public readonly record struct Trade(long Id, string Symbol, double Price);

public interface ITradeRepository
{
    void Add(Trade trade);
    Trade? FindById(long id);
}

public sealed class InMemoryTradeRepository : ITradeRepository
{
    private readonly Dictionary<long, Trade> _storage = new();

    public void Add(Trade trade)
    {
        _storage[trade.Id] = trade;
    }

    public Trade? FindById(long id)
    {
        return _storage.TryGetValue(id, out Trade trade)
            ? trade
            : null;
    }
}

ITradeRepository repository = new InMemoryTradeRepository();

repository.Add(new Trade(1, "KRW-BTC", 150.25d));
Trade? found = repository.FindById(1);

Console.WriteLine(found is null
    ? "[Repository] Trade not found."
    : $"[Repository] Found: {found.Value.Symbol} {found.Value.Price}");
