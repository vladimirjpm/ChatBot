namespace ChatBot.Core;

/// <summary>
/// Чанк документа, сохранённый в векторной БД для RAG-поиска.
/// </summary>
public record RagChunk(
    Guid Id,
    string DocumentName,
    int PageNumber,
    string Text,
    float[] Embedding
);

/// <summary>
/// Одно сообщение в диалоге (пользователь или ассистент).
/// </summary>
public record ChatMessage(string Role, string Content, DateTimeOffset Timestamp);

/// <summary>
/// Сессия диалога — контейнер истории сообщений.
/// </summary>
public class ChatSession
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public List<ChatMessage> Messages { get; } = [];
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Запрос стримингового чата от клиента.
/// Role — роль для симулятора собеседований (dotnet / react / devops).
/// Передаётся только при старте новой сессии.
/// </summary>
/// <summary>
/// Language — язык интервью: "ru" или "en". По умолчанию русский.
/// </summary>
public record ChatRequest(
    string Message,
    Guid? SessionId = null,
    string? Role = null,
    string? ResumeContext = null,
    string? Language = "ru",
    // "interview" — симулятор собеседования, RAG в роли вспомогательного контекста (мягкий grounding)
    // "assistant" — RAG-чат поверх документов с жёстким grounding ("не нашёл — не знаю")
    string? Mode = null);

/// <summary>
/// Результат загрузки документа.
/// </summary>
public record IngestionResult(int ChunksIndexed, string DocumentName);

/// <summary>
/// Информация о загруженном документе для отображения в UI.
/// Scope: "shared" (виден всем) или "private" (только в своей сессии).
/// </summary>
public record DocumentInfo(string Name, string Scope, int Chunks);
