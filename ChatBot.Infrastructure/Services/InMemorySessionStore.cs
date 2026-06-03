using System.Collections.Concurrent;
using ChatBot.Core;
using ChatBot.Core.Interfaces;

namespace ChatBot.Infrastructure.Services;

/// <summary>
/// In-memory реализация <see cref="ISessionStore"/> на ConcurrentDictionary.
///
/// Регистрируется как Singleton — состояние переживает HTTP-запросы.
/// Подходит для single-instance деплоя (Railway, локально).
/// Для горизонтального масштабирования нужен RedisSessionStore.
///
/// .NET: эквивалент MemoryCache, но без TTL/eviction (техдолг — закроется
/// переездом на Redis, где TTL из коробки).
/// </summary>
public class InMemorySessionStore : ISessionStore
{
    // ConcurrentDictionary — потокобезопасный для конкурентных Get/Set из разных сессий.
    // Гонка возможна только внутри одного sessionId при одновременных запросах (см. SessionState).
    private readonly ConcurrentDictionary<Guid, SessionState> _sessions = new();

    public ValueTask<SessionState?> GetAsync(Guid sessionId, CancellationToken ct = default)
        => ValueTask.FromResult(_sessions.TryGetValue(sessionId, out var state) ? state : null);

    public ValueTask SetAsync(Guid sessionId, SessionState state, CancellationToken ct = default)
    {
        _sessions[sessionId] = state;
        return ValueTask.CompletedTask;
    }
}
