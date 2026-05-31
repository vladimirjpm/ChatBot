namespace ChatBot.Core.Interfaces;

/// <summary>
/// Загрузка и индексация документов в векторную БД.
/// </summary>
public interface IIngestionService
{
    /// <summary>
    /// Парсит PDF, нарезает на чанки, создаёт эмбеддинги и сохраняет в Qdrant.
    /// </summary>
    /// <param name="stream">Поток байт загружаемого файла.</param>
    /// <param name="fileName">Оригинальное имя файла для метаданных.</param>
    Task<IngestionResult> IngestAsync(Stream stream, string fileName, CancellationToken ct = default);
}
