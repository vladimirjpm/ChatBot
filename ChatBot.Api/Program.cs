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

// CORS: в Production разрешаем только домен фронта (Vercel), в Development — localhost Vite.
// Список берётся из Cors:AllowedOrigins в appsettings — переопределяется через appsettings.{env}.json.
// .NET: аналог app.UseCors(policy => policy.WithOrigins(...)) в старом ASP.NET.
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddCors(opt =>
    opt.AddDefaultPolicy(p => p
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()));

// Регистрация Semantic Kernel с поддержкой OpenAI и Ollama
// .NET: аналог — builder.Services.AddSingleton + HttpClient + IOptions<LlmOptions>
var llmConfig = builder.Configuration.GetSection("Llm");
var kernelBuilder = Kernel.CreateBuilder();

if (llmConfig["Provider"] == "Ollama")
{
    // Переключение на локальную модель через OpenAI-совместимый API Ollama
    kernelBuilder.AddOpenAIChatCompletion(
        modelId: llmConfig["OllamaModel"] ?? "llama3",
        apiKey: "ollama",
        httpClient: new HttpClient { BaseAddress = new Uri(llmConfig["OllamaEndpoint"] ?? "http://localhost:11434") });
}
else
{
    // Проверка через IsNullOrWhiteSpace — пустая строка из appsettings не должна считаться валидным ключом
    var apiKey = llmConfig["ApiKey"];
    if (string.IsNullOrWhiteSpace(apiKey))
        throw new InvalidOperationException("Llm:ApiKey не задан (ожидается env var Llm__ApiKey)");

    kernelBuilder.AddOpenAIChatCompletion(
        modelId: llmConfig["ModelId"] ?? "gpt-4o-mini",
        apiKey: apiKey);
}

var kernel = kernelBuilder.Build();
builder.Services.AddSingleton(kernel);
builder.Services.AddSingleton(kernel.GetRequiredService<IChatCompletionService>());

// Эмбеддинги через Microsoft.Extensions.AI (IEmbeddingGenerator<,>) —
// провайдер-агностичный интерфейс, в отличие от SK-шного ITextEmbeddingGenerationService (experimental).
// .NET: builder.Services.AddSingleton<IEmbeddingGenerator<...>>(...) под капотом.
if (llmConfig["Provider"] != "Ollama")
{
    var apiKey = llmConfig["ApiKey"]!; // уже провалидирован выше
    var embeddingModel = llmConfig["EmbeddingModel"] ?? "text-embedding-3-small";
    // SKEXP0010 — API помечен Experimental; стабилизируется в M.Extensions.AI 10.0
    #pragma warning disable SKEXP0010
    builder.Services.AddOpenAIEmbeddingGenerator(embeddingModel, apiKey);
    #pragma warning restore SKEXP0010
}

// Qdrant gRPC-клиент. Соединение ленивое — устанавливается при первом вызове,
// поэтому отсутствие Qdrant не валит старт API (важно для тестов и health-чеков).
// .NET: AddSingleton, потому что QdrantClient thread-safe и хранит gRPC-канал.
//
// Для Qdrant Cloud — нужны UseTls=true (HTTPS на 6334) и ApiKey.
// Локально через docker compose — оба пусты, ходим plaintext на localhost:6334.
var qdrantConfig = builder.Configuration.GetSection("Qdrant");
builder.Services.AddSingleton(new QdrantClient(
    host: qdrantConfig["Host"] ?? "localhost",
    port: int.Parse(qdrantConfig["Port"] ?? "6334"),
    https: bool.Parse(qdrantConfig["UseTls"] ?? "false"),
    apiKey: qdrantConfig["ApiKey"]));

/*
 * ────────────────────────────────────────────────────────────────────────────────
 *  БУДУЩЕЕ: миграция на Microsoft.Extensions.AI (M.E.AI) — новый официальный SDK
 *  от MS, который заменит Semantic Kernel ChatCompletion как абстракцию.
 *  Пакеты: Microsoft.Extensions.AI + Microsoft.Extensions.AI.OpenAI
 *
 *  Плюсы:
 *  - Провайдер-агностичный интерфейс IChatClient (OpenAI, Azure, Anthropic, Ollama
 *    через единый API, без kernel-овского IoC внутри SK)
 *  - Встроенная композиция через middleware: tracing, кэш, retry, function calling
 *  - Регистрируется как обычный сервис в стандартном DI .NET (без Kernel-обёртки)
 *
 *  Как будет выглядеть (закомментировано — для миграции на этапе 6+):
 *
 *  using Microsoft.Extensions.AI;          // .NET: главный namespace
 *  using OpenAI;                            // официальный OpenAI SDK
 *
 *  // 1. Создаём низкоуровневый клиент OpenAI и оборачиваем его в IChatClient
 *  //    .NET: эквивалент HttpClientFactory + типизированный клиент
 *  IChatClient chatClient = new OpenAIClient(apiKey)
 *      .GetChatClient(modelId)              // получаем ChatClient для конкретной модели
 *      .AsIChatClient();                    // extension-метод адаптер OpenAI → M.E.AI
 *
 *  // 2. Композиция через билдер — наслаиваем middleware декораторами.
 *  //    .NET: похоже на HttpMessageHandler pipeline у HttpClient
 *  chatClient = new ChatClientBuilder(chatClient)
 *      .UseLogging(loggerFactory)           // лог каждого запроса/ответа
 *      .UseDistributedCache(cache)          // кэш ответов на одинаковые промпты
 *      .UseFunctionInvocation()             // авто-вызов tools/functions
 *      .UseOpenTelemetry()                  // трейсинг для Jaeger/Grafana
 *      .Build();
 *
 *  // 3. Регистрация в DI — обычный AddSingleton, без Kernel
 *  builder.Services.AddSingleton(chatClient);
 *
 *  // Альтернатива через extension methods (когда выйдут официально):
 *  // builder.Services.AddChatClient(sp => new OpenAIClient(apiKey)
 *  //     .GetChatClient(modelId).AsIChatClient())
 *  //     .UseLogging()
 *  //     .UseDistributedCache();
 * ────────────────────────────────────────────────────────────────────────────────
 */

