using ChatBot.Core;
using ChatBot.Core.Interfaces;
using ChatBot.Infrastructure.Services;
using Xunit;

namespace ChatBot.Tests;

/// <summary>
/// Юнит-тесты <see cref="InMemorySessionStore"/>.
/// Главный регрессионный кейс: Set → Get возвращает то, что положили
/// (раньше Dictionary жил в Scoped-сервисе и терял состояние между запросами).
/// </summary>
public class InMemorySessionStoreTests
{
    [Fact]
    public async Task SetThenGet_ReturnsSameState()
    {
        var store = new InMemorySessionStore();
        var id = Guid.NewGuid();
        var state = new SessionState("system prompt", [new SessionMessage("user", "hi")]);

        await store.SetAsync(id, state);
        var actual = await store.GetAsync(id);

        Assert.NotNull(actual);
        Assert.Same(state, actual);
        Assert.Equal("system prompt", actual!.BaseSystemPrompt);
        Assert.Single(actual.History);
    }

    [Fact]
    public async Task GetMissingId_ReturnsNull()
    {
        var store = new InMemorySessionStore();
        var actual = await store.GetAsync(Guid.NewGuid());
        Assert.Null(actual);
    }

    [Fact]
    public async Task ConcurrentSets_DoNotThrow()
    {
        // ConcurrentDictionary-based реализация должна выдержать конкурентные записи
        // в разные ключи без исключений (главная задача — Thread-safe API).
        var store = new InMemorySessionStore();

        var tasks = Enumerable.Range(0, 200).Select(i => Task.Run(async () =>
        {
            var id = Guid.NewGuid();
            await store.SetAsync(id, new SessionState($"prompt {i}", []));
            var loaded = await store.GetAsync(id);
            Assert.NotNull(loaded);
        }));

        await Task.WhenAll(tasks);
    }
}
