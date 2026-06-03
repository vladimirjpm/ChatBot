using ChatBot.Core;
using ChatBot.Core.Interfaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Qdrant.Client;

namespace ChatBot.Infrastructure.Services;

/// <summary>
/// RAG retrieval with privacy awareness: shared documents are visible to all,
/// private ones only within their own session.
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

        // Search with access filter — shared OR (private + current session).
        var hits = await qdrant.SearchAsync(
            CollectionName,
            queryVector,
            filter: QdrantFilters.ForSession(sessionId),
            limit: (ulong)topK,
            cancellationToken: ct);

        logger.LogInformation("RAG: session={Session}, found {Count} chunks",
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
