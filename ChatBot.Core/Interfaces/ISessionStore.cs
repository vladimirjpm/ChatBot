namespace ChatBot.Core.Interfaces;

/// <summary>
/// Хранилище состояния диалоговых сессий.
///
/// Регистрируется как Singleton — состояние живёт между HTTP-запросами.
/// Так решаем проблему Scoped IChatService + instance-Dictionary: раньше
/// каждый запрос получал новый ChatService с пустым словарём, и история
/// диалога никогда не сохранялась.
///
/// In-memory реализация (<see cref="ChatBot.Infrastructure.Services.InMemorySessionStore"/>)
/// подходит для одной инстанции. Для масштабирования планируется
/// RedisSessionStore — поэтому API сразу асинхронный (ValueTask).
///
/// .NET: аналог IDistributedCache, но типизированный и без сериализации
/// на этом уровне абстракции. Эквивалент builder.Services.AddSingleton&lt;ISessionStore, ...&gt;().
/// </summary>
public interface ISessionStore
{
    /// <summary>
    /// Получить состояние сессии. null — если сессия не создавалась.
    /// </summary>
    ValueTask<SessionState?> GetAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>
    /// Сохранить/перезаписать состояние сессии.
    /// </summary>
    ValueTask SetAsync(Guid sessionId, SessionState state, CancellationToken ct = default);
}
