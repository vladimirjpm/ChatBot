using System.Runtime.CompilerServices;
using ChatBot.Core;
using ChatBot.Core.Interfaces;
using ChatBot.Infrastructure.Localization;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace ChatBot.Infrastructure.Services;

/// <summary>
/// Реализация стримингового чата через Semantic Kernel.
///
/// История сессии хранится во внешнем <see cref="ISessionStore"/> (Singleton),
/// а сам сервис остаётся Scoped — это валидная связь
/// (Scoped может зависеть от Singleton, обратное даёт captive dependency).
///
/// До рефакторинга история жила в instance-Dictionary, а Scoped lifetime
/// создавал новый ChatService на каждый запрос — словарь всегда был пустой.
/// </summary>
public class ChatService(
    IChatCompletionService chatCompletion,
    IRagService ragService,
    ILocalizationProvider localization,
    ISessionStore sessions) : IChatService
{
    /*
     * ────────────────────────────────────────────────────────────────────────────
     *  БУДУЩЕЕ: версия на Microsoft.Extensions.AI (IChatClient).
     *  Главные отличия от SK:
     *  1. Вместо ChatHistory (SK-специфичный класс) — List<ChatMessage> из M.E.AI
     *  2. Вместо AddSystemMessage/AddUserMessage — конструктор ChatMessage(role, text)
     *  3. Вместо GetStreamingChatMessageContentsAsync — GetStreamingResponseAsync
     *  4. Для function calling (этап 7+) — .UseFunctionInvocation() в Program.cs,
     *     IChatClient сам распарсит tool_calls и вызовет C# функцию.
     * ────────────────────────────────────────────────────────────────────────────
     */

    /// <inheritdoc/>
    public async IAsyncEnumerable<string> StreamAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var sessionId = request.SessionId ?? Guid.NewGuid();
        var mode = string.IsNullOrEmpty(request.Mode)
            // Совместимость: если режим не указан — определяем по наличию роли (старое поведение)
            ? (string.IsNullOrEmpty(request.Role) ? "assistant" : "interview")
            : request.Mode;

        var session = await GetOrCreateSessionAsync(sessionId, mode, request.Role, request.ResumeContext, request.Language, cancellationToken);

        // Ищем релевантные чанки: shared + приватные именно этой сессии
        var chunks = await ragService.SearchAsync(request.Message, sessionId.ToString(), topK: 5, ct: cancellationToken);

        // Собираем промпт СВЕЖИМ на каждый запрос: base system + grounding + RAG → пары user/assistant → новое сообщение.
        // Старые RAG-контексты не утекают в следующие запросы, потому что не записываются в SessionState.History.
        var locale = localization.Get(request.Language);
        var prompt = new ChatHistory(session.BaseSystemPrompt);
        if (chunks.Count > 0)
        {
            var grounding = mode == "assistant" ? locale.GroundingHard : locale.GroundingSoft;
            var context = string.Join("\n\n", chunks.Select(c => $"[{c.DocumentName} стр.{c.PageNumber}]\n{c.Text}"));
            prompt.AddSystemMessage($"{grounding}\n\n{locale.ContextHeader}\n{context}");
        }

        // Маппинг строковой роли → AuthorRole SK. Делаем здесь, а не в Core,
        // чтобы не тащить SK-зависимость в ChatBot.Core.
        foreach (var msg in session.History)
            prompt.AddMessage(MapRole(msg.Role), msg.Content);
        prompt.AddUserMessage(request.Message);

        var sb = new System.Text.StringBuilder();

        await foreach (var chunk in chatCompletion.GetStreamingChatMessageContentsAsync(prompt, cancellationToken: cancellationToken))
        {
            var token = chunk.Content ?? string.Empty;
            if (!string.IsNullOrEmpty(token))
            {
                sb.Append(token);
                yield return token;
            }
        }

        // Сохраняем в долговременную историю ТОЛЬКО user/assistant — без RAG-системников.
        // List мутируется in-place (record-ом не replace-имся), но явная SetAsync на случай
        // будущего RedisSessionStore — там потребуется повторная сериализация.
        session.History.Add(new SessionMessage("user", request.Message));
        session.History.Add(new SessionMessage("assistant", sb.ToString()));
        await sessions.SetAsync(sessionId, session, cancellationToken);
    }

    private async ValueTask<SessionState> GetOrCreateSessionAsync(
        Guid sessionId, string mode, string? role, string? resumeContext, string? language, CancellationToken ct)
    {
        var existing = await sessions.GetAsync(sessionId, ct);
        if (existing is not null)
            return existing;

        var basePrompt = BuildSystemPrompt(mode, role, resumeContext, language);
        var fresh = new SessionState(basePrompt, []);
        await sessions.SetAsync(sessionId, fresh, ct);
        return fresh;
    }

    /// <summary>
    /// Маппинг строковой роли (Core-слой) в SK-шный AuthorRole.
    /// </summary>
    private static AuthorRole MapRole(string role) => role switch
    {
        "user" => AuthorRole.User,
        "assistant" => AuthorRole.Assistant,
        "system" => AuthorRole.System,
        _ => AuthorRole.User,
    };

    /// <summary>
    /// Строит базовый системный промпт сессии из загруженной локали.
    /// Режимы: "interview" (симулятор собеседования) или "assistant" (RAG-чат).
    /// </summary>
    private string BuildSystemPrompt(string mode, string? role, string? resumeContext, string? language)
    {
        var locale = localization.Get(language);

        if (mode == "assistant")
            return locale.AssistantBase;

        if (string.IsNullOrWhiteSpace(role))
            return locale.GenericAssistant;

        var roleName = locale.RoleNames.GetValueOrDefault(role.ToLowerInvariant())
            ?? role + locale.UnknownRoleSuffix;

        var prompt = Locale.Format(locale.InterviewRules, new Dictionary<string, string> { ["roleName"] = roleName });

        if (!string.IsNullOrWhiteSpace(resumeContext))
            prompt += Locale.Format(locale.InterviewResumeAddendum, new Dictionary<string, string> { ["resume"] = resumeContext });

        prompt += locale.InterviewStartCue;
        return prompt;
    }
}
