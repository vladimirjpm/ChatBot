using System.Runtime.CompilerServices;
using ChatBot.Core;
using ChatBot.Core.Interfaces;
using ChatBot.Infrastructure.Localization;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace ChatBot.Infrastructure.Services;

/// <summary>
/// Реализация стримингового чата через Semantic Kernel.
/// Поддерживает историю в памяти процесса (без персистентности).
/// Для продакшна историю нужно хранить в Redis или PostgreSQL.
/// </summary>
public class ChatService(
    IChatCompletionService chatCompletion,
    IRagService ragService,
    ILocalizationProvider localization) : IChatService
{
    /// <summary>
    /// Состояние сессии: базовый системный промпт (фиксируется при первом сообщении)
    /// и хронология user/assistant. RAG-контекст и grounding-правила НЕ сохраняются —
    /// они инжектятся свежими на каждый запрос, иначе устаревшие чанки засоряли бы контекст.
    /// </summary>
    private sealed record SessionState(string BaseSystemPrompt, List<(AuthorRole Role, string Content)> History);

    // Хранилище сессий в памяти — заменить на распределённый кэш в prod
    private readonly Dictionary<Guid, SessionState> _sessions = [];

    /*
     * ────────────────────────────────────────────────────────────────────────────
     *  БУДУЩЕЕ: версия на Microsoft.Extensions.AI (IChatClient).
     *  Главные отличия от SK:
     *  1. Вместо ChatHistory (SK-специфичный класс) — List<ChatMessage> из M.E.AI
     *  2. Вместо AddSystemMessage/AddUserMessage — конструктор ChatMessage(role, text)
     *  3. Вместо GetStreamingChatMessageContentsAsync — GetStreamingResponseAsync
     *
     *  Закомментированная сигнатура (для рефактора на этапе 6+):
     *
     *  using Microsoft.Extensions.AI;
     *
     *  public class ChatService(IChatClient chatClient, IRagService ragService) : IChatService
     *  {
     *      // Тип сменился: ChatHistory → List<ChatMessage> (провайдер-агностичный)
     *      private readonly Dictionary<Guid, List<ChatMessage>> _sessions = [];
     *
     *      public async IAsyncEnumerable<string> StreamAsync(
     *          ChatRequest request,
     *          [EnumeratorCancellation] CancellationToken ct = default)
     *      {
     *          var history = GetOrCreateHistory(...);   // теперь List<ChatMessage>
     *
     *          // Добавление сообщений — через конструктор record-а ChatMessage
     *          // .NET: эквивалент new HttpRequestMessage(method, uri)
     *          history.Add(new ChatMessage(ChatRole.System, contextText));
     *          history.Add(new ChatMessage(ChatRole.User, request.Message));
     *
     *          var sb = new StringBuilder();
     *
     *          // GetStreamingResponseAsync вместо SK-шного GetStreamingChatMessageContentsAsync
     *          // Возвращает IAsyncEnumerable<ChatResponseUpdate> — каждый update это дельта.
     *          await foreach (var update in chatClient.GetStreamingResponseAsync(history, cancellationToken: ct))
     *          {
     *              // update.Text — текстовая дельта (раньше chunk.Content в SK)
     *              // update также содержит FunctionCalls, FinishReason и др. метаданные
     *              var token = update.Text ?? string.Empty;
     *              if (!string.IsNullOrEmpty(token))
     *              {
     *                  sb.Append(token);
     *                  yield return token;
     *              }
     *          }
     *
     *          // Финальное сообщение — assistant role
     *          history.Add(new ChatMessage(ChatRole.Assistant, sb.ToString()));
     *      }
     *  }
     *
     *  Бонус: для function calling (когда понадобится — этап 7+) код почти не меняется,
     *  потому что .UseFunctionInvocation() в Program.cs делает всё прозрачно — IChatClient
     *  сам распарсит tool_calls из ответа, вызовет твою C# функцию и подставит результат.
     *  В SK для этого нужны KernelPlugin/KernelFunction с атрибутами.
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

        var session = GetOrCreateSession(sessionId, mode, request.Role, request.ResumeContext, request.Language);

        // Ищем релевантные чанки: shared + приватные именно этой сессии
        var chunks = await ragService.SearchAsync(request.Message, sessionId.ToString(), topK: 5, ct: cancellationToken);

        // Собираем промпт СВЕЖИМ на каждый запрос: base system + grounding + RAG → пары user/assistant → новое сообщение.
        // Старые RAG-контексты не утекают в следующие запросы, потому что не записываются в _sessions.
        var locale = localization.Get(request.Language);
        var prompt = new ChatHistory(session.BaseSystemPrompt);
        if (chunks.Count > 0)
        {
            var grounding = mode == "assistant" ? locale.GroundingHard : locale.GroundingSoft;
            var context = string.Join("\n\n", chunks.Select(c => $"[{c.DocumentName} стр.{c.PageNumber}]\n{c.Text}"));
            prompt.AddSystemMessage($"{grounding}\n\n{locale.ContextHeader}\n{context}");
        }
        foreach (var (role, content) in session.History)
            prompt.AddMessage(role, content);
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
        session.History.Add((AuthorRole.User, request.Message));
        session.History.Add((AuthorRole.Assistant, sb.ToString()));
    }

    private SessionState GetOrCreateSession(Guid sessionId, string mode, string? role, string? resumeContext, string? language)
    {
        if (!_sessions.TryGetValue(sessionId, out var state))
        {
            var basePrompt = BuildSystemPrompt(mode, role, resumeContext, language);
            state = new SessionState(basePrompt, []);
            _sessions[sessionId] = state;
        }
        return state;
    }

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
