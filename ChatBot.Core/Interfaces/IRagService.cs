namespace ChatBot.Core.Interfaces;

/// <summary>
/// RAG-поиск: семантический поиск релевантных чанков по запросу пользователя.
/// </summary>
public interface IRagService
{
    /// <summary>
    /// Ищет top-K наиболее близких чанков. Видны shared-документы + private текущей сессии.
    /// </summary>
    /// <param name="query">Текст запроса пользователя.</param>
    /// <param name="sessionId">ID сессии для доступа к приватным чанкам. null = только shared.</param>
    /// <param name="topK">Количество возвращаемых чанков.</param>
    Task<IReadOnlyList<RagChunk>> SearchAsync(string query, string? sessionId, int topK = 5, CancellationToken ct = default);
}
