using ChatBot.Api.HealthChecks;
using ChatBot.Core;
using ChatBot.Core.Interfaces;
using ChatBot.Infrastructure.Localization;
using ChatBot.Infrastructure.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Qdrant.Client;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// .NET: builder.Services.AddOpenApi()
builder.Services.AddOpenApi();

// CORS: in Production allow only the frontend domain (Vercel), in Development — Vite localhost.
// Origins are read from Cors:AllowedOrigins in appsettings, overridden via appsettings.{env}.json.
// .NET: equivalent of app.UseCors(policy => policy.WithOrigins(...)) in classic ASP.NET.
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddCors(opt =>
    opt.AddDefaultPolicy(p => p
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()));

// Register Semantic Kernel with OpenAI and Ollama support.
// .NET: equivalent of builder.Services.AddSingleton + HttpClient + IOptions<LlmOptions>
var llmConfig = builder.Configuration.GetSection("Llm");
var kernelBuilder = Kernel.CreateBuilder();

if (llmConfig["Provider"] == "Ollama")
{
    // Switch to a local model via Ollama's OpenAI-compatible API.
    kernelBuilder.AddOpenAIChatCompletion(
        modelId: llmConfig["OllamaModel"] ?? "llama3",
        apiKey: "ollama",
        httpClient: new HttpClient { BaseAddress = new Uri(llmConfig["OllamaEndpoint"] ?? "http://localhost:11434") });
}
else
{
    // Use IsNullOrWhiteSpace — an empty string from appsettings must not be treated as a valid key.
    var apiKey = llmConfig["ApiKey"];
    if (string.IsNullOrWhiteSpace(apiKey))
        throw new InvalidOperationException("Llm:ApiKey is not set (expected env var Llm__ApiKey)");

    kernelBuilder.AddOpenAIChatCompletion(
        modelId: llmConfig["ModelId"] ?? "gpt-4o-mini",
        apiKey: apiKey);
}

var kernel = kernelBuilder.Build();
builder.Services.AddSingleton(kernel);
builder.Services.AddSingleton(kernel.GetRequiredService<IChatCompletionService>());

// Embeddings via Microsoft.Extensions.AI (IEmbeddingGenerator<,>) —
// provider-agnostic interface, unlike SK's ITextEmbeddingGenerationService (experimental).
// .NET: builder.Services.AddSingleton<IEmbeddingGenerator<...>>(...) under the hood.
if (llmConfig["Provider"] != "Ollama")
{
    var apiKey = llmConfig["ApiKey"]!; // validated above
    var embeddingModel = llmConfig["EmbeddingModel"] ?? "text-embedding-3-small";
    // SKEXP0010 — API is marked Experimental; stabilizes in M.Extensions.AI 10.0
    #pragma warning disable SKEXP0010
    builder.Services.AddOpenAIEmbeddingGenerator(embeddingModel, apiKey);
    #pragma warning restore SKEXP0010
}

// Qdrant gRPC client. Connection is lazy — established on first call,
// so a missing Qdrant instance does not crash API startup (important for tests and health checks).
// .NET: AddSingleton because QdrantClient is thread-safe and holds the gRPC channel.
//
// Qdrant Cloud requires UseTls=true (HTTPS on 6334) and ApiKey.
// Local docker compose — both empty, plain-text on localhost:6334.
var qdrantConfig = builder.Configuration.GetSection("Qdrant");
builder.Services.AddSingleton(new QdrantClient(
    host: qdrantConfig["Host"] ?? "localhost",
    port: int.Parse(qdrantConfig["Port"] ?? "6334"),
    https: bool.Parse(qdrantConfig["UseTls"] ?? "false"),
    apiKey: qdrantConfig["ApiKey"]));

