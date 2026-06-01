using ChatBot.Core;
using ChatBot.Core.Interfaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Qdrant.Client;

namespace ChatBot.Infrastructure.Services;

/// <summary>
/// RAG-поиск с учётом приватности: shared-документы видны всем,
/// private — только в рамках своей сессии.
/// </summary>
public class RagService(
    IEmbeddingGenerator<string, Embedding<float>> embeddings,
    QdrantClient qdrant,
    ILogger<RagService> logger) : IRagService
{
    private const string CollectionName = "documents";

    public async Task<IReadOnlyList<RagChunk>> SearchAsync(
        string query, string? sessionId, int topK = 5, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];

        if (!await qdrant.CollectionExistsAsync(CollectionName, ct))
            return [];

        var queryVector = (await embeddings.GenerateAsync([query], cancellationToken: ct))[0].Vector;

        // Поиск с фильтром доступа — shared OR (private + текущая сессия).
        var hits = await qdrant.SearchAsync(
            CollectionName,
            queryVector,
            filter: QdrantFilters.ForSession(sessionId),
            limit: (ulong)topK,
            cancellationToken: ct);

        logger.LogInformation("RAG: session={Session}, найдено {Count} чанков",
            sessionId ?? "(anonymous)", hits.Count);

        return hits.Select(hit => new RagChunk(
            Id: Guid.Parse(hit.Id.Uuid),
            DocumentName: hit.Payload.GetValueOrDefault("documentName")?.StringValue ?? "?",
            PageNumber: (int)(hit.Payload.GetValueOrDefault("pageNumber")?.IntegerValue ?? 0),
            Text: hit.Payload.GetValueOrDefault("text")?.StringValue ?? "",
            Embedding: []
        )).ToList();
    }
}
