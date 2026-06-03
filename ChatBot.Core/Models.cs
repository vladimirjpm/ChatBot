namespace ChatBot.Core;

/// <summary>Document chunk stored in the vector DB for RAG retrieval.</summary>
public record RagChunk(
    Guid Id,
    string DocumentName,
    int PageNumber,
    string Text,
    float[] Embedding
);

/// <summary>A single dialogue message (user or assistant).</summary>
public record ChatMessage(string Role, string Content, DateTimeOffset Timestamp);

/// <summary>Chat session — container for message history.</summary>
public class ChatSession
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public List<ChatMessage> Messages { get; } = [];
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Streaming chat request from the client.
/// Role — role for the interview simulator (dotnet / react / devops).
/// Sent only at the start of a new session.
/// Language — interview language: "ru" or "en". Defaults to Russian.
/// </summary>
public record ChatRequest(
    string Message,
    Guid? SessionId = null,
    string? Role = null,
    string? ResumeContext = null,
    string? Language = "ru",
    // "interview" — interview simulator; RAG acts as supplementary context (soft grounding)
    // "assistant" — RAG chat over documents with hard grounding ("not found — don't know")
    string? Mode = null);

/// <summary>Result of document ingestion.</summary>
public record IngestionResult(int ChunksIndexed, string DocumentName);

/// <summary>
/// Uploaded document metadata for the UI.
/// Scope: "shared" (visible to all) or "private" (visible only within the current session).
/// </summary>
public record DocumentInfo(string Name, string Scope, int Chunks);

/// <summary>
/// A single message in the session history passed to the LLM.
///
/// Role is a string ("user" / "assistant" / "system") so the Core layer does not
/// depend on Semantic Kernel (AuthorRole lives in SK). Mapping to AuthorRole
/// happens in ChatService when building ChatHistory.
/// </summary>
public record SessionMessage(string Role, string Content);

/// <summary>
/// Dialogue session state: a fixed system prompt
/// (built on the first message from mode / role / resume / language)
/// and the chronological user/assistant pair history.
///
/// RAG context is NOT stored in History — it is injected fresh on each request;
/// otherwise stale chunks would pollute the context window.
///
/// Stored in ISessionStore (Singleton) so it survives across HTTP requests.
/// History is a mutable List, so two concurrent requests to the same session
/// can race (TODO: per-session lock). In practice the frontend blocks input
/// during streaming (see useChat.ts isStreaming), so the real risk is low.
/// </summary>
public record SessionState(string BaseSystemPrompt, List<SessionMessage> History);
