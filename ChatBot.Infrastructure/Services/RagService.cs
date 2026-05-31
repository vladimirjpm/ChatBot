using ChatBot.Core;
using ChatBot.Core.Interfaces;

namespace ChatBot.Infrastructure.Services;

/// <summary>
/// RAG-сервис — заглушка.
/// TODO этап 5: реализовать через Qdrant.Client + эмбеддинги из Semantic Kernel.
/// </summary>
public class RagService : IRagService
{
    public Task<IReadOnlyList<RagChunk>> SearchAsync(string query, int topK = 5, CancellationToken ct = default)
    {
        // Возвращаем пустой список — RAG будет добавлен на этапе 5
        IReadOnlyList<RagChunk> empty = [];
        return Task.FromResult(empty);
    }
}
