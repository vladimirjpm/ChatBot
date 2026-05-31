namespace ChatBot.Core.Interfaces;

/// <summary>
/// RAG-поиск: семантический поиск релевантных чанков по запросу пользователя.
/// </summary>
public interface IRagService
{
    /// <summary>
    /// Ищет top-K наиболее близких чанков к тексту запроса.
    /// </summary>
    /// <param name="query">Текст запроса пользователя.</param>
    /// <param name="topK">Количество возвращаемых чанков.</param>
    Task<IReadOnlyList<RagChunk>> SearchAsync(string query, int topK = 5, CancellationToken ct = default);
}
