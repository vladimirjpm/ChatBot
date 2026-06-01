using ChatBot.Core;
using ChatBot.Core.Interfaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Qdrant.Client;

namespace ChatBot.Infrastructure.Services;

/// <summary>
/// RAG-поиск по векторной БД: эмбеддинг запроса → top-K поиск в Qdrant → возврат чанков.
///
/// .NET: оркестратор как и <see cref="IngestionService"/> — две зависимости (эмбеддинги + Qdrant),
/// никакой бизнес-логики кроме маппинга ScoredPoint → RagChunk.
/// </summary>
public class RagService(
    IEmbeddingGenerator<string, Embedding<float>> embeddings,
    QdrantClient qdrant,
    ILogger<RagService> logger) : IRagService
{
    // Должно совпадать с именем коллекции в IngestionService — общая константа была бы чище,
    // но для демо двух мест достаточно.
    private const string CollectionName = "documents";

    public async Task<IReadOnlyList<RagChunk>> SearchAsync(string query, int topK = 5, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];

        // Если коллекции ещё нет (никто не загружал документы) — возвращаем пусто,
        // чтобы чат продолжал работать в режиме «без контекста», а не падал 5xx.
        if (!await qdrant.CollectionExistsAsync(CollectionName, ct))
            return [];

        // 1. Эмбеддинг запроса — один вызов OpenAI на каждое сообщение пользователя.
        //    Кэширование (Redis) добавится в этапе 6+ — пока живём с задержкой ~50-100мс.
        var queryVector = (await embeddings.GenerateAsync([query], cancellationToken: ct))[0].Vector;

        // 2. Поиск top-K ближайших по cosine similarity
        var hits = await qdrant.SearchAsync(
            CollectionName,
            queryVector,
            limit: (ulong)topK,
            cancellationToken: ct);

        logger.LogInformation("RAG поиск: query={Len} симв, найдено {Count} чанков", query.Length, hits.Count);

        // 3. Маппинг payload → RagChunk. Embedding не возвращаем — не нужен потребителю,
        //    экономим аллокацию массива на 1536 float-ов.
        return hits.Select(hit => new RagChunk(
            Id: Guid.Parse(hit.Id.Uuid),
            DocumentName: hit.Payload.GetValueOrDefault("documentName")?.StringValue ?? "?",
            PageNumber: (int)(hit.Payload.GetValueOrDefault("pageNumber")?.IntegerValue ?? 0),
            Text: hit.Payload.GetValueOrDefault("text")?.StringValue ?? "",
            Embedding: []
        )).ToList();
    }
}
