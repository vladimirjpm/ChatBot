using System.Runtime.CompilerServices;
using ChatBot.Core;
using ChatBot.Core.Interfaces;
using ChatBot.Infrastructure.Localization;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace ChatBot.Infrastructure.Services;

/// <summary>
/// Streaming chat implementation backed by Semantic Kernel.
///
/// Session history is stored in an external <see cref="ISessionStore"/> (Singleton),
/// while the service itself stays Scoped — a valid relationship
/// (Scoped can depend on Singleton; the reverse produces a captive dependency).
///
/// Before refactoring, history lived in an instance Dictionary and Scoped lifetime
/// created a new ChatService per request — the dictionary was always empty.
/// </summary>
public class ChatService(
    IChatCompletionService chatCompletion,
    IRagService ragService,
    IPromptProvider promptProvider,
    ISessionStore sessions) : IChatService
{
    /*
     * ────────────────────────────────────────────────────────────────────────────
     *  FUTURE: Microsoft.Extensions.AI (IChatClient) version.
     *  Key differences from SK:
     *  1. Instead of ChatHistory (SK-specific) — List<ChatMessage> from M.E.AI
     *  2. Instead of AddSystemMessage/AddUserMessage — ChatMessage(role, text) constructor
     *  3. Instead of GetStreamingChatMessageContentsAsync — GetStreamingResponseAsync
     *  4. For function calling (stage 7+) — .UseFunctionInvocation() in Program.cs;
     *     IChatClient will parse tool_calls and invoke the C# function automatically.
     * ────────────────────────────────────────────────────────────────────────────
     */

    /// <inheritdoc/>
    public async IAsyncEnumerable<string> StreamAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var sessionId = request.SessionId ?? Guid.NewGuid();
        var mode = string.IsNullOrEmpty(request.Mode)
            // Compatibility: if mode is absent — infer from the presence of a role (legacy behavior)
            ? (string.IsNullOrEmpty(request.Role) ? "assistant" : "interview")
            : request.Mode;

        var session = await GetOrCreateSessionAsync(sessionId, mode, request.Role, request.ResumeContext, request.Language, cancellationToken);

        // Search for relevant chunks: shared + private ones belonging to this session.
        var chunks = await ragService.SearchAsync(request.Message, sessionId.ToString(), topK: 5, ct: cancellationToken);

        // Rebuild the prompt fresh on every request: base system + grounding + RAG → user/assistant pairs → new message.
        // Stale RAG contexts don't leak into subsequent requests because they are never written to SessionState.History.
        var prompts = promptProvider.Get(request.Language);
        var prompt = new ChatHistory(session.BaseSystemPrompt);
        if (chunks.Count > 0)
        {
            var grounding = mode == "assistant" ? prompts.GroundingHard : prompts.GroundingSoft;
            var context = string.Join("\n\n", chunks.Select(c => $"[{c.DocumentName} p.{c.PageNumber}]\n{c.Text}"));
            prompt.AddSystemMessage($"{grounding}\n\n{prompts.ContextHeader}\n{context}");
        }

        // Map string role → SK AuthorRole here, not in Core,
        // to avoid pulling an SK dependency into ChatBot.Core.
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

        // Persist to long-term history ONLY user/assistant messages — no RAG system messages.
        // List is mutated in-place (no record replacement), but explicit SetAsync is called in case
        // of a future RedisSessionStore that requires re-serialization.
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

    /// <summary>Maps a string role (Core layer) to SK's AuthorRole.</summary>
    private static AuthorRole MapRole(string role) => role switch
    {
        "user" => AuthorRole.User,
        "assistant" => AuthorRole.Assistant,
        "system" => AuthorRole.System,
        _ => AuthorRole.User,
    };

    /// <summary>
    /// Builds the base system prompt for a session from the loaded locale.
    /// Modes: "interview" (interview simulator) or "assistant" (RAG chat).
    /// </summary>
    private string BuildSystemPrompt(string mode, string? role, string? resumeContext, string? language)
    {
        var prompts = promptProvider.Get(language);

        if (mode == "assistant")
            return prompts.AssistantBase;

        if (string.IsNullOrWhiteSpace(role))
            return prompts.GenericAssistant;

        var roleName = prompts.RoleNames.GetValueOrDefault(role.ToLowerInvariant())
            ?? role + prompts.UnknownRoleSuffix;

        var prompt = Locale.Format(prompts.InterviewRules, new Dictionary<string, string> { ["roleName"] = roleName });

        if (!string.IsNullOrWhiteSpace(resumeContext))
            prompt += Locale.Format(prompts.InterviewResumeAddendum, new Dictionary<string, string> { ["resume"] = resumeContext });

        prompt += prompts.InterviewStartCue;
        return prompt;
    }
}