// .NET: builder.Services.AddScoped<IRagService, RagService>()
// Локализация. JSON-файлы лежат рядом с бинарником (копируются через .csproj Content include).
// Singleton — читаем один раз на старте, дальше всё в памяти.
builder.Services.AddSingleton<ILocalizationProvider>(sp => new JsonLocalizationProvider(
    localesDirectory: Path.Combine(AppContext.BaseDirectory, "locales"),
    defaultLanguage: builder.Configuration["Localization:DefaultLanguage"] ?? "ru",
    logger: sp.GetRequiredService<ILoggerFactory>().CreateLogger<JsonLocalizationProvider>()));

builder.Services.AddScoped<IRagService, RagService>();
builder.Services.AddScoped<IIngestionService, IngestionService>();

// Хранилище диалоговых сессий — Singleton, чтобы история переживала HTTP-запросы.
// ChatService остаётся Scoped (зависит от Scoped IRagService) — Scoped → Singleton ОК.
// .NET: эквивалент builder.Services.AddSingleton<ISessionStore, InMemorySessionStore>().
builder.Services.AddSingleton<ISessionStore, InMemorySessionStore>();
builder.Services.AddScoped<IChatService, ChatService>();

// Health checks — для Railway / K8s liveness/readiness проб.
// .NET: builder.Services.AddHealthChecks().AddCheck<T>(name, tags) — стандартный паттерн ASP.NET Core.
// Теги "ready" vs "live": live проверяет что процесс жив, ready — что зависимости доступны.
builder.Services.AddHealthChecks()
    // OpenAI: только наличие ключа (реальный ping стоил бы денег и фейлил бы при rate-limit)
    .AddCheck("openai_apikey",
        () => string.IsNullOrWhiteSpace(builder.Configuration["Llm:ApiKey"])
            ? HealthCheckResult.Unhealthy("Llm:ApiKey не задан")
            : HealthCheckResult.Healthy(),
        tags: ["ready"])
    // Qdrant: реальный gRPC health-ping с таймаутом 3 сек
    .AddCheck<QdrantHealthCheck>("qdrant", tags: ["ready"]);

var app = builder.Build();

app.UseCors();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

// /health — полный отчёт со статусом каждого чека.
// .NET: app.MapHealthChecks("/health") + кастомный ResponseWriter для JSON-вывода.
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

// /health/live — лёгкая проверка «процесс жив» без зависимостей (Predicate: _ => false означает «ни одного чека»)
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false
});

// SSE-стриминг чата
// .NET: [HttpPost("stream")] с Response.Body streaming
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

// Загрузка PDF-документа для RAG-индексации.
// scope = "shared" (видно всем) | "private" (только в своей сессии).
// .NET: [HttpPost("upload")] + IFormFile + [FromForm].
app.MapPost("/api/documents/upload", async (
    IFormFile file,
    IIngestionService ingestionService,
    HttpRequest req,
    CancellationToken ct) =>
{
    if (file.Length == 0)
        return Results.BadRequest("Файл пустой");

    // Form-поля — multipart, не JSON. Берём напрямую из формы.
    var scope = req.Form["scope"].ToString();
    if (string.IsNullOrEmpty(scope)) scope = "private"; // безопасный дефолт
    var sessionId = req.Form["sessionId"].ToString();
    if (string.IsNullOrEmpty(sessionId)) sessionId = null!;

    if (scope == "private" && string.IsNullOrEmpty(sessionId))
        return Results.BadRequest("Для scope=private требуется sessionId");

    await using var stream = file.OpenReadStream();
    var result = await ingestionService.IngestAsync(stream, file.FileName, scope, sessionId, ct);

    return Results.Ok(result);
})
.WithName("DocumentUpload")
.WithTags("Documents")
.DisableAntiforgery();

// Список доступных в данной сессии документов (shared + свои private).
app.MapGet("/api/documents", async (string? sessionId, IIngestionService svc, CancellationToken ct) =>
    Results.Ok(await svc.ListAsync(sessionId, ct)))
.WithName("DocumentList")
.WithTags("Documents");

// Удаление собственного приватного документа.
// .NET: [HttpDelete("{name}")] + [FromQuery] sessionId.
app.MapDelete("/api/documents/{name}", async (
    string name,
    string sessionId,
    IIngestionService svc,
    CancellationToken ct) =>
{
    if (string.IsNullOrEmpty(sessionId))
        return Results.BadRequest("sessionId обязателен");
    var removed = await svc.DeleteAsync(name, sessionId, ct);
    return removed == 0 ? Results.NotFound() : Results.Ok(new { removed });
})
.WithName("DocumentDelete")
.WithTags("Documents");

app.Run();

// .NET: WebApplicationFactory<Program> требует, чтобы Program был public-типом.
// В минимальных API он генерится internal, поэтому объявляем partial-расширение.
public partial class Program;
