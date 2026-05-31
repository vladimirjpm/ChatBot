using ChatBot.Core;
using ChatBot.Core.Interfaces;

namespace ChatBot.Infrastructure.Services;

/// <summary>
/// Сервис загрузки документов — заглушка.
/// TODO этап 4: реализовать парсинг PDF через PdfPig, нарезку на чанки,
/// генерацию эмбеддингов и сохранение в Qdrant.
/// </summary>
public class IngestionService : IIngestionService
{
    public Task<IngestionResult> IngestAsync(Stream stream, string fileName, CancellationToken ct = default)
    {
        // Заглушка — реализация добавляется на этапе 4
        return Task.FromResult(new IngestionResult(0, fileName));
    }
}