/*
 * ────────────────────────────────────────────────────────────────────────────────
 *  FUTURE: migrate to Microsoft.Extensions.AI (M.E.AI) — the new official MS SDK
 *  that will replace Semantic Kernel ChatCompletion as the abstraction layer.
 *  Packages: Microsoft.Extensions.AI + Microsoft.Extensions.AI.OpenAI
 *
 *  Benefits:
 *  - Provider-agnostic IChatClient (OpenAI, Azure, Anthropic, Ollama
 *    through a single API, without SK's internal IoC Kernel wrapper)
 *  - Built-in middleware composition: tracing, caching, retry, function calling
 *  - Registered as a plain .NET DI service (no Kernel wrapper)
 *
 *  What it will look like (commented out — for migration at stage 6+):
 *
 *  using Microsoft.Extensions.AI;          // .NET: main namespace
 *  using OpenAI;                            // official OpenAI SDK
 *
 *  // 1. Create a low-level OpenAI client and wrap it in IChatClient.
 *  //    .NET: equivalent of HttpClientFactory + typed client
 *  IChatClient chatClient = new OpenAIClient(apiKey)
 *      .GetChatClient(modelId)              // get a ChatClient for the specific model
 *      .AsIChatClient();                    // extension-method adapter OpenAI → M.E.AI
 *
 *  // 2. Compose via builder — layer middleware as decorators.
 *  //    .NET: similar to HttpMessageHandler pipeline in HttpClient
 *  chatClient = new ChatClientBuilder(chatClient)
 *      .UseLogging(loggerFactory)           // log every request/response
 *      .UseDistributedCache(cache)          // cache responses for identical prompts
 *      .UseFunctionInvocation()             // auto-invoke tools/functions
 *      .UseOpenTelemetry()                  // tracing for Jaeger/Grafana
 *      .Build();
 *
 *  // 3. Register in DI — plain AddSingleton, no Kernel
 *  builder.Services.AddSingleton(chatClient);
 *
 *  // Alternative via extension methods (when officially released):
 *  // builder.Services.AddChatClient(sp => new OpenAIClient(apiKey)
 *  //     .GetChatClient(modelId).AsIChatClient())
 *  //     .UseLogging()
 *  //     .UseDistributedCache();
 * ────────────────────────────────────────────────────────────────────────────────
 */

// .NET: builder.Services.AddScoped<IRagService, RagService>()
// Localization. JSON files sit next to the binary (copied via .csproj Content include).
// Singleton — loaded once at startup, everything stays in memory afterward.
builder.Services.AddSingleton<ILocalizationProvider>(sp => new JsonLocalizationProvider(
    localesDirectory: Path.Combine(AppContext.BaseDirectory, "locales"),
    defaultLanguage: builder.Configuration["Localization:DefaultLanguage"] ?? "ru",
    logger: sp.GetRequiredService<ILoggerFactory>().CreateLogger<JsonLocalizationProvider>()));

builder.Services.AddScoped<IRagService, RagService>();
builder.Services.AddScoped<IIngestionService, IngestionService>();

// Session store is Singleton so chat history survives across HTTP requests.
// ChatService stays Scoped (depends on Scoped IRagService) — Scoped depending on Singleton is fine.
// .NET: equivalent of builder.Services.AddSingleton<ISessionStore, InMemorySessionStore>().
builder.Services.AddSingleton<ISessionStore, InMemorySessionStore>();
builder.Services.AddScoped<IChatService, ChatService>();

// Health checks for Railway / K8s liveness/readiness probes.
// .NET: builder.Services.AddHealthChecks().AddCheck<T>(name, tags) — standard ASP.NET Core pattern.
// Tags "ready" vs "live": live checks that the process is alive, ready — that dependencies are reachable.
builder.Services.AddHealthChecks()
    // OpenAI: key presence only (a real ping would cost money and fail under rate-limit)
    .AddCheck("openai_apikey",
        () => string.IsNullOrWhiteSpace(builder.Configuration["Llm:ApiKey"])
            ? HealthCheckResult.Unhealthy("Llm:ApiKey is not set")
            : HealthCheckResult.Healthy(),
        tags: ["ready"])
    // Qdrant: real gRPC health-ping with a 3-second timeout
    .AddCheck<QdrantHealthCheck>("qdrant", tags: ["ready"]);

var app = builder.Build();

app.UseCors();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

