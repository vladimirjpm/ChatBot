using System.Runtime.CompilerServices;
using ChatBot.Core;
using ChatBot.Core.Interfaces;

namespace ChatBot.Tests;

/// <summary>
/// Fake <see cref="IChatService"/> — returns a deterministic set of tokens without calling the LLM.
/// .NET: equivalent of a Moq mock but without the Moq dependency — easier to hand-write for a simple streaming API.
/// </summary>
public sealed class FakeChatService : IChatService
{
    public List<ChatRequest> ReceivedRequests { get; } = [];

    public async IAsyncEnumerable<string> StreamAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ReceivedRequests.Add(request);

        // Simulate streaming with three tokens — async to satisfy the IAsyncEnumerable signature
        foreach (var token in new[] { "Hello", ", ", "world!" })
        {
            await Task.Yield();
            yield return token;
        }
    }
}

/// <summary>Fake <see cref="IIngestionService"/> — returns fixed responses and writes nothing.</summary>
public sealed class FakeIngestionService : IIngestionService
{
    public Task<IngestionResult> IngestAsync(Stream stream, string fileName, string scope, string? sessionId, CancellationToken ct = default)
        => Task.FromResult(new IngestionResult(ChunksIndexed: 7, DocumentName: fileName));

    public Task<IReadOnlyList<DocumentInfo>> ListAsync(string? sessionId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<DocumentInfo>>([]);

    public Task<int> DeleteAsync(string documentName, string sessionId, CancellationToken ct = default)
        => Task.FromResult(0);
}
