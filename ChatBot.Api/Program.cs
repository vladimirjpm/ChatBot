using ChatBot.Core;
using ChatBot.Core.Interfaces;
using ChatBot.Infrastructure.Services;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

var builder = WebApplication.CreateBuilder(args);

// Диагностика — какие env vars пришли в контейнер (печатаем только имена + маску, без значений)
// .NET: эквивалент Environment.GetEnvironmentVariables()
Console.WriteLine("=== ENV VARS DIAGNOSTIC ===");
foreach (System.Collections.DictionaryEntry e in Environment.GetEnvironmentVariables())
{
    var name = e.Key?.ToString() ?? "";
    if (name.StartsWith("Llm", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("OPENAI", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("RAILWAY", StringComparison.OrdinalIgnoreCase))
    {
        var val = e.Value?.ToString() ?? "";
        var masked = val.Length > 8 ? val[..4] + "***" + val[^4..] : "***";
        Console.WriteLine($"  {name} = {masked} (len={val.Length})");
    }
}
Console.WriteLine("=== END DIAGNOSTIC ===");

// .NET: builder.Services.AddOpenApi()
builder.Services.AddOpenApi();

builder.Services.AddCors(opt =>
    opt.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

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

// .NET: builder.Services.AddScoped<IRagService, RagService>()
builder.Services.AddScoped<IRagService, RagService>();
builder.Services.AddScoped<IIngestionService, IngestionService>();
builder.Services.AddScoped<IChatService, ChatService>();

var app = builder.Build();

app.UseCors();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

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

// Загрузка PDF-документа для RAG-индексации
// .NET: [HttpPost("upload")] + IFormFile
app.MapPost("/api/documents/upload", async (IFormFile file, IIngestionService ingestionService, CancellationToken ct) =>
{
    if (file.Length == 0)
        return Results.BadRequest("Файл пустой");

    await using var stream = file.OpenReadStream();
    var result = await ingestionService.IngestAsync(stream, file.FileName, ct);

    return Results.Ok(result);
})
.WithName("DocumentUpload")
.WithTags("Documents")
.DisableAntiforgery();

app.Run();
