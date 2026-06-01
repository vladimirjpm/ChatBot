using ChatBot.Core;
using ChatBot.Core.Interfaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace ChatBot.Infrastructure.Services;

/// <summary>
/// Pipeline загрузки документа: PDF → текст → чанки → эмбеддинги → Qdrant.
/// Также отвечает за листинг и удаление документов в коллекции.
/// </summary>
public class IngestionService(
    IEmbeddingGenerator<string, Embedding<float>> embeddings,
    QdrantClient qdrant,
    ILogger<IngestionService> logger) : IIngestionService
{
    private const string CollectionName = "documents";
    private const ulong VectorSize = 1536;

    public async Task<IngestionResult> IngestAsync(
        Stream stream, string fileName, string scope, string? sessionId, CancellationToken ct = default)
    {
        if (scope != "shared" && scope != "private")
            throw new ArgumentException($"scope должен быть 'shared' или 'private', получено '{scope}'", nameof(scope));
        if (scope == "private" && string.IsNullOrEmpty(sessionId))
            throw new ArgumentException("Для scope=private обязателен sessionId", nameof(sessionId));

        var pages = PdfTextExtractor.ExtractPages(stream);
        logger.LogInformation("PDF {File} ({Scope}): {Pages} страниц", fileName, scope, pages.Count);

        await EnsureCollectionAsync(ct);

        var totalChunks = 0;
        var points = new List<PointStruct>();

        foreach (var page in pages)
        {
            var chunks = TextChunker.Chunk(page.Text);
            if (chunks.Count == 0) continue;

            var vectors = await embeddings.GenerateAsync(chunks, cancellationToken: ct);

            for (var i = 0; i < chunks.Count; i++)
            {
                var point = new PointStruct
                {
                    Id = new PointId { Uuid = Guid.NewGuid().ToString() },
                    Vectors = vectors[i].Vector.ToArray(),
                };
                point.Payload["documentName"] = fileName;
                point.Payload["pageNumber"] = page.PageNumber;
                point.Payload["text"] = chunks[i];
                point.Payload["scope"] = scope;
                if (scope == "private")
                    point.Payload["sessionId"] = sessionId!;
                points.Add(point);
                totalChunks++;
            }
        }

        if (points.Count > 0)
            await qdrant.UpsertAsync(CollectionName, points, cancellationToken: ct);

        logger.LogInformation("Документ {File}: проиндексировано {Count} чанков", fileName, totalChunks);
        return new IngestionResult(totalChunks, fileName);
    }

    public async Task<IReadOnlyList<DocumentInfo>> ListAsync(string? sessionId, CancellationToken ct = default)
    {
        if (!await qdrant.CollectionExistsAsync(CollectionName, ct))
            return [];

        // Scroll достаёт все доступные точки с payload, без векторов (экономим трафик).
        // .NET: эквивалент IAsyncEnumerable + пагинации, но для демо-объёмов хватит одного батча.
        var response = await qdrant.ScrollAsync(
            CollectionName,
            filter: QdrantFilters.ForSession(sessionId),
            limit: 10_000,
            vectorsSelector: new WithVectorsSelector { Enable = false },
            cancellationToken: ct);

        // Группируем чанки по (documentName, scope) — каждая пара это один документ для UI.
        return response.Result
            .GroupBy(p => (
                Name: p.Payload.GetValueOrDefault("documentName")?.StringValue ?? "?",
                Scope: p.Payload.GetValueOrDefault("scope")?.StringValue ?? "shared"))
            .Select(g => new DocumentInfo(g.Key.Name, g.Key.Scope, g.Count()))
            .OrderBy(d => d.Scope) // private сверху чтобы свои документы видеть первыми
            .ThenBy(d => d.Name)
            .ToList();
    }

    public async Task<int> DeleteAsync(string documentName, string sessionId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(sessionId))
            throw new ArgumentException("sessionId обязателен", nameof(sessionId));
        if (!await qdrant.CollectionExistsAsync(CollectionName, ct))
            return 0;

        // Фильтр гарантирует что удаляются только приватные чанки этой сессии.
        // Shared удалить через эндпоинт нельзя — это защита от случайной порчи общей базы.
        var filter = QdrantFilters.OwnedPrivateDocument(documentName, sessionId);

        // Сначала считаем сколько удалим (для логов и UI)
        var countResp = await qdrant.CountAsync(CollectionName, filter, cancellationToken: ct);
        if (countResp == 0)
        {
            logger.LogInformation("Удаление {Doc}: нет приватных чанков для сессии", documentName);
            return 0;
        }

        await qdrant.DeleteAsync(CollectionName, filter, cancellationToken: ct);
        logger.LogInformation("Удалено {Count} чанков документа {Doc}", countResp, documentName);
        return (int)countResp;
    }

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
