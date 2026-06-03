using ChatBot.Core;
using ChatBot.Core.Interfaces;
using ChatBot.Infrastructure.Services;
using Xunit;

namespace ChatBot.Tests;

/// <summary>
/// Unit tests for <see cref="InMemorySessionStore"/>.
/// Key regression case: Set → Get returns exactly what was stored
/// (previously the Dictionary lived in a Scoped service and lost state between requests).
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
        // ConcurrentDictionary-based implementation must survive concurrent writes
        // to different keys without exceptions (the primary goal — thread-safe API).
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
