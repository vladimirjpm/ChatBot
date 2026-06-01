namespace ChatBot.Core.Interfaces;

/// <summary>
/// Загрузка, листинг и удаление документов в векторной БД.
///
/// Scope:
/// - "shared"  — доступен всем сессиям (общая база знаний)
/// - "private" — доступен только в своей сессии (приватный CV/контекст)
/// </summary>
public interface IIngestionService
{
    /// <summary>
    /// Парсит PDF, нарезает на чанки, создаёт эмбеддинги и сохраняет в Qdrant.
    /// </summary>
    /// <param name="stream">Поток байт загружаемого файла.</param>
    /// <param name="fileName">Оригинальное имя файла для метаданных.</param>
    /// <param name="scope">"shared" или "private".</param>
    /// <param name="sessionId">Обязателен для scope=private, игнорируется для shared.</param>
    Task<IngestionResult> IngestAsync(Stream stream, string fileName, string scope, string? sessionId, CancellationToken ct = default);

    /// <summary>
    /// Возвращает список документов, доступных в данной сессии (shared + private этой сессии).
    /// </summary>
    Task<IReadOnlyList<DocumentInfo>> ListAsync(string? sessionId, CancellationToken ct = default);

    /// <summary>
    /// Удаляет приватный документ текущей сессии. Shared удалить нельзя через этот метод.
    /// </summary>
    /// <returns>Число удалённых чанков.</returns>
    Task<int> DeleteAsync(string documentName, string sessionId, CancellationToken ct = default);
}
