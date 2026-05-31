namespace ChatBot.Core.Interfaces;

/// <summary>
/// Стриминговый чат-сервис на основе LLM.
/// Возвращает токены по мере генерации через IAsyncEnumerable (SSE-friendly).
/// </summary>
public interface IChatService
{
    /// <summary>
    /// Стриминг ответа ассистента токен за токеном.
    /// </summary>
    /// <param name="request">Сообщение пользователя и ID сессии.</param>
    /// <param name="cancellationToken">Токен отмены (разрыв соединения клиентом).</param>
    IAsyncEnumerable<string> StreamAsync(ChatRequest request, CancellationToken cancellationToken = default);
}
