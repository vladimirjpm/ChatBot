namespace ChatBot.Core.Interfaces;

/// <summary>
/// Upload, list, and delete documents in the vector DB.
///
/// Scope:
/// - "shared"  — accessible to all sessions (shared knowledge base)
/// - "private" — accessible only within its own session (private CV/context)
/// </summary>
public interface IIngestionService
{
    /// <summary>
    /// Parses a PDF, splits it into chunks, generates embeddings, and stores them in Qdrant.
    /// </summary>
    /// <param name="stream">Byte stream of the uploaded file.</param>
    /// <param name="fileName">Original file name for metadata.</param>
    /// <param name="scope">"shared" or "private".</param>
    /// <param name="sessionId">Required for scope=private; ignored for shared.</param>
    Task<IngestionResult> IngestAsync(Stream stream, string fileName, string scope, string? sessionId, CancellationToken ct = default);

    /// <summary>
    /// Returns documents accessible in the given session (shared + private of this session).
    /// </summary>
    Task<IReadOnlyList<DocumentInfo>> ListAsync(string? sessionId, CancellationToken ct = default);

    /// <summary>
    /// Deletes a private document of the current session. Shared documents cannot be deleted via this method.
    /// </summary>
    /// <returns>Number of chunks deleted.</returns>
    Task<int> DeleteAsync(string documentName, string sessionId, CancellationToken ct = default);
}
