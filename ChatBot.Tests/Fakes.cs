using System.Runtime.CompilerServices;
using ChatBot.Core;
using ChatBot.Core.Interfaces;

namespace ChatBot.Tests;

/// <summary>
/// Фейк <see cref="IChatService"/> — отдаёт детерминированный набор токенов без обращения к LLM.
/// .NET: аналог Moq-объекта, но без зависимости от Moq — для простого стримингового API проще написать руками.
/// </summary>
public sealed class FakeChatService : IChatService
{
    public List<ChatRequest> ReceivedRequests { get; } = [];

    public async IAsyncEnumerable<string> StreamAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ReceivedRequests.Add(request);

        // Имитируем стриминг тремя токенами — async для соответствия сигнатуре IAsyncEnumerable
        foreach (var token in new[] { "Hello", ", ", "world!" })
        {
            await Task.Yield();
            yield return token;
        }
    }
}

/// <summary>
/// Фейк <see cref="IIngestionService"/> — возвращает фиксированный <see cref="IngestionResult"/>.
/// </summary>
public sealed class FakeIngestionService : IIngestionService
{
    public Task<IngestionResult> IngestAsync(Stream stream, string fileName, CancellationToken ct = default)
        => Task.FromResult(new IngestionResult(ChunksIndexed: 7, DocumentName: fileName));
}
