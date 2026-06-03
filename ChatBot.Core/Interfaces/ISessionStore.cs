namespace ChatBot.Core.Interfaces;

/// <summary>
/// Storage for dialogue session state.
///
/// Registered as Singleton — state persists across HTTP requests.
/// This solves the Scoped IChatService + instance-Dictionary problem: previously
/// each request received a new ChatService with an empty dictionary, so chat history
/// was never preserved.
///
/// The in-memory implementation (<see cref="ChatBot.Infrastructure.Services.InMemorySessionStore"/>)
/// is suitable for single-instance deployments. A RedisSessionStore is planned for
/// scale-out — that is why the API is async (ValueTask) from the start.
///
/// .NET: similar to IDistributedCache but typed and without serialization
/// at this abstraction level. Equivalent of builder.Services.AddSingleton&lt;ISessionStore, ...&gt;().
/// </summary>
public interface ISessionStore
{
    /// <summary>
    /// Get session state. Returns null if the session has not been created yet.
    /// </summary>
    ValueTask<SessionState?> GetAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>
    /// Save or overwrite session state.
    /// </summary>
    ValueTask SetAsync(Guid sessionId, SessionState state, CancellationToken ct = default);
}
