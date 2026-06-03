namespace ChatBot.Core.Interfaces;

/// <summary>
/// RAG retrieval: semantic search for relevant chunks matching a user query.
/// </summary>
public interface IRagService
{
    /// <summary>
    /// Finds the top-K closest chunks. Includes shared documents + private ones of the current session.
    /// </summary>
    /// <param name="query">User query text.</param>
    /// <param name="sessionId">Session ID for access to private chunks. null = shared only.</param>
    /// <param name="topK">Number of chunks to return.</param>
    Task<IReadOnlyList<RagChunk>> SearchAsync(string query, string? sessionId, int topK = 5, CancellationToken ct = default);
}
