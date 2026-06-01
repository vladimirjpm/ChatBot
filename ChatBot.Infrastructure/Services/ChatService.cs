using System.Runtime.CompilerServices;
using ChatBot.Core;
using ChatBot.Core.Interfaces;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace ChatBot.Infrastructure.Services;

/// <summary>
/// Реализация стримингового чата через Semantic Kernel.
/// Поддерживает историю в памяти процесса (без персистентности).
/// Для продакшна историю нужно хранить в Redis или PostgreSQL.
/// </summary>
public class ChatService(IChatCompletionService chatCompletion, IRagService ragService) : IChatService
{
    // Хранилище истории сессий в памяти — заменить на распределённый кэш в prod
    private readonly Dictionary<Guid, ChatHistory> _sessions = [];

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
        var history = GetOrCreateHistory(request.SessionId, request.Role, request.ResumeContext, request.Language);

        // Ищем релевантные чанки и строим системный контекст (RAG — этап 5)
        var chunks = await ragService.SearchAsync(request.Message, topK: 5, ct: cancellationToken);
        if (chunks.Count > 0)
        {
            var context = string.Join("\n\n", chunks.Select(c => $"[{c.DocumentName} стр.{c.PageNumber}]\n{c.Text}"));
            history.AddSystemMessage($"Используй следующий контекст для ответа:\n{context}");
        }

        history.AddUserMessage(request.Message);

        var sb = new System.Text.StringBuilder();

        // Semantic Kernel StreamingChatMessageContentsAsync — аналог IAsyncEnumerable<StreamingChatCompletionUpdate> в M.Extensions.AI
        await foreach (var chunk in chatCompletion.GetStreamingChatMessageContentsAsync(history, cancellationToken: cancellationToken))
        {
            var token = chunk.Content ?? string.Empty;
            if (!string.IsNullOrEmpty(token))
            {
                sb.Append(token);
                yield return token;
            }
        }

        // Сохраняем полный ответ ассистента в историю
        history.AddAssistantMessage(sb.ToString());
    }

    private ChatHistory GetOrCreateHistory(Guid? sessionId, string? role, string? resumeContext, string? language = "ru")
    {
        var id = sessionId ?? Guid.NewGuid();
        if (!_sessions.TryGetValue(id, out var history))
        {
            var systemPrompt = BuildSystemPrompt(role, resumeContext, language);
            history = new ChatHistory(systemPrompt);
            _sessions[id] = history;
        }
        return history;
    }

    /// <summary>
    /// Строит системный промпт в зависимости от режима:
    /// симулятор собеседований (с опциональным резюме) или обычный ассистент.
    /// </summary>
    private static string BuildSystemPrompt(string? role, string? resumeContext, string? language = "ru")
    {
        if (string.IsNullOrWhiteSpace(role))
            return "Ты полезный ассистент. Отвечай на основе предоставленного контекста, если он есть.";

        var isEnglish = language?.ToLowerInvariant() == "en";

        var roleName = role.ToLowerInvariant() switch
        {
            "dotnet" => isEnglish ? ".NET / C# Developer" : ".NET / C# разработчика",
            "react"  => isEnglish ? "React / TypeScript Developer" : "React / TypeScript разработчика",
            "devops" => isEnglish ? "DevOps / Cloud Engineer" : "DevOps / Cloud инженера",
            _        => role + (isEnglish ? " Developer" : " разработчика")
        };

        // Правила и инструкции на языке интервью
        var prompt = isEnglish
            ? $"""
            You are an experienced technical interviewer conducting a job interview for a {roleName} position.

            Rules:
            - Ask ONE question at a time and wait for the candidate's answer
            - After each answer: give a score from 1 to 10 and brief feedback (1-2 sentences)
            - Then ask the next question
            - Questions should vary in difficulty: from basic to advanced
            - Be specific, professional, and friendly
            - Always respond in English
            """
            : $"""
            Ты опытный технический интервьюер, проводишь собеседование на позицию {roleName}.

            Правила:
            - Задавай ОДИН вопрос за раз, жди ответа кандидата
            - После каждого ответа: дай оценку от 1 до 10 и краткий фидбек (1-2 предложения)
            - Затем задай следующий вопрос
            - Вопросы должны быть разного уровня: от базовых к сложным
            - Будь конкретным, профессиональным, но доброжелательным
            - Всегда отвечай на русском языке
            """;

        // Если кандидат предоставил резюме — адаптируй вопросы под его опыт
        if (!string.IsNullOrWhiteSpace(resumeContext))
        {
            prompt += isEnglish
                ? $"\n\nCandidate's resume / context:\n{resumeContext}\n\nAdapt your questions based on the candidate's experience."
                : $"\n\nРезюме / контекст кандидата:\n{resumeContext}\n\nАдаптируй вопросы под опыт кандидата из резюме.";
        }

        prompt += isEnglish
            ? "\n\nStart with a brief greeting and your first question."
            : "\n\nНачни с приветствия и первого вопроса.";

        return prompt;
    }
}
