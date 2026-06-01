using ChatBot.Core.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ChatBot.Tests;

/// <summary>
/// Тестовая фабрика приложения: поднимает <c>Program</c> in-memory и подменяет
/// внешние зависимости (LLM, ingestion) на фейки, чтобы тесты были детерминированными
/// и не требовали сети / OpenAI ключа.
///
/// .NET: эквивалент <c>TestServer</c> + <c>IConfigureWebHostBuilder</c> из старого ASP.NET Core.
/// </summary>
public sealed class TestWebFactory : WebApplicationFactory<Program>
{
    public FakeChatService ChatService { get; } = new();
    public FakeIngestionService IngestionService { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Llm:ApiKey проверяется на старте Program — даём заведомо валидную (по форме) строку,
        // реальный IChatCompletionService всё равно не вызывается, так как IChatService подменён ниже.
        builder.UseSetting("Llm:ApiKey", "test-key-not-used");
        builder.UseSetting("Llm:Provider", "OpenAI");
        builder.UseSetting("Llm:ModelId", "gpt-4o-mini");

        builder.ConfigureServices(services =>
        {
            // .NET: services.Replace(ServiceDescriptor.Scoped<...>) — но проще удалить и переподписать
            services.RemoveAll<IChatService>();
            services.RemoveAll<IIngestionService>();
            services.AddSingleton<IChatService>(ChatService);
            services.AddSingleton<IIngestionService>(IngestionService);
        });

        builder.UseEnvironment("Development");
    }
}
