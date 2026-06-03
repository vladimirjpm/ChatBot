using System.Collections.Concurrent;
using ChatBot.Core;
using ChatBot.Core.Interfaces;

namespace ChatBot.Infrastructure.Services;

/// <summary>
/// In-memory implementation of <see cref="ISessionStore"/> backed by ConcurrentDictionary.
///
/// Registered as Singleton — state survives across HTTP requests.
/// Suitable for single-instance deployments (Railway, local).
/// Horizontal scaling requires a RedisSessionStore.
///
/// .NET: equivalent of MemoryCache but without TTL/eviction (tech debt — will be
/// resolved when migrating to Redis, which has TTL built in).
/// </summary>
public class InMemorySessionStore : ISessionStore
{
    // ConcurrentDictionary is thread-safe for concurrent Get/Set across different sessions.
    // A race is only possible within a single sessionId under simultaneous requests (see SessionState).
    private readonly ConcurrentDictionary<Guid, SessionState> _sessions = new();

    public ValueTask<SessionState?> GetAsync(Guid sessionId, CancellationToken ct = default)
        => ValueTask.FromResult(_sessions.TryGetValue(sessionId, out var state) ? state : null);

    public ValueTask SetAsync(Guid sessionId, SessionState state, CancellationToken ct = default)
    {
        _sessions[sessionId] = state;
        return ValueTask.CompletedTask;
    }
}
