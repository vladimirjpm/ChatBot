using ChatBot.Core;
using ChatBot.Core.Interfaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace ChatBot.Infrastructure.Services;

/// <summary>
/// Document ingestion pipeline: PDF → text → chunks → embeddings → Qdrant.
/// Also handles listing and deleting documents in the collection.
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
            throw new ArgumentException($"scope must be 'shared' or 'private', got '{scope}'", nameof(scope));
        if (scope == "private" && string.IsNullOrEmpty(sessionId))
            throw new ArgumentException("sessionId is required for scope=private", nameof(sessionId));

        var pages = PdfTextExtractor.ExtractPages(stream);
        logger.LogInformation("PDF {File} ({Scope}): {Pages} pages", fileName, scope, pages.Count);

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

        logger.LogInformation("Document {File}: indexed {Count} chunks", fileName, totalChunks);
        return new IngestionResult(totalChunks, fileName);
    }

    public async Task<IReadOnlyList<DocumentInfo>> ListAsync(string? sessionId, CancellationToken ct = default)
    {
        logger.LogInformation("ListAsync start: sessionId={Sid}", sessionId);

        bool exists;
        try
        {
            exists = await qdrant.CollectionExistsAsync(CollectionName, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CollectionExistsAsync failed for {Coll}", CollectionName);
            throw;
        }
        if (!exists)
        {
            logger.LogWarning("Collection {Coll} does not exist — returning empty list", CollectionName);
            return [];
        }

        // Scroll fetches all matching points with payload, without vectors (saves bandwidth).
        // .NET: equivalent of IAsyncEnumerable + pagination, but a single batch is fine for demo volumes.
        Qdrant.Client.Grpc.ScrollResponse response;
        try
        {
            response = await qdrant.ScrollAsync(
                CollectionName,
                filter: QdrantFilters.ForSession(sessionId),
                limit: 10_000,
                vectorsSelector: new WithVectorsSelector { Enable = false },
                cancellationToken: ct);
            logger.LogInformation("ScrollAsync OK: returned {N} points", response.Result.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ScrollAsync failed: sessionId={Sid}", sessionId);
            throw;
        }

        // Group chunks by (documentName, scope) — each pair is one document for the UI.
        return response.Result
            .GroupBy(p => (
                Name: p.Payload.GetValueOrDefault("documentName")?.StringValue ?? "?",
                Scope: p.Payload.GetValueOrDefault("scope")?.StringValue ?? "shared"))
            .Select(g => new DocumentInfo(g.Key.Name, g.Key.Scope, g.Count()))
            .OrderBy(d => d.Scope) // private first so the user's own documents appear at the top
            .ThenBy(d => d.Name)
            .ToList();
    }

    public async Task<int> DeleteAsync(string documentName, string sessionId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(sessionId))
            throw new ArgumentException("sessionId is required", nameof(sessionId));
        if (!await qdrant.CollectionExistsAsync(CollectionName, ct))
            return 0;

        // Filter ensures only private chunks belonging to this session are deleted.
        // Shared documents cannot be deleted via this endpoint — protects the shared collection from accidental damage.
        var filter = QdrantFilters.OwnedPrivateDocument(documentName, sessionId);

        // Count first to know how many will be deleted (for logs and UI feedback).
        var countResp = await qdrant.CountAsync(CollectionName, filter, cancellationToken: ct);
        if (countResp == 0)
        {
            logger.LogInformation("Delete {Doc}: no private chunks found for this session", documentName);
            return 0;
        }

        await qdrant.DeleteAsync(CollectionName, filter, cancellationToken: ct);
        logger.LogInformation("Deleted {Count} chunks of document {Doc}", countResp, documentName);
        return (int)countResp;
    }

    // Payload fields used to build filters (Ingestion/Rag/listing/deletion).
    // Qdrant Cloud runs in strict mode: filtering on a non-indexed field → InvalidArgument.
    // .NET: conceptually similar to CREATE INDEX in EF Core migrations.
    private static readonly string[] IndexedKeywordFields = ["scope", "sessionId", "documentName"];

    private async Task EnsureCollectionAsync(CancellationToken ct)
    {
        var existed = await qdrant.CollectionExistsAsync(CollectionName, ct);
        if (!existed)
        {
            await qdrant.CreateCollectionAsync(
                CollectionName,
                new VectorParams { Size = VectorSize, Distance = Distance.Cosine },
                cancellationToken: ct);
            logger.LogInformation("Created Qdrant collection {Name} (dim={Dim}, cosine)", CollectionName, VectorSize);
        }

        // Always create indexes — idempotent for collections created before the fix.
        // CreatePayloadIndexAsync is safe to call repeatedly: if the index already exists, Qdrant returns ok.
        foreach (var field in IndexedKeywordFields)
        {
            try
            {
                await qdrant.CreatePayloadIndexAsync(
                    CollectionName,
                    field,
                    schemaType: PayloadSchemaType.Keyword,
                    cancellationToken: ct);
                logger.LogInformation("Payload index {Field} (keyword) on collection {Coll} is ready", field, CollectionName);
            }
            catch (Exception ex)
            {
                // Don't fail the ingest because of an index error — log and continue. Filter will fail later if the index truly wasn't created.
                logger.LogWarning(ex, "Failed to create payload index {Field}", field);
            }
        }
    }
}
