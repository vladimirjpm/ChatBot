namespace ChatBot.Core.Interfaces;

/// <summary>
/// LLM-backed streaming chat service.
/// Yields tokens as they are generated via IAsyncEnumerable (SSE-friendly).
/// </summary>
public interface IChatService
{
    /// <summary>
    /// Streams the assistant response token by token.
    /// </summary>
    /// <param name="request">User message and session ID.</param>
    /// <param name="cancellationToken">Cancellation token (client disconnect).</param>
    IAsyncEnumerable<string> StreamAsync(ChatRequest request, CancellationToken cancellationToken = default);
}