// /health — full report with each check's status.
// .NET: app.MapHealthChecks("/health") + custom ResponseWriter for JSON output.
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (ctx, report) =>
    {
        ctx.Response.ContentType = "application/json";
        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                durationMs = e.Value.Duration.TotalMilliseconds,
                error = e.Value.Exception?.Message,
            }),
        };
        await ctx.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
});

// /health/live — lightweight "process is alive" check with no dependencies (Predicate: _ => false means no checks run)
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false
});

// SSE chat streaming.
// .NET: [HttpPost("stream")] with Response.Body streaming
app.MapPost("/api/chat/stream", async (ChatRequest request, IChatService chatService, HttpResponse response, CancellationToken ct) =>
{
    response.ContentType = "text/event-stream";
    response.Headers["Cache-Control"] = "no-cache";
    response.Headers["X-Accel-Buffering"] = "no";

    await foreach (var token in chatService.StreamAsync(request, ct))
    {
        await response.WriteAsync($"data: {System.Text.Json.JsonSerializer.Serialize(token)}\n\n", ct);
        await response.Body.FlushAsync(ct);
    }

    await response.WriteAsync("data: [DONE]\n\n", ct);
})
.WithName("ChatStream")
.WithTags("Chat");

// Upload a PDF document for RAG indexing.
// scope = "shared" (visible to all) | "private" (only within the current session).
// .NET: [HttpPost("upload")] + IFormFile + [FromForm].
app.MapPost("/api/documents/upload", async (
    IFormFile file,
    IIngestionService ingestionService,
    HttpRequest req,
    CancellationToken ct) =>
{
    if (file.Length == 0)
        return Results.BadRequest("File is empty");

    // Form fields are multipart, not JSON — read directly from the form.
    var scope = req.Form["scope"].ToString();
    if (string.IsNullOrEmpty(scope)) scope = "private"; // safe default
    var sessionId = req.Form["sessionId"].ToString();
    if (string.IsNullOrEmpty(sessionId)) sessionId = null!;

    if (scope == "private" && string.IsNullOrEmpty(sessionId))
        return Results.BadRequest("sessionId is required for scope=private");

    await using var stream = file.OpenReadStream();
    var result = await ingestionService.IngestAsync(stream, file.FileName, scope, sessionId, ct);

    return Results.Ok(result);
})
.WithName("DocumentUpload")
.WithTags("Documents")
.DisableAntiforgery();

// List documents available in the current session (shared + own private ones).
app.MapGet("/api/documents", async (
    string? sessionId,
    IIngestionService svc,
    ILoggerFactory loggerFactory,
    CancellationToken ct) =>
{
    var log = loggerFactory.CreateLogger("DocumentList");
    try
    {
        var docs = await svc.ListAsync(sessionId, ct);
        log.LogInformation("ListAsync OK: sessionId={Sid}, count={Count}", sessionId, docs.Count);
        return Results.Ok(docs);
    }
    catch (Exception ex)
    {
        // .NET: instead of a default 500 with an empty body — return ProblemDetails with the exception type
        // so the cause is visible directly in DevTools without opening Railway logs.
        log.LogError(ex, "ListAsync FAILED: sessionId={Sid}", sessionId);
        return Results.Problem(
            title: "Documents list failed",
            detail: $"{ex.GetType().Name}: {ex.Message}",
            statusCode: 500);
    }
})
.WithName("DocumentList")
.WithTags("Documents");

// Delete the caller's own private document.
// .NET: [HttpDelete("{name}")] + [FromQuery] sessionId.
app.MapDelete("/api/documents/{name}", async (
    string name,
    string sessionId,
    IIngestionService svc,
    CancellationToken ct) =>
{
    if (string.IsNullOrEmpty(sessionId))
        return Results.BadRequest("sessionId is required");
    var removed = await svc.DeleteAsync(name, sessionId, ct);
    return removed == 0 ? Results.NotFound() : Results.Ok(new { removed });
})
.WithName("DocumentDelete")
.WithTags("Documents");

app.Run();

// .NET: WebApplicationFactory<Program> requires Program to be a public type.
// In minimal APIs it is generated as internal, so we declare a partial extension.
public partial class Program;
