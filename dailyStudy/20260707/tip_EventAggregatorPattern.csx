#nullable enable

using System;
using System.Collections.Generic;

// 실무 패턴: Event Aggregator
// 여러 컴포넌트가 직접 서로 참조하지 않고 이벤트를 통해 느슨하게 연결되는 구조입니다.

public sealed class EventBus
{
    private readonly Dictionary<string, List<Action<string>>> _handlers = new();

    public void Subscribe(string eventName, Action<string> handler)
    {
        if (!_handlers.TryGetValue(eventName, out List<Action<string>>? handlers))
        {
            handlers = new List<Action<string>>();
            _handlers[eventName] = handlers;
        }

        handlers.Add(handler);
    }

    public void Publish(string eventName, string payload)
    {
        if (!_handlers.TryGetValue(eventName, out List<Action<string>>? handlers))
        {
            return;
        }

        foreach (Action<string> handler in handlers)
        {
            handler(payload);
        }
    }
}

var bus = new EventBus();

bus.Subscribe("OrderCreated", payload =>
{
    Console.WriteLine($"[EventBus] Send email for {payload}");
});

bus.Subscribe("OrderCreated", payload =>
{
    Console.WriteLine($"[EventBus] Write audit log for {payload}");
});

bus.Publish("OrderCreated", "order-1001");
