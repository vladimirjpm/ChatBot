using ChatBot.Core;
using ChatBot.Core.Interfaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace ChatBot.Infrastructure.Services;

/// <summary>
/// Pipeline загрузки документа: PDF → текст → чанки → эмбеддинги → Qdrant.
///
/// .NET: оркестратор-сервис, эквивалент middleware-цепочки или MediatR-команды.
/// Все шаги вынесены в отдельные классы (TextChunker / PdfTextExtractor) — этот класс
/// только склеивает их и пишет в Qdrant.
/// </summary>
public class IngestionService(
    IEmbeddingGenerator<string, Embedding<float>> embeddings,
    QdrantClient qdrant,
    ILogger<IngestionService> logger) : IIngestionService
{
    // Имя коллекции и размерность — должны совпадать с моделью эмбеддингов:
    // text-embedding-3-small → 1536 dim, cosine distance.
    // Если когда-то переедем на text-embedding-3-large (3072 dim) — нужно будет
    // пересоздать коллекцию или сделать отдельную.
    private const string CollectionName = "documents";
    private const ulong VectorSize = 1536;

    public async Task<IngestionResult> IngestAsync(Stream stream, string fileName, CancellationToken ct = default)
    {
        // 1. Парсинг PDF постранично
        var pages = PdfTextExtractor.ExtractPages(stream);
        logger.LogInformation("PDF {File}: {Pages} страниц", fileName, pages.Count);

        // 2. Гарантируем существование коллекции (идемпотентно)
        await EnsureCollectionAsync(ct);

        var totalChunks = 0;
        var points = new List<PointStruct>();

        foreach (var page in pages)
        {
            // 3. Нарезка страницы на чанки
            var chunks = TextChunker.Chunk(page.Text);
            if (chunks.Count == 0) continue;

            // 4. Эмбеддинги пачкой — один HTTP-запрос на страницу, экономит latency и деньги
            //    .NET: GenerateAsync принимает IEnumerable<string> — возвращает GeneratedEmbeddings
            var vectors = await embeddings.GenerateAsync(chunks, cancellationToken: ct);

            for (var i = 0; i < chunks.Count; i++)
            {
                var point = new PointStruct
                {
                    Id = new PointId { Uuid = Guid.NewGuid().ToString() },
                    Vectors = vectors[i].Vector.ToArray(),
                };
                // Payload — метаданные для фильтрации и отображения в RAG-результатах
                point.Payload["documentName"] = fileName;
                point.Payload["pageNumber"] = page.PageNumber;
                point.Payload["text"] = chunks[i];
                points.Add(point);
                totalChunks++;
            }
        }

        // 5. Один upsert всех точек — Qdrant сам разобьёт на батчи через gRPC streaming
        if (points.Count > 0)
            await qdrant.UpsertAsync(CollectionName, points, cancellationToken: ct);

        logger.LogInformation("Документ {File}: проиндексировано {Count} чанков", fileName, totalChunks);
        return new IngestionResult(totalChunks, fileName);
    }

    /// <summary>
    /// Создаёт коллекцию при первом запуске. Идемпотентно — повторный вызов no-op.
    /// </summary>
    private async Task EnsureCollectionAsync(CancellationToken ct)
    {
        if (await qdrant.CollectionExistsAsync(CollectionName, ct)) return;

        await qdrant.CreateCollectionAsync(
            CollectionName,
            new VectorParams { Size = VectorSize, Distance = Distance.Cosine },
            cancellationToken: ct);

        logger.LogInformation("Создана Qdrant-коллекция {Name} (dim={Dim}, cosine)", CollectionName, VectorSize);
    }
}
